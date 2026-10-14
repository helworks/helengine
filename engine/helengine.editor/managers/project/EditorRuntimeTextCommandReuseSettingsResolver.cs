using System.Text.Json;

namespace helengine.editor {
        /// <summary>Resolves the bounded platform text-command metadata budget while preserving explicit user zero as disabled.</summary>
    public static class EditorRuntimeTextCommandReuseSettingsResolver {
        /// <summary>Identifies the serialized per-platform byte budget in both project and user configuration.</summary>
        const string BudgetFieldName = "render2DTextCommandReuseMemoryBudgetBytes";

        /// <summary>Rejects negative budgets; zero is a valid disabled value rather than an inheritance sentinel.</summary>
        public static void ValidateBudget(int bytes) {
            if (bytes < 0) { throw new InvalidOperationException("Text-command reuse metadata budget must be nonnegative; zero disables reuse."); }
        }

        /// <summary>Uses an explicitly serialized user value, otherwise the project platform value, otherwise the backward-compatible disabled default.</summary>
        public static int LoadBudget(string projectRootPath, string platformId) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) { throw new ArgumentException("Project root is required.", nameof(projectRootPath)); }
            if (string.IsNullOrWhiteSpace(platformId)) { throw new ArgumentException("Platform id is required.", nameof(platformId)); }
            if (TryReadBudget(Path.Combine(projectRootPath, "user_settings", "build_config.json"), platformId, out int localBudget)) { return localBudget; }
            if (TryReadBudget(Path.Combine(projectRootPath, "settings", "build_config.json"), platformId, out int projectBudget)) { return projectBudget; }
            return 0;
        }

        /// <summary>Populates only omitted user fields before UI binding or persistence, preventing a legacy default zero from masking a project budget.</summary>
        public static void ApplyProjectDefaults(string projectRootPath, EditorBuildConfigDocument document) {
            if (document == null) { throw new ArgumentNullException(nameof(document)); }
            if (document.Platforms == null) { return; }
            foreach (EditorBuildPlatformConfigDocument platform in document.Platforms) {
                if (platform == null || string.IsNullOrWhiteSpace(platform.PlatformId)) { continue; }
                if (!TryReadBudget(Path.Combine(projectRootPath, "user_settings", "build_config.json"), platform.PlatformId, out _)) {
                    platform.Render2DTextCommandReuseMemoryBudgetBytes = LoadBudget(projectRootPath, platform.PlatformId);
                }
                ValidateBudget(platform.Render2DTextCommandReuseMemoryBudgetBytes);
            }
        }

        /// <summary>Checks field presence separately from its integer value, rejecting malformed budgets and duplicate platform entries without swallowing errors.</summary>
        static bool TryReadBudget(string path, string platformId, out int budget) {
            budget = 0;
            if (!File.Exists(path)) { return false; }
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("platforms", out JsonElement platforms) || platforms.ValueKind == JsonValueKind.Null) { return false; }
            bool matched = false;
            bool hasBudget = false;
            foreach (JsonElement platform in platforms.EnumerateArray()) {
                if (platform.ValueKind == JsonValueKind.Null) { continue; }
                bool hasValue = platform.TryGetProperty(BudgetFieldName, out JsonElement value);
                int parsed = hasValue ? value.GetInt32() : 0;
                ValidateBudget(parsed);
                if (!platform.TryGetProperty("platformId", out JsonElement id)
                    || !string.Equals(id.GetString(), platformId, StringComparison.OrdinalIgnoreCase)) { continue; }
                if (matched) { throw new InvalidOperationException("Duplicate text-command reuse settings exist for platform '" + platformId + "'."); }
                matched = true;
                hasBudget = hasValue;
                budget = parsed;
            }
            return hasBudget;
        }
    }
}
