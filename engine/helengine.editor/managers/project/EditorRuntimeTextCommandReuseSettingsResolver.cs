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
            return ResolveBudget(ReadIfExists(Path.Combine(projectRootPath, "settings", "build_config.json")),
                ReadIfExists(Path.Combine(projectRootPath, "user_settings", "build_config.json")), platformId);
        }

        /// <summary>Populates only omitted user fields before UI binding or persistence, preventing a legacy default zero from masking a project budget.</summary>
        public static void ApplyProjectDefaults(string projectRootPath, EditorBuildConfigDocument document) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) { throw new ArgumentException("Project root is required.", nameof(projectRootPath)); }
            ApplyProjectDefaults(document,
                ReadIfExists(Path.Combine(projectRootPath, "settings", "build_config.json")),
                ReadIfExists(Path.Combine(projectRootPath, "user_settings", "build_config.json")));
        }

        /// <summary>
        /// Populates only omitted user fields from the given text of the two build configuration files instead of the files
        /// on disk, so a caller holding a snapshot (the background Play cook) never reads the live files.
        /// </summary>
        /// <param name="document">Composed build configuration whose budgets are filled.</param>
        /// <param name="projectJson">Text of `settings/build_config.json`, or null when it does not exist.</param>
        /// <param name="localJson">Text of `user_settings/build_config.json`, or null when it does not exist.</param>
        public static void ApplyProjectDefaults(EditorBuildConfigDocument document, string projectJson, string localJson) {
            if (document == null) { throw new ArgumentNullException(nameof(document)); }
            if (document.Platforms == null) { return; }
            foreach (EditorBuildPlatformConfigDocument platform in document.Platforms) {
                if (platform == null || string.IsNullOrWhiteSpace(platform.PlatformId)) { continue; }
                if (!TryReadBudget(localJson, platform.PlatformId, out _)) {
                    platform.Render2DTextCommandReuseMemoryBudgetBytes = ResolveBudget(projectJson, localJson, platform.PlatformId);
                }
                ValidateBudget(platform.Render2DTextCommandReuseMemoryBudgetBytes);
            }
        }

        /// <summary>Uses an explicitly serialized user value, otherwise the project platform value, otherwise zero, read from file text.</summary>
        /// <param name="projectJson">Text of the project file, or null.</param>
        /// <param name="localJson">Text of the local file, or null.</param>
        /// <param name="platformId">Platform whose budget is resolved.</param>
        /// <returns>Resolved budget in bytes.</returns>
        static int ResolveBudget(string projectJson, string localJson, string platformId) {
            if (TryReadBudget(localJson, platformId, out int localBudget)) { return localBudget; }
            if (TryReadBudget(projectJson, platformId, out int projectBudget)) { return projectBudget; }
            return 0;
        }

        /// <summary>Reads one configuration file, or returns null when it does not exist; read failures propagate.</summary>
        /// <param name="path">File to read.</param>
        /// <returns>File text, or null.</returns>
        static string ReadIfExists(string path) {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        /// <summary>Checks field presence separately from its integer value, rejecting malformed budgets and duplicate platform entries without swallowing errors.</summary>
        /// <param name="json">Text of one build configuration file, or null when the file does not exist.</param>
        /// <param name="platformId">Platform whose budget is read.</param>
        /// <param name="budget">Receives the budget when one is serialized.</param>
        /// <returns>True when the text serializes a budget for the platform.</returns>
        static bool TryReadBudget(string json, string platformId, out int budget) {
            budget = 0;
            if (json == null) { return false; }
            using JsonDocument document = JsonDocument.Parse(json);
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
