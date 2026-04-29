namespace helengine.editor {
    /// <summary>
    /// Resolves the world-space orientation used by translation snap-preview grids.
    /// </summary>
    public static class TransformTranslationSnapPreviewResolver {
        /// <summary>
        /// Smallest squared vector magnitude treated as non-zero during basis solving.
        /// </summary>
        const double MinimumVectorLengthSquared = 0.000000000001;
        /// <summary>
        /// Maximum absolute dot product allowed before two directions are treated as effectively parallel.
        /// </summary>
        const double ParallelDotThreshold = 0.95;
        /// <summary>
        /// World-up fallback used when resolving axis-preview planes.
        /// </summary>
        static readonly float3 WorldUp = new float3(0f, 1f, 0f);
        /// <summary>
        /// World-forward fallback used when the active axis is near the world-up direction.
        /// </summary>
        static readonly float3 WorldForward = new float3(0f, 0f, -1f);
        /// <summary>
        /// World-right fallback used when both world-up and world-forward are near the active axis.
        /// </summary>
        static readonly float3 WorldRight = new float3(1f, 0f, 0f);

        /// <summary>
        /// Resolves the preview-plane orientation for the supplied translation handle.
        /// </summary>
        /// <param name="handleEntity">Translation handle currently hovered or dragged.</param>
        /// <param name="gizmoOrigin">World-space origin of the active translation gizmo.</param>
        /// <param name="cameraPosition">World-space camera position for camera-facing axis-plane resolution.</param>
        /// <param name="previewOrientation">Resolved world-space preview orientation when successful.</param>
        /// <returns>True when a valid preview orientation could be resolved; otherwise false.</returns>
        public static bool TryResolvePreviewOrientation(
            Entity handleEntity,
            float3 gizmoOrigin,
            float3 cameraPosition,
            out float4 previewOrientation) {
            if (handleEntity == null) {
                previewOrientation = float4.Identity;
                return false;
            }

            if (!TryFindTransformHandleComponent(handleEntity, out TransformGizmoHandleComponent handleComponent)) {
                previewOrientation = float4.Identity;
                return false;
            }

            float3 primaryDirection = NormalizeDirection(float4.RotateVector(handleComponent.LocalPrimaryDirection, handleEntity.Orientation));
            if (primaryDirection == float3.Zero) {
                previewOrientation = float4.Identity;
                return false;
            } else if (handleComponent.ConstraintType == TransformGizmoHandleConstraintType.Plane) {
                return TryResolvePlanePreviewOrientation(handleEntity, handleComponent, primaryDirection, out previewOrientation);
            } else if (handleComponent.ConstraintType == TransformGizmoHandleConstraintType.Axis) {
                return TryResolveAxisPreviewOrientation(primaryDirection, gizmoOrigin, cameraPosition, out previewOrientation);
            } else {
                throw new InvalidOperationException("Transform gizmo handle constraint type is not supported.");
            }
        }

        /// <summary>
        /// Resolves the preview orientation for a plane-constrained translation handle.
        /// </summary>
        /// <param name="handleEntity">Plane-handle entity that defines the preview plane.</param>
        /// <param name="handleComponent">Handle component describing the plane axes.</param>
        /// <param name="primaryDirection">Resolved world-space primary plane direction.</param>
        /// <param name="previewOrientation">Resolved preview orientation when successful.</param>
        /// <returns>True when the preview orientation could be resolved; otherwise false.</returns>
        static bool TryResolvePlanePreviewOrientation(
            Entity handleEntity,
            TransformGizmoHandleComponent handleComponent,
            float3 primaryDirection,
            out float4 previewOrientation) {
            if (handleEntity == null) {
                throw new ArgumentNullException(nameof(handleEntity));
            }

            if (handleComponent == null) {
                throw new ArgumentNullException(nameof(handleComponent));
            }

            float3 secondaryDirection = NormalizeDirection(float4.RotateVector(handleComponent.LocalSecondaryDirection, handleEntity.Orientation));
            if (secondaryDirection == float3.Zero) {
                previewOrientation = float4.Identity;
                return false;
            }

            float3 planeNormal = NormalizeDirection(float3.Cross(primaryDirection, secondaryDirection));
            if (planeNormal == float3.Zero) {
                previewOrientation = float4.Identity;
                return false;
            }

            return TryCreateOrientation(primaryDirection, secondaryDirection, planeNormal, out previewOrientation);
        }

        /// <summary>
        /// Resolves the preview orientation for an axis-constrained translation handle.
        /// </summary>
        /// <param name="primaryDirection">Resolved world-space translation axis direction.</param>
        /// <param name="gizmoOrigin">World-space gizmo origin used to derive a camera-facing plane.</param>
        /// <param name="cameraPosition">World-space camera position used to keep the preview readable.</param>
        /// <param name="previewOrientation">Resolved preview orientation when successful.</param>
        /// <returns>True when the preview orientation could be resolved; otherwise false.</returns>
        static bool TryResolveAxisPreviewOrientation(
            float3 primaryDirection,
            float3 gizmoOrigin,
            float3 cameraPosition,
            out float4 previewOrientation) {
            float3 cameraDirection = NormalizeDirection(cameraPosition - gizmoOrigin);
            float3 planeNormal = NormalizeDirection(float3.Cross(primaryDirection, cameraDirection));
            if (planeNormal == float3.Zero) {
                float3 referenceDirection = ResolveAxisPreviewReferenceDirection(primaryDirection);
                planeNormal = NormalizeDirection(float3.Cross(primaryDirection, referenceDirection));
            }

            if (planeNormal == float3.Zero) {
                previewOrientation = float4.Identity;
                return false;
            }

            float3 secondaryDirection = NormalizeDirection(float3.Cross(planeNormal, primaryDirection));
            if (secondaryDirection == float3.Zero) {
                previewOrientation = float4.Identity;
                return false;
            }

            return TryCreateOrientation(primaryDirection, secondaryDirection, planeNormal, out previewOrientation);
        }

        /// <summary>
        /// Chooses a stable fallback direction that is not parallel to the active translation axis.
        /// </summary>
        /// <param name="primaryDirection">Active world-space translation axis direction.</param>
        /// <returns>Reference direction suitable for building an axis preview plane.</returns>
        static float3 ResolveAxisPreviewReferenceDirection(float3 primaryDirection) {
            double upDot = Math.Abs(float3.Dot(primaryDirection, WorldUp));
            if (upDot < ParallelDotThreshold) {
                return WorldUp;
            }

            double forwardDot = Math.Abs(float3.Dot(primaryDirection, WorldForward));
            if (forwardDot < ParallelDotThreshold) {
                return WorldForward;
            }

            return WorldRight;
        }

        /// <summary>
        /// Creates a quaternion whose local XY plane maps to the supplied orthonormal basis.
        /// </summary>
        /// <param name="primaryDirection">World-space direction mapped from local +X.</param>
        /// <param name="secondaryDirection">World-space direction mapped from local +Y.</param>
        /// <param name="planeNormal">World-space direction mapped from local +Z.</param>
        /// <param name="previewOrientation">Resolved quaternion when successful.</param>
        /// <returns>True when the basis produced a valid quaternion; otherwise false.</returns>
        static bool TryCreateOrientation(
            float3 primaryDirection,
            float3 secondaryDirection,
            float3 planeNormal,
            out float4 previewOrientation) {
            if (primaryDirection == float3.Zero || secondaryDirection == float3.Zero || planeNormal == float3.Zero) {
                previewOrientation = float4.Identity;
                return false;
            }

            var rotationMatrix = new System.Numerics.Matrix4x4(
                primaryDirection.X, primaryDirection.Y, primaryDirection.Z, 0f,
                secondaryDirection.X, secondaryDirection.Y, secondaryDirection.Z, 0f,
                planeNormal.X, planeNormal.Y, planeNormal.Z, 0f,
                0f, 0f, 0f, 1f);
            System.Numerics.Quaternion quaternion = System.Numerics.Quaternion.CreateFromRotationMatrix(rotationMatrix);
            previewOrientation = new float4(quaternion.X, quaternion.Y, quaternion.Z, quaternion.W);
            previewOrientation.Normalize();
            return true;
        }

        /// <summary>
        /// Finds a transform-gizmo handle component on the supplied entity.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <param name="handleComponent">Resolved handle component when present.</param>
        /// <returns>True when a handle component was found; otherwise false.</returns>
        static bool TryFindTransformHandleComponent(Entity entity, out TransformGizmoHandleComponent handleComponent) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            if (entity.Components == null) {
                handleComponent = null;
                return false;
            }

            for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                if (entity.Components[componentIndex] is TransformGizmoHandleComponent transformHandle) {
                    handleComponent = transformHandle;
                    return true;
                }
            }

            handleComponent = null;
            return false;
        }

        /// <summary>
        /// Normalizes a direction vector and returns zero when the input magnitude is too small.
        /// </summary>
        /// <param name="value">Direction vector to normalize.</param>
        /// <returns>Normalized direction, or zero when the input magnitude is near zero.</returns>
        static float3 NormalizeDirection(float3 value) {
            double lengthSquared =
                (value.X * value.X) +
                (value.Y * value.Y) +
                (value.Z * value.Z);
            if (lengthSquared <= MinimumVectorLengthSquared) {
                return float3.Zero;
            }

            double inverseLength = 1.0 / Math.Sqrt(lengthSquared);
            return new float3(
                (float)(value.X * inverseLength),
                (float)(value.Y * inverseLength),
                (float)(value.Z * inverseLength));
        }
    }
}
