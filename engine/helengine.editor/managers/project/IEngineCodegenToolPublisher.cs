namespace helengine.editor {
    /// <summary>
    /// Abstracts the two external commands the on-demand codegen build needs, so the provider is testable without them.
    /// </summary>
    public interface IEngineCodegenToolPublisher {
        /// <summary>
        /// Reads the checked-out commit of the codegen submodule.
        /// </summary>
        /// <param name="submoduleRootPath">Absolute submodule root path.</param>
        /// <returns>Full commit hash.</returns>
        string ReadCommit(string submoduleRootPath);

        /// <summary>
        /// Publishes the codegen project in Release into the supplied directory.
        /// </summary>
        /// <param name="codegenProjectPath">Absolute path to <c>codegen/codegen.csproj</c>.</param>
        /// <param name="outputDirectoryPath">Absolute publish output directory.</param>
        void Publish(string codegenProjectPath, string outputDirectoryPath);
    }
}
