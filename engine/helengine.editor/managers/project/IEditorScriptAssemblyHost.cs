namespace helengine.editor {
    /// <summary>
    /// Imports a freshly built scripting assembly into a collectible load context.
    /// </summary>
    public interface IEditorScriptAssemblyHost : IDisposable {
        /// <summary>
        /// Gets the shared script type resolver backed by the currently loaded module assemblies.
        /// </summary>
        IScriptTypeResolver ScriptTypeResolver { get; }

        /// <summary>
        /// Reloads the current scripting assemblies from the newly built module descriptors.
        /// </summary>
        /// <param name="assemblies">Descriptors for the freshly built module assemblies.</param>
        void Reload(IReadOnlyList<ScriptAssemblyDescriptor> assemblies);

        /// <summary>
        /// Returns the addable component descriptors discovered from the currently loaded scripting assembly.
        /// </summary>
        /// <param name="entity">Entity that will receive one selected component.</param>
        /// <returns>Descriptors built from the active scripting assembly.</returns>
        IReadOnlyList<EditorComponentAddDescriptor> GetAvailableScriptComponents(Entity entity);
    }
}
