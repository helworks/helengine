namespace helengine.editor {
    /// <summary>Reads optional GPU policy from either established model sidecar format without mutating import metadata.</summary>
    public static class ModelGpuReductionAssetSettingsReader {
        /// <summary>Returns a detached effective platform/environment policy or null when the model inherits build defaults.</summary>
        public static ModelGpuReductionSettings Read(string sourcePath, string platformId, string environmentId) {
            string sidecar = sourcePath + ".hasset";
            if (!File.Exists(sidecar)) return null;
            using FileStream stream = File.OpenRead(sidecar);
            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
            stream.Position = 0;
            if (header.ValueKind == (ushort)AssetImportSettingsBinaryValueKind.ModelAssetImportSettings) {
                ModelAssetImportSettings settings = ModelAssetImportSettingsBinarySerializer.Deserialize(stream);
                ModelGpuReductionSettings policy = null;
                if (settings.Processor.Platforms.TryGetValue(platformId, out ModelAssetProcessorSettings platform)) policy = platform.GpuReduction;
                if (!string.IsNullOrWhiteSpace(environmentId) && settings.Processor.Environments.TryGetValue(platformId, out Dictionary<string, ModelAssetProcessorSettings> environments)
                    && environments.TryGetValue(environmentId, out ModelAssetProcessorSettings environment) && environment.GpuReduction != null) policy = environment.GpuReduction;
                return policy?.Clone();
            }
            AssetImportSettings sectioned = SectionedAssetImportSettingsBinarySerializer.Deserialize(stream);
            AssetPlatformProcessorSettings effective = AssetProcessorSettingsScopeResolver.Resolve(sectioned.Processor, new EditorOverrideScope(platformId, environmentId));
            return effective.Sections.TryGetValue(ModelGpuReductionSectionDefinition.SectionIdValue, out AssetPlatformSettingsSection section) ? ((ModelGpuReductionSettings)section.Settings).Clone() : null;
        }
    }
}
