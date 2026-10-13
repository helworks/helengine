namespace helengine.baseplatform.Definitions {

    /// <summary>
    /// Describes whether one platform supports editor Play through the desktop C# player and how the player finds
    /// the platform adapter and the cooked content.
    /// </summary>
    public sealed class PlatformPlayerCapability {
        /// <summary>
        /// Initializes one platform Play capability description.
        /// </summary>
        /// <param name="supportsPlay">Whether the platform supports editor Play.</param>
        /// <param name="adapterTypeName">Assembly-qualified or full type name of the platform's <c>IPlatformPlayerAdapter</c> implementation.</param>
        /// <param name="contentRootRelativePath">Cooked content root, relative to the builder working root (PS2: <c>ps2-staging</c>).</param>
        /// <exception cref="ArgumentException">Thrown when Play is supported but the adapter type name or content root is blank.</exception>
        public PlatformPlayerCapability(bool supportsPlay, string adapterTypeName, string contentRootRelativePath) {
            if (supportsPlay && string.IsNullOrWhiteSpace(adapterTypeName)) {
                throw new ArgumentException("A platform that supports Play must name its player adapter type.", nameof(adapterTypeName));
            } else if (supportsPlay && string.IsNullOrWhiteSpace(contentRootRelativePath)) {
                throw new ArgumentException("A platform that supports Play must name its cooked content root.", nameof(contentRootRelativePath));
            }

            SupportsPlay = supportsPlay;
            AdapterTypeName = adapterTypeName;
            ContentRootRelativePath = contentRootRelativePath;
        }

        /// <summary>
        /// Gets whether the platform supports editor Play.
        /// </summary>
        public bool SupportsPlay { get; }

        /// <summary>
        /// Gets the type name of the platform's player adapter; <c>none</c> when Play is unsupported.
        /// </summary>
        public string AdapterTypeName { get; }

        /// <summary>
        /// Gets the cooked content root relative to the builder working root; <c>none</c> when Play is unsupported.
        /// </summary>
        public string ContentRootRelativePath { get; }

        /// <summary>
        /// Creates the capability for platforms that do not opt in to editor Play.
        /// </summary>
        /// <returns>Unsupported Play capability.</returns>
        public static PlatformPlayerCapability CreateUnsupported() {
            return new PlatformPlayerCapability(false, "none", "none");
        }
    }
}
