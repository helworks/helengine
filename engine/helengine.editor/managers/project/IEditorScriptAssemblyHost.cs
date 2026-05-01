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
    }
}
