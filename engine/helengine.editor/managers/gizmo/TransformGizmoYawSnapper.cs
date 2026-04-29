namespace helengine.editor {
    /// <summary>
    /// Computes snapped yaw orientations for transform gizmos so camera-facing behavior is deterministic.
    /// </summary>
    public static class TransformGizmoYawSnapper {
        /// <summary>
        /// Smallest horizontal camera-to-gizmo magnitude used for facing-orientation decisions.
        /// </summary>
        const double MinimumHorizontalFacingLengthSquared = 0.000000000001;
        /// <summary>
        /// Quarter-turn angle used for 90-degree Y-axis snapping.
        /// </summary>
        const double QuarterTurnRadians = Math.PI * 0.5;
        /// <summary>
        /// Number of snapped quarter-turn sectors in one full horizontal orbit.
        /// </summary>
        const int FullOrbitQuarterTurnCount = 4;
        /// <summary>
        /// World-space up axis used for gizmo yaw rotations.
        /// </summary>
        static readonly float3 WorldUpAxis = new float3(0f, 1f, 0f);

        /// <summary>
        /// Computes a snapped 90-degree yaw orientation that keeps gizmo facing updates stable.
        /// </summary>
        /// <param name="gizmoPosition">Current gizmo world position.</param>
        /// <param name="cameraPosition">Scene camera world position.</param>
        /// <returns>Snapped world-space yaw orientation around the Y axis.</returns>
        public static float4 ComputeSnappedYawFacingOrientation(float3 gizmoPosition, float3 cameraPosition) {
            int snappedQuarterTurns = ComputeSnappedQuarterTurns(gizmoPosition, cameraPosition);
            double snappedYaw = (snappedQuarterTurns * QuarterTurnRadians) - QuarterTurnRadians;
            return CreateYawOrientation(snappedYaw);
        }

        /// <summary>
        /// Computes a snapped quarter-turn count around world Y from gizmo-to-camera direction.
        /// </summary>
        /// <param name="gizmoPosition">Current gizmo world position.</param>
        /// <param name="cameraPosition">Scene camera world position.</param>
        /// <returns>Signed snapped quarter-turn count.</returns>
        public static int ComputeSnappedQuarterTurns(float3 gizmoPosition, float3 cameraPosition) {
            float3 toCamera = cameraPosition - gizmoPosition;
            float3 horizontalToCamera = new float3(toCamera.X, 0f, toCamera.Z);
            double horizontalLengthSquared =
                (horizontalToCamera.X * horizontalToCamera.X) +
                (horizontalToCamera.Z * horizontalToCamera.Z);
            if (horizontalLengthSquared <= MinimumHorizontalFacingLengthSquared) {
                return 0;
            }

            double inverseLength = 1.0 / Math.Sqrt(horizontalLengthSquared);
            float3 horizontalDirection = new float3(
                (float)(horizontalToCamera.X * inverseLength),
                0f,
                (float)(horizontalToCamera.Z * inverseLength));
            double angleToCamera = NormalizeAngleRadians(Math.Atan2(horizontalDirection.X, horizontalDirection.Z));
            int snappedSectorIndex = (int)Math.Floor(angleToCamera / QuarterTurnRadians);
            while (snappedSectorIndex >= FullOrbitQuarterTurnCount / 2) {
                snappedSectorIndex -= FullOrbitQuarterTurnCount;
            }

            while (snappedSectorIndex < -(FullOrbitQuarterTurnCount / 2)) {
                snappedSectorIndex += FullOrbitQuarterTurnCount;
            }

            return snappedSectorIndex + 1;
        }

        /// <summary>
        /// Creates a world-up yaw orientation quaternion from an angle in radians.
        /// </summary>
        /// <param name="yawRadians">Yaw angle in radians.</param>
        /// <returns>Yaw orientation quaternion.</returns>
        static float4 CreateYawOrientation(double yawRadians) {
            float3 axis = WorldUpAxis;
            float4 orientation;
            float4.CreateFromAxisAngle(ref axis, (float)NormalizeAngleRadians(yawRadians), out orientation);
            return orientation;
        }

        /// <summary>
        /// Normalizes an angle in radians into the [-PI, PI] interval.
        /// </summary>
        /// <param name="angleRadians">Angle to normalize.</param>
        /// <returns>Normalized angle in radians.</returns>
        static double NormalizeAngleRadians(double angleRadians) {
            double twoPi = Math.PI * 2.0;
            double normalized = angleRadians;
            while (normalized > Math.PI) {
                normalized -= twoPi;
            }
            while (normalized < -Math.PI) {
                normalized += twoPi;
            }

            return normalized;
        }
    }
}
