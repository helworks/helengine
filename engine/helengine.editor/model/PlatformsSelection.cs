namespace helengine.editor {
    /// <summary>
    /// Stores one confirmed project-platform selection and its explicit active platform.
    /// </summary>
    public sealed class PlatformsSelection {
        /// <summary>
        /// Gets the supported platform identifiers selected by the user in stable row order.
        /// </summary>
        public IReadOnlyList<string> SupportedPlatformIds { get; }

        /// <summary>
        /// Gets the explicit active platform identifier selected by the user.
        /// </summary>
        public string ActivePlatformId { get; }

        /// <summary>
        /// Initializes one confirmed project-platform selection.
        /// </summary>
        /// <param name="supportedPlatformIds">Supported platform identifiers selected by the user.</param>
        /// <param name="activePlatformId">Explicit active platform identifier selected by the user.</param>
        public PlatformsSelection(IReadOnlyList<string> supportedPlatformIds, string activePlatformId) {
            if (supportedPlatformIds == null) {
                throw new ArgumentNullException(nameof(supportedPlatformIds));
            }
            if (string.IsNullOrWhiteSpace(activePlatformId)) {
                throw new ArgumentException("Active platform id must be provided.", nameof(activePlatformId));
            }

            List<string> copiedPlatformIds = new List<string>(supportedPlatformIds.Count);
            for (int index = 0; index < supportedPlatformIds.Count; index++) {
                copiedPlatformIds.Add(supportedPlatformIds[index]);
            }

            SupportedPlatformIds = copiedPlatformIds;
            ActivePlatformId = activePlatformId;
        }
    }
}
