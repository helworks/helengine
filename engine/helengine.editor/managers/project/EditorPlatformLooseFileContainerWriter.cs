using helengine.baseplatform.Manifest;
using helengine.files;

namespace helengine.editor {
    /// <summary>
    /// Writes the loose-file container form used by the first Windows layout mode.
    /// </summary>
    internal sealed class EditorPlatformLooseFileContainerWriter {
        readonly LooseFileContainerWriter InnerWriter = new();

        /// <summary>
        /// Writes the supplied manifest by mirroring cooked files into the destination root.
        /// </summary>
        public void Write(PlatformBuildManifest manifest, string sourceRootPath, string outputRootPath) {
            InnerWriter.Write(manifest, sourceRootPath, outputRootPath);
        }
    }
}
