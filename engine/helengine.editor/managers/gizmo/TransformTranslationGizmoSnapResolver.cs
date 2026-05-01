namespace helengine.editor {
    /// <summary>
    /// Resolves snapped translation offsets for transform-gizmo drag movement.
    /// </summary>
    public static class TransformTranslationGizmoSnapResolver {
        /// <summary>
        /// Smallest squared vector magnitude accepted as a valid snap basis direction.
        /// </summary>
        const double MinimumDirectionLengthSquared = 0.000000000001;

        /// <summary>
        /// Resolves a snapped axis offset from a signed axis delta.
        /// </summary>
        /// <param name="worldAxisDirection">World-space axis direction that receives the snapped movement.</param>
        /// <param name="axisDelta">Signed drag delta along the axis.</param>
        /// <param name="snapValue">Snap interval to apply.</param>
        /// <returns>Snapped world-space axis offset.</returns>
        public static float3 ResolveAxisOffset(float3 worldAxisDirection, double axisDelta, double snapValue) {
            if (snapValue <= 0.0) {
                throw new ArgumentOutOfRangeException(nameof(snapValue), "Snap value must be greater than zero.");
            }

            float3 normalizedAxis = NormalizeDirection(worldAxisDirection);
            double snappedDelta = SnapScalar(axisDelta, snapValue);
            return normalizedAxis * (float)snappedDelta;
        }

        /// <summary>
        /// Resolves a snapped axis offset from an absolute axis position anchored at the drag start.
        /// </summary>
        /// <param name="startPosition">World-space position captured when dragging started.</param>
        /// <param name="worldAxisDirection">World-space axis direction that receives the snapped movement.</param>
        /// <param name="axisDelta">Signed drag delta along the axis.</param>
        /// <param name="snapValue">Snap interval to apply.</param>
        /// <returns>Snapped world-space axis offset from the drag start position.</returns>
        public static float3 ResolveAxisOffset(float3 startPosition, float3 worldAxisDirection, double axisDelta, double snapValue) {
            if (snapValue <= 0.0) {
                throw new ArgumentOutOfRangeException(nameof(snapValue), "Snap value must be greater than zero.");
            }

            float3 normalizedAxis = NormalizeDirection(worldAxisDirection);
            double startAxisComponent = float3.Dot(startPosition, normalizedAxis);
            double snappedAxisComponent = SnapScalar(startAxisComponent + axisDelta, snapValue);
            double snappedDelta = snappedAxisComponent - startAxisComponent;
            return normalizedAxis * (float)snappedDelta;
        }

        /// <summary>
        /// Resolves a snapped plane offset from plane-basis directions and a world-space plane delta.
        /// </summary>
        /// <param name="worldPrimaryDirection">First world-space plane basis direction.</param>
        /// <param name="worldSecondaryDirection">Second world-space plane basis direction.</param>
        /// <param name="planeDelta">World-space delta measured on the drag plane.</param>
        /// <param name="snapValue">Snap interval to apply per plane basis axis.</param>
        /// <returns>Snapped world-space plane offset.</returns>
        public static float3 ResolvePlaneOffset(
            float3 worldPrimaryDirection,
            float3 worldSecondaryDirection,
            float3 planeDelta,
            double snapValue) {
            if (snapValue <= 0.0) {
                throw new ArgumentOutOfRangeException(nameof(snapValue), "Snap value must be greater than zero.");
            }

            float3 normalizedPrimary = NormalizeDirection(worldPrimaryDirection);
            float3 normalizedSecondary = NormalizeDirection(worldSecondaryDirection);
            double primaryDelta = float3.Dot(planeDelta, normalizedPrimary);
            double secondaryDelta = float3.Dot(planeDelta, normalizedSecondary);
            double snappedPrimary = SnapScalar(primaryDelta, snapValue);
            double snappedSecondary = SnapScalar(secondaryDelta, snapValue);
            return (normalizedPrimary * (float)snappedPrimary) + (normalizedSecondary * (float)snappedSecondary);
        }

        /// <summary>
        /// Resolves a snapped plane offset from an absolute plane position anchored at the drag start.
        /// </summary>
        /// <param name="startPosition">World-space position captured when dragging started.</param>
        /// <param name="worldPrimaryDirection">First world-space plane basis direction.</param>
        /// <param name="worldSecondaryDirection">Second world-space plane basis direction.</param>
        /// <param name="planeDelta">World-space delta measured on the drag plane.</param>
        /// <param name="snapValue">Snap interval to apply per plane basis axis.</param>
        /// <returns>Snapped world-space plane offset from the drag start position.</returns>
        public static float3 ResolvePlaneOffset(
            float3 startPosition,
            float3 worldPrimaryDirection,
            float3 worldSecondaryDirection,
            float3 planeDelta,
            double snapValue) {
            if (snapValue <= 0.0) {
                throw new ArgumentOutOfRangeException(nameof(snapValue), "Snap value must be greater than zero.");
            }

            float3 normalizedPrimary = NormalizeDirection(worldPrimaryDirection);
            float3 normalizedSecondary = NormalizeDirection(worldSecondaryDirection);
            double startPrimary = float3.Dot(startPosition, normalizedPrimary);
            double startSecondary = float3.Dot(startPosition, normalizedSecondary);
            double primaryDelta = float3.Dot(planeDelta, normalizedPrimary);
            double secondaryDelta = float3.Dot(planeDelta, normalizedSecondary);
            double snappedPrimary = SnapScalar(startPrimary + primaryDelta, snapValue) - startPrimary;
            double snappedSecondary = SnapScalar(startSecondary + secondaryDelta, snapValue) - startSecondary;
            return (normalizedPrimary * (float)snappedPrimary) + (normalizedSecondary * (float)snappedSecondary);
        }

        /// <summary>
        /// Normalizes a direction vector used for snap basis solving.
        /// </summary>
        /// <param name="direction">Direction to normalize.</param>
        /// <returns>Normalized direction vector.</returns>
        static float3 NormalizeDirection(float3 direction) {
            double lengthSquared =
                (direction.X * direction.X) +
                (direction.Y * direction.Y) +
                (direction.Z * direction.Z);
            if (lengthSquared <= MinimumDirectionLengthSquared) {
                throw new InvalidOperationException("Snap direction vector must be non-zero.");
            }

            double inverseLength = 1.0 / Math.Sqrt(lengthSquared);
            return new float3(
                (float)(direction.X * inverseLength),
                (float)(direction.Y * inverseLength),
                (float)(direction.Z * inverseLength));
        }

        /// <summary>
        /// Snaps one signed scalar to the nearest configured step.
        /// </summary>
        /// <param name="value">Scalar value to snap.</param>
        /// <param name="snapValue">Step size used by the snap.</param>
        /// <returns>Snapped scalar value.</returns>
        static double SnapScalar(double value, double snapValue) {
            double snappedStepCount = Math.Round(value / snapValue, MidpointRounding.AwayFromZero);
            return snappedStepCount * snapValue;
        }
    }
}
