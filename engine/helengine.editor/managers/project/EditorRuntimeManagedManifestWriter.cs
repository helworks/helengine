using System.Text;
using helengine.baseplatform.Manifest;

namespace helengine.editor {
    /// <summary>
    /// Writes the managed runtime scene catalog into the packaged build output root.
    /// </summary>
    public sealed class EditorRuntimeManagedManifestWriter {
        /// <summary>
        /// Writes the managed runtime scene catalog metadata.
        /// </summary>
        /// <param name="runtimeRootPath">Packaged runtime root that receives the JSON files.</param>
        /// <param name="cookedManifest">Cooked build manifest that owns the final scene layout.</param>
        public void Write(string runtimeRootPath, PlatformBuildManifest cookedManifest) {
            if (string.IsNullOrWhiteSpace(runtimeRootPath)) {
                throw new ArgumentException("Runtime root path must be provided.", nameof(runtimeRootPath));
            }
            if (cookedManifest == null) {
                throw new ArgumentNullException(nameof(cookedManifest));
            }

            Directory.CreateDirectory(runtimeRootPath);
            File.WriteAllText(Path.Combine(runtimeRootPath, "runtime-scene-catalog.json"), BuildSceneCatalogJson(cookedManifest));
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
