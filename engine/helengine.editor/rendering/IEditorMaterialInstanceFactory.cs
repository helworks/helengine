namespace helengine.editor {
    /// <summary>
    /// Creates a renderer-specific runtime-material instance from an existing generated material.
    /// </summary>
    public interface IEditorMaterialInstanceFactory {
        /// <summary>
        /// Creates one independent instance while preserving the source material's authored bindings and state.
        /// </summary>
        /// <param name="sourceMaterial">Generated material whose values should be copied.</param>
        /// <returns>Renderer-specific material instance.</returns>
        RuntimeMaterial CreateInstance(RuntimeMaterial sourceMaterial);
    }
}
