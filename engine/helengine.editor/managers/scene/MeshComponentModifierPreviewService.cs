namespace helengine.editor {
    /// <summary>
    /// Applies and restores live editor-viewport previews of MeshComponent modifier results.
    /// </summary>
    public sealed class MeshComponentModifierPreviewService {
        /// <summary>
        /// Original runtime models keyed by the mesh components whose previews replaced them.
        /// </summary>
        readonly Dictionary<MeshComponent, RuntimeModel> OriginalModelsByComponent = new Dictionary<MeshComponent, RuntimeModel>();

        /// <summary>
        /// Live preview models keyed by their owning mesh components so replaced previews can be disposed.
        /// </summary>
        readonly Dictionary<MeshComponent, RuntimeModel> PreviewModelsByComponent = new Dictionary<MeshComponent, RuntimeModel>();

        /// <summary>
        /// Applies one tessellated preview model built from the supplied source geometry.
        /// </summary>
        /// <param name="meshComponent">Mesh component whose viewport model should preview the modifier result.</param>
        /// <param name="sourceModelAsset">Unmodified source geometry for the mesh.</param>
        /// <param name="maximumEdgeLength">Maximum world-space edge length used by the tessellation modifier.</param>
        public void ApplyTessellationPreview(MeshComponent meshComponent, ModelAsset sourceModelAsset, double maximumEdgeLength) {
            if (meshComponent == null) {
                throw new ArgumentNullException(nameof(meshComponent));
            }
            if (sourceModelAsset == null) {
                throw new ArgumentNullException(nameof(sourceModelAsset));
            }
            if (Core.Instance?.RenderManager3D == null) {
                throw new InvalidOperationException("Modifier previews require an active 3D render manager.");
            }

            ModelAsset preparedAsset = ModelTessellationProcessor.Clone(sourceModelAsset);
            preparedAsset.Id = string.Empty;
            float3 worldScale = meshComponent.Parent != null ? meshComponent.Parent.Scale : float3.One;
            ModelTessellationProcessor.Apply(preparedAsset, maximumEdgeLength, worldScale);

            RuntimeModel previewModel = Core.Instance.RenderManager3D.BuildModelFromRaw(preparedAsset);
            if (!OriginalModelsByComponent.ContainsKey(meshComponent)) {
                OriginalModelsByComponent[meshComponent] = meshComponent.Model;
            }
            if (PreviewModelsByComponent.TryGetValue(meshComponent, out RuntimeModel replacedPreview)) {
                replacedPreview.Dispose();
            }

            PreviewModelsByComponent[meshComponent] = previewModel;
            meshComponent.Model = previewModel;
        }

        /// <summary>
        /// Restores the original model of one previewed mesh component and releases its preview model.
        /// </summary>
        /// <param name="meshComponent">Mesh component whose preview should be removed.</param>
        public void RestorePreview(MeshComponent meshComponent) {
            if (meshComponent == null) {
                throw new ArgumentNullException(nameof(meshComponent));
            }
            if (!OriginalModelsByComponent.TryGetValue(meshComponent, out RuntimeModel originalModel)) {
                return;
            }

            meshComponent.Model = originalModel;
            OriginalModelsByComponent.Remove(meshComponent);
            if (PreviewModelsByComponent.TryGetValue(meshComponent, out RuntimeModel previewModel)) {
                previewModel.Dispose();
                PreviewModelsByComponent.Remove(meshComponent);
            }
        }

        /// <summary>
        /// Returns whether one mesh component currently shows a preview model.
        /// </summary>
        /// <param name="meshComponent">Mesh component to inspect.</param>
        /// <returns>True when a preview replaced the component's original model.</returns>
        public bool HasPreview(MeshComponent meshComponent) {
            return meshComponent != null && OriginalModelsByComponent.ContainsKey(meshComponent);
        }
    }
}
