// MIT License - Copyright (C) The Mono.Xna Team
// Portions of this file are based on work by The Mono.Xna Team and are subject to
// the terms and conditions defined in file 'LICENSE.txt', which is part of this source code package.
//
// Additional modifications and work by Helena.

namespace helengine {
    /// <summary>
    /// Represents a four-component build-configurable Real value used for quaternion storage.
    /// </summary>
    public struct float4 {
        /// <summary>
        /// Zero value with all components set to 0.
        /// </summary>
        private static readonly float4 ZeroValue = new float4(0, 0, 0, 0);

        /// <summary>
        /// Identity quaternion representing no rotation.
        /// </summary>
        private static readonly float4 IdentityValue = new float4(0, 0, 0, 1);

        /// <summary>
        /// Value with all components set to one.
        /// </summary>
        private static readonly float4 OneValue = new float4(1, 1, 1, 1);

        /// <summary>
        /// X component of the quaternion.
        /// </summary>
        public Real X;

        /// <summary>
        /// Y component of the quaternion.
        /// </summary>
        public Real Y;

        /// <summary>
        /// Z component of the quaternion.
        /// </summary>
        public Real Z;

        /// <summary>
        /// W component representing the rotation scalar.
        /// </summary>
        public Real W;

        /// <summary>
        /// Constructs a quaternion with X, Y, Z, and W values.
        /// </summary>
        /// <param name="x">X component.</param>
        /// <param name="y">Y component.</param>
        /// <param name="z">Z component.</param>
        /// <param name="w">Rotation scalar.</param>
        public float4(Real x, Real y, Real z, Real w) {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        /// <summary>
        /// Constructs a four-component value with all components set to the same scalar.
        /// </summary>
        /// <param name="value">Scalar applied to every component.</param>
        public float4(Real value) {
            X = value;
            Y = value;
            Z = value;
            W = value;
        }

        /// <summary>
        /// Constructs a four-component value from one three-component vector and one scalar tail component.
        /// </summary>
        /// <param name="xyz">Three-component source used for X, Y, and Z.</param>
        /// <param name="w">Scalar used for W.</param>
        public float4(float3 xyz, Real w) {
            X = xyz.X;
            Y = xyz.Y;
            Z = xyz.Z;
            W = w;
        }

        /// <summary>
        /// Returns a quaternion representing no rotation.
        /// </summary>
        public static float4 Identity {
            get { return IdentityValue; }
        }

        /// <summary>
        /// Returns a zero value with all components cleared.
        /// </summary>
        public static float4 Zero {
            get { return ZeroValue; }
        }

        /// <summary>
        /// Returns a value with all components set to one.
        /// </summary>
        public static float4 One {
            get { return OneValue; }
        }

        /// <summary>
        /// Returns the component-wise square root of the supplied value.
        /// </summary>
        /// <param name="value">Value whose components should be square-rooted.</param>
        /// <returns>Value composed of the square roots of each component.</returns>
        public static float4 SquareRoot(float4 value) {
            return new float4(
                RealMath.Sqrt(value.X),
                RealMath.Sqrt(value.Y),
                RealMath.Sqrt(value.Z),
                RealMath.Sqrt(value.W));
        }

        /// <summary>
        /// Computes the squared Euclidean length of this quaternion value.
        /// </summary>
        /// <returns>Squared magnitude.</returns>
        public RealEvaluation LengthSquared() {
            return X * X + Y * Y + Z * Z + W * W;
        }

        /// <summary>
        /// Computes the Euclidean length of this quaternion value.
        /// </summary>
        /// <returns>Quaternion magnitude.</returns>
        public RealEvaluation Length() {
            return RealMath.Sqrt(LengthSquared());
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
        public bool Contains(RealEvaluation x, RealEvaluation y) {
            return ((((X <= x) && (x < (X + Z))) && (Y <= y)) && (y < (Y + W)));
        }

        /// <summary>
        /// Scales the quaternion magnitude to unit length.
        /// </summary>
        public void Normalize() {
            float4 normalized = TransformNumericKernel<RealEvaluation, RuntimeNumericOperations>.Normalize(this, new RuntimeNumericOperations());
            X = normalized.X; Y = normalized.Y; Z = normalized.Z; W = normalized.W;
        }

        /// <summary>
        /// Linearly interpolates between two quaternions and normalizes the result.
        /// </summary>
        /// <param name="start">Start quaternion.</param>
        /// <param name="end">End quaternion.</param>
        /// <param name="amount">Interpolation amount where 0 returns <paramref name="start"/> and 1 returns <paramref name="end"/>.</param>
        /// <returns>Normalized interpolated quaternion.</returns>
        public static float4 Lerp(float4 start, float4 end, RealEvaluation amount) {
            RealEvaluation normalizedAmount = amount;
            RealEvaluation dot =
                (start.X * end.X) +
                (start.Y * end.Y) +
                (start.Z * end.Z) +
                (start.W * end.W);

            float4 adjustedEnd = end;
            if (dot < 0d) {
                adjustedEnd = new float4(-end.X, -end.Y, -end.Z, -end.W);
            }

            float4 result = new float4(
                (start.X + ((adjustedEnd.X - start.X) * normalizedAmount)),
                (start.Y + ((adjustedEnd.Y - start.Y) * normalizedAmount)),
                (start.Z + ((adjustedEnd.Z - start.Z) * normalizedAmount)),
                (start.W + ((adjustedEnd.W - start.W) * normalizedAmount)));
            result.Normalize();
            return result;
        }

        /// <summary>
        /// Returns the component-wise minimum between two four-component values.
        /// </summary>
        /// <param name="left">First value to compare.</param>
        /// <param name="right">Second value to compare.</param>
        /// <returns>Value composed of the minimum components.</returns>
        public static float4 Min(float4 left, float4 right) {
            return new float4(
                left.X < right.X ? left.X : right.X,
                left.Y < right.Y ? left.Y : right.Y,
                left.Z < right.Z ? left.Z : right.Z,
                left.W < right.W ? left.W : right.W);
        }

        /// <summary>
        /// Returns the component-wise maximum between two four-component values.
        /// </summary>
        /// <param name="left">First value to compare.</param>
        /// <param name="right">Second value to compare.</param>
        /// <returns>Value composed of the maximum components.</returns>
        public static float4 Max(float4 left, float4 right) {
            return new float4(
                left.X > right.X ? left.X : right.X,
                left.Y > right.Y ? left.Y : right.Y,
                left.Z > right.Z ? left.Z : right.Z,
                left.W > right.W ? left.W : right.W);
        }

        /// <summary>
        /// Clamps each component of a value between the corresponding minimum and maximum bounds.
        /// </summary>
        /// <param name="value">Value to clamp.</param>
        /// <param name="min">Minimum component bounds.</param>
        /// <param name="max">Maximum component bounds.</param>
        /// <returns>Clamped value.</returns>
        public static float4 Clamp(float4 value, float4 min, float4 max) {
            return new float4(
                value.X < min.X ? min.X : value.X > max.X ? max.X : value.X,
                value.Y < min.Y ? min.Y : value.Y > max.Y ? max.Y : value.Y,
                value.Z < min.Z ? min.Z : value.Z > max.Z ? max.Z : value.Z,
                value.W < min.W ? min.W : value.W > max.W ? max.W : value.W);
        }

        /// <summary>
        /// Computes the dot product between two four-component values.
        /// </summary>
        /// <param name="left">First value.</param>
        /// <param name="right">Second value.</param>
        /// <returns>Sum of the component-wise products.</returns>
        public static RealEvaluation Dot(float4 left, float4 right) {
            return
                (left.X * right.X) +
                (left.Y * right.Y) +
                (left.Z * right.Z) +
                (left.W * right.W);
        }

        /// <summary>
        /// Rotates a vector by the provided quaternion.
        /// </summary>
        /// <param name="value">Vector to rotate.</param>
        /// <param name="rotation">Quaternion rotation.</param>
        /// <returns>Rotated vector.</returns>
        public static float3 RotateVector(float3 value, float4 rotation) {
            return TransformNumericKernel<RealEvaluation, RuntimeNumericOperations>.Rotate(value, rotation, new RuntimeNumericOperations());
        }

        /// <summary>
        /// Computes the inverse quaternion that undoes the provided rotation.
        /// </summary>
        /// <param name="value">Quaternion to invert.</param>
        /// <returns>Inverse quaternion.</returns>
        public static float4 Inverse(float4 value) {
            RealEvaluation lengthSquared = (value.X * value.X) + (value.Y * value.Y) + (value.Z * value.Z) + (value.W * value.W);
            if (lengthSquared <= 0.0) {
                throw new InvalidOperationException("Cannot invert a zero-length quaternion.");
            }

            RealEvaluation inverseLengthSquared = 1.0 / lengthSquared;
            return new float4(
                (-value.X * inverseLengthSquared),
                (-value.Y * inverseLengthSquared),
                (-value.Z * inverseLengthSquared),
                (value.W * inverseLengthSquared));
        }

        /// <summary>
        /// Creates a quaternion from yaw, pitch, and roll angles.
        /// </summary>
        /// <param name="yaw">Yaw around the Y axis in radians.</param>
        /// <param name="pitch">Pitch around the X axis in radians.</param>
        /// <param name="roll">Roll around the Z axis in radians.</param>
        /// <param name="result">Output quaternion.</param>
        public static void CreateFromYawPitchRoll(RealEvaluation yaw, RealEvaluation pitch, RealEvaluation roll, out float4 result) {
            RealEvaluation halfRoll = roll * 0.5f;
            RealEvaluation halfPitch = pitch * 0.5f;
            RealEvaluation halfYaw = yaw * 0.5f;

            RealEvaluation sinRoll = RealMath.Sin(halfRoll);
            RealEvaluation cosRoll = RealMath.Cos(halfRoll);
            RealEvaluation sinPitch = RealMath.Sin(halfPitch);
            RealEvaluation cosPitch = RealMath.Cos(halfPitch);
            RealEvaluation sinYaw = RealMath.Sin(halfYaw);
            RealEvaluation cosYaw = RealMath.Cos(halfYaw);

            result.X = (cosYaw * sinPitch * cosRoll) + (sinYaw * cosPitch * sinRoll);
            result.Y = (sinYaw * cosPitch * cosRoll) - (cosYaw * sinPitch * sinRoll);
            result.Z = (cosYaw * cosPitch * sinRoll) - (sinYaw * sinPitch * cosRoll);
            result.W = (cosYaw * cosPitch * cosRoll) + (sinYaw * sinPitch * sinRoll);
        }

        /// <summary>
        /// Decomposes this quaternion into pitch, yaw and roll angles expressed in degrees.
        /// The decomposition is the inverse of <see cref="CreateFromYawPitchRoll(Real, Real, Real, out float4)"/>:
        /// pitch rotates around X, yaw around Y and roll around Z. Near the poles the pitch term saturates, so the
        /// angle is clamped to the pole instead of feeding an out-of-range value to the arc sine.
        /// All intermediate math runs in RealEvaluation precision so the angles stay stable for near-normalized inputs.
        /// </summary>
        /// <param name="pitch">Pitch angle around the X axis in degrees.</param>
        /// <param name="yaw">Yaw angle around the Y axis in degrees.</param>
        /// <param name="roll">Roll angle around the Z axis in degrees.</param>
        public void ToEulerDegrees(out double pitch, out double yaw, out double roll) {
            RealEvaluation pitchValue;
            RealEvaluation x = X;
            RealEvaluation y = Y;
            RealEvaluation z = Z;
            RealEvaluation w = W;

            RealEvaluation sinPitch = 2.0 * (w * x - y * z);
            if (sinPitch >= 1.0) {
                pitchValue = RealMath.PI / 2.0;
            } else if (sinPitch <= -1.0) {
                pitchValue = -(RealMath.PI / 2.0);
            } else {
                pitchValue = RealMath.Asin(sinPitch);
            }

            RealEvaluation sinYaw = 2.0 * (w * y + x * z);
            RealEvaluation cosYaw = 1.0 - 2.0 * (x * x + y * y);
            RealEvaluation yawValue = RealMath.Atan2(sinYaw, cosYaw);

            RealEvaluation sinRoll = 2.0 * (w * z + x * y);
            RealEvaluation cosRoll = 1.0 - 2.0 * (y * y + z * z);
            RealEvaluation rollValue = RealMath.Atan2(sinRoll, cosRoll);

            RealEvaluation radiansToDegrees = 180.0 / RealMath.PI;
            pitch = (double)(pitchValue * radiansToDegrees);
            yaw = (double)(yawValue * radiansToDegrees);
            roll = (double)(rollValue * radiansToDegrees);
        }

        /// <summary>
        /// Concatenates two quaternions.
        /// </summary>
        /// <param name="value1">First quaternion.</param>
        /// <param name="value2">Second quaternion.</param>
        /// <param name="result">Resulting concatenated quaternion.</param>
        public static void Concatenate(ref float4 value1, ref float4 value2, out float4 result) {
            result = TransformNumericKernel<RealEvaluation, RuntimeNumericOperations>.Concatenate(value1, value2, new RuntimeNumericOperations());
        }

        /// <summary>
        /// Creates a quaternion from an axis and angle.
        /// </summary>
        /// <param name="axis">Axis of rotation.</param>
        /// <param name="angle">Angle in radians.</param>
        /// <param name="result">Output quaternion.</param>
        public static void CreateFromAxisAngle(float3 axis, RealEvaluation angle, out float4 result) {
            CreateFromAxisAngle(ref axis, angle, out result);
        }

        /// <summary>
        /// Creates a quaternion from an axis and angle.
        /// </summary>
        /// <param name="axis">Axis of rotation.</param>
        /// <param name="angle">Angle in radians.</param>
        /// <param name="result">Output quaternion.</param>
        public static void CreateFromAxisAngle(ref float3 axis, RealEvaluation angle, out float4 result) {
            RealEvaluation half = angle * 0.5f;
            RealEvaluation sin = RealMath.Sin(half);
            RealEvaluation cos = RealMath.Cos(half);
            result.X = axis.X * sin;
            result.Y = axis.Y * sin;
            result.Z = axis.Z * sin;
            result.W = cos;
        }

        /// <summary>
        /// Negates every component of the provided four-component value.
        /// </summary>
        /// <param name="value">Value to negate.</param>
        /// <returns>Negated four-component value.</returns>
        public static float4 operator -(float4 value) {
            return new float4(-value.X, -value.Y, -value.Z, -value.W);
        }

        /// <summary>
        /// Adds two values component-wise.
        /// </summary>
        /// <param name="left">Left operand.</param>
        /// <param name="right">Right operand.</param>
        /// <returns>Component-wise sum.</returns>
        public static float4 operator +(float4 left, float4 right) {
            return new float4(left.X + right.X, left.Y + right.Y, left.Z + right.Z, left.W + right.W);
        }

        /// <summary>
        /// Subtracts two values component-wise.
        /// </summary>
        /// <param name="left">Left operand.</param>
        /// <param name="right">Right operand.</param>
        /// <returns>Component-wise difference.</returns>
        public static float4 operator -(float4 left, float4 right) {
            return new float4(left.X - right.X, left.Y - right.Y, left.Z - right.Z, left.W - right.W);
        }

        /// <summary>
        /// Multiplies two values component-wise.
        /// </summary>
        /// <param name="left">Left operand.</param>
        /// <param name="right">Right operand.</param>
        /// <returns>Component-wise product.</returns>
        public static float4 operator *(float4 left, float4 right) {
            return new float4(left.X * right.X, left.Y * right.Y, left.Z * right.Z, left.W * right.W);
        }

        /// <summary>
        /// Multiplies a value by a scalar.
        /// </summary>
        /// <param name="value">Value to scale.</param>
        /// <param name="scalar">Scalar multiplier.</param>
        /// <returns>Scaled value.</returns>
        public static float4 operator *(float4 value, RealEvaluation scalar) {
            return new float4(value.X * scalar, value.Y * scalar, value.Z * scalar, value.W * scalar);
        }

        /// <summary>
        /// Divides two values component-wise.
        /// </summary>
        /// <param name="left">Left operand.</param>
        /// <param name="right">Right operand.</param>
        /// <returns>Component-wise quotient.</returns>
        public static float4 operator /(float4 left, float4 right) {
            return new float4(left.X / right.X, left.Y / right.Y, left.Z / right.Z, left.W / right.W);
        }

        /// <summary>
        /// Divides a value by a scalar.
        /// </summary>
        /// <param name="value">Value to scale.</param>
        /// <param name="scalar">Scalar divisor.</param>
        /// <returns>Scaled value.</returns>
        public static float4 operator /(float4 value, RealEvaluation scalar) {
            return new float4(value.X / scalar, value.Y / scalar, value.Z / scalar, value.W / scalar);
        }
    }
}
