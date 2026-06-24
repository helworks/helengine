namespace helengine.editor {
    /// <summary>
    /// Creates sanctioned editor-generated scene asset references.
    /// </summary>
    public static class EditorSceneAssetReferenceFactory {
        /// <summary>
        /// Stable provider identifier used by editor-generated scene asset references.
        /// </summary>
        public const string ProviderIdValue = "editor";

        /// <summary>
        /// Stable asset identifier used by the editor UI font reference.
        /// </summary>
        public const string EditorUiFontAssetId = "ui-font";

        /// <summary>
        /// Stable relative path used by the editor UI font reference.
        /// </summary>
        public const string EditorUiFontRelativePath = "generated/editor/fonts/ui.hefont";

        /// <summary>
        /// Creates the editor UI font scene asset reference.
        /// </summary>
        /// <returns>Validated editor UI font scene asset reference.</returns>
        public static SceneAssetReference CreateEditorUiFont() {
            return global::helengine.SceneAssetReferenceFactory.Rehydrate(
                SceneAssetReferenceSourceKind.Generated,
                EditorUiFontRelativePath,
                ProviderIdValue,
                EditorUiFontAssetId);
        }
    }
}
