// MIT License - Copyright (C) The Mono.Xna Team
// Portions of this file are based on work by The Mono.Xna Team and are subject to
// the terms and conditions defined in file 'LICENSE.txt', which is part of this source code package.
//
// Additional modifications and work by Helena.

namespace helengine {
    public struct float3 : IEquatable<float3> {
        private static readonly float3 zero = new(0f, 0f, 0f);
        private static readonly float3 one = new(1f, 1f, 1f);

        public static float3 Zero {
            get { return zero; }
        }

        public static float3 One {
            get { return one; }
        }

        public float X;
        public float Y;
        public float Z;

        public float3(float x, float y, float z) {
            this.X = x;
            this.Y = y;
            this.Z = z;
        }

        public float3(float value) {
            this.X = value;
            this.Y = value;
            this.Z = value;
        }

        public float3(float2 value, float z) {
            this.X = value.X;
            this.Y = value.Y;
            this.Z = z;
        }

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

        public bool Equals(float3 other) {
            return X == other.X &&
                    Y == other.Y &&
                    Z == other.Z;
        }

        public static bool operator ==(float3 a, float3 b) {
            return a.X == b.X && a.Y == b.Y && a.Z == b.Z;
        }

        public static bool operator !=(float3 a, float3 b) {
            return a.X != b.X || a.Y != b.Y || a.Z != b.Z;
        }

        public static float3 operator +(float3 a, float3 b) {
            return new float3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        public static float3 operator *(float3 a, float3 b) {
            return new float3(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
        }

        public static float3 operator *(float3 a, float scalar) {
            return new float3(a.X * scalar, a.Y * scalar, a.Z * scalar);
        }

        public static float3 operator /(float3 a, float3 b) {
            return new float3(a.X / b.X, a.Y / b.Y, a.Z / b.Z);
        }

        public static float3 operator /(float3 a, float scalar) {
            return new float3(a.X / scalar, a.Y / scalar, a.Z / scalar);
        }

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
