using System;
using System.Runtime.InteropServices;

namespace Godot
{
    /// <summary>
    /// 2×3 matrix (2 rows, 3 columns) used for 2D linear transformations.
    /// It can represent transformations such as translation, rotation, or scaling.
    /// It consists of a three <see cref="Vector2"/> values: x, y, and the origin.
    ///
    /// For more information, read this documentation article:
    /// https://docs.godotengine.org/en/latest/tutorials/math/matrices_and_transforms.html
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct Transform2D : IEquatable<Transform2D>
    {
        /// <summary>
        /// The basis matrix's X vector (column 0). Equivalent to array index <c>[0]</c>.
        /// </summary>
        public Vector2 x;

        /// <summary>
        /// The basis matrix's Y vector (column 1). Equivalent to array index <c>[1]</c>.
        /// </summary>
        public Vector2 y;

        /// <summary>
        /// The origin vector (column 2, the third column). Equivalent to array index <c>[2]</c>.
        /// The origin vector represents translation.
        /// </summary>
        public Vector2 origin;

        /// <summary>
        /// Access whole columns in the form of <see cref="Vector2"/>.
        /// The third column is the <see cref="origin"/> vector.
        /// </summary>
        /// <param name="column">Which column vector.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="column"/> is not 0, 1 or 2.
        /// </exception>
        public Vector2 this[int column]
        {
            readonly get
            {
                switch (column)
                {
                    case 0:
                        return x;
                    case 1:
                        return y;
                    case 2:
                        return origin;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(column));
                }
            }
            set
            {
                switch (column)
                {
                    case 0:
                        x = value;
                        return;
                    case 1:
                        y = value;
                        return;
                    case 2:
                        origin = value;
                        return;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(column));
                }
            }
        }

        /// <summary>
        /// Access matrix elements in column-major order.
        /// The third column is the <see cref="origin"/> vector.
        /// </summary>
        /// <param name="column">Which column, the matrix horizontal position.</param>
        /// <param name="row">Which row, the matrix vertical position.</param>
        public real_t this[int column, int row]
        {
            readonly get
            {
                return this[column][row];
            }
            set
            {
                Vector2 columnVector = this[column];
                columnVector[row] = value;
                this[column] = columnVector;
            }
        }

        /// <summary>
        /// Returns the inverse of the transform, under the assumption that
        /// the transformation is composed of rotation, scaling, and translation.
        /// </summary>
        /// <seealso cref="Inverse"/>
        /// <returns>The inverse transformation matrix.</returns>
        public readonly Transform2D AffineInverse()
        {
            real_t det = BasisDeterminant();

            if (det == 0)
                throw new InvalidOperationException("Matrix determinant is zero and cannot be inverted.");

            Transform2D inv = this;

            inv[0, 0] = this[1, 1];
            inv[1, 1] = this[0, 0];

            real_t detInv = 1.0f / det;

            inv[0] *= new Vector2(detInv, -detInv);
            inv[1] *= new Vector2(-detInv, detInv);

            inv[2] = inv.BasisXform(-inv[2]);

            return inv;
        }

        /// <summary>
        /// Returns the determinant of the basis matrix. If the basis is
        /// uniformly scaled, its determinant is the square of the scale.
        ///
        /// A negative determinant means the Y scale is negative.
        /// A zero determinant means the basis isn't invertible,
        /// and is usually considered invalid.
        /// </summary>
        /// <returns>The determinant of the basis matrix.</returns>
        private readonly real_t BasisDeterminant()
        {
            return (x.x * y.y) - (x.y * y.x);
        }

        /// <summary>
        /// Returns a vector transformed (multiplied) by the basis matrix.
        /// This method does not account for translation (the <see cref="origin"/> vector).
        /// </summary>
        /// <seealso cref="BasisXformInv(Vector2)"/>
        /// <param name="v">A vector to transform.</param>
        /// <returns>The transformed vector.</returns>
        public readonly Vector2 BasisXform(Vector2 v)
        {
            return new Vector2(Tdotx(v), Tdoty(v));
        }

        /// <summary>
        /// Returns a vector transformed (multiplied) by the inverse basis matrix.
        /// This method does not account for translation (the <see cref="origin"/> vector).
        ///
        /// Note: This results in a multiplication by the inverse of the
        /// basis matrix only if it represents a rotation-reflection.
        /// </summary>
        /// <seealso cref="BasisXform(Vector2)"/>
        /// <param name="v">A vector to inversely transform.</param>
        /// <returns>The inversely transformed vector.</returns>
        public readonly Vector2 BasisXformInv(Vector2 v)
        {
            return new Vector2(x.Dot(v), y.Dot(v));
        }

        /// <summary>
        /// Returns the transform's rotation (in radians).
        /// </summary>
        public readonly real_t GetRotation()
        {
            return Mathf.Atan2(x.y, x.x);
        }

        /// <summary>
        /// Returns the scale.
        /// </summary>
        public readonly Vector2 GetScale()
        {
            real_t detSign = Mathf.Sign(BasisDeterminant());
            return new Vector2(x.Length(), detSign * y.Length());
        }

        /// <summary>
        /// Interpolates this transform to the other <paramref name="transform"/> by <paramref name="weight"/>.
        /// </summary>
        /// <param name="transform">The other transform.</param>
        /// <param name="weight">A value on the range of 0.0 to 1.0, representing the amount of interpolation.</param>
        /// <returns>The interpolated transform.</returns>
        public readonly Transform2D InterpolateWith(Transform2D transform, real_t weight)
        {
            real_t r1 = GetRotation();
            real_t r2 = transform.GetRotation();

            Vector2 s1 = GetScale();
            Vector2 s2 = transform.GetScale();

            // Slerp rotation
            var v1 = new Vector2(Mathf.Cos(r1), Mathf.Sin(r1));
            var v2 = new Vector2(Mathf.Cos(r2), Mathf.Sin(r2));

            real_t dot = v1.Dot(v2);

            dot = Mathf.Clamp(dot, -1.0f, 1.0f);

            Vector2 v;

            if (dot > 0.9995f)
            {
                // Linearly interpolate to avoid numerical precision issues
                v = v1.Lerp(v2, weight).Normalized();
            }
            else
            {
                real_t angle = weight * Mathf.Acos(dot);
                Vector2 v3 = (v2 - (v1 * dot)).Normalized();
                v = (v1 * Mathf.Cos(angle)) + (v3 * Mathf.Sin(angle));
            }

            // Extract parameters
            Vector2 p1 = origin;
            Vector2 p2 = transform.origin;

            // Construct matrix
            var res = new Transform2D(Mathf.Atan2(v.y, v.x), p1.Lerp(p2, weight));
            Vector2 scale = s1.Lerp(s2, weight);
            res.x *= scale;
            res.y *= scale;

            return res;
        }

        /// <summary>
        /// Returns the inverse of the transform, under the assumption that
        /// the transformation is composed of rotation and translation
        /// (no scaling, use <see cref="AffineInverse"/> for transforms with scaling).
        /// </summary>
        /// <returns>The inverse matrix.</returns>
        public readonly Transform2D Inverse()
        {
            Transform2D inv = this;

            // Swap
            inv.x.y = y.x;
            inv.y.x = x.y;

            inv.origin = inv.BasisXform(-inv.origin);

            return inv;
        }

        /// <summary>
        /// Returns <see langword="true"/> if this transform is finite, by calling
        /// <see cref="Mathf.IsFinite"/> on each component.
        /// </summary>
        /// <returns>Whether this vector is finite or not.</returns>
        public readonly bool IsFinite()
        {
            return x.IsFinite() && y.IsFinite() && origin.IsFinite();
        }

        /// <summary>
        /// Returns the transform with the basis orthogonal (90 degrees),
        /// and normalized axis vectors (scale of 1 or -1).
        /// </summary>
        /// <returns>The orthonormalized transform.</returns>
        public readonly Transform2D Orthonormalized()
        {
            Transform2D on = this;

            Vector2 onX = on.x;
            Vector2 onY = on.y;

            onX.Normalize();
            onY = onY - (onX * onX.Dot(onY));
            onY.Normalize();

            on.x = onX;
            on.y = onY;

            return on;
        }

        /// <summary>
        /// Rotates the transform by <paramref name="angle"/> (in radians).
        /// The operation is done in the parent/global frame, equivalent to
        /// multiplying the matrix from the left.
        /// </summary>
        /// <param name="angle">The angle to rotate, in radians.</param>
        /// <returns>The rotated transformation matrix.</returns>
        public readonly Transform2D Rotated(real_t angle)
        {
            return this * new Transform2D(angle, new Vector2());
        }

        /// <summary>
        /// Rotates the transform by <paramref name="angle"/> (in radians).
        /// The operation is done in the local frame, equivalent to
        /// multiplying the matrix from the right.
        /// </summary>
        /// <param name="angle">The angle to rotate, in radians.</param>
        /// <returns>The rotated transformation matrix.</returns>
        public readonly Transform2D RotatedLocal(real_t angle)
        {
            return new Transform2D(angle, new Vector2()) * this;
        }

        /// <summary>
        /// Scales the transform by the given scaling factor.
        /// The operation is done in the parent/global frame, equivalent to
        /// multiplying the matrix from the left.
        /// </summary>
        /// <param name="scale">The scale to introduce.</param>
        /// <returns>The scaled transformation matrix.</returns>
        public readonly Transform2D Scaled(Vector2 scale)
        {
            Transform2D copy = this;
            copy.x *= scale;
            copy.y *= scale;
            copy.origin *= scale;
            return copy;
        }

        /// <summary>
        /// Scales the transform by the given scaling factor.
        /// The operation is done in the local frame, equivalent to
        /// multiplying the matrix from the right.
        /// </summary>
        /// <param name="scale">The scale to introduce.</param>
        /// <returns>The scaled transformation matrix.</returns>
        public readonly Transform2D ScaledLocal(Vector2 scale)
        {
            Transform2D copy = this;
            copy.x *= scale;
            copy.y *= scale;
            return copy;
        }

        private readonly real_t Tdotx(Vector2 with)
        {
            return (this[0, 0] * with[0]) + (this[1, 0] * with[1]);
        }

        private readonly real_t Tdoty(Vector2 with)
        {
            return (this[0, 1] * with[0]) + (this[1, 1] * with[1]);
        }

        /// <summary>
        /// Translates the transform by the given <paramref name="offset"/>.
        /// The operation is done in the parent/global frame, equivalent to
        /// multiplying the matrix from the left.
        /// </summary>
        /// <param name="offset">The offset to translate by.</param>
        /// <returns>The translated matrix.</returns>
        public readonly Transform2D Translated(Vector2 offset)
        {
            Transform2D copy = this;
            copy.origin += offset;
            return copy;
        }

        /// <summary>
        /// Translates the transform by the given <paramref name="offset"/>.
        /// The operation is done in the local frame, equivalent to
        /// multiplying the matrix from the right.
        /// </summary>
        /// <param name="offset">The offset to translate by.</param>
        /// <returns>The translated matrix.</returns>
        public readonly Transform2D TranslatedLocal(Vector2 offset)
        {
            Transform2D copy = this;
            copy.origin += copy.BasisXform(offset);
            return copy;
        }

        // Constants
        private static readonly Transform2D _identity = new Transform2D(1, 0, 0, 1, 0, 0);
        private static readonly Transform2D _flipX = new Transform2D(-1, 0, 0, 1, 0, 0);
        private static readonly Transform2D _flipY = new Transform2D(1, 0, 0, -1, 0, 0);

        /// <summary>
        /// The identity transform, with no translation, rotation, or scaling applied.
        /// This is used as a replacement for <c>Transform2D()</c> in GDScript.
        /// Do not use <c>new Transform2D()</c> with no arguments in C#, because it sets all values to zero.
        /// </summary>
        /// <value>Equivalent to <c>new Transform2D(Vector2.Right, Vector2.Down, Vector2.Zero)</c>.</value>
        public static Transform2D Identity { get { return _identity; } }
        /// <summary>
        /// The transform that will flip something along the X axis.
        /// </summary>
        /// <value>Equivalent to <c>new Transform2D(Vector2.Left, Vector2.Down, Vector2.Zero)</c>.</value>
        public static Transform2D FlipX { get { return _flipX; } }
        /// <summary>
        /// The transform that will flip something along the Y axis.
        /// </summary>
        /// <value>Equivalent to <c>new Transform2D(Vector2.Right, Vector2.Up, Vector2.Zero)</c>.</value>
        public static Transform2D FlipY { get { return _flipY; } }

        /// <summary>
        /// Constructs a transformation matrix from 3 vectors (matrix columns).
        /// </summary>
        /// <param name="xAxis">The X vector, or column index 0.</param>
        /// <param name="yAxis">The Y vector, or column index 1.</param>
        /// <param name="originPos">The origin vector, or column index 2.</param>
        public Transform2D(Vector2 xAxis, Vector2 yAxis, Vector2 originPos)
        {
            x = xAxis;
            y = yAxis;
            origin = originPos;
        }

        /// <summary>
        /// Constructs a transformation matrix from the given components.
        /// Arguments are named such that xy is equal to calling <c>x.y</c>.
        /// </summary>
        /// <param name="xx">The X component of the X column vector, accessed via <c>t.x.x</c> or <c>[0][0]</c>.</param>
        /// <param name="xy">The Y component of the X column vector, accessed via <c>t.x.y</c> or <c>[0][1]</c>.</param>
        /// <param name="yx">The X component of the Y column vector, accessed via <c>t.y.x</c> or <c>[1][0]</c>.</param>
        /// <param name="yy">The Y component of the Y column vector, accessed via <c>t.y.y</c> or <c>[1][1]</c>.</param>
        /// <param name="ox">The X component of the origin vector, accessed via <c>t.origin.x</c> or <c>[2][0]</c>.</param>
        /// <param name="oy">The Y component of the origin vector, accessed via <c>t.origin.y</c> or <c>[2][1]</c>.</param>
        public Transform2D(real_t xx, real_t xy, real_t yx, real_t yy, real_t ox, real_t oy)
        {
            x = new Vector2(xx, xy);
            y = new Vector2(yx, yy);
            origin = new Vector2(ox, oy);
        }

        /// <summary>
        /// Constructs a transformation matrix from a <paramref name="rotation"/> value and
        /// <paramref name="origin"/> vector.
        /// </summary>
        /// <param name="rotation">The rotation of the new transform, in radians.</param>
        /// <param name="origin">The origin vector, or column index 2.</param>
        public Transform2D(real_t rotation, Vector2 origin)
        {
            x.x = y.y = Mathf.Cos(rotation);
            x.y = y.x = Mathf.Sin(rotation);
            y.x *= -1;
            this.origin = origin;
        }

        /// <summary>
        /// Constructs a transformation matrix from a <paramref name="rotation"/> value,
        /// <paramref name="scale"/> vector, <paramref name="skew"/> value, and
        /// <paramref name="origin"/> vector.
        /// </summary>
        /// <param name="rotation">The rotation of the new transform, in radians.</param>
        /// <param name="scale">The scale of the new transform.</param>
        /// <param name="skew">The skew of the new transform, in radians.</param>
        /// <param name="origin">The origin vector, or column index 2.</param>
        public Transform2D(real_t rotation, Vector2 scale, real_t skew, Vector2 origin)
        {
            x.x = Mathf.Cos(rotation) * scale.x;
            y.y = Mathf.Cos(rotation + skew) * scale.y;
            y.x = -Mathf.Sin(rotation + skew) * scale.y;
            x.y = Mathf.Sin(rotation) * scale.x;
            this.origin = origin;
        }

        /// <summary>
        /// Composes these two transformation matrices by multiplying them
        /// together. This has the effect of transforming the second transform
        /// (the child) by the first transform (the parent).
        /// </summary>
        /// <param name="left">The parent transform.</param>
        /// <param name="right">The child transform.</param>
        /// <returns>The composed transform.</returns>
        public static Transform2D operator *(Transform2D left, Transform2D right)
        {
            left.origin = left * right.origin;

            real_t x0 = left.Tdotx(right.x);
            real_t x1 = left.Tdoty(right.x);
            real_t y0 = left.Tdotx(right.y);
            real_t y1 = left.Tdoty(right.y);

            left.x.x = x0;
            left.x.y = x1;
            left.y.x = y0;
            left.y.y = y1;

            return left;
        }

        /// <summary>
        /// Returns a Vector2 transformed (multiplied) by the transformation matrix.
        /// </summary>
        /// <param name="transform">The transformation to apply.</param>
        /// <param name="vector">A Vector2 to transform.</param>
        /// <returns>The transformed Vector2.</returns>
        public static Vector2 operator *(Transform2D transform, Vector2 vector)
        {
            return new Vector2(transform.Tdotx(vector), transform.Tdoty(vector)) + transform.origin;
        }

        /// <summary>
        /// Returns a Vector2 transformed (multiplied) by the inverse transformation matrix.
        /// </summary>
        /// <param name="vector">A Vector2 to inversely transform.</param>
        /// <param name="transform">The transformation to apply.</param>
        /// <returns>The inversely transformed Vector2.</returns>
        public static Vector2 operator *(Vector2 vector, Transform2D transform)
        {
            Vector2 vInv = vector - transform.origin;
            return new Vector2(transform.x.Dot(vInv), transform.y.Dot(vInv));
        }

        /// <summary>
        /// Returns a Rect2 transformed (multiplied) by the transformation matrix.
        /// </summary>
        /// <param name="transform">The transformation to apply.</param>
        /// <param name="rect">A Rect2 to transform.</param>
        /// <returns>The transformed Rect2.</returns>
        public static Rect2 operator *(Transform2D transform, Rect2 rect)
        {
            Vector2 pos = transform * rect.Position;
            Vector2 toX = transform.x * rect.Size.x;
            Vector2 toY = transform.y * rect.Size.y;

            return new Rect2(pos, new Vector2()).Expand(pos + toX).Expand(pos + toY).Expand(pos + toX + toY);
        }

        /// <summary>
        /// Returns a Rect2 transformed (multiplied) by the inverse transformation matrix.
        /// </summary>
        /// <param name="rect">A Rect2 to inversely transform.</param>
        /// <param name="transform">The transformation to apply.</param>
        /// <returns>The inversely transformed Rect2.</returns>
        public static Rect2 operator *(Rect2 rect, Transform2D transform)
        {
            Vector2 pos = rect.Position * transform;
            Vector2 to1 = new Vector2(rect.Position.x, rect.Position.y + rect.Size.y) * transform;
            Vector2 to2 = new Vector2(rect.Position.x + rect.Size.x, rect.Position.y + rect.Size.y) * transform;
            Vector2 to3 = new Vector2(rect.Position.x + rect.Size.x, rect.Position.y) * transform;

            return new Rect2(pos, new Vector2()).Expand(to1).Expand(to2).Expand(to3);
        }

        /// <summary>
        /// Returns a copy of the given Vector2[] transformed (multiplied) by the transformation matrix.
        /// </summary>
        /// <param name="transform">The transformation to apply.</param>
        /// <param name="array">A Vector2[] to transform.</param>
        /// <returns>The transformed copy of the Vector2[].</returns>
        public static Vector2[] operator *(Transform2D transform, Vector2[] array)
        {
            Vector2[] newArray = new Vector2[array.Length];

            for (int i = 0; i < array.Length; i++)
            {
                newArray[i] = transform * array[i];
            }

            return newArray;
        }

        /// <summary>
        /// Returns a copy of the given Vector2[] transformed (multiplied) by the inverse transformation matrix.
        /// </summary>
        /// <param name="array">A Vector2[] to inversely transform.</param>
        /// <param name="transform">The transformation to apply.</param>
        /// <returns>The inversely transformed copy of the Vector2[].</returns>
        public static Vector2[] operator *(Vector2[] array, Transform2D transform)
        {
            Vector2[] newArray = new Vector2[array.Length];

            for (int i = 0; i < array.Length; i++)
            {
                newArray[i] = array[i] * transform;
            }

            return newArray;
        }

        /// <summary>
        /// Returns <see langword="true"/> if the transforms are exactly equal.
        /// Note: Due to floating-point precision errors, consider using
        /// <see cref="IsEqualApprox"/> instead, which is more reliable.
        /// </summary>
        /// <param name="left">The left transform.</param>
        /// <param name="right">The right transform.</param>
        /// <returns>Whether or not the transforms are exactly equal.</returns>
        public static bool operator ==(Transform2D left, Transform2D right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Returns <see langword="true"/> if the transforms are not equal.
        /// Note: Due to floating-point precision errors, consider using
        /// <see cref="IsEqualApprox"/> instead, which is more reliable.
        /// </summary>
        /// <param name="left">The left transform.</param>
        /// <param name="right">The right transform.</param>
        /// <returns>Whether or not the transforms are not equal.</returns>
        public static bool operator !=(Transform2D left, Transform2D right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Returns <see langword="true"/> if the transform is exactly equal
        /// to the given object (<see paramref="obj"/>).
        /// Note: Due to floating-point precision errors, consider using
        /// <see cref="IsEqualApprox"/> instead, which is more reliable.
        /// </summary>
        /// <param name="obj">The object to compare with.</param>
        /// <returns>Whether or not the transform and the object are exactly equal.</returns>
        public override readonly bool Equals(object obj)
        {
            return obj is Transform2D other && Equals(other);
        }

        /// <summary>
        /// Returns <see langword="true"/> if the transforms are exactly equal.
        /// Note: Due to floating-point precision errors, consider using
        /// <see cref="IsEqualApprox"/> instead, which is more reliable.
        /// </summary>
        /// <param name="other">The other transform to compare.</param>
        /// <returns>Whether or not the matrices are exactly equal.</returns>
        public readonly bool Equals(Transform2D other)
        {
            return x.Equals(other.x) && y.Equals(other.y) && origin.Equals(other.origin);
        }

        /// <summary>
        /// Returns <see langword="true"/> if this transform and <paramref name="other"/> are approximately equal,
        /// by running <see cref="Vector2.IsEqualApprox(Vector2)"/> on each component.
        /// </summary>
        /// <param name="other">The other transform to compare.</param>
        /// <returns>Whether or not the matrices are approximately equal.</returns>
        public readonly bool IsEqualApprox(Transform2D other)
        {
            return x.IsEqualApprox(other.x) && y.IsEqualApprox(other.y) && origin.IsEqualApprox(other.origin);
        }

        /// <summary>
        /// Serves as the hash function for <see cref="Transform2D"/>.
        /// </summary>
        /// <returns>A hash code for this transform.</returns>
        public override readonly int GetHashCode()
        {
            return x.GetHashCode() ^ y.GetHashCode() ^ origin.GetHashCode();
        }

        /// <summary>
        /// Converts this <see cref="Transform2D"/> to a string.
        /// </summary>
        /// <returns>A string representation of this transform.</returns>
        public override readonly string ToString()
        {
            return $"[X: {x}, Y: {y}, O: {origin}]";
        }

        /// <summary>
        /// Converts this <see cref="Transform2D"/> to a string with the given <paramref name="format"/>.
        /// </summary>
        /// <returns>A string representation of this transform.</returns>
        public readonly string ToString(string format)
        {
            return $"[X: {x.ToString(format)}, Y: {y.ToString(format)}, O: {origin.ToString(format)}]";
        }
    }
}
