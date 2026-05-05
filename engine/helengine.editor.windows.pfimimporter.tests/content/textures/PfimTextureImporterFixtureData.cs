namespace helengine.editor.windows.pfimimporter.tests.content.textures {
    /// <summary>
    /// Provides tiny encoded image fixtures used by the Pfim importer tests.
    /// </summary>
    public static class PfimTextureImporterFixtureData {
        /// <summary>
        /// Creates a 1x1 32-bit uncompressed TGA file whose pixel bytes are stored as BGRA.
        /// </summary>
        /// <returns>Encoded TGA file bytes.</returns>
        public static byte[] CreateSinglePixelTga32File() {
            return new byte[] {
                0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 32, 40,
                1, 2, 3, 4
            };
        }

        /// <summary>
        /// Creates a 1x1 24-bit uncompressed TGA file whose pixel bytes are stored as BGR.
        /// </summary>
        /// <returns>Encoded TGA file bytes.</returns>
        public static byte[] CreateSinglePixelTga24File() {
            return new byte[] {
                0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 24, 32,
                5, 6, 7
            };
        }

        /// <summary>
        /// Creates a 1x1 uncompressed DDS file whose single pixel is encoded as RGBA 9,8,7,255.
        /// </summary>
        /// <returns>Encoded DDS file bytes.</returns>
        public static byte[] CreateSinglePixelDdsFile() {
            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(new byte[] { 0x44, 0x44, 0x53, 0x20 });
            writer.Write(124);
            writer.Write(0x0000100F);
            writer.Write(1);
            writer.Write(1);
            writer.Write(4);
            writer.Write(0);
            writer.Write(0);
            WriteZeros(writer, 11);
            writer.Write(32);
            writer.Write(0x00000041);
            writer.Write(0);
            writer.Write(32);
            writer.Write(unchecked((int)0x00FF0000));
            writer.Write(unchecked((int)0x0000FF00));
            writer.Write(unchecked((int)0x000000FF));
            writer.Write(unchecked((int)0xFF000000));
            writer.Write(0x00001000);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(new byte[] { 7, 8, 9, 255 });
            return stream.ToArray();
        }

        /// <summary>
        /// Writes a sequence of zeroed 32-bit integers into the encoded DDS header.
        /// </summary>
        /// <param name="writer">Writer receiving the zeros.</param>
        /// <param name="count">Number of zeroed integers to write.</param>
        static void WriteZeros(BinaryWriter writer, int count) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (count < 0) {
                throw new ArgumentOutOfRangeException(nameof(count), "Count must be non-negative.");
            }

            for (int index = 0; index < count; index++) {
                writer.Write(0);
            }
        }
    }
}
