namespace helengine.editor {
    /// <summary>
    /// Texture importer backed by GDI utilities for simple formats.
    /// </summary>
    public class GDITextureImporter : ITextureImporter {
        /// <summary>
        /// Imports a texture asset from the provided stream.
        /// </summary>
        /// <param name="stream">Stream containing the raw texture data.</param>
        /// <returns>Created <see cref="TextureAsset"/> instance.</returns>
        public TextureAsset ImportTexture(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            using var sourceBitmap = new System.Drawing.Bitmap(stream);
            int width = sourceBitmap.Width;
            int height = sourceBitmap.Height;
            if (width > ushort.MaxValue || height > ushort.MaxValue) {
                throw new InvalidOperationException("Texture dimensions exceed supported limits.");
            }

            var bounds = new System.Drawing.Rectangle(0, 0, width, height);
            using var bitmap = sourceBitmap.Clone(bounds, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var data = bitmap.LockBits(bounds, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try {
                int bytesPerPixel = 4;
                int rowLength = width * bytesPerPixel;
                byte[] colors = new byte[width * height * bytesPerPixel];
                byte[] rowData = new byte[rowLength];

                for (int y = 0; y < height; y++) {
                    var rowPtr = System.IntPtr.Add(data.Scan0, y * data.Stride);
                    System.Runtime.InteropServices.Marshal.Copy(rowPtr, rowData, 0, rowLength);

                    int rowOffset = y * rowLength;
                    for (int x = 0; x < width; x++) {
                        int sourceIndex = x * bytesPerPixel;
                        int destIndex = rowOffset + sourceIndex;

                        colors[destIndex] = rowData[sourceIndex + 2];
                        colors[destIndex + 1] = rowData[sourceIndex + 1];
                        colors[destIndex + 2] = rowData[sourceIndex];
                        colors[destIndex + 3] = rowData[sourceIndex + 3];
                    }
                }

                return new TextureAsset {
                    Colors = colors,
                    Width = (ushort)width,
                    Height = (ushort)height
                };
            } finally {
                bitmap.UnlockBits(data);
            }
        }
    }
}
