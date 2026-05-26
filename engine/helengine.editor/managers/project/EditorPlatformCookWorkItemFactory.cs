using System.Text;
using System.Text.Json;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Manifest;

namespace helengine.editor;

/// <summary>
/// Creates builder-owned platform cook work items from editor-resolved source assets and processor settings.
/// </summary>
internal static class EditorPlatformCookWorkItemFactory {
    /// <summary>
    /// Creates one builder-owned texture cook work item when the selected platform publishes that capability.
    /// </summary>
    /// <param name="platformDefinition">Platform definition that may publish builder-owned texture cooking.</param>
    /// <param name="targetPlatformId">Target platform identifier used to resolve platform settings.</param>
    /// <param name="projectRootPath">Absolute project root that owns the source asset.</param>
    /// <param name="sourceRelativePath">Project-relative source asset path.</param>
    /// <param name="outputRelativePath">Runtime-relative output path the builder must produce.</param>
    /// <param name="settings">Resolved texture import settings for the source asset.</param>
    /// <param name="fileHasher">Hasher used to compute source and settings hashes.</param>
    /// <returns>Resolved work item when the platform owns texture cooking; otherwise null.</returns>
    public static PlatformCookWorkItem CreateTextureWorkItem(
        PlatformDefinition platformDefinition,
        string targetPlatformId,
        string projectRootPath,
        string sourceRelativePath,
        string outputRelativePath,
        TextureAssetImportSettings settings,
        AssetFileHasher fileHasher) {
        if (settings == null) {
            throw new ArgumentNullException(nameof(settings));
        }

        PlatformAssetCookCapabilityDefinition capability = ResolveBuilderOwnedCapability(platformDefinition, "texture");
        if (capability == null) {
            return null;
        }

        TextureAssetProcessorSettings processorSettings = ResolveTextureProcessorSettings(targetPlatformId, settings.Processor?.Platforms, capability);
        return CreateWorkItem(
            capability,
            targetPlatformId,
            projectRootPath,
            sourceRelativePath,
            "texture",
            outputRelativePath,
            settings.Importer?.AssetId ?? sourceRelativePath,
            processorSettings,
            fileHasher);
    }

    /// <summary>
    /// Creates one builder-owned font-atlas cook work item when the selected platform publishes that capability.
    /// </summary>
    /// <param name="platformDefinition">Platform definition that may publish builder-owned font-atlas cooking.</param>
    /// <param name="targetPlatformId">Target platform identifier used to resolve platform settings.</param>
    /// <param name="projectRootPath">Absolute project root that owns the source asset.</param>
    /// <param name="sourceRelativePath">Project-relative source asset path.</param>
    /// <param name="outputRelativePath">Runtime-relative output path the builder must produce.</param>
    /// <param name="settings">Resolved generic import settings for the source font asset.</param>
    /// <param name="fileHasher">Hasher used to compute source and settings hashes.</param>
    /// <returns>Resolved work item when the platform owns font-atlas cooking; otherwise null.</returns>
    public static PlatformCookWorkItem CreateFontAtlasTextureWorkItem(
        PlatformDefinition platformDefinition,
        string targetPlatformId,
        string projectRootPath,
        string sourceRelativePath,
        string outputRelativePath,
        AssetImportSettings settings,
        AssetFileHasher fileHasher) {
        if (settings == null) {
            throw new ArgumentNullException(nameof(settings));
        }

        PlatformAssetCookCapabilityDefinition capability = ResolveBuilderOwnedCapability(platformDefinition, "font-atlas-texture");
        if (capability == null) {
            return null;
        }

        TextureAssetProcessorSettings processorSettings = ResolveTextureProcessorSettings(targetPlatformId, settings.Processor?.Platforms, capability);
        return CreateWorkItem(
            capability,
            targetPlatformId,
            projectRootPath,
            sourceRelativePath,
            "font-atlas-texture",
            outputRelativePath,
            settings.Importer?.AssetId ?? sourceRelativePath,
            processorSettings,
            fileHasher);
    }

