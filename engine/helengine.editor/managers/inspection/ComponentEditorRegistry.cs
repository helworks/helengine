namespace helengine.editor {
    /// <summary>
    /// Central registry of per-component editor extensions: scene selection visuals and properties-panel custom property editors.
    /// </summary>
    public static class ComponentEditorRegistry {
        /// <summary>
        /// Registered scene selection editors consulted for each component of the selected entity.
        /// </summary>
        static readonly List<IComponentSceneSelectionEditor> SceneSelectionEditorList = new List<IComponentSceneSelectionEditor>();

        /// <summary>
        /// Registered properties-panel custom property editor providers.
        /// </summary>
        static readonly List<IComponentPropertyEditorProvider> PropertyEditorProviderList = new List<IComponentPropertyEditorProvider>();

        /// <summary>
        /// Registers the built-in editor extensions once per process.
        /// </summary>
        static ComponentEditorRegistry() {
            SceneSelectionEditorList.Add(new BoxCollider3DSceneSelectionEditor());
            PropertyEditorProviderList.Add(new CameraClearSettingsPropertyEditorProvider());
            PropertyEditorProviderList.Add(new SceneMapPropertyEditorProvider());
        }

        /// <summary>
        /// Gets the registered scene selection editors.
        /// </summary>
        public static IReadOnlyList<IComponentSceneSelectionEditor> SceneSelectionEditors => SceneSelectionEditorList;

        /// <summary>
        /// Gets the registered properties-panel custom property editor providers.
        /// </summary>
        public static IReadOnlyList<IComponentPropertyEditorProvider> PropertyEditorProviders => PropertyEditorProviderList;

        /// <summary>
        /// Registers one additional scene selection editor.
        /// </summary>
        /// <param name="editor">Editor that visualizes one component type while its owner is selected.</param>
        public static void RegisterSceneSelectionEditor(IComponentSceneSelectionEditor editor) {
            if (editor == null) {
                throw new ArgumentNullException(nameof(editor));
            }

            SceneSelectionEditorList.Add(editor);
        }

        /// <summary>
        /// Registers one additional properties-panel custom property editor provider.
        /// </summary>
        /// <param name="provider">Provider that supplies custom rows for matching component properties.</param>
        public static void RegisterPropertyEditorProvider(IComponentPropertyEditorProvider provider) {
            if (provider == null) {
                throw new ArgumentNullException(nameof(provider));
            }

            PropertyEditorProviderList.Add(provider);
        }
    }
}
