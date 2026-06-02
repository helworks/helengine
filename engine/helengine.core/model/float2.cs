// MIT License - Copyright (C) The Mono.Xna Team
// Portions of this file are based on work by The Mono.Xna Team and are subject to
// the terms and conditions defined in file 'LICENSE.txt', which is part of this source code package.
//
// Additional modifications and work by Helena.

namespace helengine {
    /// <summary>
    /// Represents a 2D vector of single-precision floating point components.
    /// </summary>
    public struct float2 {
        /// <summary>
        /// X component of the vector.
        /// </summary>
        public float X;

        /// <summary>
        /// Y component of the vector.
        /// </summary>
        public float Y;

        /// <summary>
        /// Initializes a vector with the specified components.
        /// </summary>
        /// <param name="x">Value for the X component.</param>
        /// <param name="y">Value for the Y component.</param>
        public float2(float x, float y) {
            X = x;
            Y = y;
        }

        /// <summary>
        /// Initializes a vector with both components set to the same value.
        /// </summary>
        /// <param name="value">Value to assign to both X and Y.</param>
        public float2(float value) {
            X = value;
            Y = value;
        }

        /// <summary>
        /// Computes the squared Euclidean length of this vector.
        /// </summary>
        /// <returns>Squared magnitude.</returns>
        public float LengthSquared() {
            return X * X + Y * Y;
        }

        /// <summary>
        /// Computes the Euclidean length of this vector.
        /// </summary>
        /// <returns>Vector magnitude.</returns>
        public float Length() {
            return (float)Math.Sqrt(LengthSquared());
        }

        /// <summary>
        /// Computes the dot product between two vectors.
        /// </summary>
        /// <param name="value1">First vector.</param>
        /// <param name="value2">Second vector.</param>
        /// <returns>Dot product value.</returns>
        public static float Dot(float2 value1, float2 value2) {
            return value1.X * value2.X + value1.Y * value2.Y;
        }

        /// <summary>
        /// Returns a unit-length vector pointing in the same direction as the supplied vector.
        /// </summary>
        /// <param name="value">Vector to normalize.</param>
        /// <returns>Normalized vector.</returns>
        public static float2 Normalize(float2 value) {
            float factor = (float)Math.Sqrt((value.X * value.X) + (value.Y * value.Y));
            factor = 1f / factor;
            return new float2(value.X * factor, value.Y * factor);
        }

        /// <summary>
        /// Determines whether this instance and another object are equal.
        /// </summary>
        /// <param name="obj">Object to compare.</param>
        /// <returns>True when the vectors have identical components; otherwise false.</returns>
        public override bool Equals(object obj) {
            if (obj == null) {
                return false;
            } else if (!(obj is float2)) {
                return false;
            }

            var other = (float2)obj;
            return X == other.X &&
                    Y == other.Y;
        }

        /// <summary>
        /// Determines whether this instance equals the supplied <see cref="float3"/> (ignoring Z).
        /// </summary>
        /// <param name="other">Vector to compare.</param>
        /// <returns>True when the X and Y components match.</returns>
        public bool Equals(float3 other) {
            return X == other.X &&
                    Y == other.Y;
        }

        /// <summary>
        /// Compares two vectors for equality.
        /// </summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        /// <returns>True when components match.</returns>
        public static bool operator ==(float2 a, float2 b) {
            return a.X == b.X && a.Y == b.Y;
        }

        /// <summary>
        /// Compares two vectors for inequality.
        /// </summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        /// <returns>True when any component differs.</returns>
        public static bool operator !=(float2 a, float2 b) {
            return a.X != b.X || a.Y != b.Y;
        }

        /// <summary>
        /// Adds two vectors component-wise.
        /// </summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        /// <returns>Summed vector.</returns>
        public static float2 operator +(float2 a, float2 b) {
            return new float2(a.X + b.X, a.Y + b.Y);
        }

        /// <summary>
        /// Negates both vector components.
        /// </summary>
        /// <param name="value">Vector to negate.</param>
        /// <returns>Vector with both components sign-inverted.</returns>
        public static float2 operator -(float2 value) {
            return new float2(-value.X, -value.Y);
        }

        /// <summary>
        /// Subtracts one vector from another component-wise.
        /// </summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        /// <returns>Difference vector.</returns>
        public static float2 operator -(float2 a, float2 b) {
            return new float2(a.X - b.X, a.Y - b.Y);
        }

        /// <summary>
        /// Multiplies two vectors component-wise.
        /// </summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        /// <returns>Scaled vector.</returns>
        public static float2 operator *(float2 a, float2 b) {
            return new float2(a.X * b.X, a.Y * b.Y);
        }

        /// <summary>
        /// Multiplies a vector by a scalar.
        /// </summary>
        /// <param name="a">Vector to scale.</param>
        /// <param name="scalar">Scalar multiplier.</param>
        /// <returns>Scaled vector.</returns>
        public static float2 operator *(float2 a, float scalar) {
            return new float2(a.X * scalar, a.Y * scalar);
        }

        /// <summary>
        /// Divides two vectors component-wise.
        /// </summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        /// <returns>Resulting vector.</returns>
        public static float2 operator /(float2 a, float2 b) {
            return new float2(a.X / b.X, a.Y / b.Y);
        }

        /// <summary>
        /// Divides a vector by a scalar.
        /// </summary>
        /// <param name="a">Vector to scale.</param>
        /// <param name="scalar">Scalar divisor.</param>
        /// <returns>Scaled vector.</returns>
        public static float2 operator /(float2 a, float scalar) {
            return new float2(a.X / scalar, a.Y / scalar);
        }

        /// <summary>
        /// Returns a hash code for the vector.
        /// </summary>
        /// <returns>Hash code.</returns>
        public override int GetHashCode() {
            unchecked {
                int hash = X.GetHashCode();
                hash = (hash * 397) ^ Y.GetHashCode();
                return hash;
            }
        }
    }
}
