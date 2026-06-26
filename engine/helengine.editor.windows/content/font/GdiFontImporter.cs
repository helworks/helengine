using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace helengine.editor {
    /// <summary>
    /// Imports source font files through the existing GDI-backed font rasterization path.
    /// </summary>
    public sealed class GdiFontImporter : IFontImporter {
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
                return GDIFontProcessor.ImportFont(font);
            } finally {
                if (!string.IsNullOrWhiteSpace(temporaryFontFilePath) && File.Exists(temporaryFontFilePath)) {
                    File.Delete(temporaryFontFilePath);
                }
            }
        }

        /// <summary>
        /// Loads one private font collection from raw source bytes, falling back to a temporary font file when the in-memory GDI path does not surface any usable families.
        /// </summary>
        /// <param name="bytes">Source font bytes copied from the importer stream.</param>
        /// <param name="temporaryFontFilePath">Receives the temporary font-file path when the fallback path is used.</param>
        /// <returns>Private font collection that exposes at least one installable font family.</returns>
        static PrivateFontCollection LoadFontCollection(byte[] bytes, ref string temporaryFontFilePath) {
            if (bytes == null) {
                throw new ArgumentNullException(nameof(bytes));
            } else if (bytes.Length == 0) {
                throw new InvalidOperationException("Font source stream must contain data.");
            }

            nint nativeBuffer = Marshal.AllocCoTaskMem(bytes.Length);
            try {
                Marshal.Copy(bytes, 0, nativeBuffer, bytes.Length);
                PrivateFontCollection fontCollection = new PrivateFontCollection();
                fontCollection.AddMemoryFont(nativeBuffer, bytes.Length);
                if (fontCollection.Families.Length > 0) {
                    return fontCollection;
                }

                fontCollection.Dispose();
                temporaryFontFilePath = CreateTemporaryFontFile(bytes);
                PrivateFontCollection fallbackFontCollection = new PrivateFontCollection();
                fallbackFontCollection.AddFontFile(temporaryFontFilePath);
                if (fallbackFontCollection.Families.Length == 0) {
                    fallbackFontCollection.Dispose();
                    throw new InvalidOperationException("Source font did not produce any installable font families.");
                }

                return fallbackFontCollection;
            } finally {
                Marshal.FreeCoTaskMem(nativeBuffer);
            }
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
