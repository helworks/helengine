// MIT License - Copyright (C) The Mono.Xna Team
// Portions of this file are based on work by The Mono.Xna Team and are subject to
// the terms and conditions defined in file 'LICENSE.txt', which is part of this source code package.
//
// Additional modifications and work by Helena.

namespace helengine {
    public struct float2 {
        public float X;
        public float Y;

        public float2(float x, float y) {
            this.X = x;
            this.Y = y;
        }

        public float2(float value) {
            this.X = value;
            this.Y = value;
        }

        public override bool Equals(object? obj) {
            if (obj == null) {
                return false;
            } else if (!(obj is float2)) {
                return false;
            }

            var other = (float2)obj;
            return X == other.X &&
                    Y == other.Y;
        }

        public bool Equals(float3 other) {
            return X == other.X &&
                    Y == other.Y;
        }

        public static bool operator ==(float2 a, float2 b) {
            return a.X == b.X && a.Y == b.Y;
        }

        public static bool operator !=(float2 a, float2 b) {
            return a.X != b.X || a.Y != b.Y;
        }

        public static float2 operator *(float2 a, float2 b) {
            return new float2(a.X * b.X, a.Y * b.Y);
        }

        public static float2 operator *(float2 a, float scalar) {
            return new float2(a.X * scalar, a.Y * scalar);
        }

        public static float2 operator /(float2 a, float2 b) {
            return new float2(a.X / b.X, a.Y / b.Y);
        }

        public static float2 operator /(float2 a, float scalar) {
            return new float2(a.X / scalar, a.Y / scalar);
        }
    }
}
