namespace helengine.editor.tests.content.texture {
    /// <summary>
    /// Verifies the shared texture format catalog exposed to the editor.
    /// </summary>
    public sealed class TextureImportFormatCatalogTests {
        /// <summary>
        /// Ensures the union of texture extensions includes modern formats and does not duplicate overlapping entries.
        /// </summary>
        [Fact]
        public void AllTextureExtensions_WhenRead_ReturnsDeDuplicatedUnion() {
            IReadOnlyList<string> extensions = TextureImportFormatCatalog.AllTextureExtensions;

            Assert.Contains(".png", extensions, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(".dds", extensions, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(".webp", extensions, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(1, extensions.Count(extension => string.Equals(extension, ".tga", StringComparison.OrdinalIgnoreCase)));
        }
    }
}
