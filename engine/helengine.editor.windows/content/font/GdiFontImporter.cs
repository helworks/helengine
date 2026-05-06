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
        /// <returns>Imported font asset.</returns>
        public FontAsset ImportFont(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            using MemoryStream buffer = new MemoryStream();
            stream.CopyTo(buffer);
            byte[] bytes = buffer.ToArray();
            if (bytes.Length == 0) {
                throw new InvalidOperationException("Font source stream must contain data.");
            }

            nint nativeBuffer = Marshal.AllocCoTaskMem(bytes.Length);
            try {
                Marshal.Copy(bytes, 0, nativeBuffer, bytes.Length);
                using PrivateFontCollection fontCollection = new PrivateFontCollection();
                fontCollection.AddMemoryFont(nativeBuffer, bytes.Length);
                if (fontCollection.Families.Length == 0) {
                    throw new InvalidOperationException("Source font did not produce any installable font families.");
                }

                using System.Drawing.Font font = new System.Drawing.Font(fontCollection.Families[0], 32f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
                return GDIFontProcessor.ImportFont(font);
            } finally {
                Marshal.FreeCoTaskMem(nativeBuffer);
            }
        }
    }
}
