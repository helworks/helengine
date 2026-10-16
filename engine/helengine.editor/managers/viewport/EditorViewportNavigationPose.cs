namespace helengine.editor {
    /// <summary>
    /// Immutable viewport camera pose expressed around a world-space pivot.
    /// </summary>
    public sealed class EditorViewportNavigationPose {
        /// <summary>
        /// Initializes a validated camera pose around one pivot.
        /// </summary>
        /// <param name="pivot">World-space camera orbit pivot.</param>
        /// <param name="orientation">Camera orientation.</param>
        /// <param name="distance">Positive distance between the camera and pivot.</param>
        public EditorViewportNavigationPose(float3 pivot, float4 orientation, double distance) {
            if (!float.IsFinite(pivot.X) || !float.IsFinite(pivot.Y) || !float.IsFinite(pivot.Z)) {
                throw new ArgumentOutOfRangeException(nameof(pivot), "The pose pivot must contain only finite values.");
            }
            if (!float.IsFinite(orientation.X) || !float.IsFinite(orientation.Y) || !float.IsFinite(orientation.Z) || !float.IsFinite(orientation.W)) {
                throw new ArgumentOutOfRangeException(nameof(orientation), "The pose orientation must contain only finite values.");
            }
            double orientationLengthSquared =
                (orientation.X * orientation.X) +
                (orientation.Y * orientation.Y) +
                (orientation.Z * orientation.Z) +
                (orientation.W * orientation.W);
            if (!double.IsFinite(orientationLengthSquared) || orientationLengthSquared <= 0.000000000001) {
                throw new ArgumentOutOfRangeException(nameof(orientation), "The pose orientation must have non-zero magnitude.");
            }
            if (!double.IsFinite(distance) || distance < CameraProjectionUtils.MinimumOrthographicVerticalSpan) {
                throw new ArgumentOutOfRangeException(nameof(distance), distance, "The pose distance must be finite and at least 0.001 world units.");
            }

            float4 normalizedOrientation = orientation;
            normalizedOrientation.Normalize();
            Pivot = pivot;
            Orientation = normalizedOrientation;
            Distance = distance;
        }

        /// <summary>
        /// Gets the world-space orbit pivot.
        /// </summary>
        public float3 Pivot { get; }

        /// <summary>
        /// Gets the normalized camera orientation.
        /// </summary>
        public float4 Orientation { get; }

        /// <summary>
        /// Gets the positive distance between camera and pivot.
        /// </summary>
        public double Distance { get; }
    }
}
