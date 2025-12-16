using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace helengine {
    /// <summary>
    /// Provides a simple helper for locking and editing bitmap pixel data safely.
    /// </summary>
    public class LockBitmap {
        readonly Bitmap source = null;
        IntPtr Iptr = IntPtr.Zero;
        BitmapData bitmapData = null;

        /// <summary>
        /// Initializes a new <see cref="LockBitmap"/> instance for the given bitmap.
        /// </summary>
        /// <param name="source">Bitmap to lock for pixel access.</param>
        public LockBitmap(Bitmap source) {
            this.source = source;
        }

        /// <summary>
        /// Gets or sets the pixel buffer representing the locked bitmap.
        /// </summary>
        public byte[] Pixels { get; set; }

        /// <summary>
        /// Gets the pixel depth (bits per pixel) for the locked bitmap.
        /// </summary>
        public int Depth { get; private set; }

        /// <summary>
        /// Gets the width of the locked bitmap.
        /// </summary>
        public int Width { get; private set; }

        /// <summary>
        /// Gets the height of the locked bitmap.
        /// </summary>
        public int Height { get; private set; }

        /// <summary>
        /// Locks the bitmap for read/write access and fills the <see cref="Pixels"/> buffer.
        /// </summary>
        public void LockBits() {
            try {
                // Get width and height of bitmap
                Width = source.Width;
                Height = source.Height;

                // get total locked pixels count
                int PixelCount = Width * Height;

                // Create rectangle to lock
                Rectangle rect = new Rectangle(0, 0, Width, Height);

                // get source bitmap pixel format size
                Depth = System.Drawing.Bitmap.GetPixelFormatSize(source.PixelFormat);

                // Check if bpp (Bits Per Pixel) is 8, 24, or 32
                if (Depth != 8 && Depth != 24 && Depth != 32) {
                    throw new ArgumentException("Only 8, 24 and 32 bpp images are supported.");
                }

                // Lock bitmap and return bitmap data
                bitmapData = source.LockBits(rect, ImageLockMode.ReadWrite,
                                             source.PixelFormat);

                // create byte array to copy pixel values
                int step = Depth / 8;
                Pixels = new byte[PixelCount * step];
                Iptr = bitmapData.Scan0;

                // Copy data from pointer to array
                Marshal.Copy(Iptr, Pixels, 0, Pixels.Length);
            } catch (Exception ex) {
                throw ex;
            }
        }

        /// <summary>
        /// Unlocks the bitmap data and optionally writes modified pixels back to the source.
        /// </summary>
        /// <param name="transferBack">True to copy the <see cref="Pixels"/> buffer back into the bitmap.</param>
        public void UnlockBits(bool transferBack) {
            try {
                // Copy data from byte array to pointer
                if (transferBack) {
                    Marshal.Copy(Pixels, 0, Iptr, Pixels.Length);
                }

                // Unlock bitmap data
                source.UnlockBits(bitmapData);
            } catch (Exception ex) {
                throw ex;
            }
        }

        /// <summary>
        /// Gets the color of the specified pixel.
        /// </summary>
        /// <param name="x">X coordinate of the pixel.</param>
        /// <param name="y">Y coordinate of the pixel.</param>
        /// <returns>Color value at the specified coordinate.</returns>
        public Color GetPixel(int x, int y) {
            Color clr = Color.Empty;

            // Get color components count
            int cCount = Depth / 8;

            // Get start index of the specified pixel
            int i = ((y * Width) + x) * cCount;

            if (i > Pixels.Length - cCount)
                throw new IndexOutOfRangeException();

            if (Depth == 32) // For 32 bpp get Red, Green, Blue and Alpha
            {
                byte b = Pixels[i];
                byte g = Pixels[i + 1];
                byte r = Pixels[i + 2];
                byte a = Pixels[i + 3]; // a
                clr = Color.FromArgb(a, r, g, b);
            }
            if (Depth == 24) // For 24 bpp get Red, Green and Blue
            {
                byte b = Pixels[i];
                byte g = Pixels[i + 1];
                byte r = Pixels[i + 2];
                clr = Color.FromArgb(r, g, b);
            }
            if (Depth == 8)
            // For 8 bpp get color value (Red, Green and Blue values are the same)
            {
                byte c = Pixels[i];
                clr = Color.FromArgb(c, c, c);
            }
            return clr;
        }

        /// <summary>
        /// Sets the color of a pixel using raw channel values.
        /// </summary>
        /// <param name="x">X coordinate of the pixel.</param>
        /// <param name="y">Y coordinate of the pixel.</param>
        /// <param name="r">Red channel value.</param>
        /// <param name="g">Green channel value.</param>
        /// <param name="b">Blue channel value.</param>
        public void SetPixel(int x, int y, byte r, byte g, byte b) {
            // Get color components count
            int cCount = Depth / 8;

            // Get start index of the specified pixel
            int i = ((y * Width) + x) * cCount;

            if (Depth == 32) // For 32 bpp set Red, Green, Blue and Alpha
            {
                Pixels[i] = r;
                Pixels[i + 1] = g;
                Pixels[i + 2] = b;
                Pixels[i + 3] = 255;
            }
            if (Depth == 24) // For 24 bpp set Red, Green and Blue
            {
                Pixels[i] = r;
                Pixels[i + 1] = g;
                Pixels[i + 2] = b;
            }
            if (Depth == 8)
            // For 8 bpp set color value (Red, Green and Blue values are the same)
            {
                //Pixels[i] = color.B;
            }
        }

        /// <summary>
        /// Sets the color of the specified pixel.
        /// </summary>
        /// <param name="x">X coordinate of the pixel.</param>
        /// <param name="y">Y coordinate of the pixel.</param>
        /// <param name="color">Color to apply.</param>
        public void SetPixel(int x, int y, Color color) {
            // Get color components count
            int cCount = Depth / 8;

            // Get start index of the specified pixel
            int i = ((y * Width) + x) * cCount;

            if (Depth == 32) // For 32 bpp set Red, Green, Blue and Alpha
            {
                Pixels[i] = color.B;
                Pixels[i + 1] = color.G;
                Pixels[i + 2] = color.R;
                Pixels[i + 3] = color.A;
            }
            if (Depth == 24) // For 24 bpp set Red, Green and Blue
            {
                Pixels[i] = color.B;
                Pixels[i + 1] = color.G;
                Pixels[i + 2] = color.R;
            }
            if (Depth == 8)
            // For 8 bpp set color value (Red, Green and Blue values are the same)
            {
                Pixels[i] = color.B;
            }
        }
    }
}
