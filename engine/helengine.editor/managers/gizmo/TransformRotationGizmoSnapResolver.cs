namespace helengine.editor {
    /// <summary>
    /// Resolves snapped rotation deltas for transform-gizmo ring dragging.
    /// </summary>
    public static class TransformRotationGizmoSnapResolver {
        /// <summary>
        /// Converts degrees into radians.
        /// </summary>
        const double DegreesToRadians = Math.PI / 180.0;

        /// <summary>
        /// Snaps one rotation angle to the nearest configured increment on a fixed angle grid.
        /// </summary>
        /// <param name="angleRadians">Rotation angle in radians to snap.</param>
        /// <param name="snapDegrees">Snap interval in degrees.</param>
        /// <returns>Snapped rotation angle in radians.</returns>
        public static double ResolveSnappedAngle(double angleRadians, double snapDegrees) {
            if (snapDegrees <= 0.0) {
                throw new ArgumentOutOfRangeException(nameof(snapDegrees), "Snap value must be greater than zero.");
            }

            double snapRadians = snapDegrees * DegreesToRadians;
            double snappedStepCount = Math.Round(angleRadians / snapRadians, MidpointRounding.AwayFromZero);
            return snappedStepCount * snapRadians;
        }

        /// <summary>
        /// Snaps a signed rotation delta to the nearest configured angle increment.
        /// </summary>
        /// <param name="deltaAngleRadians">Signed unsnapped drag angle in radians.</param>
        /// <param name="snapDegrees">Snap interval in degrees.</param>
        /// <returns>Snapped rotation delta in radians.</returns>
        public static double ResolveSnappedDeltaAngle(double deltaAngleRadians, double snapDegrees) {
            return ResolveSnappedAngle(deltaAngleRadians, snapDegrees);
        }
    }
}