    static PlatformCookWorkItem CreateWorkItem(
        PlatformAssetCookCapabilityDefinition capability,
        string targetPlatformId,
        string projectRootPath,
        string sourceRelativePath,
        string sourceAssetKind,
        string outputRelativePath,
        string sourceAssetId,
        TextureAssetProcessorSettings processorSettings,
        AssetFileHasher fileHasher) {
        if (capability == null) {
            throw new ArgumentNullException(nameof(capability));
        } else if (string.IsNullOrWhiteSpace(targetPlatformId)) {
            throw new ArgumentException("Target platform id must be provided.", nameof(targetPlatformId));
        } else if (string.IsNullOrWhiteSpace(projectRootPath)) {
            throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
        } else if (string.IsNullOrWhiteSpace(sourceRelativePath)) {
            throw new ArgumentException("Source relative path must be provided.", nameof(sourceRelativePath));
        } else if (string.IsNullOrWhiteSpace(sourceAssetKind)) {
            throw new ArgumentException("Source asset kind must be provided.", nameof(sourceAssetKind));
        } else if (string.IsNullOrWhiteSpace(outputRelativePath)) {
            throw new ArgumentException("Output relative path must be provided.", nameof(outputRelativePath));
        } else if (processorSettings == null) {
            throw new ArgumentNullException(nameof(processorSettings));
        } else if (fileHasher == null) {
            throw new ArgumentNullException(nameof(fileHasher));
        }

        string normalizedSourceRelativePath = sourceRelativePath.Replace('\\', '/');
        string normalizedOutputRelativePath = outputRelativePath.Replace('\\', '/');
        string fullSourcePath = Path.GetFullPath(Path.Combine(projectRootPath, "assets", normalizedSourceRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!File.Exists(fullSourcePath)) {
            throw new InvalidOperationException($"Builder-owned platform cook source '{fullSourcePath}' was not found for asset kind '{sourceAssetKind}'.");
        }
        string serializedSettings = SerializeTextureSettings(processorSettings);
        string settingsHash = ComputeStringHash(fileHasher, serializedSettings);
        string sourceHash = fileHasher.ComputeHash(fullSourcePath);
        string workItemId = string.Concat(targetPlatformId, ":", sourceAssetKind, ":", normalizedOutputRelativePath);

        return new PlatformCookWorkItem(
            workItemId,
            fullSourcePath,
            sourceAssetKind,
            targetPlatformId,
            capability.TargetArtifactKind,
            normalizedOutputRelativePath,
            string.Concat(capability.TargetArtifactKind, ":", normalizedOutputRelativePath),
            sourceHash,
            settingsHash,
            serializedSettings,
            [
                new PlatformCookWorkItemMetadata("source-asset-id", sourceAssetId ?? normalizedSourceRelativePath),
                new PlatformCookWorkItemMetadata("settings-contract-id", capability.SettingsContractId)
            ]);
    }

    static PlatformAssetCookCapabilityDefinition ResolveBuilderOwnedCapability(PlatformDefinition platformDefinition, string sourceAssetKind) {
        if (platformDefinition == null) {
            throw new ArgumentNullException(nameof(platformDefinition));
        } else if (string.IsNullOrWhiteSpace(sourceAssetKind)) {
            throw new ArgumentException("Source asset kind must be provided.", nameof(sourceAssetKind));
        }

        PlatformAssetCookCapabilityDefinition[] capabilities = platformDefinition.AssetCookCapabilities ?? [];
        for (int index = 0; index < capabilities.Length; index++) {
            PlatformAssetCookCapabilityDefinition capability = capabilities[index];
            if (capability == null) {
                continue;
            }
            if (!string.Equals(capability.SourceAssetKind, sourceAssetKind, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }
            if (capability.OwnershipKind == PlatformAssetCookOwnershipKind.BuilderOwned) {
                return capability;
            }
        }

        return null;
    }

    static TextureAssetProcessorSettings ResolveTextureProcessorSettings(
        string targetPlatformId,
        IDictionary<string, TextureAssetProcessorSettings> platformSettingsById,
        PlatformAssetCookCapabilityDefinition capability) {
        if (!string.IsNullOrWhiteSpace(targetPlatformId)
            && platformSettingsById != null
            && platformSettingsById.TryGetValue(targetPlatformId, out TextureAssetProcessorSettings platformSettings)
            && platformSettings != null) {
            return platformSettings;
        }

        return ResolveDefaultTextureProcessorSettings(capability);
    }

    static TextureAssetProcessorSettings ResolveTextureProcessorSettings(
        string targetPlatformId,
        IDictionary<string, AssetPlatformProcessorSettings> platformSettingsById,
        PlatformAssetCookCapabilityDefinition capability) {
        if (!string.IsNullOrWhiteSpace(targetPlatformId)
            && platformSettingsById != null
            && platformSettingsById.TryGetValue(targetPlatformId, out AssetPlatformProcessorSettings platformSettings)
            && platformSettings?.Texture != null) {
            return platformSettings.Texture;
        }

        return ResolveDefaultTextureProcessorSettings(capability);
    }

    static TextureAssetProcessorSettings ResolveDefaultTextureProcessorSettings(PlatformAssetCookCapabilityDefinition capability) {
        if (capability == null) {
            throw new ArgumentNullException(nameof(capability));
        }

        if (!string.IsNullOrWhiteSpace(capability.DefaultSerializedPlatformSettings)) {
            return DeserializeTextureSettings(capability.DefaultSerializedPlatformSettings);
        }

        return new TextureAssetProcessorSettings {
            MaxResolution = 0,
            ColorFormatId = TextureAssetColorFormat.Rgba32.ToString(),
            AlphaPrecision = TextureAssetAlphaPrecision.A8,
            IndexingMethodId = string.Empty
        };
    }

    static TextureAssetProcessorSettings DeserializeTextureSettings(string serializedSettings) {
        if (string.IsNullOrWhiteSpace(serializedSettings)) {
            throw new ArgumentException("Serialized settings must be provided.", nameof(serializedSettings));
        }

        using JsonDocument document = JsonDocument.Parse(serializedSettings);
        JsonElement root = document.RootElement;
        int maxResolution = root.TryGetProperty("maxResolution", out JsonElement maxResolutionElement)
            ? maxResolutionElement.GetInt32()
            : 0;
        string colorFormatId = root.TryGetProperty("colorFormat", out JsonElement colorFormatElement)
            ? colorFormatElement.GetString() ?? TextureAssetColorFormat.Rgba32.ToString()
            : TextureAssetColorFormat.Rgba32.ToString();
        string alphaPrecisionName = root.TryGetProperty("alphaPrecision", out JsonElement alphaPrecisionElement)
            ? alphaPrecisionElement.GetString() ?? TextureAssetAlphaPrecision.A8.ToString()
            : TextureAssetAlphaPrecision.A8.ToString();
        string indexingMethodId = root.TryGetProperty("indexingMethod", out JsonElement indexingMethodElement)
            ? indexingMethodElement.GetString() ?? string.Empty
            : string.Empty;

        if (!Enum.TryParse(alphaPrecisionName, ignoreCase: true, out TextureAssetAlphaPrecision alphaPrecision)) {
            throw new InvalidOperationException($"Unsupported texture alpha precision '{alphaPrecisionName}'.");
        }

        return new TextureAssetProcessorSettings {
            MaxResolution = maxResolution,
            ColorFormatId = colorFormatId,
            AlphaPrecision = alphaPrecision,
            IndexingMethodId = indexingMethodId
        };
    }

    static string SerializeTextureSettings(TextureAssetProcessorSettings processorSettings) {
        if (processorSettings == null) {
            throw new ArgumentNullException(nameof(processorSettings));
        }

        string indexingMethodId = processorSettings.UsesIndexedColorFormat()
            ? processorSettings.ResolveIndexingMethod().ToString()
            : string.Empty;
        return JsonSerializer.Serialize(new Dictionary<string, object> {
            ["maxResolution"] = processorSettings.MaxResolution,
            ["colorFormat"] = processorSettings.ColorFormatId,
            ["alphaPrecision"] = processorSettings.AlphaPrecision.ToString(),
            ["indexingMethod"] = indexingMethodId
        });
    }

    static string ComputeStringHash(AssetFileHasher fileHasher, string value) {
        if (fileHasher == null) {
            throw new ArgumentNullException(nameof(fileHasher));
        } else if (value == null) {
            throw new ArgumentNullException(nameof(value));
        }

        using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(value));
        return fileHasher.ComputeHash(stream);
    }
}
