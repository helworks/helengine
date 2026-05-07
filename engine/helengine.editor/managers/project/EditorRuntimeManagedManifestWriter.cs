using System.Text;
using helengine.baseplatform.Manifest;

namespace helengine.editor {
    /// <summary>
    /// Writes managed runtime manifest files into the packaged build output root.
    /// </summary>
    public sealed class EditorRuntimeManagedManifestWriter {
        /// <summary>
        /// Writes managed runtime startup and scene catalog metadata.
        /// </summary>
        /// <param name="runtimeRootPath">Packaged runtime root that receives the JSON files.</param>
        /// <param name="cookedManifest">Cooked build manifest that owns the final scene layout.</param>
        /// <param name="selectedStorageProfileId">Stable runtime storage profile id.</param>
        public void Write(string runtimeRootPath, PlatformBuildManifest cookedManifest, string selectedStorageProfileId) {
            if (string.IsNullOrWhiteSpace(runtimeRootPath)) {
                throw new ArgumentException("Runtime root path must be provided.", nameof(runtimeRootPath));
            }
            if (cookedManifest == null) {
                throw new ArgumentNullException(nameof(cookedManifest));
            }
            if (string.IsNullOrWhiteSpace(selectedStorageProfileId)) {
                throw new ArgumentException("Selected storage profile id must be provided.", nameof(selectedStorageProfileId));
            }

            Directory.CreateDirectory(runtimeRootPath);
            File.WriteAllText(Path.Combine(runtimeRootPath, "runtime-startup.json"), BuildStartupManifestJson(cookedManifest, selectedStorageProfileId));
            File.WriteAllText(Path.Combine(runtimeRootPath, "runtime-scene-catalog.json"), BuildSceneCatalogJson(cookedManifest));
        }

        /// <summary>
        /// Builds the managed runtime startup manifest JSON.
        /// </summary>
        /// <param name="cookedManifest">Cooked manifest that contains the startup scene id.</param>
        /// <param name="selectedStorageProfileId">Stable runtime storage profile id.</param>
        /// <returns>Managed runtime startup manifest JSON.</returns>
        static string BuildStartupManifestJson(PlatformBuildManifest cookedManifest, string selectedStorageProfileId) {
            if (string.IsNullOrWhiteSpace(cookedManifest.StartupSceneId)) {
                throw new InvalidOperationException("Cooked manifest did not define a startup scene.");
            }

            return
                "{\n"
                + "  \"StartupSceneId\": \"" + EscapeJson(cookedManifest.StartupSceneId) + "\",\n"
                + "  \"StorageProfileId\": {\n"
                + "    \"Value\": \"" + EscapeJson(selectedStorageProfileId) + "\"\n"
                + "  }\n"
                + "}\n";
        }

        /// <summary>
        /// Builds the managed runtime scene catalog JSON.
        /// </summary>
        /// <param name="cookedManifest">Cooked manifest that contains the final built scenes.</param>
        /// <returns>Managed runtime scene catalog JSON.</returns>
        static string BuildSceneCatalogJson(PlatformBuildManifest cookedManifest) {
            if (cookedManifest.Scenes == null) {
                throw new InvalidOperationException("Cooked manifest did not define any built scenes.");
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"Entries\": [");
            for (int index = 0; index < cookedManifest.Scenes.Length; index++) {
                PlatformBuildScene scene = cookedManifest.Scenes[index];
                string cookedRelativePath = ResolveCookedRelativePath(scene);
                builder.AppendLine("    {");
                builder.AppendLine("      \"SceneId\": \"" + EscapeJson(scene.SceneId) + "\",");
                builder.Append("      \"CookedRelativePath\": \"" + EscapeJson(cookedRelativePath) + "\"");
                builder.AppendLine();
                builder.Append("    }");
                if (index < cookedManifest.Scenes.Length - 1) {
                    builder.Append(",");
                }

                builder.AppendLine();
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");
            return builder.ToString();
        }

        /// <summary>
        /// Resolves the cooked runtime path for one built scene entry.
        /// </summary>
        /// <param name="scene">Built scene entry to inspect.</param>
        /// <returns>Cooked runtime-relative scene payload path.</returns>
        static string ResolveCookedRelativePath(PlatformBuildScene scene) {
            if (scene == null) {
                throw new ArgumentNullException(nameof(scene));
            }
            if (scene.ResolvedMetadata == null) {
                throw new InvalidOperationException($"Built scene '{scene.SceneId}' did not define any resolved metadata.");
            }

            for (int index = 0; index < scene.ResolvedMetadata.Length; index++) {
                KeyValuePair<string, string> metadata = scene.ResolvedMetadata[index];
                if (string.Equals(metadata.Key, PlatformBuildSceneMetadataKeys.CookedRelativePath, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(metadata.Value)) {
                    return metadata.Value.Replace('\\', '/');
                }
            }

            throw new InvalidOperationException($"Built scene '{scene.SceneId}' did not define a cooked relative path.");
        }

        /// <summary>
        /// Escapes one string for JSON output.
        /// </summary>
        /// <param name="value">String value to escape.</param>
        /// <returns>Escaped JSON string contents.</returns>
        static string EscapeJson(string value) {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);
        }
    }
}
