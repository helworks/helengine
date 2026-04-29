namespace helengine.editor {
    /// <summary>
    /// Builds raw preview meshes used to visualize rotation snapping.
    /// </summary>
    public static class TransformRotationSnapPreviewModelFactory {
        /// <summary>
        /// Radius of the preview disc rendered inside the rotation-gizmo ring.
        /// </summary>
        public const float PreviewRadius = TransformRotationGizmoFactory.InnerRadius;
        /// <summary>
        /// Diameter of the preview disc rendered inside the rotation-gizmo ring.
        /// </summary>
        public const float PreviewDiameter = PreviewRadius * 2f;

        /// <summary>
        /// Creates a centered preview plane that encodes one snap interval for shader-side spoke generation.
        /// </summary>
        /// <param name="snapDegrees">Angular snap interval in degrees.</param>
        /// <returns>Centered preview mesh whose texture coordinates carry the requested snap interval.</returns>
        public static ModelAsset Create(double snapDegrees) {
            if (snapDegrees <= 0.0) {
                throw new ArgumentOutOfRangeException(nameof(snapDegrees), "Snap value must be greater than zero.");
            }

            return TransformGizmoMeshFactory.CreateCenteredPlaneSquare(
                PreviewDiameter,
                new float2((float)snapDegrees, 0f));
        }
    }
}
