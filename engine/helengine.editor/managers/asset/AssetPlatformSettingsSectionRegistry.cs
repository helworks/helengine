namespace helengine.editor {
    /// <summary>
    /// Registers built-in platform settings sections and exposes typed access helpers.
    /// </summary>
    public sealed class AssetPlatformSettingsSectionRegistry {
        /// <summary>
        /// Shared registry instance used by the editor asset import pipeline.
        /// </summary>
        public static readonly AssetPlatformSettingsSectionRegistry Shared = new AssetPlatformSettingsSectionRegistry();

        /// <summary>
        /// Registered section definitions keyed by section id.
        /// </summary>
        readonly Dictionary<string, IAssetPlatformSettingsSectionDefinition> DefinitionsById;

        /// <summary>
        /// Initializes the built-in section registry.
        /// </summary>
        public AssetPlatformSettingsSectionRegistry() {
            DefinitionsById = new Dictionary<string, IAssetPlatformSettingsSectionDefinition>(StringComparer.OrdinalIgnoreCase);
            RegisterDefinition(new TextureAssetPlatformSettingsSectionDefinition());
            RegisterDefinition(new FontAtlasTextureAssetPlatformSettingsSectionDefinition());
            RegisterDefinition(new ModelAssetPlatformSettingsSectionDefinition());
            RegisterDefinition(new MaterialAssetPlatformSettingsSectionDefinition());
            RegisterDefinition(new FontAssetPlatformSettingsSectionDefinition());
        }

        /// <summary>
        /// Registers one section definition.
        /// </summary>
        /// <param name="definition">Definition to register.</param>
        public void RegisterDefinition(IAssetPlatformSettingsSectionDefinition definition) {
            if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            } else if (string.IsNullOrWhiteSpace(definition.SectionId)) {
                throw new InvalidOperationException("Section definition id must be provided.");
            } else if (definition.SettingsType == null) {
                throw new InvalidOperationException($"Section definition '{definition.SectionId}' must publish a payload type.");
            } else if (DefinitionsById.ContainsKey(definition.SectionId)) {
                throw new InvalidOperationException($"Section definition '{definition.SectionId}' is already registered.");
            }

            DefinitionsById.Add(definition.SectionId, definition);
        }

        /// <summary>
        /// Resolves one section definition by id.
        /// </summary>
        /// <param name="sectionId">Registered section identifier.</param>
        /// <returns>Resolved section definition.</returns>
        public IAssetPlatformSettingsSectionDefinition GetDefinition(string sectionId) {
            if (string.IsNullOrWhiteSpace(sectionId)) {
                throw new ArgumentException("Section id must be provided.", nameof(sectionId));
            }

            if (!DefinitionsById.TryGetValue(sectionId, out IAssetPlatformSettingsSectionDefinition definition)) {
                throw new InvalidOperationException($"Unknown asset-platform settings section id '{sectionId}'.");
            }

            return definition;
        }

        /// <summary>
        /// Gets or creates one typed section payload on the supplied platform settings record.
        /// </summary>
        /// <typeparam name="TSettings">Expected payload type.</typeparam>
        /// <param name="platformSettings">Platform settings record that owns the payload.</param>
        /// <param name="sectionId">Registered section identifier.</param>
        /// <returns>Resolved typed section payload.</returns>
        public TSettings GetOrCreateSection<TSettings>(AssetPlatformProcessorSettings platformSettings, string sectionId) where TSettings : class {
            if (platformSettings == null) {
                throw new ArgumentNullException(nameof(platformSettings));
            }

            IAssetPlatformSettingsSectionDefinition definition = GetDefinition(sectionId);
            if (definition.SettingsType != typeof(TSettings)) {
                throw new InvalidOperationException($"Asset-platform settings section '{sectionId}' requested as '{typeof(TSettings).Name}' but registered as '{definition.SettingsType.Name}'.");
            }

            if (!platformSettings.Sections.TryGetValue(sectionId, out AssetPlatformSettingsSection section)) {
                TSettings defaultSettings = (TSettings)definition.CreateDefaultSettings();
                section = new AssetPlatformSettingsSection(sectionId, defaultSettings);
                platformSettings.Sections[sectionId] = section;
            } else if (section.Settings is not TSettings) {
                throw new InvalidOperationException($"Asset-platform settings section '{sectionId}' stored one '{section.Settings.GetType().Name}' payload instead of '{typeof(TSettings).Name}'.");
            }

            return (TSettings)section.Settings;
        }

        /// <summary>
        /// Replaces one section payload on the supplied platform settings record.
        /// </summary>
        /// <param name="platformSettings">Platform settings record that owns the payload.</param>
        /// <param name="sectionId">Registered section identifier.</param>
        /// <param name="settings">Typed section payload to store.</param>
        public void SetSection(AssetPlatformProcessorSettings platformSettings, string sectionId, object settings) {
            if (platformSettings == null) {
                throw new ArgumentNullException(nameof(platformSettings));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            IAssetPlatformSettingsSectionDefinition definition = GetDefinition(sectionId);
            if (settings.GetType() != definition.SettingsType) {
                throw new InvalidOperationException($"Asset-platform settings section '{sectionId}' requires one '{definition.SettingsType.Name}' payload.");
            }

            platformSettings.Sections[sectionId] = new AssetPlatformSettingsSection(sectionId, settings);
        }

        /// <summary>
        /// Serializes one registered section payload.
        /// </summary>
        /// <param name="writer">Writer that owns the destination stream.</param>
        /// <param name="sectionId">Registered section identifier.</param>
        /// <param name="settings">Payload instance to serialize.</param>
        public void SerializeSection(EngineBinaryWriter writer, string sectionId, object settings) {
            IAssetPlatformSettingsSectionDefinition definition = GetDefinition(sectionId);
            definition.Serialize(writer, settings);
        }

        /// <summary>
        /// Deserializes one registered section payload.
        /// </summary>
        /// <param name="reader">Reader positioned at the payload body.</param>
        /// <param name="sectionId">Registered section identifier.</param>
        /// <returns>Deserialized payload instance.</returns>
        public object DeserializeSection(EngineBinaryReader reader, string sectionId) {
            IAssetPlatformSettingsSectionDefinition definition = GetDefinition(sectionId);
            return definition.Deserialize(reader);
        }

        /// <summary>
        /// Creates one deep clone of the supplied section record.
        /// </summary>
        /// <param name="sectionId">Registered section identifier.</param>
        /// <param name="section">Section record to clone.</param>
        /// <returns>Cloned section record.</returns>
        public AssetPlatformSettingsSection CloneSection(string sectionId, AssetPlatformSettingsSection section) {
            if (section == null) {
                throw new ArgumentNullException(nameof(section));
            }

            IAssetPlatformSettingsSectionDefinition definition = GetDefinition(sectionId);
            object clonedSettings = definition.CloneSettings(section.Settings);
            return new AssetPlatformSettingsSection(sectionId, clonedSettings);
        }

        /// <summary>
        /// Returns whether two platform settings records contain equal registered section payloads.
        /// </summary>
        /// <param name="left">First platform settings record.</param>
        /// <param name="right">Second platform settings record.</param>
        /// <returns>True when both records carry equal registered sections.</returns>
        public bool SectionsEqual(AssetPlatformProcessorSettings left, AssetPlatformProcessorSettings right) {
            if (left == null && right == null) {
                return true;
            } else if (left == null || right == null) {
                return false;
            }

            HashSet<string> sectionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string sectionId in left.Sections.Keys) {
                sectionIds.Add(sectionId);
            }

            foreach (string sectionId in right.Sections.Keys) {
                sectionIds.Add(sectionId);
            }

            foreach (string sectionId in sectionIds) {
                IAssetPlatformSettingsSectionDefinition definition = GetDefinition(sectionId);
                object leftSettings = left.Sections.TryGetValue(sectionId, out AssetPlatformSettingsSection leftSection)
                    ? leftSection.Settings
                    : definition.CreateDefaultSettings();
                object rightSettings = right.Sections.TryGetValue(sectionId, out AssetPlatformSettingsSection rightSection)
                    ? rightSection.Settings
                    : definition.CreateDefaultSettings();
                if (!definition.SettingsEqual(leftSettings, rightSettings)) {
                    return false;
                }
            }

            return true;
        }
    }
}
