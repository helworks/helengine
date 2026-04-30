// MIT License - Copyright (C) The Mono.Xna Team
// Portions of this file are based on work by The Mono.Xna Team and are subject to
// the terms and conditions defined in file 'LICENSE.txt', which is part of this source code package.
//
// Additional modifications and work by Helena.

namespace helengine {
    /// <summary>
    /// Represents a quaternion with single-precision floating point components.
    /// </summary>
    public struct float4 {
        /// <summary>
        /// Identity quaternion representing no rotation.
        /// </summary>
        private static readonly float4 identity = new float4(0, 0, 0, 1);

        /// <summary>
        /// X component of the quaternion.
        /// </summary>
        public float X;

        /// <summary>
        /// Y component of the quaternion.
        /// </summary>
        public float Y;

        /// <summary>
        /// Z component of the quaternion.
        /// </summary>
        public float Z;

        /// <summary>
        /// W component representing the rotation scalar.
        /// </summary>
        public float W;

        /// <summary>
        /// Constructs a quaternion with X, Y, Z, and W values.
        /// </summary>
        /// <param name="x">X component.</param>
        /// <param name="y">Y component.</param>
        /// <param name="z">Z component.</param>
        /// <param name="w">Rotation scalar.</param>
        public float4(float x, float y, float z, float w) {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        /// <summary>
        /// Returns a quaternion representing no rotation.
        /// </summary>
        public static float4 Identity {
            get { return identity; }
        }

        /// <summary>
        /// Returns a string representing the quaternion components.
        /// </summary>
        /// <returns>Formatted string.</returns>
        public override string ToString() {
            return $"{X}, {Y}, {Z}, {W}";
        }

        /// <summary>
        /// Gets whether the provided point lies within the bounds represented by this value.
        /// </summary>
        /// <param name="x">X coordinate to test.</param>
        /// <param name="y">Y coordinate to test.</param>
        /// <returns>True if the point is inside the bounds; otherwise false.</returns>
        public bool Contains(float x, float y) {
            return ((((X <= x) && (x < (X + Z))) && (Y <= y)) && (y < (Y + W)));
        }

        /// <summary>
        /// Scales the quaternion magnitude to unit length.
        /// </summary>
        public void Normalize() {
            float num = 1f / (float)Math.Sqrt((X * X) + (Y * Y) + (Z * Z) + (W * W));
            X *= num;
            Y *= num;
            Z *= num;
            W *= num;
        }

        /// <summary>
        /// Linearly interpolates between two quaternions and normalizes the result.
        /// </summary>
        /// <param name="start">Start quaternion.</param>
        /// <param name="end">End quaternion.</param>
        /// <param name="amount">Interpolation amount where 0 returns <paramref name="start"/> and 1 returns <paramref name="end"/>.</param>
        /// <returns>Normalized interpolated quaternion.</returns>
        public static float4 Lerp(float4 start, float4 end, float amount) {
            double normalizedAmount = amount;
            double dot =
                (start.X * end.X) +
                (start.Y * end.Y) +
                (start.Z * end.Z) +
                (start.W * end.W);

            float4 adjustedEnd = end;
            if (dot < 0d) {
                adjustedEnd = new float4(-end.X, -end.Y, -end.Z, -end.W);
            }

            float4 result = new float4(
                (float)(start.X + ((adjustedEnd.X - start.X) * normalizedAmount)),
                (float)(start.Y + ((adjustedEnd.Y - start.Y) * normalizedAmount)),
                (float)(start.Z + ((adjustedEnd.Z - start.Z) * normalizedAmount)),
                (float)(start.W + ((adjustedEnd.W - start.W) * normalizedAmount)));
            result.Normalize();
            return result;
        }

        /// <summary>
        /// Rotates a vector by the provided quaternion.
        /// </summary>
        /// <param name="value">Vector to rotate.</param>
        /// <param name="rotation">Quaternion rotation.</param>
        /// <returns>Rotated vector.</returns>
        public static float3 RotateVector(float3 value, float4 rotation) {
            double qx = rotation.X;
            double qy = rotation.Y;
            double qz = rotation.Z;
            double qw = rotation.W;

            double vx = value.X;
            double vy = value.Y;
            double vz = value.Z;

            double tx = 2.0 * (qy * vz - qz * vy);
            double ty = 2.0 * (qz * vx - qx * vz);
            double tz = 2.0 * (qx * vy - qy * vx);

            double cx = (qy * tz) - (qz * ty);
            double cy = (qz * tx) - (qx * tz);
            double cz = (qx * ty) - (qy * tx);

            double rx = vx + (tx * qw) + cx;
            double ry = vy + (ty * qw) + cy;
            double rz = vz + (tz * qw) + cz;

            return new float3((float)rx, (float)ry, (float)rz);
        }

        /// <summary>
        /// Computes the inverse quaternion that undoes the provided rotation.
        /// </summary>
        /// <param name="value">Quaternion to invert.</param>
        /// <returns>Inverse quaternion.</returns>
        public static float4 Inverse(float4 value) {
            double lengthSquared = (value.X * value.X) + (value.Y * value.Y) + (value.Z * value.Z) + (value.W * value.W);
            if (lengthSquared <= 0.0) {
                throw new InvalidOperationException("Cannot invert a zero-length quaternion.");
            }

            double inverseLengthSquared = 1.0 / lengthSquared;
            return new float4(
                (float)(-value.X * inverseLengthSquared),
                (float)(-value.Y * inverseLengthSquared),
                (float)(-value.Z * inverseLengthSquared),
                (float)(value.W * inverseLengthSquared));
        }

        /// <summary>
        /// Creates a quaternion from yaw, pitch, and roll angles.
        /// </summary>
        /// <param name="yaw">Yaw around the Y axis in radians.</param>
        /// <param name="pitch">Pitch around the X axis in radians.</param>
        /// <param name="roll">Roll around the Z axis in radians.</param>
        /// <param name="result">Output quaternion.</param>
        public static void CreateFromYawPitchRoll(float yaw, float pitch, float roll, out float4 result) {
            float halfRoll = roll * 0.5f;
            float halfPitch = pitch * 0.5f;
            float halfYaw = yaw * 0.5f;

            float sinRoll = (float)Math.Sin(halfRoll);
            float cosRoll = (float)Math.Cos(halfRoll);
            float sinPitch = (float)Math.Sin(halfPitch);
            float cosPitch = (float)Math.Cos(halfPitch);
            float sinYaw = (float)Math.Sin(halfYaw);
            float cosYaw = (float)Math.Cos(halfYaw);

            result.X = (cosYaw * sinPitch * cosRoll) + (sinYaw * cosPitch * sinRoll);
            result.Y = (sinYaw * cosPitch * cosRoll) - (cosYaw * sinPitch * sinRoll);
            result.Z = (cosYaw * cosPitch * sinRoll) - (sinYaw * sinPitch * cosRoll);
            result.W = (cosYaw * cosPitch * cosRoll) + (sinYaw * sinPitch * sinRoll);
        }

        /// <summary>
        /// Concatenates two quaternions.
        /// </summary>
        /// <param name="value1">First quaternion.</param>
        /// <param name="value2">Second quaternion.</param>
        /// <param name="result">Resulting concatenated quaternion.</param>
        public static void Concatenate(ref float4 value1, ref float4 value2, out float4 result) {
            float x1 = value1.X;
            float y1 = value1.Y;
            float z1 = value1.Z;
            float w1 = value1.W;

            float x2 = value2.X;
            float y2 = value2.Y;
            float z2 = value2.Z;
            float w2 = value2.W;

            result.X = ((x2 * w1) + (x1 * w2)) + ((y2 * z1) - (z2 * y1));
            result.Y = ((y2 * w1) + (y1 * w2)) + ((z2 * x1) - (x2 * z1));
            result.Z = ((z2 * w1) + (z1 * w2)) + ((x2 * y1) - (y2 * x1));
            result.W = (w2 * w1) - (((x2 * x1) + (y2 * y1)) + (z2 * z1));
        }

        /// <summary>
        /// Creates a quaternion from an axis and angle.
        /// </summary>
        /// <param name="axis">Axis of rotation.</param>
        /// <param name="angle">Angle in radians.</param>
        /// <param name="result">Output quaternion.</param>
        public static void CreateFromAxisAngle(ref float3 axis, float angle, out float4 result) {
            float half = angle * 0.5f;
            float sin = (float)Math.Sin(half);
            float cos = (float)Math.Cos(half);
            result.X = axis.X * sin;
            result.Y = axis.Y * sin;
            result.Z = axis.Z * sin;
            result.W = cos;
        }

        /// <summary>
        /// Multiplies two quaternions.
        /// </summary>
        /// <param name="quaternion1">Left operand.</param>
        /// <param name="quaternion2">Right operand.</param>
        /// <returns>Product quaternion.</returns>
        public static float4 operator *(float4 quaternion1, float4 quaternion2) {
            float4 quaternion;
            float x = quaternion1.X;
            float y = quaternion1.Y;
            float z = quaternion1.Z;
            float w = quaternion1.W;
            float num4 = quaternion2.X;
            float num3 = quaternion2.Y;
            float num2 = quaternion2.Z;
            float num = quaternion2.W;
            float num12 = (y * num2) - (z * num3);
            float num11 = (z * num4) - (x * num2);
            float num10 = (x * num3) - (y * num4);
            float num9 = ((x * num4) + (y * num3)) + (z * num2);
            quaternion.X = ((x * num) + (num4 * w)) + num12;
            quaternion.Y = ((y * num) + (num3 * w)) + num11;
            quaternion.Z = ((z * num) + (num2 * w)) + num10;
            quaternion.W = (w * num) - num9;
            return quaternion;
        }
    }
}
