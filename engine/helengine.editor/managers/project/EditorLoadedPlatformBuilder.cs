using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;
using helengine.platforms;

namespace helengine.editor {
    /// <summary>
    /// Represents one dynamically loaded platform builder and the metadata it exposes.
    /// </summary>
    public sealed class EditorLoadedPlatformBuilder {
        /// <summary>
        /// Initializes one loaded platform builder.
        /// </summary>
        /// <param name="platformDescriptor">Resolved platform descriptor from the platform catalog.</param>
        /// <param name="builder">Loaded platform builder instance.</param>
        public EditorLoadedPlatformBuilder(AvailablePlatformDescriptor platformDescriptor, IPlatformAssetBuilder builder) {
            if (platformDescriptor == null) {
                throw new ArgumentNullException(nameof(platformDescriptor));
            }
            if (builder == null) {
                throw new ArgumentNullException(nameof(builder));
            }

            PlatformDescriptor = platformDescriptor;
            Builder = builder;
            Definition = builder.Definition ?? throw new InvalidOperationException($"Builder '{builder.Descriptor.BuilderId}' did not expose platform metadata.");
            SelectionModel = EditorPlatformBuildSelectionModel.From(Definition);
        }

        /// <summary>
        /// Gets the resolved platform descriptor from the platform catalog.
        /// </summary>
        public AvailablePlatformDescriptor PlatformDescriptor { get; }

        /// <summary>
        /// Gets the loaded platform builder instance.
        /// </summary>
        public IPlatformAssetBuilder Builder { get; }

        /// <summary>
        /// Gets the builder-provided typed platform metadata.
        /// </summary>
        public PlatformDefinition Definition { get; }

        /// <summary>
        /// Gets the selection model the editor can use to populate build dialogs.
        /// </summary>
        public EditorPlatformBuildSelectionModel SelectionModel { get; }
    }
}
