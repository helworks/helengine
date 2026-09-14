using helengine.platforms;

namespace helengine.editor {
    /// <summary>
    /// Decides which platforms the editor's build, platform and profile menus may offer for the
    /// open project. A platform is offerable only when the project declares it and the local engine
    /// installation actually carries its payload, so these two independent facts — project intent
    /// and local installation — are reconciled here rather than inside the dialog handlers.
    /// The editor session still owns the dialogs and the persisted active-platform choice.
    /// </summary>
    public sealed class EditorBuildMenuCoordinator {
        /// <summary>
        /// Loads the platforms available to the local engine installation.
        /// </summary>
        AvailablePlatformProviderResolver AvailablePlatformProviderResolver { get; }

        /// <summary>
        /// Exact engine version the open project requires; platform payloads are resolved against it.
        /// </summary>
        string RequiredEngineVersion { get; }

        /// <summary>
        /// Initializes one build-menu coordinator for the open project.
        /// </summary>
        /// <param name="availablePlatformProviderResolver">Resolver that loads locally available platforms.</param>
        /// <param name="requiredEngineVersion">Exact engine version declared by the project file.</param>
        public EditorBuildMenuCoordinator(AvailablePlatformProviderResolver availablePlatformProviderResolver, string requiredEngineVersion) {
            if (availablePlatformProviderResolver == null) {
                throw new ArgumentNullException(nameof(availablePlatformProviderResolver));
            }
            if (string.IsNullOrWhiteSpace(requiredEngineVersion)) {
                throw new ArgumentException("Required engine version must be provided.", nameof(requiredEngineVersion));
            }

            AvailablePlatformProviderResolver = availablePlatformProviderResolver;
            RequiredEngineVersion = requiredEngineVersion;
        }

        /// <summary>
        /// Resolves the platform id that should be shown first in one filtered dialog without persisting any replacement.
        /// </summary>
        /// <param name="visiblePlatformIds">Platforms currently visible in the dialog.</param>
        /// <param name="preferredPlatformId">Preferred platform id, typically the user-local active platform.</param>
        /// <returns>Visible platform id to show first.</returns>
        public static string ResolveVisiblePlatformId(IReadOnlyList<string> visiblePlatformIds, string preferredPlatformId) {
            if (visiblePlatformIds == null) {
                throw new ArgumentNullException(nameof(visiblePlatformIds));
            }
            if (visiblePlatformIds.Count < 1) {
                throw new InvalidOperationException("At least one visible platform is required.");
            }

            if (!string.IsNullOrWhiteSpace(preferredPlatformId)) {
                for (int index = 0; index < visiblePlatformIds.Count; index++) {
                    if (string.Equals(visiblePlatformIds[index], preferredPlatformId, StringComparison.OrdinalIgnoreCase)) {
                        return visiblePlatformIds[index];
                    }
                }
            }

            return visiblePlatformIds[0];
        }

        /// <summary>
        /// Returns true when the supplied platform exists in the current engine catalog and has an installed payload.
        /// </summary>
        /// <param name="platformId">Platform identifier to inspect.</param>
        /// <returns>True when the platform is installed for the current engine; otherwise false.</returns>
        public bool IsInstalledPlatform(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                return false;
            }

            IReadOnlyList<AvailablePlatformDescriptor> availablePlatforms = AvailablePlatformProviderResolver.LoadPlatforms(RequiredEngineVersion);
            for (int index = 0; index < availablePlatforms.Count; index++) {
                if (string.Equals(availablePlatforms[index].Id, platformId, StringComparison.OrdinalIgnoreCase)) {
                    return availablePlatforms[index].IsInstalled;
                }
            }

            return false;
        }

        /// <summary>
        /// Resolves the installed platform identifiers currently available for the active engine version.
        /// </summary>
        /// <returns>Alphabetically ordered installed platform identifiers.</returns>
        public IReadOnlyList<string> ResolveInstalledPlatformIds() {
            return AvailablePlatformProviderResolver
                .LoadPlatforms(RequiredEngineVersion)
                .Where(platform => platform.IsInstalled)
                .Select(platform => platform.Id)
                .OrderBy(platformId => platformId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        /// <summary>
        /// Resolves the platforms that are both project-enabled and currently installed on the local machine.
        /// </summary>
        /// <param name="supportedPlatforms">Platform identifiers declared by the project.</param>
        /// <returns>Alphabetically ordered visible platform identifiers.</returns>
        public IReadOnlyList<string> ResolveVisibleSupportedPlatforms(IReadOnlyList<string> supportedPlatforms) {
            if (supportedPlatforms == null) {
                throw new ArgumentNullException(nameof(supportedPlatforms));
            }

            IReadOnlyList<string> installedPlatformIds = ResolveInstalledPlatformIds();
            if (installedPlatformIds.Count < 1 || supportedPlatforms.Count < 1) {
                return Array.Empty<string>();
            }

            HashSet<string> installedPlatformIdSet = new HashSet<string>(installedPlatformIds, StringComparer.OrdinalIgnoreCase);
            List<string> visiblePlatformIds = new List<string>(supportedPlatforms.Count);
            for (int index = 0; index < supportedPlatforms.Count; index++) {
                string platformId = supportedPlatforms[index];
                if (installedPlatformIdSet.Contains(platformId)) {
                    visiblePlatformIds.Add(platformId);
                }
            }

            visiblePlatformIds.Sort(StringComparer.OrdinalIgnoreCase);
            return visiblePlatformIds;
        }

        /// <summary>
        /// Returns true when the supplied platform is both project-supported and installed for the current engine.
        /// </summary>
        /// <param name="supportedPlatforms">Platform identifiers declared by the project.</param>
        /// <param name="platformId">Platform identifier to validate.</param>
        /// <returns>True when the platform can be used as the current project platform.</returns>
        public bool CanUseProjectPlatform(IReadOnlyList<string> supportedPlatforms, string platformId) {
            if (supportedPlatforms == null) {
                throw new ArgumentNullException(nameof(supportedPlatforms));
            }
            if (string.IsNullOrWhiteSpace(platformId)) {
                return false;
            }

            for (int index = 0; index < supportedPlatforms.Count; index++) {
                if (string.Equals(supportedPlatforms[index], platformId, StringComparison.OrdinalIgnoreCase)) {
                    return IsInstalledPlatform(platformId);
                }
            }

            return false;
        }
    }
}
