namespace helengine.demo_disc_scene_writer {
    /// <summary>
    /// Copies authored source font files into the city project for the generated demo-disc menu.
    /// </summary>
    public sealed class DemoDiscFontWriter {
        /// <summary>
        /// Writes one authored source font file into the generated city project assets.
        /// </summary>
        /// <param name="outputPath">Destination source-font asset path.</param>
        /// <param name="installedFontFileName">Installed Windows font file name to copy into the project.</param>
        public void WriteFont(string outputPath, string installedFontFileName) {
            if (string.IsNullOrWhiteSpace(outputPath)) {
                throw new ArgumentException("Output path must be provided.", nameof(outputPath));
            }
            if (string.IsNullOrWhiteSpace(installedFontFileName)) {
                throw new ArgumentException("Installed font file name must be provided.", nameof(installedFontFileName));
            }

            string installedFontPath = ResolveInstalledFontPath(installedFontFileName);
            string fullOutputPath = Path.GetFullPath(outputPath);
            string directoryPath = Path.GetDirectoryName(fullOutputPath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidOperationException("Output directory could not be resolved.");
            }

            Directory.CreateDirectory(directoryPath);
            File.Copy(installedFontPath, fullOutputPath, true);
        }

        /// <summary>
        /// Resolves one installed Windows font file path.
        /// </summary>
        /// <param name="installedFontFileName">Installed Windows font file name to resolve.</param>
        /// <returns>Absolute installed font file path.</returns>
        string ResolveInstalledFontPath(string installedFontFileName) {
            string fontsRootPath = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            if (string.IsNullOrWhiteSpace(fontsRootPath)) {
                throw new InvalidOperationException("The Windows fonts folder could not be resolved.");
            }

            string installedFontPath = Path.Combine(fontsRootPath, installedFontFileName);
            if (!File.Exists(installedFontPath)) {
                throw new FileNotFoundException($"Installed Windows font '{installedFontFileName}' was not found.", installedFontPath);
            }

            return installedFontPath;
        }
    }
}
