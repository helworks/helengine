using helengine;

namespace helengine.editor {

    /// <summary>
    /// Represents one immutable authored asset and its typed cook dependencies.
    /// </summary>
    public sealed class EditorAssetCookNode {
        /// <summary>
        /// Initializes one cook node.
        /// </summary>
        /// <param name="key">Stable node key.</param>
        /// <param name="sourceReference">Canonical authored source reference.</param>
        /// <param name="sourceKind">Source asset kind.</param>
        /// <param name="dependencies">Typed dependencies.</param>
        /// <param name="processorId">Processor identity.</param>
        public EditorAssetCookNode(
            EditorAssetCookNodeKey key,
            SceneAssetReference sourceReference,
            AssetEntryKind sourceKind,
            IEnumerable<EditorAssetCookDependency> dependencies,
            string processorId) {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            SourceReference = sourceReference ?? throw new ArgumentNullException(nameof(sourceReference));
            if (dependencies == null) {
                throw new ArgumentNullException(nameof(dependencies));
            }
            if (string.IsNullOrWhiteSpace(processorId)) {
                throw new ArgumentException("Processor id must be provided.", nameof(processorId));
            }

            SourceKind = sourceKind;
            Dependencies = Array.AsReadOnly(dependencies.ToArray());
            ProcessorId = processorId;
        }

        /// <summary>
        /// Gets the stable node key.
        /// </summary>
        public EditorAssetCookNodeKey Key { get; }

        /// <summary>
        /// Gets the canonical authored source reference.
        /// </summary>
        public SceneAssetReference SourceReference { get; }

        /// <summary>
        /// Gets the source asset kind.
        /// </summary>
        public AssetEntryKind SourceKind { get; }

        /// <summary>
        /// Gets the immutable dependency list.
        /// </summary>
        public IReadOnlyList<EditorAssetCookDependency> Dependencies { get; }

        /// <summary>
        /// Gets the processor identity used to produce the artifact.
        /// </summary>
        public string ProcessorId { get; }
    }
}
