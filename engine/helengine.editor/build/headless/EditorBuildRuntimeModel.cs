namespace helengine.editor {
    /// <summary>Owns an independent geometry snapshot and shared model metadata used by headless authoring.</summary>
    public sealed class EditorBuildRuntimeModel : RuntimeModel {
        /// <summary>Clones borrowed geometry before transferring ownership to the runtime model.</summary>
        /// <param name="owner">Renderer responsible for this model's lifetime.</param>
        /// <param name="asset">Raw model whose buffers remain owned by the caller.</param>
        internal EditorBuildRuntimeModel(EditorBuildRenderManager3D owner, ModelAsset asset) {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            ArgumentNullException.ThrowIfNull(asset);
            SetSubmeshes(ModelSubmeshResolver.BuildRuntimeSubmeshes(asset));
            SetBounds(asset.BoundsMin, asset.BoundsMax);
            SetRawModelAsset(ModelTessellationProcessor.Clone(asset));
        }

        /// <summary>Gets the renderer that created this CPU geometry snapshot.</summary>
        internal EditorBuildRenderManager3D Owner { get; }
        /// <summary>Gets whether this model has already released its owned geometry.</summary>
        public bool IsDisposed { get; private set; }

        /// <summary>Releases only this model's cloned geometry and submesh containers.</summary>
        public override void Dispose() {
            if (IsDisposed) {
                return;
            }
            base.Dispose();
            IsDisposed = true;
        }
    }
}
