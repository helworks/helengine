namespace helengine {
    /// <summary>
    /// Resolves world-space kinematic linear and angular velocities from one previous pose and one current pose.
    /// </summary>
    public static class KinematicPoseVelocityResolver3D {
        /// <summary>
        /// Smallest quaternion-vector length treated as non-rotating when deriving angular velocity from one pose delta.
        /// </summary>
        const double RotationEpsilonSquared = 0.0000000001d;

        /// <summary>
        /// Computes world-space linear and angular velocities that reproduce the supplied pose delta over the provided elapsed time.
        /// </summary>
        /// <param name="previousPosition">Previous world-space position.</param>
        /// <param name="previousOrientation">Previous world-space orientation.</param>
        /// <param name="currentPosition">Current world-space position.</param>
        /// <param name="currentOrientation">Current world-space orientation.</param>
        /// <param name="elapsedSeconds">Elapsed time separating both poses.</param>
        /// <param name="linearVelocity">Resolved world-space linear velocity in units per second.</param>
        /// <param name="angularVelocity">Resolved world-space angular velocity in radians per second.</param>
        public static void ResolveMotion(
            float3 previousPosition,
            float4 previousOrientation,
            float3 currentPosition,
            float4 currentOrientation,
            double elapsedSeconds,
            out float3 linearVelocity,
            out float3 angularVelocity) {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), "Elapsed time must be a finite value greater than zero.");
            }

            float elapsedSecondsFloat = (float)elapsedSeconds;
            linearVelocity = (currentPosition - previousPosition) / elapsedSecondsFloat;

            previousOrientation.Normalize();
            currentOrientation.Normalize();
            float4 inversePreviousOrientation = float4.Inverse(previousOrientation);
            float4.Concatenate(ref inversePreviousOrientation, ref currentOrientation, out float4 relativeRotation);
            relativeRotation.Normalize();
            if (relativeRotation.W < 0f) {
                relativeRotation = -relativeRotation;
            }

            double vectorLengthSquared =
                (relativeRotation.X * relativeRotation.X) +
                (relativeRotation.Y * relativeRotation.Y) +
                (relativeRotation.Z * relativeRotation.Z);
            if (vectorLengthSquared <= RotationEpsilonSquared) {
                angularVelocity = float3.Zero;
                return;
            }

            double vectorLength = Math.Sqrt(vectorLengthSquared);
            double angleRadians = 2d * Math.Atan2(vectorLength, relativeRotation.W);
            float inverseVectorLength = (float)(1d / vectorLength);
            float3 axis = new float3(
                relativeRotation.X * inverseVectorLength,
                relativeRotation.Y * inverseVectorLength,
                relativeRotation.Z * inverseVectorLength);
            angularVelocity = axis * (float)(angleRadians / elapsedSeconds);
        }
    }
}
