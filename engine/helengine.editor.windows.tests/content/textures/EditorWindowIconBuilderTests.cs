using helengine.editor;

namespace helengine.editor.windows.tests.content.textures {
    /// <summary>
    /// Verifies the ICO container built around the editor title-bar PNG for the native window icon.
    /// </summary>
    public sealed class EditorWindowIconBuilderTests {
        /// <summary>
        /// Ensures a PNG is wrapped in a single-entry ICO directory whose entry describes the PNG dimensions and payload.
        /// </summary>
        [Fact]
        public void BuildIconFromPng_WhenGivenPng_WrapsItInSingleEntryIcoContainer() {
            byte[] png = CreatePngHeader(128, 128, 20);

            byte[] ico = EditorWindowIconBuilder.BuildIconFromPng(png);

            Assert.Equal(22 + png.Length, ico.Length);
            Assert.Equal(0, BitConverter.ToUInt16(ico, 0));
            Assert.Equal(1, BitConverter.ToUInt16(ico, 2));
            Assert.Equal(1, BitConverter.ToUInt16(ico, 4));
            Assert.Equal(128, ico[6]);
            Assert.Equal(128, ico[7]);
            Assert.Equal(0, ico[8]);
            Assert.Equal(0, ico[9]);
            Assert.Equal(1, BitConverter.ToUInt16(ico, 10));
            Assert.Equal(32, BitConverter.ToUInt16(ico, 12));
            Assert.Equal((uint)png.Length, BitConverter.ToUInt32(ico, 14));
            Assert.Equal(22u, BitConverter.ToUInt32(ico, 18));
            Assert.Equal(png, ico.Skip(22).ToArray());
        }

        /// <summary>
        /// Ensures 256-pixel dimensions are encoded as zero, as the ICO directory format requires.
        /// </summary>
        [Fact]
        public void BuildIconFromPng_WhenPngIs256Pixels_EncodesDimensionsAsZero() {
            byte[] png = CreatePngHeader(256, 256, 4);

            byte[] ico = EditorWindowIconBuilder.BuildIconFromPng(png);

            Assert.Equal(0, ico[6]);
            Assert.Equal(0, ico[7]);
        }

        /// <summary>
        /// Ensures non-PNG input is rejected rather than silently wrapped.
        /// </summary>
        [Fact]
        public void BuildIconFromPng_WhenBytesAreNotPng_Throws() {
            byte[] notPng = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24 };

            Assert.Throws<ArgumentException>(() => EditorWindowIconBuilder.BuildIconFromPng(notPng));
        }

        /// <summary>
        /// Ensures the title-bar icon path resolves under the application root to the PNG the in-engine title bar loads.
        /// </summary>
        [Fact]
        public void GetTitleBarIconPath_WhenGivenRoot_ResolvesTitleBarPng() {
            string root = Path.Combine(Path.GetTempPath(), "helengine-icon-tests");

            string path = EditorToolbarIconLoader.GetTitleBarIconPath(root);

            Assert.Equal(Path.GetFullPath(Path.Combine(root, "content", "icons", "titlebar", "helengine_icon.png")), path);
        }

        /// <summary>
        /// Ensures the shipped title-bar PNG round-trips into a native icon Windows accepts, at the PNG's own size.
        /// </summary>
        [Fact]
        public void BuildIconFromPng_WhenGivenShippedTitleBarPng_ProducesLoadableNativeIcon() {
            string iconPath = EditorToolbarIconLoader.GetTitleBarIconPath(AppContext.BaseDirectory);
            Assert.True(File.Exists(iconPath), $"Expected title-bar icon at '{iconPath}'.");

            byte[] ico = EditorWindowIconBuilder.BuildIconFromPng(File.ReadAllBytes(iconPath));

            using MemoryStream stream = new MemoryStream(ico, false);
            using System.Drawing.Icon icon = new System.Drawing.Icon(stream);

            Assert.Equal(128, icon.Width);
            Assert.Equal(128, icon.Height);
        }

        /// <summary>
        /// Creates a minimal PNG signature plus IHDR chunk header followed by filler bytes.
        /// </summary>
        /// <param name="width">Image width to encode.</param>
        /// <param name="height">Image height to encode.</param>
        /// <param name="fillerLength">Number of trailing filler bytes.</param>
        /// <returns>Byte array that begins like a PNG file.</returns>
        static byte[] CreatePngHeader(int width, int height, int fillerLength) {
            byte[] bytes = new byte[24 + fillerLength];
            byte[] signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52 };
            Array.Copy(signature, bytes, signature.Length);
            bytes[16] = (byte)(width >> 24);
            bytes[17] = (byte)(width >> 16);
            bytes[18] = (byte)(width >> 8);
            bytes[19] = (byte)width;
            bytes[20] = (byte)(height >> 24);
            bytes[21] = (byte)(height >> 16);
            bytes[22] = (byte)(height >> 8);
            bytes[23] = (byte)height;
            return bytes;
        }
    }
}
