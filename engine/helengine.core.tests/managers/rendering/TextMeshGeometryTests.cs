using helengine;

namespace helengine.core.tests.managers.rendering {
    /// <summary>Checks planar meshes against explicit synthetic glyphs from the engine's own font library.</summary>
    public sealed class TextMeshGeometryTests {
        /// <summary>Preserves crop bearings, logical advances, accents, UVs and winding in local positive-X/negative-Y coordinates.</summary>
        [Fact]
        public void OwnFontMetricsProduceAccentedPlanarGeometry() {
            using FontAsset font = CreateFont();
            using ModelAsset mesh = TextMeshGeometry.Build(font, "A ó\r\nA");
            Assert.Equal(12, mesh.Positions.Length);
            Assert.Equal(new float3(-1, -2, 0), mesh.Positions[0]);
            Assert.Equal(new float3(3, -8, 0), mesh.Positions[2]);
            Assert.Equal(new float3(12, -1, 0), mesh.Positions[4]);
            Assert.Equal(new float3(-1, -12, 0), mesh.Positions[8]);
            Assert.Equal(new float2(0.25f, 0.125f), mesh.TexCoords[0]);
            Assert.Equal(new float2(0.3125f, 0.3125f), mesh.TexCoords[2]);
            Assert.All(mesh.Normals, normal => Assert.Equal(float3.UnitZ, normal));
            Assert.Equal(new ushort[] { 0, 2, 1, 0, 3, 2, 4, 6, 5, 4, 7, 6, 8, 10, 9, 8, 11, 10 }, mesh.Indices16);
            Assert.Null(mesh.Indices32);
            Assert.Equal(new float3(-1, -18, 0), mesh.BoundsMin);
            Assert.Equal(new float3(17, -1, 0), mesh.BoundsMax);
            Assert.Single(mesh.Submeshes);
            Assert.Equal(18, mesh.Submeshes[0].IndexCount);
        }
        /// <summary>Spaces and tabs move the pen using font space metrics while absent glyphs fail rather than vanish.</summary>
        [Fact]
        public void ControlsAndMissingGlyphsAreExplicit() {
            using FontAsset font = CreateFont();
            using ModelAsset mesh = TextMeshGeometry.Build(font, "\tA");
            Assert.Equal(11d, mesh.Positions[0].X.ToDouble());
            Assert.Throws<InvalidOperationException>(() => TextMeshGeometry.Build(font, "ç"));
            Assert.Throws<NotSupportedException>(() => TextMeshGeometry.Build(font, "😀"));
            Assert.Throws<ArgumentNullException>(() => TextMeshGeometry.Build(null, "A"));
            Assert.Throws<ArgumentNullException>(() => TextMeshGeometry.Build(font, null));
        }
        /// <summary>Empty and whitespace runs return no drawable triangles, so callers can avoid uploading empty GPU resources.</summary>
        [Theory]
        [InlineData("")]
        [InlineData(" \t\r\n")]
        public void EmptyRunsHaveNoGeometry(string text) {
            using FontAsset font = CreateFont();
            using ModelAsset mesh = TextMeshGeometry.Build(font, text);
            Assert.Empty(mesh.Positions); Assert.Empty(mesh.Normals); Assert.Empty(mesh.TexCoords);
            Assert.Empty(mesh.Indices16); Assert.Null(mesh.Indices32); Assert.Empty(mesh.Submeshes);
            Assert.Equal(float3.Zero, mesh.BoundsMin); Assert.Equal(float3.Zero, mesh.BoundsMax);
        }
        /// <summary>Uses the complete unsigned 16-bit index range, then promotes larger runs without truncating glyph vertices.</summary>
        [Theory]
        [InlineData(16384, false)]
        [InlineData(16385, true)]
        public void IndexStorageFitsEveryVertex(int glyphCount, bool wide) {
            using FontAsset font = CreateFont();
            using ModelAsset mesh = TextMeshGeometry.Build(font, new string('A', glyphCount));
            Assert.Equal(glyphCount * 4, mesh.Positions.Length);
            if (wide) { Assert.Null(mesh.Indices16); Assert.Equal((uint)(glyphCount * 4 - 1), mesh.Indices32[^2]); } else { Assert.Null(mesh.Indices32); Assert.Equal((ushort)(glyphCount * 4 - 1), mesh.Indices16[^2]); }
        }
        /// <summary>Rejects corrupted font metrics before non-finite coordinates can reach a model upload.</summary>
        [Fact]
        public void NonFiniteMetricsFailBeforeGeometryAllocation() {
            using FontAsset font = CreateFont();
            FontChar glyph = font.Characters['A']; glyph.BearingX = float.NaN; font.Characters['A'] = glyph;
            Assert.Throws<InvalidOperationException>(() => TextMeshGeometry.Build(font, "A"));
            glyph.BearingX = 0; glyph.SourceRect.Z = (Real)float.PositiveInfinity; font.Characters['A'] = glyph;
            Assert.Throws<InvalidOperationException>(() => TextMeshGeometry.Build(font, "A"));
            font.FontInfo.SpaceWidth = float.NaN;
            Assert.Throws<InvalidOperationException>(() => TextMeshGeometry.Build(font, " "));
        }        /// <summary>Creates a small explicit atlas containing only A and an accented o, with distinctive independent metrics.</summary>
        internal static FontAsset CreateFont() {
            return new FontAsset(new FontInfo("Synthetic own font", 10, 3), new ManagedRuntimeTexture { Width = 64, Height = 32 },
                new Dictionary<char, FontChar> {
                    ['A'] = new FontChar(new float4(0.25f, 0.125f, 0.0625f, 0.1875f), 2, 7, -1, 0),
                    ['ó'] = new FontChar(new float4(0.5f, 0.5f, 0.078125f, 0.25f), 1, 6, 2, 0)
                }, 10, 64, 32);
        }
    }
}