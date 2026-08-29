namespace helengine.editor {
    /// <summary>
    /// Central registry of per-component editor extensions: scene selection visuals and properties-panel custom property editors.
    /// </summary>
    public sealed class ComponentEditorRegistry : IDisposable {
        /// <summary>
        /// Registered scene selection editors consulted for each component of the selected entity.
        /// </summary>
        readonly List<IComponentSceneSelectionEditor> SceneSelectionEditorList = new List<IComponentSceneSelectionEditor>();

        /// <summary>
        /// Registered properties-panel custom property editor providers.
        /// </summary>
        readonly List<IComponentPropertyEditorProvider> PropertyEditorProviderList = new List<IComponentPropertyEditorProvider>();

        /// <summary>
        /// Registers the built-in editor extensions for this session.
        /// </summary>
        public ComponentEditorRegistry() {
            SceneSelectionEditorList.Add(new BoxCollider3DSceneSelectionEditor());
            PropertyEditorProviderList.Add(new CameraClearSettingsPropertyEditorProvider());
            PropertyEditorProviderList.Add(new SceneMapPropertyEditorProvider());
        }

        /// <summary>
        /// Gets the registered scene selection editors.
        /// </summary>
        public IReadOnlyList<IComponentSceneSelectionEditor> SceneSelectionEditors => SceneSelectionEditorList;

        /// <summary>
        /// Gets the registered properties-panel custom property editor providers.
        /// </summary>
        public IReadOnlyList<IComponentPropertyEditorProvider> PropertyEditorProviders => PropertyEditorProviderList;

        /// <summary>
        /// Registers one additional scene selection editor.
        /// </summary>
        /// <param name="editor">Editor that visualizes one component type while its owner is selected.</param>
        public void RegisterSceneSelectionEditor(IComponentSceneSelectionEditor editor) {
            if (editor == null) {
                throw new ArgumentNullException(nameof(editor));
            }

            SceneSelectionEditorList.Add(editor);
        }

        /// <summary>
        /// Registers one additional properties-panel custom property editor provider.
        /// </summary>
        /// <param name="provider">Provider that supplies custom rows for matching component properties.</param>
        public void RegisterPropertyEditorProvider(IComponentPropertyEditorProvider provider) {
            if (provider == null) {
                throw new ArgumentNullException(nameof(provider));
            }

            PropertyEditorProviderList.Add(provider);
        }

        /// <inheritdoc />
        public void Dispose() {
            SceneSelectionEditorList.Clear();
            PropertyEditorProviderList.Clear();
        }
    }
}
