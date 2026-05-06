namespace helengine.editor.tests.testing {
    /// <summary>
    /// Minimal script assembly host used by editor-session menu tests without loading real assemblies.
    /// </summary>
    internal sealed class TestEditorScriptAssemblyHost : IEditorScriptAssemblyHost {
        /// <summary>
        /// Initializes one fake script assembly host with an empty resolver-backed catalog.
        /// </summary>
        public TestEditorScriptAssemblyHost() {
            ScriptTypeResolver = new ScriptTypeResolver();
            Assemblies = Array.Empty<EditorScriptAssemblyDescriptor>();
            AvailableScriptComponents = Array.Empty<EditorComponentAddDescriptor>();
            AvailableEditorCommands = Array.Empty<EditorProjectCommandDescriptor>();
            AvailableEditorMenuItems = Array.Empty<EditorMenuItemDescriptor>();
        }

        /// <summary>
        /// Gets the number of reload requests received by the fake host.
        /// </summary>
        public int ReloadCount { get; private set; }

        /// <summary>
        /// Gets the script type resolver surfaced by the fake host.
        /// </summary>
        public IScriptTypeResolver ScriptTypeResolver { get; }

        /// <summary>
        /// Gets the assembly descriptors passed to the fake host.
        /// </summary>
        public IReadOnlyList<EditorScriptAssemblyDescriptor> Assemblies { get; private set; }

        /// <summary>
        /// Gets or sets the addable script components surfaced by the fake host.
        /// </summary>
        public IReadOnlyList<EditorComponentAddDescriptor> AvailableScriptComponents { get; set; }

        /// <summary>
        /// Gets or sets the project-authored editor commands surfaced by the fake host.
        /// </summary>
        public IReadOnlyList<EditorProjectCommandDescriptor> AvailableEditorCommands { get; set; }

        /// <summary>
        /// Gets or sets the contributed editor menu items surfaced by the fake host.
        /// </summary>
        public IReadOnlyList<EditorMenuItemDescriptor> AvailableEditorMenuItems { get; set; }

        /// <summary>
        /// Records the supplied assembly descriptors without loading any real assemblies.
        /// </summary>
        /// <param name="assemblies">Descriptors for the freshly built module assemblies.</param>
        public void Reload(IReadOnlyList<EditorScriptAssemblyDescriptor> assemblies) {
            Assemblies = assemblies ?? throw new ArgumentNullException(nameof(assemblies));
            ReloadCount++;
        }

        /// <summary>
        /// Returns the configured addable script components.
        /// </summary>
        /// <param name="entity">Entity that would receive the reflected component.</param>
        /// <returns>Configured addable script component descriptors.</returns>
        public IReadOnlyList<EditorComponentAddDescriptor> GetAvailableScriptComponents(Entity entity) {
            return AvailableScriptComponents;
        }

        /// <summary>
        /// Returns the configured project-authored editor commands.
        /// </summary>
        /// <returns>Configured editor command descriptors.</returns>
        public IReadOnlyList<EditorProjectCommandDescriptor> GetAvailableEditorCommands() {
            return AvailableEditorCommands;
        }

        /// <summary>
        /// Returns the configured contributed editor menu items.
        /// </summary>
        /// <returns>Configured contributed menu descriptors.</returns>
        public IReadOnlyList<EditorMenuItemDescriptor> GetAvailableEditorMenuItems() {
            return AvailableEditorMenuItems;
        }

        /// <summary>
        /// Disposes the fake host.
        /// </summary>
        public void Dispose() {
        }
    }
}
