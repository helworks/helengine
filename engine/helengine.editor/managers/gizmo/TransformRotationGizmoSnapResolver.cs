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
        /// Snaps a signed rotation delta to the nearest configured angle increment.
        /// </summary>
        /// <param name="deltaAngleRadians">Signed unsnapped drag angle in radians.</param>
        /// <param name="snapDegrees">Snap interval in degrees.</param>
        /// <returns>Snapped rotation delta in radians.</returns>
        public static double ResolveSnappedDeltaAngle(double deltaAngleRadians, double snapDegrees) {
            if (snapDegrees <= 0.0) {
                throw new ArgumentOutOfRangeException(nameof(snapDegrees), "Snap value must be greater than zero.");
            }

            double snapRadians = snapDegrees * DegreesToRadians;
            double snappedStepCount = Math.Round(deltaAngleRadians / snapRadians, MidpointRounding.AwayFromZero);
            return snappedStepCount * snapRadians;
        }
    }
}
