namespace helengine.editor {
    /// <summary>
    /// Stores processor settings for one target platform.
    /// </summary>
    public class AssetPlatformProcessorSettings {
        /// <summary>
        /// Initializes the registered processor settings sections for one target platform.
        /// </summary>
        public AssetPlatformProcessorSettings() {
            Sections = new Dictionary<string, AssetPlatformSettingsSection>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets or sets the registered section payloads keyed by section id.
        /// </summary>
        public Dictionary<string, AssetPlatformSettingsSection> Sections { get; set; }

        /// <summary>
        /// Gets or sets the processor settings that affect texture asset generation.
        /// </summary>
        public TextureAssetProcessorSettings Texture {
            get {
                return AssetPlatformSettingsSectionRegistry.Shared.GetOrCreateSection<TextureAssetProcessorSettings>(this, TextureAssetPlatformSettingsSectionDefinition.SectionIdValue);
            }
            set {
                AssetPlatformSettingsSectionRegistry.Shared.SetSection(this, TextureAssetPlatformSettingsSectionDefinition.SectionIdValue, value ?? throw new ArgumentNullException(nameof(value)));
            }
        }

        /// <summary>
        /// Gets or sets the processor settings that affect model asset generation.
        /// </summary>
        public ModelAssetProcessorSettings Model {
            get {
                return AssetPlatformSettingsSectionRegistry.Shared.GetOrCreateSection<ModelAssetProcessorSettings>(this, ModelAssetPlatformSettingsSectionDefinition.SectionIdValue);
            }
            set {
                AssetPlatformSettingsSectionRegistry.Shared.SetSection(this, ModelAssetPlatformSettingsSectionDefinition.SectionIdValue, value ?? throw new ArgumentNullException(nameof(value)));
            }
        }

        /// <summary>
        /// Gets or sets the processor settings that affect material asset authoring on this platform.
        /// </summary>
        public MaterialAssetProcessorSettings Material {
            get {
                return AssetPlatformSettingsSectionRegistry.Shared.GetOrCreateSection<MaterialAssetProcessorSettings>(this, MaterialAssetPlatformSettingsSectionDefinition.SectionIdValue);
            }
            set {
                AssetPlatformSettingsSectionRegistry.Shared.SetSection(this, MaterialAssetPlatformSettingsSectionDefinition.SectionIdValue, value ?? throw new ArgumentNullException(nameof(value)));
            }
        }

        /// <summary>
        /// Gets or sets the processor settings that affect font rasterization on this platform.
        /// </summary>
        public FontAssetProcessorSettings Font {
            get {
                return AssetPlatformSettingsSectionRegistry.Shared.GetOrCreateSection<FontAssetProcessorSettings>(this, FontAssetPlatformSettingsSectionDefinition.SectionIdValue);
            }
            set {
                AssetPlatformSettingsSectionRegistry.Shared.SetSection(this, FontAssetPlatformSettingsSectionDefinition.SectionIdValue, value ?? throw new ArgumentNullException(nameof(value)));
            }
        }

        /// <summary>
        /// Gets or sets the processor settings that affect generated font-atlas texture cooking on this platform.
        /// </summary>
        public TextureAssetProcessorSettings FontAtlasTexture {
            get {
                return AssetPlatformSettingsSectionRegistry.Shared.GetOrCreateSection<TextureAssetProcessorSettings>(this, FontAtlasTextureAssetPlatformSettingsSectionDefinition.SectionIdValue);
            }
            set {
                AssetPlatformSettingsSectionRegistry.Shared.SetSection(this, FontAtlasTextureAssetPlatformSettingsSectionDefinition.SectionIdValue, value ?? throw new ArgumentNullException(nameof(value)));
            }
        }
    }
}
