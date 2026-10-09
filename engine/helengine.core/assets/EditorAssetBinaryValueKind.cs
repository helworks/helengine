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
        /// The payload stores the shader asset value kind reserved for shader-owned serializers.
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
        PlatformMaterialAsset = 9,

        /// <summary>
        /// The payload stores a <see cref="BlueprintAsset"/>.
        /// </summary>
        BlueprintAsset = 10,

        /// <summary>
        /// The payload stores an <see cref="AudioAsset"/>.
        /// </summary>
        AudioAsset = 11,

        /// <summary>
        /// The payload stores an <see cref="EffectAsset"/>.
        /// </summary>
        EffectAsset = 12,

        /// <summary>
        /// The payload stores a <see cref="GraphicTemplateAsset"/>.
        /// </summary>
        GraphicTemplateAsset = 13,

        /// <summary>
        /// The payload stores an authoring timeline (<c>helengine.timeline.TimelineAsset</c>, <c>.htimeline</c>). Core only
        /// reserves the number; the optional timeline module registers the serializer, so core never references it.
        /// </summary>
        TimelineAsset = 14,

        /// <summary>
        /// Reserved for the cooked, flat timeline form read on target platforms; its serializer is registered by the
        /// optional timeline runtime module.
        /// </summary>
        CookedTimelineAsset = 15
    }
}
