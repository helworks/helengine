using System.Buffers.Binary;
using System.Drawing;
using System.Drawing.Text;
using System.Reflection;
using helengine;
using helengine.editor;

namespace helengine.editor.windows.gdiimporter.tests {
    /// <summary>Verifies source face styles drive the actual importer, independent of installed regular faces, with bounded SFNT validation.</summary>
    public sealed class GdiFontSourceStyleTests {
        /// <summary>Each real Trebuchet source rasterizes byte-identically to explicitly selecting that private face, including repeated mixed-style imports.</summary>
        /// <param name="fileName">Installed Windows source file containing the authored face.</param>
        /// <param name="style">Actual source face style expected from the SFNT flags.</param>
        [Theory]
        [InlineData("trebuc.ttf", FontStyle.Regular)]
        [InlineData("trebucbd.ttf", FontStyle.Bold)]
        [InlineData("trebucit.ttf", FontStyle.Italic)]
        [InlineData("trebucbi.ttf", FontStyle.Bold | FontStyle.Italic)]
        public void RealSourceFaceMatchesExplicitPrivateFont(string fileName, FontStyle style) {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), fileName);
            Assert.Equal(style, ReadStyle(File.ReadAllBytes(path)));
            using GdiFontFileFixture fixture = new(path);
            using System.Drawing.Font font = fixture.CreateFont(34, style);
            using FontAsset expected = GDIFontProcessor.ImportFont(font, null, FontCharacterSet.BasicLatin);
            for (int iteration = 0; iteration < 3; iteration++) {
                using FileStream stream = File.OpenRead(path);
                using FontAsset actual = new GdiFontImporter(null).ImportFont(stream, new FontAssetProcessorSettings { PixelSize = 34 });
                Assert.Equal(expected.AtlasWidth, actual.AtlasWidth);
                Assert.Equal(expected.AtlasHeight, actual.AtlasHeight);
                Assert.Equal(expected.SourceTextureAsset.Colors, actual.SourceTextureAsset.Colors);
                Assert.Equal(expected.LineHeight, actual.LineHeight);
                Assert.Equal(expected.Characters.Keys.OrderBy(value => value), actual.Characters.Keys.OrderBy(value => value));
                foreach (char character in expected.Characters.Keys) { Assert.Equal(expected.Characters[character], actual.Characters[character]); }
            }
        }

        /// <summary>Big-endian style bits are respected for TrueType and OpenType/CFF directories without narrowing invalid offsets.</summary>
        [Fact]
        public void SfntStyleTablesUseBoundedBigEndianFields() {
            byte[] bytes = CreateStyleFixture();
            Assert.Equal(FontStyle.Bold | FontStyle.Italic, ReadStyle(bytes));
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(0, 4), 0x4F54544Fu);
            Assert.Equal(FontStyle.Bold | FontStyle.Italic, ReadStyle(bytes));
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(88, 2), 0);
            Assert.Equal(FontStyle.Bold | FontStyle.Italic, ReadStyle(bytes));
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(152, 2), 0);
            Assert.Equal(FontStyle.Regular, ReadStyle(bytes));
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20, 4), uint.MaxValue);
            Assert.Throws<InvalidOperationException>(() => ReadStyle(bytes));
        }

        /// <summary>Malformed source bytes fail explicitly before GDI can select an unrelated family fallback.</summary>
        [Fact]
        public void MalformedFontFailsBeforeFamilyFallback() {
            using MemoryStream stream = new MemoryStream(new byte[12]);
            Assert.Throws<InvalidOperationException>(() => new GdiFontImporter(null).ImportFont(stream, new FontAssetProcessorSettings { PixelSize = 34 }));
            byte[] bytes = CreateStyleFixture();
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(4, 2), ushort.MaxValue);
            Assert.Throws<InvalidOperationException>(() => ReadStyle(bytes));
        }

        /// <summary>Invokes the actual internal source-style reader without publishing an additional engine API for tests.</summary>
        /// <param name="bytes">SFNT source bytes to parse.</param>
        /// <returns>The production parser's face style.</returns>
        static FontStyle ReadStyle(byte[] bytes) {
            Type type = typeof(GdiFontImporter).Assembly.GetType("helengine.editor.GdiFontSourceStyle", true);
            try { return (FontStyle)type.GetMethod("Read", BindingFlags.Public | BindingFlags.Static).Invoke(null, [bytes]); }
            catch (TargetInvocationException exception) when (exception.InnerException != null) { throw exception.InnerException; }
        }

        /// <summary>Builds a minimal bounded directory containing head and OS/2 source style flags for parser-only tests.</summary>
        /// <returns>The standalone TrueType-style table fixture.</returns>
        static byte[] CreateStyleFixture() {
            byte[] bytes = new byte[154];
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(0, 4), 0x00010000u);
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(4, 2), 2);
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(12, 4), 0x68656164u);
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20, 4), 44);
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(24, 4), 46);
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(28, 4), 0x4F532F32u);
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(36, 4), 90);
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(40, 4), 64);
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(88, 2), 3);
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(152, 2), 33);
            return bytes;
        }
    }
}