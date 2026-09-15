namespace helengine.editor {
    /// <summary>
    /// Describes every input that can change the compiled output of the generated scripting solution.
    /// </summary>
    public sealed class EditorScriptBuildInputs {
        /// <summary>
        /// Initializes one build-input description.
        /// </summary>
        /// <param name="generatedFilePaths">Absolute paths of generated solution, project, usings, and props files whose contents feed the build.</param>
        /// <param name="sourceDirectoryPaths">Absolute source directories whose C# files are compiled.</param>
        /// <param name="referencedAssemblyPaths">Absolute paths of engine assemblies referenced by the generated projects.</param>
        /// <param name="tokens">Additional configuration values, such as the compilation mode, that select the build output.</param>
        public EditorScriptBuildInputs(
            IReadOnlyList<string> generatedFilePaths,
            IReadOnlyList<string> sourceDirectoryPaths,
            IReadOnlyList<string> referencedAssemblyPaths,
            IReadOnlyList<string> tokens) {
            GeneratedFilePaths = generatedFilePaths ?? throw new ArgumentNullException(nameof(generatedFilePaths));
            SourceDirectoryPaths = sourceDirectoryPaths ?? throw new ArgumentNullException(nameof(sourceDirectoryPaths));
            ReferencedAssemblyPaths = referencedAssemblyPaths ?? throw new ArgumentNullException(nameof(referencedAssemblyPaths));
            Tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        }

        /// <summary>
        /// Gets the generated metadata files whose contents feed the build.
        /// </summary>
        public IReadOnlyList<string> GeneratedFilePaths { get; }

        /// <summary>
        /// Gets the source directories whose C# files are compiled.
        /// </summary>
        public IReadOnlyList<string> SourceDirectoryPaths { get; }

        /// <summary>
        /// Gets the engine assemblies referenced by the generated projects.
        /// </summary>
        public IReadOnlyList<string> ReferencedAssemblyPaths { get; }

        /// <summary>
        /// Gets the configuration values that select the build output.
        /// </summary>
        public IReadOnlyList<string> Tokens { get; }
    }
}
