using helengine.baseplatform.Manifest;

namespace helengine.files {
    /// <summary>
    /// Writes a loose-file container layout by mirroring cooked payload files into the destination root.
    /// </summary>
    public sealed class LooseFileContainerWriter : IPlatformContainerWriter {
        /// <summary>
        /// Writes the supplied source tree into the destination root while preserving relative paths.
        /// </summary>
        public void Write(PlatformBuildManifest manifest, string sourceRootPath, string outputRootPath) {
            if (manifest == null) {
                throw new ArgumentNullException(nameof(manifest));
            }
            if (string.IsNullOrWhiteSpace(sourceRootPath)) {
                throw new ArgumentException("Source root path must be provided.", nameof(sourceRootPath));
            }
            if (string.IsNullOrWhiteSpace(outputRootPath)) {
                throw new ArgumentException("Output root path must be provided.", nameof(outputRootPath));
            }

            if (!Directory.Exists(sourceRootPath)) {
                return;
            }

            Directory.CreateDirectory(outputRootPath);

            string[] sourceFiles = Directory.GetFiles(sourceRootPath, "*", SearchOption.AllDirectories);
            Array.Sort(sourceFiles, StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < sourceFiles.Length; index++) {
                string sourceFilePath = sourceFiles[index];
                string relativePath = Path.GetRelativePath(sourceRootPath, sourceFilePath);
                string destinationFilePath = Path.Combine(outputRootPath, relativePath);
                string destinationDirectoryPath = Path.GetDirectoryName(destinationFilePath);
                if (!string.IsNullOrWhiteSpace(destinationDirectoryPath)) {
                    Directory.CreateDirectory(destinationDirectoryPath);
                }

                File.Copy(sourceFilePath, destinationFilePath, true);
            }
        }
    }
}
