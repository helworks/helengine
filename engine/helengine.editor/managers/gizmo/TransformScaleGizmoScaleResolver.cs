namespace helengine.editor {
    /// <summary>
    /// Resolves scale-vector updates from scale-gizmo drag movement in world space.
    /// </summary>
    public static class TransformScaleGizmoScaleResolver {
        /// <summary>
        /// Smallest squared vector magnitude accepted as a valid axis direction.
        /// </summary>
        const double MinimumDirectionLengthSquared = 0.000000000001;

        /// <summary>
        /// Resolves a scaled vector for a single-axis scale drag.
        /// </summary>
        /// <param name="startScale">Scale captured when dragging started.</param>
        /// <param name="worldAxisDirection">World-space axis direction currently driven by the handle.</param>
        /// <param name="axisDelta">Signed drag delta measured along the handle axis.</param>
        /// <param name="minimumScaleComponent">Smallest allowed scale component value.</param>
        /// <returns>Resolved scale vector after the axis drag.</returns>
        public static float3 ResolveAxisScale(
            float3 startScale,
            float3 worldAxisDirection,
            double axisDelta,
            float minimumScaleComponent) {
            int axisIndex = ResolveDominantAxisIndex(worldAxisDirection);
            double startValue = GetScaleComponent(startScale, axisIndex);
            double resolvedValue = ClampScaleComponent(startValue + axisDelta, minimumScaleComponent);
            return SetScaleComponent(startScale, axisIndex, (float)resolvedValue);
        }

        /// <summary>
        /// Resolves a snapped scaled vector for a single-axis scale drag.
        /// </summary>
        /// <param name="startScale">Scale captured when dragging started.</param>
        /// <param name="worldAxisDirection">World-space axis direction currently driven by the handle.</param>
        /// <param name="axisDelta">Signed drag delta measured along the handle axis.</param>
        /// <param name="snapValue">Snap interval applied to the drag delta.</param>
        /// <param name="minimumScaleComponent">Smallest allowed scale component value.</param>
        /// <returns>Resolved scale vector after the snapped axis drag.</returns>
        public static float3 ResolveSnappedAxisScale(
            float3 startScale,
            float3 worldAxisDirection,
            double axisDelta,
            double snapValue,
            float minimumScaleComponent) {
            if (snapValue <= 0.0) {
                throw new ArgumentOutOfRangeException(nameof(snapValue), "Snap value must be greater than zero.");
            }

            int axisIndex = ResolveDominantAxisIndex(worldAxisDirection);
            double startValue = GetScaleComponent(startScale, axisIndex);
            double resolvedValue = ClampScaleComponent(SnapScalar(startValue + axisDelta, snapValue), minimumScaleComponent);
            return SetScaleComponent(startScale, axisIndex, (float)resolvedValue);
        }

        /// <summary>
        /// Resolves a scaled vector for a plane scale drag that affects two axes at once.
        /// </summary>
        /// <param name="startScale">Scale captured when dragging started.</param>
        /// <param name="worldPrimaryDirection">First world-space plane basis direction currently driven by the handle.</param>
        /// <param name="worldSecondaryDirection">Second world-space plane basis direction currently driven by the handle.</param>
        /// <param name="planeDelta">World-space pointer delta across the drag plane.</param>
        /// <param name="minimumScaleComponent">Smallest allowed scale component value.</param>
        /// <returns>Resolved scale vector after the plane drag.</returns>
        public static float3 ResolvePlaneScale(
            float3 startScale,
            float3 worldPrimaryDirection,
            float3 worldSecondaryDirection,
            float3 planeDelta,
            float minimumScaleComponent) {
            float3 normalizedPrimary = NormalizeDirection(worldPrimaryDirection);
            float3 normalizedSecondary = NormalizeDirection(worldSecondaryDirection);
            int primaryAxisIndex = ResolveDominantAxisIndex(normalizedPrimary);
            int secondaryAxisIndex = ResolveDominantAxisIndex(normalizedSecondary);
            if (primaryAxisIndex == secondaryAxisIndex) {
                throw new InvalidOperationException("Plane scale directions must affect two distinct scale components.");
            }

            double primaryDelta = float3.Dot(planeDelta, normalizedPrimary);
            double secondaryDelta = float3.Dot(planeDelta, normalizedSecondary);
            double resolvedPrimary = ClampScaleComponent(GetScaleComponent(startScale, primaryAxisIndex) + primaryDelta, minimumScaleComponent);
            double resolvedSecondary = ClampScaleComponent(GetScaleComponent(startScale, secondaryAxisIndex) + secondaryDelta, minimumScaleComponent);

            float3 resolvedScale = SetScaleComponent(startScale, primaryAxisIndex, (float)resolvedPrimary);
            resolvedScale = SetScaleComponent(resolvedScale, secondaryAxisIndex, (float)resolvedSecondary);
            return resolvedScale;
        }

        /// <summary>
        /// Resolves a snapped scaled vector for a plane scale drag that affects two axes at once.
        /// </summary>
        /// <param name="startScale">Scale captured when dragging started.</param>
        /// <param name="worldPrimaryDirection">First world-space plane basis direction currently driven by the handle.</param>
        /// <param name="worldSecondaryDirection">Second world-space plane basis direction currently driven by the handle.</param>
        /// <param name="planeDelta">World-space pointer delta across the drag plane.</param>
        /// <param name="snapValue">Snap interval applied independently along each plane basis direction.</param>
        /// <param name="minimumScaleComponent">Smallest allowed scale component value.</param>
        /// <returns>Resolved scale vector after the snapped plane drag.</returns>
        public static float3 ResolveSnappedPlaneScale(
            float3 startScale,
            float3 worldPrimaryDirection,
            float3 worldSecondaryDirection,
            float3 planeDelta,
            double snapValue,
            float minimumScaleComponent) {
            if (snapValue <= 0.0) {
                throw new ArgumentOutOfRangeException(nameof(snapValue), "Snap value must be greater than zero.");
            }

            float3 normalizedPrimary = NormalizeDirection(worldPrimaryDirection);
            float3 normalizedSecondary = NormalizeDirection(worldSecondaryDirection);
            double primaryDelta = float3.Dot(planeDelta, normalizedPrimary);
            double secondaryDelta = float3.Dot(planeDelta, normalizedSecondary);
            int primaryAxisIndex = ResolveDominantAxisIndex(normalizedPrimary);
            int secondaryAxisIndex = ResolveDominantAxisIndex(normalizedSecondary);
            if (primaryAxisIndex == secondaryAxisIndex) {
                throw new InvalidOperationException("Plane scale directions must affect two distinct scale components.");
            }

            double startPrimary = GetScaleComponent(startScale, primaryAxisIndex);
            double startSecondary = GetScaleComponent(startScale, secondaryAxisIndex);
            double resolvedPrimary = ClampScaleComponent(SnapScalar(startPrimary + primaryDelta, snapValue), minimumScaleComponent);
            double resolvedSecondary = ClampScaleComponent(SnapScalar(startSecondary + secondaryDelta, snapValue), minimumScaleComponent);

            float3 resolvedScale = SetScaleComponent(startScale, primaryAxisIndex, (float)resolvedPrimary);
            resolvedScale = SetScaleComponent(resolvedScale, secondaryAxisIndex, (float)resolvedSecondary);
            return resolvedScale;
        }

        /// <summary>
        /// Resolves which scale-vector component is represented by a world-space axis direction.
        /// </summary>
        /// <param name="direction">World-space direction to inspect.</param>
        /// <returns>Component index where 0=X, 1=Y, and 2=Z.</returns>
        static int ResolveDominantAxisIndex(float3 direction) {
            float3 normalizedDirection = NormalizeDirection(direction);
            double absX = Math.Abs(normalizedDirection.X);
            double absY = Math.Abs(normalizedDirection.Y);
            double absZ = Math.Abs(normalizedDirection.Z);

            if (absX > absY && absX > absZ) {
                return 0;
            }

            if (absY > absX && absY > absZ) {
                return 1;
            }

            if (absZ > absX && absZ > absY) {
                return 2;
            }

            throw new InvalidOperationException("Scale axis direction must resolve to a single dominant component.");
        }

        /// <summary>
        /// Normalizes a direction vector.
        /// </summary>
        /// <param name="direction">Direction vector to normalize.</param>
        /// <returns>Normalized direction vector.</returns>
        static float3 NormalizeDirection(float3 direction) {
            double lengthSquared =
                (direction.X * direction.X) +
                (direction.Y * direction.Y) +
                (direction.Z * direction.Z);
            if (lengthSquared <= MinimumDirectionLengthSquared) {
                throw new InvalidOperationException("Scale direction vector must be non-zero.");
            }

            double inverseLength = 1.0 / Math.Sqrt(lengthSquared);
            return new float3(
                (float)(direction.X * inverseLength),
                (float)(direction.Y * inverseLength),
                (float)(direction.Z * inverseLength));
        }

        /// <summary>
        /// Reads one component from a scale vector by index.
        /// </summary>
        /// <param name="scale">Scale vector to inspect.</param>
        /// <param name="axisIndex">Component index where 0=X, 1=Y, and 2=Z.</param>
        /// <returns>Requested scale component.</returns>
        static double GetScaleComponent(float3 scale, int axisIndex) {
            switch (axisIndex) {
                case 0:
                    return scale.X;
                case 1:
                    return scale.Y;
                case 2:
                    return scale.Z;
                default:
                    throw new ArgumentOutOfRangeException(nameof(axisIndex), "Axis index must be between 0 and 2.");
            }
        }

        /// <summary>
        /// Writes one component into a scale vector by index.
        /// </summary>
        /// <param name="scale">Scale vector to update.</param>
        /// <param name="axisIndex">Component index where 0=X, 1=Y, and 2=Z.</param>
        /// <param name="value">New component value.</param>
        /// <returns>Updated scale vector.</returns>
        static float3 SetScaleComponent(float3 scale, int axisIndex, float value) {
            switch (axisIndex) {
                case 0:
                    return new float3(value, scale.Y, scale.Z);
                case 1:
                    return new float3(scale.X, value, scale.Z);
                case 2:
                    return new float3(scale.X, scale.Y, value);
                default:
                    throw new ArgumentOutOfRangeException(nameof(axisIndex), "Axis index must be between 0 and 2.");
            }
        }

        /// <summary>
        /// Clamps a resolved scale component to the minimum allowed value.
        /// </summary>
        /// <param name="value">Resolved scale component.</param>
        /// <param name="minimumScaleComponent">Smallest allowed scale component value.</param>
        /// <returns>Clamped scale component.</returns>
        static double ClampScaleComponent(double value, float minimumScaleComponent) {
            if (minimumScaleComponent <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(minimumScaleComponent), "Minimum scale component must be greater than zero.");
            }

            return value < minimumScaleComponent ? minimumScaleComponent : value;
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
