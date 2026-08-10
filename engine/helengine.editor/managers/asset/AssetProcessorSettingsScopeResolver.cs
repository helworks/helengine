namespace helengine.editor {
    /// <summary>
    /// Resolves asset processor settings through platform and nested environment inheritance.
    /// </summary>
    public static class AssetProcessorSettingsScopeResolver {
        /// <summary>
        /// Resolves one effective processor settings record for the requested platform/environment scope.
        /// </summary>
        public static AssetPlatformProcessorSettings Resolve(AssetProcessorSettings settings, EditorOverrideScope scope) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            AssetPlatformProcessorSettings platformSettings = null;
            if (settings.Platforms != null) {
                settings.Platforms.TryGetValue(scope.PlatformId, out platformSettings);
            }

            AssetPlatformProcessorSettings effective = ClonePlatform(platformSettings);
            if (scope.IsPlatformOnly || platformSettings?.Environments == null
                || !platformSettings.Environments.TryGetValue(scope.EnvironmentId, out AssetPlatformProcessorSettings environmentSettings)
                || environmentSettings == null) {
                return effective;
            }

            foreach (KeyValuePair<string, AssetPlatformSettingsSection> entry in environmentSettings.Sections) {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null) {
                    continue;
                }
                effective.Sections[entry.Key] = AssetPlatformSettingsSectionRegistry.Shared.CloneSection(entry.Key, entry.Value);
            }

            return effective;
        }

        /// <summary>
        /// Creates a deep clone of one platform settings record, including all nested environments.
        /// </summary>
        public static AssetPlatformProcessorSettings ClonePlatform(AssetPlatformProcessorSettings source) {
            AssetPlatformProcessorSettings clone = new AssetPlatformProcessorSettings();
            if (source == null) {
                return clone;
            }

            if (source.Sections != null) {
                foreach (KeyValuePair<string, AssetPlatformSettingsSection> entry in source.Sections) {
                    if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null) {
                        continue;
                    }
                    clone.Sections[entry.Key] = AssetPlatformSettingsSectionRegistry.Shared.CloneSection(entry.Key, entry.Value);
                }
            }
            if (source.Environments != null) {
                foreach (KeyValuePair<string, AssetPlatformProcessorSettings> entry in source.Environments) {
                    if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null) {
                        continue;
                    }
                    clone.Environments[entry.Key] = ClonePlatform(entry.Value);
                }
            }

            return clone;
        }

        /// <summary>
        /// Resolves one typed processor section through platform/environment inheritance.
        /// </summary>
        public static TSettings ResolveSection<TSettings>(AssetProcessorSettings settings, EditorOverrideScope scope, string sectionId)
            where TSettings : class {
            AssetPlatformProcessorSettings effective = Resolve(settings, scope);
            return AssetPlatformSettingsSectionRegistry.Shared.GetOrCreateSection<TSettings>(effective, sectionId);
        }
    }
}
