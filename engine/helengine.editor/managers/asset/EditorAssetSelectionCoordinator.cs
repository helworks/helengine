namespace helengine.editor {
    /// <summary>
    /// Classifies the asset the browser has selected and translates between the typed per-asset
    /// import settings stored on disk and the flat processor-settings payload the import-settings
    /// view edits. Which import-settings surface an entry gets depends on the importers actually
    /// registered for its extension, not only on the entry's own kind, so that decision lives here
    /// rather than in the panel. The editor session keeps the panel wiring and the apply workflow.
    /// </summary>
    public sealed class EditorAssetSelectionCoordinator {
        /// <summary>
        /// File extension used by authored animation clip assets.
        /// </summary>
        public const string AnimationClipExtension = ".hanim";

        /// <summary>
        /// Import manager that owns the registered importer extensions for this project.
        /// </summary>
        AssetImportManager AssetImportManager { get; }

        /// <summary>
        /// Initializes one selection coordinator bound to the session's import manager.
        /// </summary>
        /// <param name="assetImportManager">Import manager that owns the registered importer extensions.</param>
        public EditorAssetSelectionCoordinator(AssetImportManager assetImportManager) {
            if (assetImportManager == null) {
                throw new ArgumentNullException(nameof(assetImportManager));
            }

            AssetImportManager = assetImportManager;
        }

        /// <summary>
        /// Determines whether the selected entry is a material asset.
        /// </summary>
        /// <param name="entry">Entry to evaluate.</param>
        /// <returns>True when the entry is a material asset.</returns>
        public static bool IsMaterialAssetEntry(AssetBrowserEntry entry) {
            if (entry == null) {
                return false;
            }

            return string.Equals(entry.Extension, EditorFileTemplateRegistry.MaterialExtension, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether the selected entry is an animation clip asset.
        /// </summary>
        /// <param name="entry">Entry to evaluate.</param>
        /// <returns>True when the entry is an animation clip asset.</returns>
        public static bool IsAnimationClipAssetEntry(AssetBrowserEntry entry) {
            if (entry == null) {
                return false;
            }

            return string.Equals(entry.Extension, AnimationClipExtension, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Creates one copy of texture processor settings. Absent settings clone to the defaults the
        /// import-settings view edits, so an asset that has never been configured still round-trips.
        /// </summary>
        /// <param name="settings">Texture processor settings to clone, or null when none are stored.</param>
        /// <returns>Cloned texture processor settings.</returns>
        public static TextureAssetProcessorSettings CloneTextureProcessorSettings(TextureAssetProcessorSettings settings) {
            TextureAssetProcessorSettings clone = new TextureAssetProcessorSettings();
            if (settings == null) {
                return clone;
            }

            clone.MaxResolution = settings.MaxResolution;
            clone.ColorFormatId = settings.ColorFormatId;
            clone.AlphaPrecision = settings.AlphaPrecision;
            clone.IndexingMethodId = settings.IndexingMethodId;
            return clone;
        }

        /// <summary>
        /// Creates one copy of model processor settings. Absent settings clone to the defaults the
        /// import-settings view edits, so an asset that has never been configured still round-trips.
        /// </summary>
        /// <param name="settings">Model processor settings to clone, or null when none are stored.</param>
        /// <returns>Cloned model processor settings.</returns>
        public static ModelAssetProcessorSettings CloneModelProcessorSettings(ModelAssetProcessorSettings settings) {
            ModelAssetProcessorSettings clone = new ModelAssetProcessorSettings();
            if (settings == null) {
                return clone;
            }

            clone.FlipWinding = settings.FlipWinding;
            clone.Tessellate = settings.Tessellate;
            clone.TessellationMaxEdgeLength = settings.TessellationMaxEdgeLength;
            return clone;
        }

        /// <summary>
        /// Creates one model-focused processor-settings payload for the import-settings view.
        /// </summary>
        /// <param name="settings">Typed model import settings to project into the view model.</param>
        /// <returns>Processor settings payload consumed by the model import-settings UI.</returns>
        public static AssetProcessorSettings CreateModelImportViewProcessorSettings(ModelAssetImportSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            AssetProcessorSettings processorSettings = new AssetProcessorSettings();
            if (settings.Processor == null || settings.Processor.Platforms == null) {
                return processorSettings;
            }

            foreach (KeyValuePair<string, ModelAssetProcessorSettings> pair in settings.Processor.Platforms) {
                if (string.IsNullOrWhiteSpace(pair.Key)) {
                    continue;
                }

                AssetPlatformProcessorSettings platformSettings = new AssetPlatformProcessorSettings {
                    Model = CloneModelProcessorSettings(pair.Value)
                };
                processorSettings.Platforms[pair.Key] = platformSettings;
                if (settings.Processor.Environments != null
                    && settings.Processor.Environments.TryGetValue(pair.Key, out Dictionary<string, ModelAssetProcessorSettings> environments)
                    && environments != null) {
                    foreach (KeyValuePair<string, ModelAssetProcessorSettings> environment in environments) {
                        platformSettings.Environments[environment.Key] = new AssetPlatformProcessorSettings {
                            Model = CloneModelProcessorSettings(environment.Value)
                        };
                    }
                }
            }

            return processorSettings;
        }

        /// <summary>
        /// Applies one model import-settings request payload to typed model settings.
        /// </summary>
        /// <param name="settings">Typed model settings to update.</param>
        /// <param name="processorSettings">Processor settings payload emitted by the import-settings view.</param>
        public static void ApplyModelImportRequestProcessorSettings(ModelAssetImportSettings settings, AssetProcessorSettings processorSettings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (processorSettings == null) {
                throw new ArgumentNullException(nameof(processorSettings));
            }

            settings.Processor = new ModelAssetProcessorPlatformSettings();
            foreach (KeyValuePair<string, AssetPlatformProcessorSettings> pair in processorSettings.Platforms) {
                if (string.IsNullOrWhiteSpace(pair.Key)) {
                    continue;
                }

                AssetPlatformProcessorSettings platformSettings = pair.Value;
                ModelAssetProcessorSettings platformModelSettings;
                if (platformSettings == null) {
                    platformModelSettings = null;
                } else {
                    platformModelSettings = platformSettings.Model;
                }

                settings.Processor.Platforms[pair.Key] = CloneModelProcessorSettings(platformModelSettings);
                if (platformSettings != null && platformSettings.Environments != null) {
                    Dictionary<string, ModelAssetProcessorSettings> environments = new Dictionary<string, ModelAssetProcessorSettings>(StringComparer.OrdinalIgnoreCase);
                    foreach (KeyValuePair<string, AssetPlatformProcessorSettings> environment in platformSettings.Environments) {
                        AssetPlatformProcessorSettings environmentSettings = environment.Value;
                        ModelAssetProcessorSettings environmentModelSettings;
                        if (environmentSettings == null) {
                            environmentModelSettings = null;
                        } else {
                            environmentModelSettings = environmentSettings.Model;
                        }

                        environments[environment.Key] = CloneModelProcessorSettings(environmentModelSettings);
                    }

                    settings.Processor.Environments[pair.Key] = environments;
                }
            }
        }

        /// <summary>
        /// Creates one texture-focused processor-settings payload for the import-settings view.
        /// </summary>
        /// <param name="settings">Typed texture import settings to project into the view model.</param>
        /// <returns>Processor settings payload consumed by the texture import-settings UI.</returns>
        public static AssetProcessorSettings CreateTextureImportViewProcessorSettings(TextureAssetImportSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            AssetProcessorSettings processorSettings = new AssetProcessorSettings();
            if (settings.Processor == null || settings.Processor.Platforms == null) {
                return processorSettings;
            }

            foreach (KeyValuePair<string, TextureAssetProcessorSettings> pair in settings.Processor.Platforms) {
                if (string.IsNullOrWhiteSpace(pair.Key)) {
                    continue;
                }

                AssetPlatformProcessorSettings platformSettings = new AssetPlatformProcessorSettings {
                    Texture = CloneTextureProcessorSettings(pair.Value)
                };
                processorSettings.Platforms[pair.Key] = platformSettings;
                if (settings.Processor.Environments != null
                    && settings.Processor.Environments.TryGetValue(pair.Key, out Dictionary<string, TextureAssetProcessorSettings> environments)
                    && environments != null) {
                    foreach (KeyValuePair<string, TextureAssetProcessorSettings> environment in environments) {
                        platformSettings.Environments[environment.Key] = new AssetPlatformProcessorSettings {
                            Texture = CloneTextureProcessorSettings(environment.Value)
                        };
                    }
                }
            }

            return processorSettings;
        }

        /// <summary>
        /// Applies one texture import-settings request payload to typed texture settings.
        /// </summary>
        /// <param name="settings">Typed texture settings to update.</param>
        /// <param name="processorSettings">Processor settings payload emitted by the import-settings view.</param>
        public static void ApplyTextureImportRequestProcessorSettings(TextureAssetImportSettings settings, AssetProcessorSettings processorSettings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (processorSettings == null) {
                throw new ArgumentNullException(nameof(processorSettings));
            }

            settings.Processor = new TextureAssetProcessorPlatformSettings();
            foreach (KeyValuePair<string, AssetPlatformProcessorSettings> pair in processorSettings.Platforms) {
                if (string.IsNullOrWhiteSpace(pair.Key)) {
                    continue;
                }

                AssetPlatformProcessorSettings platformSettings = pair.Value;
                TextureAssetProcessorSettings platformTextureSettings;
                if (platformSettings == null) {
                    platformTextureSettings = null;
                } else {
                    platformTextureSettings = platformSettings.Texture;
                }

                settings.Processor.Platforms[pair.Key] = CloneTextureProcessorSettings(platformTextureSettings);
                if (platformSettings != null && platformSettings.Environments != null) {
                    Dictionary<string, TextureAssetProcessorSettings> environments = new Dictionary<string, TextureAssetProcessorSettings>(StringComparer.OrdinalIgnoreCase);
                    foreach (KeyValuePair<string, AssetPlatformProcessorSettings> environment in platformSettings.Environments) {
                        AssetPlatformProcessorSettings environmentSettings = environment.Value;
                        TextureAssetProcessorSettings environmentTextureSettings;
                        if (environmentSettings == null) {
                            environmentTextureSettings = null;
                        } else {
                            environmentTextureSettings = environmentSettings.Texture;
                        }

                        environments[environment.Key] = CloneTextureProcessorSettings(environmentTextureSettings);
                    }

                    settings.Processor.Environments[pair.Key] = environments;
                }
            }
        }

        /// <summary>
        /// Determines whether one browser entry requires the typed model import-settings path.
        /// </summary>
        /// <param name="entry">Asset browser entry to inspect.</param>
        /// <returns>True for built-in model entries and dynamically registered model extensions.</returns>
        public bool IsModelImportSettingsEntry(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            return entry.EntryKind == AssetEntryKind.Model || AssetImportManager.IsModelExtension(entry.Extension);
        }

        /// <summary>
        /// Determines whether one browser entry requires the typed texture import-settings path.
        /// </summary>
        /// <param name="entry">Asset browser entry to inspect.</param>
        /// <returns>True for built-in image entries and dynamically registered texture extensions.</returns>
        public bool IsTextureImportSettingsEntry(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            return entry.EntryKind == AssetEntryKind.Image || AssetImportManager.IsTextureExtension(entry.Extension);
        }

        /// <summary>
        /// Determines whether one browser entry requires the audio import-settings rejection path.
        /// </summary>
        /// <param name="entry">Asset browser entry to inspect.</param>
        /// <returns>True for built-in audio entries and dynamically registered audio extensions.</returns>
        public bool IsAudioImportSettingsEntry(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            return entry.EntryKind == AssetEntryKind.Audio || AssetImportManager.IsAudioExtension(entry.Extension);
        }

        /// <summary>
        /// Resolves the processor-control kind for an import-settings view without changing the browser entry's original kind.
        /// </summary>
        /// <param name="entry">Asset browser entry whose registered importer should determine the presentation kind.</param>
        /// <returns>Typed presentation kind for model and texture importers, or the original entry kind otherwise.</returns>
        public AssetEntryKind ResolveImportSettingsPresentationKind(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            if (IsModelImportSettingsEntry(entry)) {
                return AssetEntryKind.Model;
            }
            if (IsTextureImportSettingsEntry(entry)) {
                return AssetEntryKind.Image;
            }

            return entry.EntryKind;
        }
    }
}
