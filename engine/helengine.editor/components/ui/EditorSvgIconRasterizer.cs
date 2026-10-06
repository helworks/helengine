using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Svg;

namespace helengine.editor {
    /// <summary>Rasterizes in-memory SVG geometry into transparent white masks for theme-tinted editor icons.</summary>
    public static class EditorSvgIconRasterizer {
        /// <summary>Draws an SVG at the final physical pixel size and preserves its alpha coverage in straight RGBA.</summary>
        /// <param name="source">Self-contained SVG source describing the icon geometry.</param>
        /// <param name="pixelSize">Final square canvas size in physical pixels.</param>
        /// <returns>White RGBA mask with a transparent background.</returns>
        [SupportedOSPlatform("windows")]
        public static TextureAsset CreateWhiteMask(string source, int pixelSize) {
            if (string.IsNullOrWhiteSpace(source)) {
                throw new ArgumentException("SVG source must be provided.", nameof(source));
            }
            if (pixelSize <= 0 || pixelSize > ushort.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(pixelSize));
            }
            SvgDocument document = SvgDocument.FromSvg<SvgDocument>(source);
            using System.Drawing.Bitmap bitmap = document.Draw(pixelSize, pixelSize);
            System.Drawing.Rectangle bounds = new System.Drawing.Rectangle(0, 0, pixelSize, pixelSize);
            BitmapData data = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try {
                byte[] colors = new byte[checked(pixelSize * pixelSize * 4)];
                byte[] row = new byte[pixelSize * 4];
                for (int y = 0; y < pixelSize; y++) {
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                    for (int x = 0; x < pixelSize; x++) {
                        int destination = ((y * pixelSize) + x) * 4;
                        colors[destination] = byte.MaxValue;
                        colors[destination + 1] = byte.MaxValue;
                        colors[destination + 2] = byte.MaxValue;
                        colors[destination + 3] = row[(x * 4) + 3];
                    }
                }
                return new TextureAsset { Width = (ushort)pixelSize, Height = (ushort)pixelSize, Colors = colors };
            } finally {
                bitmap.UnlockBits(data);
            }
        }
    }
}
