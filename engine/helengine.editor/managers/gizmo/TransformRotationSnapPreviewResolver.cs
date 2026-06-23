namespace helengine.editor {
    /// <summary>
    /// Resolves the world-space orientation used by rotation snap-preview discs.
    /// </summary>
    public static class TransformRotationSnapPreviewResolver {
        /// <summary>
        /// Local-plane alignment offset that maps a local +Z plane normal onto the ring's local +Y axis.
        /// </summary>
        static readonly float4 PlaneAlignmentOffset = CreatePlaneAlignmentOffset();

        /// <summary>
        /// Resolves the preview orientation for the supplied rotation handle.
        /// </summary>
        /// <param name="handleEntity">Rotation-handle entity currently hovered or dragged.</param>
        /// <param name="previewOrientation">Resolved world-space preview orientation when successful.</param>
        /// <returns>True when a valid preview orientation could be resolved; otherwise false.</returns>
        public static bool TryResolvePreviewOrientation(Entity handleEntity, out float4 previewOrientation) {
            if (handleEntity == null) {
                previewOrientation = float4.Identity;
                return false;
            }

            if (!TryFindTransformHandleComponent(handleEntity, out TransformGizmoHandleComponent handleComponent)) {
                previewOrientation = float4.Identity;
                return false;
            } else if (handleComponent.ConstraintType != TransformGizmoHandleConstraintType.Axis) {
                previewOrientation = float4.Identity;
                return false;
            } else {
                float4 planeAlignmentOffset = PlaneAlignmentOffset;
                float4 ringOrientation = handleEntity.Orientation;
                float4.Concatenate(ref planeAlignmentOffset, ref ringOrientation, out previewOrientation);
                return true;
            }
        }

        /// <summary>
        /// Creates the quaternion that maps a local +Z plane normal onto the ring's local +Y axis.
        /// </summary>
        /// <returns>Quaternion that aligns the preview plane with the rotation-ring plane.</returns>
        static float4 CreatePlaneAlignmentOffset() {
            float3 xAxis = new float3(1f, 0f, 0f);
            float4 orientation;
            float4.CreateFromAxisAngle(ref xAxis, (float)(-Math.PI * 0.5), out orientation);
            return orientation;
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
    }
}
