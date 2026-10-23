using System.Reflection;
using helengine.editor;

namespace helengine.editor.windows.gdiimporter.tests {
    /// <summary>Exercises the actual GDI atlas size selector without allocating thousands of glyph bitmaps or creating a renderer.</summary>
    public sealed class GdiFontAtlasCapacityTests {
        /// <summary>Valid requests that exceed either atlas dimension fail explicitly rather than retaining coordinates for clipped glyphs.</summary>
        /// <param name="width">Raster glyph width before transparent padding.</param>
        /// <param name="height">Raster glyph height before transparent padding.</param>
        /// <param name="count">Number of glyph cells to pack.</param>
        [Theory]
        [InlineData(2045, 20, 1)]
        [InlineData(1000, 2040, 3)]
        public void RequestsBeyondAtlasCapacityFailExplicitly(int width, int height, int count) {
            List<TempFontItem> items = Enumerable.Range(0, count).Select(index => new TempFontItem {
                Char = (char)('A' + index), Width = width, Height = height
            }).ToList();
            MethodInfo selectSize = typeof(GDIFontProcessor).GetMethod("CalculateOptimalAtlasSize", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(selectSize);
            TargetInvocationException failure = Assert.Throws<TargetInvocationException>(() => selectSize.Invoke(null, new object[] { items }));
            InvalidOperationException capacity = Assert.IsType<InvalidOperationException>(failure.InnerException);
            Assert.Contains("2048", capacity.Message);
        }
    }
}