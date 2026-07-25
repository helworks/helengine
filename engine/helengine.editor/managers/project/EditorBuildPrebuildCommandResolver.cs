namespace helengine.editor {
    /// <summary>
    /// Resolves the ordered editor authoring commands required before one selected platform build profile cooks runtime content.
    /// </summary>
    public sealed class EditorBuildPrebuildCommandResolver {
        /// <summary>
        /// Resolves editor prebuild command identifiers for one platform configuration and selected build profile.
        /// </summary>
        /// <param name="platformConfig">Persisted platform build configuration that owns profile command declarations.</param>
        /// <param name="buildProfileId">Explicit build profile id, or an empty value to use the persisted selection.</param>
        /// <returns>Ordered command identifiers that must complete before runtime-only cook and package execution.</returns>
        public IReadOnlyList<string> Resolve(EditorBuildPlatformConfigDocument platformConfig, string buildProfileId) {
            if (platformConfig == null) {
                throw new ArgumentNullException(nameof(platformConfig));
            }

            string resolvedBuildProfileId = string.IsNullOrWhiteSpace(buildProfileId)
                ? platformConfig.SelectedBuildProfileId
                : buildProfileId;
            if (string.IsNullOrWhiteSpace(resolvedBuildProfileId)) {
                return [];
            }

            Dictionary<string, List<string>> commandIdsByProfileId = platformConfig.EditorPrebuildCommandIdsByBuildProfileId;
            if (commandIdsByProfileId == null
                || !commandIdsByProfileId.TryGetValue(resolvedBuildProfileId, out List<string> commandIds)) {
                return [];
            }
            if (commandIds == null) {
                throw new InvalidOperationException($"Build profile '{resolvedBuildProfileId}' declares an invalid null editor prebuild command list.");
            }

            for (int index = 0; index < commandIds.Count; index++) {
                if (string.IsNullOrWhiteSpace(commandIds[index])) {
                    throw new InvalidOperationException($"Build profile '{resolvedBuildProfileId}' declares a blank editor prebuild command id at index {index}.");
                }
            }

            return [.. commandIds];
        }
    }
}
