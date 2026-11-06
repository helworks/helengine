namespace helengine.editor {
    /// <summary>
    /// Keyframes for one node of an imported animation, addressed by the node path from the scene root.
    /// </summary>
    public sealed class ImportedSceneAnimationChannel {
        /// <summary>
        /// Creates the immutable ImportedSceneAnimationChannel from already-read data; reference arguments must not be null.
        /// </summary>
        /// <param name="nodePath">The unique path of the animated node, matching ImportedSceneNode.Path.</param>
        /// <param name="positionKeys">The translation keys in seconds.</param>
        /// <param name="rotationKeys">The rotation keys in seconds.</param>
        /// <param name="scaleKeys">The scale keys in seconds.</param>
        public ImportedSceneAnimationChannel(string nodePath, ImportedSceneVectorKey[] positionKeys, ImportedSceneRotationKey[] rotationKeys, ImportedSceneVectorKey[] scaleKeys) {
            NodePath = nodePath ?? throw new ArgumentNullException(nameof(nodePath));
            PositionKeys = positionKeys ?? throw new ArgumentNullException(nameof(positionKeys));
            RotationKeys = rotationKeys ?? throw new ArgumentNullException(nameof(rotationKeys));
            ScaleKeys = scaleKeys ?? throw new ArgumentNullException(nameof(scaleKeys));
        }

        /// <summary>
        /// Gets the unique path of the animated node, matching ImportedSceneNode.Path.
        /// </summary>
        public string NodePath { get; }

        /// <summary>
        /// Gets the translation keys in seconds.
        /// </summary>
        public ImportedSceneVectorKey[] PositionKeys { get; }

        /// <summary>
        /// Gets the rotation keys in seconds.
        /// </summary>
        public ImportedSceneRotationKey[] RotationKeys { get; }

        /// <summary>
        /// Gets the scale keys in seconds.
        /// </summary>
        public ImportedSceneVectorKey[] ScaleKeys { get; }
    }
}
