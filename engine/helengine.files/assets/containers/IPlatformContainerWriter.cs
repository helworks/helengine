using helengine.baseplatform.Manifest;

namespace helengine.files {
    /// <summary>
    /// Writes one cooked build manifest into a concrete runtime container layout.
    /// </summary>
    public interface IPlatformContainerWriter {
        /// <summary>
        /// Writes the supplied manifest from the source root into the destination root.
        /// </summary>
        /// <param name="manifest">The cooked build manifest to write.</param>
        /// <param name="sourceRootPath">The source root containing cooked runtime payloads.</param>
        /// <param name="outputRootPath">The destination root for the emitted container layout.</param>
        void Write(PlatformBuildManifest manifest, string sourceRootPath, string outputRootPath);
    }
}
