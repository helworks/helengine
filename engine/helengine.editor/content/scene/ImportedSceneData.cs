namespace helengine.editor {
    /// <summary>
    /// Neutral, format-independent description of an imported scene: the node hierarchy with local transforms, the shared mesh and material tables, and the authored animations.
    /// </summary>
    public sealed class ImportedSceneData {
        /// <summary>
        /// Creates the immutable ImportedSceneData from already-read data; reference arguments must not be null.
        /// </summary>
        /// <param name="rootNode">The root of the node hierarchy; its children are the top-level scene nodes.</param>
        /// <param name="meshes">The unique meshes of the scene; nodes reference them by index so shared meshes are read once.</param>
        /// <param name="materials">The materials of the scene; meshes reference them by index.</param>
        /// <param name="animations">The animations authored in the source file.</param>
        /// <param name="sourceFileName">The file name of the source the scene was read from.</param>
        public ImportedSceneData(ImportedSceneNode rootNode, ImportedSceneMesh[] meshes, ImportedSceneMaterial[] materials, ImportedSceneAnimation[] animations, string sourceFileName) {
            RootNode = rootNode ?? throw new ArgumentNullException(nameof(rootNode));
            Meshes = meshes ?? throw new ArgumentNullException(nameof(meshes));
            Materials = materials ?? throw new ArgumentNullException(nameof(materials));
            Animations = animations ?? throw new ArgumentNullException(nameof(animations));
            SourceFileName = sourceFileName ?? throw new ArgumentNullException(nameof(sourceFileName));
        }

        /// <summary>
        /// Gets the root of the node hierarchy; its children are the top-level scene nodes.
        /// </summary>
        public ImportedSceneNode RootNode { get; }

        /// <summary>
        /// Gets the unique meshes of the scene; nodes reference them by index so shared meshes are read once.
        /// </summary>
        public ImportedSceneMesh[] Meshes { get; }

        /// <summary>
        /// Gets the materials of the scene; meshes reference them by index.
        /// </summary>
        public ImportedSceneMaterial[] Materials { get; }

        /// <summary>
        /// Gets the animations authored in the source file.
        /// </summary>
        public ImportedSceneAnimation[] Animations { get; }

        /// <summary>
        /// Gets the file name of the source the scene was read from.
        /// </summary>
        public string SourceFileName { get; }
    }
}
