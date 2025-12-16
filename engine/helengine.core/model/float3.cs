// MIT License - Copyright (C) The Mono.Xna Team
// Portions of this file are based on work by The Mono.Xna Team and are subject to
// the terms and conditions defined in file 'LICENSE.txt', which is part of this source code package.
//
// Additional modifications and work by Helena.

namespace helengine {
    /// <summary>
    /// Represents a 3D vector of single-precision floating point components.
    /// </summary>
    public struct float3 : IEquatable<float3> {
        /// <summary>
        /// Zero vector (0, 0, 0).
        /// </summary>
        private static readonly float3 zero = new(0f, 0f, 0f);
        /// <summary>
        /// Unit vector with all components set to one.
        /// </summary>
        private static readonly float3 one = new(1f, 1f, 1f);

        /// <summary>
        /// X component of the vector.
        /// </summary>
        public float X;

        /// <summary>
        /// Y component of the vector.
        /// </summary>
        public float Y;

        /// <summary>
        /// Z component of the vector.
        /// </summary>
        public float Z;

        /// <summary>
        /// Initializes a vector with the specified components.
        /// </summary>
        /// <param name="x">Value for the X component.</param>
        /// <param name="y">Value for the Y component.</param>
        /// <param name="z">Value for the Z component.</param>
        public float3(float x, float y, float z) {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Initializes a vector with all components set to the same value.
        /// </summary>
        /// <param name="value">Value applied to X, Y, and Z.</param>
        public float3(float value) {
            X = value;
            Y = value;
            Z = value;
        }

        /// <summary>
        /// Initializes a vector using a 2D vector for X and Y, plus a supplied Z.
        /// </summary>
        /// <param name="value">Vector providing X and Y values.</param>
        /// <param name="z">Value for the Z component.</param>
        public float3(float2 value, float z) {
            X = value.X;
            Y = value.Y;
            Z = z;
        }

        /// <summary>
        /// Gets a zero vector.
        /// </summary>
        public static float3 Zero {
            get { return zero; }
        }

        /// <summary>
        /// Gets a vector with all components set to one.
        /// </summary>
        public static float3 One {
            get { return one; }
        }

        /// <summary>
        /// Returns a string representing the vector components.
        /// </summary>
        /// <returns>Formatted string.</returns>
        public override string ToString() {
            return $"{X}, {Y}, {Z}";
        }

        /// <summary>
        /// Normalizes a vector to unit length.
        /// </summary>
        /// <param name="value">Vector to normalize.</param>
        /// <returns>Normalized vector.</returns>
        public static float3 Normalize(float3 value) {
            float factor = (float)Math.Sqrt((value.X * value.X) + (value.Y * value.Y) + (value.Z * value.Z));
            factor = 1f / factor;
            return new float3(value.X * factor, value.Y * factor, value.Z * factor);
        }

        /// <summary>
        /// Computes the cross product of two vectors.
        /// </summary>
        /// <param name="vector1">First vector.</param>
        /// <param name="vector2">Second vector.</param>
        /// <returns>Cross product.</returns>
        public static float3 Cross(float3 vector1, float3 vector2) {
            Cross(ref vector1, ref vector2, out vector1);
            return vector1;
        }

        /// <summary>
        /// Computes the cross product of two vectors.
        /// </summary>
        /// <param name="vector1">First vector.</param>
        /// <param name="vector2">Second vector.</param>
        /// <param name="result">Result of the cross product.</param>
        public static void Cross(ref float3 vector1, ref float3 vector2, out float3 result) {
            var x = vector1.Y * vector2.Z - vector2.Y * vector1.Z;
            var y = -(vector1.X * vector2.Z - vector2.X * vector1.Z);
            var z = vector1.X * vector2.Y - vector2.X * vector1.Y;
            result.X = x;
            result.Y = y;
            result.Z = z;
        }

        /// <summary>
        /// Computes the dot product of two vectors.
        /// </summary>
        /// <param name="value1">First vector.</param>
        /// <param name="value2">Second vector.</param>
        /// <returns>Dot product value.</returns>
        public static float Dot(float3 value1, float3 value2) {
            return value1.X * value2.X + value1.Y * value2.Y + value1.Z * value2.Z;
        }

        /// <summary>
        /// Computes the dot product of two vectors.
        /// </summary>
        /// <param name="value1">First vector.</param>
        /// <param name="value2">Second vector.</param>
        /// <param name="result">Output dot product.</param>
        public static void Dot(ref float3 value1, ref float3 value2, out float result) {
            result = value1.X * value2.X + value1.Y * value2.Y + value1.Z * value2.Z;
        }

        /// <summary>
        /// Determines whether this instance and another object are equal.
        /// </summary>
        /// <param name="obj">Object to compare.</param>
        /// <returns>True if the components match; otherwise false.</returns>
        public override bool Equals(object? obj) {
            if (obj == null) {
                return false;
            } else if (!(obj is float3)) {
                return false;
            }

            var other = (float3)obj;
            return X == other.X &&
                    Y == other.Y &&
                    Z == other.Z;
        }

        /// <summary>
        /// Determines whether this instance equals another vector.
        /// </summary>
        /// <param name="other">Vector to compare.</param>
        /// <returns>True when components are identical.</returns>
        public bool Equals(float3 other) {
            return X == other.X &&
                    Y == other.Y &&
                    Z == other.Z;
        }

        /// <summary>
        /// Compares two vectors for equality.
        /// </summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        /// <returns>True when components match.</returns>
        public static bool operator ==(float3 a, float3 b) {
            return a.X == b.X && a.Y == b.Y && a.Z == b.Z;
        }

        /// <summary>
        /// Compares two vectors for inequality.
        /// </summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        /// <returns>True when any component differs.</returns>
        public static bool operator !=(float3 a, float3 b) {
            return a.X != b.X || a.Y != b.Y || a.Z != b.Z;
        }

        /// <summary>
        /// Adds two vectors component-wise.
        /// </summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        /// <returns>Resulting vector.</returns>
        public static float3 operator +(float3 a, float3 b) {
            return new float3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        /// <summary>
        /// Subtracts two vectors component-wise.
        /// </summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        /// <returns>Resulting vector.</returns>
        public static float3 operator -(float3 a, float3 b) {
            return new float3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        /// <summary>
        /// Multiplies two vectors component-wise.
        /// </summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        /// <returns>Resulting vector.</returns>
        public static float3 operator *(float3 a, float3 b) {
            return new float3(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
        }

        /// <summary>
        /// Multiplies a vector by a scalar.
        /// </summary>
        /// <param name="a">Vector to scale.</param>
        /// <param name="scalar">Scalar multiplier.</param>
        /// <returns>Scaled vector.</returns>
        public static float3 operator *(float3 a, float scalar) {
            return new float3(a.X * scalar, a.Y * scalar, a.Z * scalar);
        }

        /// <summary>
        /// Divides two vectors component-wise.
        /// </summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        /// <returns>Resulting vector.</returns>
        public static float3 operator /(float3 a, float3 b) {
            return new float3(a.X / b.X, a.Y / b.Y, a.Z / b.Z);
        }

        /// <summary>
        /// Divides a vector by a scalar.
        /// </summary>
        /// <param name="a">Vector to scale.</param>
        /// <param name="scalar">Scalar divisor.</param>
        /// <returns>Resulting vector.</returns>
        public static float3 operator /(float3 a, float scalar) {
            return new float3(a.X / scalar, a.Y / scalar, a.Z / scalar);
        }

        /// <summary>
        /// Returns a hash code for the vector.
        /// </summary>
        /// <returns>Hash code.</returns>
        public override int GetHashCode() {
            unchecked {
                var hashCode = X.GetHashCode();
                hashCode = (hashCode * 397) ^ Y.GetHashCode();
                hashCode = (hashCode * 397) ^ Z.GetHashCode();
                return hashCode;
            }
        }
    }
}
