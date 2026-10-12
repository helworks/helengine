using System.Globalization;

namespace helengine.editor {
    /// <summary>Resolves optional builder-exposed render settings without touching base model processing.</summary>
    public static class ModelGpuReductionProfileResolver {
        /// <summary>Reads persisted per-platform options; missing optional fields migrate to disabled behavior.</summary>
        public static ModelGpuReductionProfile Resolve(string projectRootPath, string platformId) {
            EditorProfileSettingsDocument document = new EditorProfileSettingsService(projectRootPath).TryLoadExisting();
            EditorPlatformProfileSettingsDocument platform = document?.Platforms.FirstOrDefault(item => string.Equals(item.PlatformId, platformId, StringComparison.OrdinalIgnoreCase));
            IReadOnlyDictionary<string, string> values = platform?.Build?.SelectedOptionValues;
            ModelGpuReductionProfile profile = new ModelGpuReductionProfile();
            if (values == null) return profile;
            if (values.TryGetValue("gpu-model-reduction", out string enabled)) profile.Reduction.Enabled = bool.Parse(enabled);
            profile.Reduction.MaximumVertices = ReadLimit(values, "gpu-model-maximum-vertices", profile.Reduction.MaximumVertices);
            profile.Reduction.MaximumIndices = ReadLimit(values, "gpu-model-maximum-indices", profile.Reduction.MaximumIndices);
            profile.SphereRings = ReadLimit(values, "gpu-sphere-rings", profile.SphereRings);
            profile.SphereSectors = ReadLimit(values, "gpu-sphere-sectors", profile.SphereSectors);
            profile.Validate();
            ValidatePlatformLimits(profile, platformId);
            return profile;
        }
        /// <summary>Rejects an enabled PS1 profile that asks the fixed renderer to exceed its hardware pool limits.</summary>
        public static void ValidatePlatformLimits(ModelGpuReductionProfile profile, string platformId) {
            profile.Validate();
            if (profile.Reduction.Enabled && string.Equals(platformId, "ps1", StringComparison.OrdinalIgnoreCase)
                && (profile.Reduction.MaximumVertices > 256 || profile.Reduction.MaximumIndices > 768)) {
                throw new InvalidOperationException("PS1 GPU model reduction caps cannot exceed 256 vertices/768 indices.");
            }
        }
        /// <summary>Reads one explicitly supplied integer; invalid values fail instead of using defaults.</summary>
        static int ReadLimit(IReadOnlyDictionary<string, string> values, string key, int inherited) {
            return values.TryGetValue(key, out string value) ? int.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture) : inherited;
        }
    }
}
