namespace helengine.editor {
    /// <summary>
    /// Resolves the csharpcodegen executable that belongs to the running engine build.
    /// </summary>
    public interface IEngineCodegenToolProvider {
        /// <summary>
        /// Returns the absolute path of the codegen executable, building it on demand when nothing was published.
        /// </summary>
        /// <returns>Absolute path to <c>codegen.exe</c>.</returns>
        string Resolve();
    }
}
