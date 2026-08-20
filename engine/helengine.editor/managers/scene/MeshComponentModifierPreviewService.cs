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
        /// Applies one preview model built by running every preview-enabled modifier over the supplied source geometry in stack order.
        /// </summary>
        /// <param name="meshComponent">Mesh component whose viewport model should preview the modifier results.</param>
        /// <param name="sourceModelAsset">Unmodified source geometry for the mesh.</param>
        /// <param name="modifiers">Ordered modifier stack; only preview-enabled entries are applied.</param>
        public void ApplyStackPreview(MeshComponent meshComponent, ModelAsset sourceModelAsset, IReadOnlyList<MeshComponentModifier> modifiers) {
            if (meshComponent == null) {
                throw new ArgumentNullException(nameof(meshComponent));
            }
            if (sourceModelAsset == null) {
                throw new ArgumentNullException(nameof(sourceModelAsset));
            }
            if (modifiers == null) {
                throw new ArgumentNullException(nameof(modifiers));
            }
            if (Core.Instance?.RenderManager3D == null) {
                throw new InvalidOperationException("Modifier previews require an active 3D render manager.");
            }

            bool hasPreviewModifiers = false;
            ModelAsset preparedAsset = ModelTessellationProcessor.Clone(sourceModelAsset);
            preparedAsset.Id = string.Empty;
            float3 worldScale = meshComponent.Parent != null ? meshComponent.Parent.Scale : float3.One;
            float3 worldPosition = meshComponent.Parent != null ? meshComponent.Parent.Position : float3.Zero;
            float4 worldOrientation = meshComponent.Parent != null ? meshComponent.Parent.Orientation : float4.Identity;

            for (int index = 0; index < modifiers.Count; index++) {
                MeshComponentModifier modifier = modifiers[index];
                if (modifier == null || !modifier.Preview) {
                    continue;
                }

                if (string.Equals(modifier.Kind, MeshComponentModifier.TessellateKind, StringComparison.Ordinal)) {
                    ModelTessellationProcessor.Apply(preparedAsset, modifier.MaxEdgeLength, worldScale);
                    hasPreviewModifiers = true;
                } else if (string.Equals(modifier.Kind, MeshComponentModifier.UvwMapKind, StringComparison.Ordinal)) {
                    if (string.Equals(modifier.UvwMode, ModelUvwMapProcessor.WorldMode, StringComparison.Ordinal)) {
                        ModelUvwMapProcessor.ApplyWorldMap(preparedAsset, modifier.UvwPlane, modifier.UvwScale, worldPosition, worldOrientation, worldScale);
                    } else {
                        ModelUvwMapProcessor.ApplyBoxMap(preparedAsset, modifier.UvwScale);
                    }

                    hasPreviewModifiers = true;
                }
            }

            if (!hasPreviewModifiers) {
                RestorePreview(meshComponent);
                return;
            }

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
