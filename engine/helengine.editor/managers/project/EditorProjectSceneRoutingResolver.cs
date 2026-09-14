using helengine.projectfile;

namespace helengine.editor {
    /// <summary>Resolves project-owned scene routing without platform-name or scene-name inference.</summary>
    public sealed class EditorProjectSceneRoutingResolver {
        /// <summary>Resolves one platform routing record against the selected scene catalog.</summary>
        /// <param name="project">Project document containing optional explicit routing.</param>
        /// <param name="platformId">Target platform identifier.</param>
        /// <param name="selectedSceneIds">Scene identifiers selected for the build.</param>
        /// <returns>Validated routing record with identity aliases when no project record exists.</returns>
        public ProjectPlatformSceneRoutingDocument Resolve(ProjectFileDocument project, string platformId, IReadOnlyList<string> selectedSceneIds) {
            if (project == null) {
                throw new ArgumentNullException(nameof(project));
            }
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }
            if (selectedSceneIds == null) {
                throw new ArgumentNullException(nameof(selectedSceneIds));
            }

            HashSet<string> selected = new(selectedSceneIds.Where(sceneId => !string.IsNullOrWhiteSpace(sceneId)), StringComparer.Ordinal);
            ProjectPlatformSceneRoutingDocument configured = null;
            if (project.SceneRouting?.Platforms != null) {
                project.SceneRouting.Platforms.TryGetValue(platformId, out configured);
            }
            if (configured == null) {
                return new ProjectPlatformSceneRoutingDocument {
                    BootSceneId = ResolveFirstStartupScene(selectedSceneIds),
                    SceneAliases = new Dictionary<string, string>(StringComparer.Ordinal)
                };
            }

            if (string.IsNullOrWhiteSpace(configured.BootSceneId) || !selected.Contains(configured.BootSceneId)) {
                throw new InvalidOperationException($"Project scene routing for platform '{platformId}' selects boot scene '{configured.BootSceneId}', which is not in the selected scene catalog.");
            }
            Dictionary<string, string> aliases = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> alias in configured.SceneAliases ?? new Dictionary<string, string>()) {
                if (string.IsNullOrWhiteSpace(alias.Key) || string.IsNullOrWhiteSpace(alias.Value) || !selected.Contains(alias.Value)) {
                    throw new InvalidOperationException($"Project scene routing alias '{alias.Key}' -> '{alias.Value}' for platform '{platformId}' targets a scene outside the selected scene catalog.");
                }
                aliases.Add(alias.Key, alias.Value);
            }
            return new ProjectPlatformSceneRoutingDocument { BootSceneId = configured.BootSceneId, SceneAliases = aliases };
        }

        static string ResolveFirstStartupScene(IReadOnlyList<string> selectedSceneIds) {
            for (int index = 0; index < selectedSceneIds.Count; index++) {
                string sceneId = selectedSceneIds[index];
                if (!string.IsNullOrWhiteSpace(sceneId) && !string.Equals(sceneId, EngineSceneIdentifiers.GeneratedBootSceneId, StringComparison.OrdinalIgnoreCase)) {
                    return sceneId;
                }
            }
            throw new InvalidOperationException("Generated boot scene requires at least one selected startup scene besides the generated boot scene.");
        }
    }
}