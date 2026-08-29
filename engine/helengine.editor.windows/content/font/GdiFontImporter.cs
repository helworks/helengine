using System.Drawing.Text;

namespace helengine.editor {
    /// <summary>
    /// Imports source font files through the existing GDI-backed font rasterization path.
    /// </summary>
    public sealed class GdiFontImporter : IFontImporter {
        /// <summary>
        /// Renderer owned by the importing editor session, when the importer is
        /// used by an interactive host. Headless callers pass null and retain a
        /// managed atlas texture without consulting ambient core state.
        /// </summary>
        readonly RenderManager2D RenderManager2D;

        /// <summary>
        /// Initializes a GDI font importer with its explicit renderer owner.
        /// </summary>
        /// <param name="renderManager2D">Session-owned renderer, or null for headless imports.</param>
        public GdiFontImporter(RenderManager2D renderManager2D) {
            RenderManager2D = renderManager2D;
        }

        /// <summary>
        /// Imports one source font stream into a runtime-ready font asset.
        /// </summary>
        /// <param name="stream">Stream containing source font bytes.</param>
        /// <param name="settings">Platform font settings supplied by the caller.</param>
        /// <returns>Imported font asset.</returns>
        public FontAsset ImportFont(Stream stream, FontAssetProcessorSettings settings) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (settings.PixelSize < 1) {
                throw new InvalidOperationException("Font pixel size must be greater than zero.");
            }

            using MemoryStream buffer = new MemoryStream();
            stream.CopyTo(buffer);
            byte[] bytes = buffer.ToArray();
            if (bytes.Length == 0) {
                throw new InvalidOperationException("Font source stream must contain data.");
            }

            string temporaryFontFilePath = string.Empty;
            try {
                using PrivateFontCollection fontCollection = LoadFontCollection(bytes, ref temporaryFontFilePath);
                using System.Drawing.Font font = new System.Drawing.Font(
                    fontCollection.Families[0],
                    settings.PixelSize,
                    System.Drawing.FontStyle.Regular,
                    System.Drawing.GraphicsUnit.Pixel);
                return GDIFontProcessor.ImportFont(font, RenderManager2D);
            } finally {
                if (!string.IsNullOrWhiteSpace(temporaryFontFilePath) && File.Exists(temporaryFontFilePath)) {
                    File.Delete(temporaryFontFilePath);
                }
            }
        }

        /// <summary>
        /// Loads one private font collection from raw source bytes through a temporary font file so GDI resolves the authored family deterministically.
        /// </summary>
        /// <param name="bytes">Source font bytes copied from the importer stream.</param>
        /// <param name="temporaryFontFilePath">Receives the temporary font-file path used by the private font collection.</param>
        /// <returns>Private font collection that exposes at least one installable font family.</returns>
        static PrivateFontCollection LoadFontCollection(byte[] bytes, ref string temporaryFontFilePath) {
            if (bytes == null) {
                throw new ArgumentNullException(nameof(bytes));
            } else if (bytes.Length == 0) {
                throw new InvalidOperationException("Font source stream must contain data.");
            }

            temporaryFontFilePath = CreateTemporaryFontFile(bytes);
            PrivateFontCollection fontCollection = new PrivateFontCollection();
            fontCollection.AddFontFile(temporaryFontFilePath);
            if (fontCollection.Families.Length == 0) {
                fontCollection.Dispose();
                throw new InvalidOperationException("Source font did not produce any installable font families.");
            }

            return fontCollection;
        }

        /// <summary>
        /// Writes one temporary source font file that GDI can open through the file-backed import path.
        /// </summary>
        /// <param name="bytes">Source font bytes copied from the importer stream.</param>
        /// <returns>Absolute temporary file path.</returns>
        static string CreateTemporaryFontFile(byte[] bytes) {
            if (bytes == null) {
                throw new ArgumentNullException(nameof(bytes));
            } else if (bytes.Length == 0) {
                throw new InvalidOperationException("Font source stream must contain data.");
            }

            string temporaryDirectoryPath = Path.Combine(Path.GetTempPath(), "helengine", "gdi-font-import");
            Directory.CreateDirectory(temporaryDirectoryPath);
            string temporaryFontFilePath = Path.Combine(temporaryDirectoryPath, Guid.NewGuid().ToString("N") + ".ttf");
            File.WriteAllBytes(temporaryFontFilePath, bytes);
            return temporaryFontFilePath;
        }
    }
}
