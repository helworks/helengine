using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// One entry of the viewport Play platform selector: an installed, project-supported platform whose builder declares
    /// Play support, together with everything Play needs to launch it.
    /// </summary>
    public sealed class EditorPlayPlatformOption {
        /// <summary>
        /// Creates one Play platform option from a builder the platform catalog already loaded.
        /// </summary>
        /// <param name="loadedBuilder">Loaded platform builder whose definition declares Play support.</param>
        /// <exception cref="ArgumentNullException">Thrown when the builder is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the builder does not support Play or its catalog
        /// entry has no display name or builder assembly path.</exception>
        public EditorPlayPlatformOption(EditorLoadedPlatformBuilder loadedBuilder) {
            if (loadedBuilder == null) {
                throw new ArgumentNullException(nameof(loadedBuilder));
            }

            PlatformPlayerCapability capability = loadedBuilder.Definition.PlayerCapability;
            if (capability == null || !capability.SupportsPlay) {
                throw new InvalidOperationException($"Platform '{loadedBuilder.PlatformDescriptor.Id}' does not support Play.");
            } else if (string.IsNullOrWhiteSpace(loadedBuilder.PlatformDescriptor.DisplayName)) {
                throw new InvalidOperationException($"Platform '{loadedBuilder.PlatformDescriptor.Id}' has no display name in the platform catalog.");
            } else if (string.IsNullOrWhiteSpace(loadedBuilder.PlatformDescriptor.BuilderAssemblyPath)) {
                throw new InvalidOperationException($"Platform '{loadedBuilder.PlatformDescriptor.Id}' has no builder assembly path in the platform catalog.");
            }

            PlatformId = loadedBuilder.PlatformDescriptor.Id;
            DisplayName = loadedBuilder.PlatformDescriptor.DisplayName;
            BuilderAssemblyPath = loadedBuilder.PlatformDescriptor.BuilderAssemblyPath;
            PlayerCapability = capability;
            VideoModeCapability = loadedBuilder.Definition.VideoModeCapability;
        }

        /// <summary>
        /// Gets the stable platform id written to `lastPlayPlatform` and passed to the Play session.
        /// </summary>
        public string PlatformId { get; }

        /// <summary>
        /// Gets the readable platform name the selector shows.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the path of the builder assembly that also holds the platform's player adapter.
        /// </summary>
        public string BuilderAssemblyPath { get; }

        /// <summary>
        /// Gets the Play capability declared by the platform builder, including the player adapter type name.
        /// </summary>
        public PlatformPlayerCapability PlayerCapability { get; }

        /// <summary>
        /// Gets the video modes the platform declares, or null when it declares none.
        /// </summary>
        public PlatformVideoModeCapability VideoModeCapability { get; }
    }
}
