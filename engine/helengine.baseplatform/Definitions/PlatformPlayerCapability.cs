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
        /// <param name="targetFrameRate">
        /// Frames per second the desktop player paces its frame loop to, matching the frame rate the platform's native
        /// host runs at (PS2: 60). Must be positive and finite when Play is supported.
        /// </param>
        /// <exception cref="ArgumentException">Thrown when Play is supported but the adapter type name or content root is blank.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when Play is supported but the target frame rate is not a positive, finite number.</exception>
        public PlatformPlayerCapability(bool supportsPlay, string adapterTypeName, string contentRootRelativePath, double targetFrameRate) {
            if (supportsPlay && string.IsNullOrWhiteSpace(adapterTypeName)) {
                throw new ArgumentException("A platform that supports Play must name its player adapter type.", nameof(adapterTypeName));
            } else if (supportsPlay && string.IsNullOrWhiteSpace(contentRootRelativePath)) {
                throw new ArgumentException("A platform that supports Play must name its cooked content root.", nameof(contentRootRelativePath));
            } else if (supportsPlay && !(targetFrameRate > 0d && double.IsFinite(targetFrameRate))) {
                throw new ArgumentOutOfRangeException(
                    nameof(targetFrameRate),
                    targetFrameRate,
                    "A platform that supports Play must declare a positive, finite player target frame rate.");
            }

            SupportsPlay = supportsPlay;
            AdapterTypeName = adapterTypeName;
            ContentRootRelativePath = contentRootRelativePath;
            TargetFrameRate = targetFrameRate;
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
        /// Gets the frames per second the desktop player paces its frame loop to, so Play runs at the platform's own
        /// frame rate instead of the editor monitor's refresh rate; <c>0</c> when Play is unsupported.
        /// </summary>
        public double TargetFrameRate { get; }

        /// <summary>
        /// Creates the capability for platforms that do not opt in to editor Play. The adapter and content root are
        /// <c>none</c> and the target frame rate is <c>0</c>, because no player is ever launched for such a platform.
        /// </summary>
        /// <returns>Unsupported Play capability.</returns>
        public static PlatformPlayerCapability CreateUnsupported() {
            return new PlatformPlayerCapability(false, "none", "none", 0d);
        }
    }
}
