namespace helengine {
    /// <summary>
    /// Identifies the concrete asset type stored in an editor-authored asset payload.
    /// </summary>
    public enum EditorAssetBinaryValueKind : ushort {
        /// <summary>
        /// The payload stores a <see cref="TextureAsset"/>.
        /// </summary>
        TextureAsset = 1,

        /// <summary>
        /// The payload stores a <see cref="ModelAsset"/>.
        /// </summary>
        ModelAsset = 2,

        /// <summary>
        /// The payload stores the legacy shader asset value kind reserved for shader-owned serializers.
        /// </summary>
        ShaderAsset = 3,

        /// <summary>
        /// The payload stores a <see cref="TextAsset"/>.
        /// </summary>
        TextAsset = 4,

        /// <summary>
        /// The payload stores a <see cref="MaterialAsset"/>.
        /// </summary>
        MaterialAsset = 5,

        /// <summary>
        /// The payload stores a <see cref="SceneAsset"/>.
        /// </summary>
        SceneAsset = 6,

        /// <summary>
        /// The payload stores an <see cref="AnimationClipAsset"/>.
        /// </summary>
        AnimationClipAsset = 8,

        /// <summary>
        /// The payload stores a <see cref="PlatformMaterialAsset"/>.
        /// </summary>
        PlatformMaterialAsset = 9
    }
}
