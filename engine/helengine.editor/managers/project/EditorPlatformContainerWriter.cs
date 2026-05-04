using helengine.baseplatform.Definitions;
using helengine.baseplatform.Manifest;
using helengine.files;

namespace helengine.editor {
    /// <summary>
    /// Chooses the appropriate container writer for the selected storage profile.
    /// </summary>
    internal sealed class EditorPlatformContainerWriter {
        readonly EditorPlatformLooseFileContainerWriter LooseFileWriter;
        readonly SegmentedPackfileContainerWriter PackfileWriter;

        /// <summary>
        /// Initializes one container writer dispatcher with default writer backends.
        /// </summary>
        public EditorPlatformContainerWriter() {
            LooseFileWriter = new EditorPlatformLooseFileContainerWriter();
            PackfileWriter = new SegmentedPackfileContainerWriter(new PackfileWritePlan("container-0", 1_073_741_824));
        }

        /// <summary>
        /// Writes the supplied manifest into the destination root using the selected storage profile.
        /// </summary>
        public void Write(
            PlatformBuildManifest manifest,
            string sourceRootPath,
            string outputRootPath,
            PlatformStorageProfileDefinition storageProfile,
            PlatformMediaProfileDefinition mediaProfile) {
            if (manifest == null) {
                throw new ArgumentNullException(nameof(manifest));
            }
            if (storageProfile == null) {
                throw new ArgumentNullException(nameof(storageProfile));
            }
            if (mediaProfile == null) {
                throw new ArgumentNullException(nameof(mediaProfile));
            }

            switch (storageProfile.StorageKind) {
                case PlatformStorageProfileKind.LooseFiles:
                    LooseFileWriter.Write(manifest, sourceRootPath, outputRootPath);
                    break;
                case PlatformStorageProfileKind.SinglePackfile:
                case PlatformStorageProfileKind.SegmentedPackfiles:
                case PlatformStorageProfileKind.DiscLayout:
                    PackfileWriter.Write(manifest, sourceRootPath, outputRootPath);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported storage profile kind '{storageProfile.StorageKind}'.");
            }
        }
    }
}
