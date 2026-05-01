namespace helengine.editor {
    /// <summary>
    /// Imports a freshly built scripting assembly into a collectible load context.
    /// </summary>
    public interface IEditorScriptAssemblyHost : IDisposable {
        /// <summary>
        /// Reloads the current scripting assembly from one newly built output directory.
        /// </summary>
        /// <param name="sourceOutputDirectoryPath">Absolute path to the fresh build output directory.</param>
        /// <param name="mainAssemblyPath">Absolute path to the main scripting assembly inside the build output.</param>
        void Reload(string sourceOutputDirectoryPath, string mainAssemblyPath);

        /// <summary>
        /// Returns the addable component descriptors discovered from the currently loaded scripting assembly.
        /// </summary>
        /// <param name="entity">Entity that will receive one selected component.</param>
        /// <returns>Descriptors built from the active scripting assembly.</returns>
        IReadOnlyList<EditorComponentAddDescriptor> GetAvailableScriptComponents(Entity entity);
    }
}
