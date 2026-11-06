namespace helengine.editor {
    /// <summary>
    /// One node of an imported scene hierarchy, carrying its local (not baked) transform and the meshes attached to it.
    /// </summary>
    public sealed class ImportedSceneNode {
        /// <summary>
        /// Creates the immutable ImportedSceneNode from already-read data; reference arguments must not be null.
        /// </summary>
        /// <param name="name">The node name, unique within the scene after disambiguation.</param>
        /// <param name="path">The slash-joined unique names from the child of the root down to this node (empty for the root), for example Base/Lid/Flap.</param>
        /// <param name="localPosition">The translation relative to the parent node.</param>
        /// <param name="localRotation">The rotation relative to the parent node as an xyzw quaternion.</param>
        /// <param name="localScale">The scale relative to the parent node.</param>
        /// <param name="meshIndices">The indices into the scene mesh table of the meshes attached to this node.</param>
        /// <param name="children">The child nodes in source order.</param>
        public ImportedSceneNode(string name, string path, float3 localPosition, float4 localRotation, float3 localScale, int[] meshIndices, ImportedSceneNode[] children) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Path = path ?? throw new ArgumentNullException(nameof(path));
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            LocalScale = localScale;
            MeshIndices = meshIndices ?? throw new ArgumentNullException(nameof(meshIndices));
            Children = children ?? throw new ArgumentNullException(nameof(children));
        }

        /// <summary>
        /// Gets the node name, unique within the scene after disambiguation.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the slash-joined unique names from the child of the root down to this node (empty for the root), for example Base/Lid/Flap.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Gets the translation relative to the parent node.
        /// </summary>
        public float3 LocalPosition { get; }

        /// <summary>
        /// Gets the rotation relative to the parent node as an xyzw quaternion.
        /// </summary>
        public float4 LocalRotation { get; }

        /// <summary>
        /// Gets the scale relative to the parent node.
        /// </summary>
        public float3 LocalScale { get; }

        /// <summary>
        /// Gets the indices into the scene mesh table of the meshes attached to this node.
        /// </summary>
        public int[] MeshIndices { get; }

        /// <summary>
        /// Gets the child nodes in source order.
        /// </summary>
        public ImportedSceneNode[] Children { get; }
    }
}
