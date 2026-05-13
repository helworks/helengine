namespace helengine.editor {
    /// <summary>
    /// Identifies the concrete value stored in an asset import settings payload.
    /// </summary>
    public enum AssetImportSettingsBinaryValueKind : ushort {
        /// <summary>
        /// The payload stores a <see cref="TextureAssetImportSettings"/> instance.
        /// </summary>
        TextureAssetImportSettings = 1,

        /// <summary>
        /// The payload stores a <see cref="ModelAssetImportSettings"/> instance.
        /// </summary>
        ModelAssetImportSettings = 2,

        /// <summary>
        /// The payload stores a <see cref="MaterialAssetImportSettings"/> instance.
        /// </summary>
        MaterialAssetImportSettings = 3,

        /// <summary>
        /// The payload stores an <see cref="AssetImportSettings"/> instance.
        /// </summary>
        AssetImportSettings = 4,

        /// <summary>
        /// The payload stores a <see cref="MaterialAssetCommonSettingsDocument"/> instance.
        /// </summary>
        MaterialAssetCommonSettingsDocument = 5,

        /// <summary>
        /// The payload stores a <see cref="MaterialAssetPlatformOverrideDocument"/> instance.
        /// </summary>
        MaterialAssetPlatformOverrideDocument = 6
    }
}
