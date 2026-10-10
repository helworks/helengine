using System.Text.Json;
using helengine.baseplatform.Manifest;

namespace helengine.editor {
    /// <summary>Resolves strict user and per-platform loading settings before writing the final native startup manifest.</summary>
    public static class EditorRuntimeLoadingSettingsResolver {
        /// <summary>Reads the existing local build configuration without rewriting it or swallowing malformed runtime settings.</summary>
        public static EditorRuntimeLoadingSettings Load(string projectRootPath, string platformId) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) { throw new ArgumentException("Project root is required.", nameof(projectRootPath)); }
            string path = Path.Combine(projectRootPath, "user_settings", "build_config.json");
            EditorBuildConfigDocument document;
            if (File.Exists(path)) {
                JsonSerializerOptions options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                options.Converters.Add(new SceneAssetReferenceJsonConverter());
                document = JsonSerializer.Deserialize<EditorBuildConfigDocument>(File.ReadAllText(path), options)
                    ?? throw new InvalidOperationException("User build settings cannot be null.");
            } else {
                document = new EditorBuildConfigDocument();
            }
            return Resolve(document, platformId);
        }

        /// <summary>Applies the selected platform override while leaving other platform settings untouched.</summary>
        public static EditorRuntimeLoadingSettings Resolve(EditorBuildConfigDocument document, string platformId) {
            if (document == null) { throw new ArgumentNullException(nameof(document)); }
            if (string.IsNullOrWhiteSpace(platformId)) { throw new ArgumentException("Platform id is required.", nameof(platformId)); }
            Validate(document);
            double budget = document.SceneLoadingBudgetMilliseconds;
            string screen = document.LoadingScreenSceneId;
            bool found = false;
            if (document.Platforms != null) {
                foreach (EditorBuildPlatformConfigDocument platform in document.Platforms) {
                    if (platform == null || !string.Equals(platform.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)) { continue; }
                    if (found) { throw new InvalidOperationException("Duplicate runtime settings exist for platform '" + platformId + "'."); }
                    found = true;
                    if (platform.SceneLoadingBudgetMilliseconds != 0d) { budget = platform.SceneLoadingBudgetMilliseconds; }
                    if (platform.OverridesLoadingScreenSceneId) { screen = platform.LoadingScreenSceneId; }
                }
            }
            return new EditorRuntimeLoadingSettings(budget, screen);
        }

        /// <summary>Rejects malformed values before persistence or build, reserving zero only for an unset platform override.</summary>
        public static void Validate(EditorBuildConfigDocument document) {
            if (document == null) { throw new ArgumentNullException(nameof(document)); }
            ValidateBudget(document.SceneLoadingBudgetMilliseconds, false);
            if (document.Platforms != null) {
                foreach (EditorBuildPlatformConfigDocument platform in document.Platforms) {
                    if (platform != null) { ValidateBudget(platform.SceneLoadingBudgetMilliseconds, true); }
                }
            }
        }

        /// <summary>Validates a finite positive effective budget, optionally allowing the zero inheritance sentinel.</summary>
        public static void ValidateBudget(double milliseconds, bool allowsInheritance) {
            if (double.IsNaN(milliseconds) || double.IsInfinity(milliseconds) || milliseconds < 0d
                || (!allowsInheritance && milliseconds == 0d)) {
                throw new InvalidOperationException("Scene loading budget must be finite and greater than zero; only a platform override may use zero to inherit.");
            }
        }

        /// <summary>Stamps settings onto the final cooked manifest and ensures the optional loading screen was actually packaged.</summary>
        public static void Apply(PlatformBuildManifest manifest, EditorRuntimeLoadingSettings settings) {
            Apply(manifest, settings, string.Empty);
        }

        /// <summary>Resolves a packaged runtime ID or an unambiguous assets-relative authored scene path before stamping the final manifest.</summary>
        public static void Apply(PlatformBuildManifest manifest, EditorRuntimeLoadingSettings settings, string projectRootPath) {
            if (manifest == null) { throw new ArgumentNullException(nameof(manifest)); }
            if (settings == null) { throw new ArgumentNullException(nameof(settings)); }
            string screenId = ResolvePackagedLoadingScreenId(manifest, settings.LoadingScreenSceneId, projectRootPath);
            manifest.SceneLoadingBudgetMilliseconds = settings.SceneLoadingBudgetMilliseconds;
            manifest.LoadingScreenSceneId = screenId;
        }

        /// <summary>Prefers the exact runtime ID; authored aliases must match the actual project catalog path and a unique packaged runtime entry.</summary>
        static string ResolvePackagedLoadingScreenId(PlatformBuildManifest manifest, string configuredId, string projectRootPath) {
            if (configuredId.Length == 0) { return string.Empty; }
            PlatformBuildScene exactScene = FindPackagedScene(manifest, configuredId);
            if (exactScene != null) { return exactScene.SceneId; }
            string authoredPath = configuredId.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(projectRootPath) && !Path.IsPathRooted(authoredPath)
                && authoredPath.EndsWith(".helen", StringComparison.OrdinalIgnoreCase)) {
                string[] segments = authoredPath.Split('/');
                foreach (string segment in segments) {
                    if (segment.Length == 0 || segment == "." || segment == "..") {
                        throw new InvalidOperationException("Loading-screen authored path must be relative to assets without traversal segments.");
                    }
                }
                EditorProjectSceneCatalogService catalog = new(projectRootPath);
                string candidateId = catalog.ResolveSceneId(Path.Combine(projectRootPath, "assets", authoredPath));
                string canonicalPath = catalog.ResolveScenePath(candidateId);
                if (string.Equals(canonicalPath, authoredPath, StringComparison.Ordinal)) {
                    PlatformBuildScene packagedScene = FindPackagedScene(manifest, candidateId);
                    if (packagedScene != null) { return packagedScene.SceneId; }
                }
            }
            throw new InvalidOperationException("Loading-screen scene '" + configuredId + "' is absent from the packaged scene catalog or does not match its authored path.");
        }

        /// <summary>Returns one exact runtime scene or rejects duplicate packaged IDs instead of selecting an arbitrary entry.</summary>
        static PlatformBuildScene FindPackagedScene(PlatformBuildManifest manifest, string sceneId) {
            PlatformBuildScene match = null;
            foreach (PlatformBuildScene scene in manifest.Scenes) {
                if (!string.Equals(scene.SceneId, sceneId, StringComparison.Ordinal)) { continue; }
                if (match != null) { throw new InvalidOperationException("Loading-screen runtime scene id '" + sceneId + "' is ambiguous in the packaged scene catalog."); }
                match = scene;
            }
            return match;
        }
    }
}
