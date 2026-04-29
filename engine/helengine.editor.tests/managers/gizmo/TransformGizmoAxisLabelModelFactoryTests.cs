using helengine;
using helengine.editor;
using Xunit;

namespace helengine.editor.tests.managers.gizmo {
    /// <summary>
    /// Verifies billboard mesh generation for transform-gizmo axis labels.
    /// </summary>
    public class TransformGizmoAxisLabelModelFactoryTests {
        /// <summary>
        /// Tolerance used when comparing generated floating-point bounds.
        /// </summary>
        const float FloatTolerance = 0.0001f;

        /// <summary>
        /// Ensures one double-sided quad is generated for each glyph and that the UVs map back to the source atlas rectangles.
        /// </summary>
        [Fact]
        public void Create_GeneratesOneQuadPerGlyph_WithExpectedUvsAndIndices() {
            FontAsset font = CreateFontAsset();

            ModelAsset model = TransformGizmoAxisLabelModelFactory.Create(font, "x+");

            Assert.Equal(8, model.Positions.Length);
            Assert.Equal(8, model.Normals.Length);
            Assert.Equal(8, model.TexCoords.Length);
            Assert.Equal(
                new ushort[] {
                    0, 3, 2, 0, 2, 1, 0, 2, 3, 0, 1, 2,
                    4, 7, 6, 4, 6, 5, 4, 6, 7, 4, 5, 6
                },
                model.Indices16);
            AssertFloat2ApproximatelyEqual(new float2(0.09f, 0.19f), model.TexCoords[0]);
            AssertFloat2ApproximatelyEqual(new float2(0.41f, 0.19f), model.TexCoords[1]);
            AssertFloat2ApproximatelyEqual(new float2(0.41f, 0.61f), model.TexCoords[2]);
            AssertFloat2ApproximatelyEqual(new float2(0.09f, 0.61f), model.TexCoords[3]);
            AssertFloat2ApproximatelyEqual(new float2(0.49f, 0.59f), model.TexCoords[4]);
            AssertFloat2ApproximatelyEqual(new float2(0.61f, 0.59f), model.TexCoords[5]);
            AssertFloat2ApproximatelyEqual(new float2(0.61f, 0.81f), model.TexCoords[6]);
            AssertFloat2ApproximatelyEqual(new float2(0.49f, 0.81f), model.TexCoords[7]);
            AssertAllNormalsFaceCamera(model);
        }

        /// <summary>
        /// Ensures each glyph contributes both front-facing and back-facing triangle windings so the billboard is not lost to culling.
        /// </summary>
        [Fact]
        public void Create_GeneratesDoubleSidedTrianglesForEachGlyph() {
            FontAsset font = CreateFontAsset();

            ModelAsset model = TransformGizmoAxisLabelModelFactory.Create(font, "x+");

            Assert.Equal(24, model.Indices16.Length);
        }

        /// <summary>
        /// Ensures generated glyph quads are centered around the billboard origin so the label stays anchored to the axis tip.
        /// </summary>
        [Fact]
        public void Create_CentersGeneratedGeometryAroundOrigin() {
            FontAsset font = CreateFontAsset();

            ModelAsset model = TransformGizmoAxisLabelModelFactory.Create(font, "x+");

            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            CaptureBounds(model, ref minX, ref minY, ref maxX, ref maxY);

            Assert.InRange(Math.Abs(minX + maxX), 0f, FloatTolerance);
            Assert.InRange(Math.Abs(minY + maxY), 0f, FloatTolerance);
        }

        /// <summary>
        /// Ensures each glyph quad includes one pixel of transparent border so the material can draw an outside outline.
        /// </summary>
        [Fact]
        public void Create_ExpandsGlyphBoundsByOnePixelForOutlinePadding() {
            FontAsset font = CreateFontAsset();

            ModelAsset model = TransformGizmoAxisLabelModelFactory.Create(font, "x");

            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            CaptureBounds(model, ref minX, ref minY, ref maxX, ref maxY);

            Assert.InRange(maxX - minX, 31.999f, 32.001f);
            Assert.InRange(maxY - minY, 41.999f, 42.001f);
        }

        /// <summary>
        /// Ensures a missing glyph fails fast instead of silently emitting a broken billboard mesh.
        /// </summary>
        [Fact]
        public void Create_Throws_WhenGlyphIsMissing() {
            FontAsset font = CreateFontAsset();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                TransformGizmoAxisLabelModelFactory.Create(font, "q"));

            Assert.Contains("q", exception.Message);
        }

        /// <summary>
        /// Builds a deterministic font atlas used by the billboard mesh tests.
        /// </summary>
        /// <returns>Font asset with glyph data for x and +.</returns>
        static FontAsset CreateFontAsset() {
            var characters = new Dictionary<char, FontChar> {
                ['x'] = new FontChar(new float4(0.10f, 0.20f, 0.30f, 0.40f), 2f, 10f, 0f, 0f),
                ['+'] = new FontChar(new float4(0.50f, 0.60f, 0.10f, 0.20f), 1f, 6f, 0f, 0f)
            };
            return new FontAsset(new FontInfo("Test", 16, 4f), null, characters, 16f, 100, 100);
        }

        /// <summary>
        /// Verifies that every generated vertex normal points toward the billboard front face.
        /// </summary>
        /// <param name="model">Generated model asset to inspect.</param>
        static void AssertAllNormalsFaceCamera(ModelAsset model) {
            if (model == null) {
                throw new ArgumentNullException(nameof(model));
            }

            for (int normalIndex = 0; normalIndex < model.Normals.Length; normalIndex++) {
                Assert.Equal(0f, model.Normals[normalIndex].X);
                Assert.Equal(0f, model.Normals[normalIndex].Y);
                Assert.Equal(1f, model.Normals[normalIndex].Z);
            }
        }

        /// <summary>
        /// Verifies two texture coordinates match within the shared floating-point tolerance.
        /// </summary>
        /// <param name="expected">Expected texture coordinate.</param>
        /// <param name="actual">Generated texture coordinate.</param>
        static void AssertFloat2ApproximatelyEqual(float2 expected, float2 actual) {
            Assert.InRange(Math.Abs(expected.X - actual.X), 0f, FloatTolerance);
            Assert.InRange(Math.Abs(expected.Y - actual.Y), 0f, FloatTolerance);
        }

        /// <summary>
        /// Captures the axis-aligned bounds of the generated billboard geometry.
        /// </summary>
        /// <param name="model">Generated model asset to inspect.</param>
        /// <param name="minX">Receives the minimum X bound.</param>
        /// <param name="minY">Receives the minimum Y bound.</param>
        /// <param name="maxX">Receives the maximum X bound.</param>
        /// <param name="maxY">Receives the maximum Y bound.</param>
        static void CaptureBounds(ModelAsset model, ref float minX, ref float minY, ref float maxX, ref float maxY) {
            if (model == null) {
                throw new ArgumentNullException(nameof(model));
            }

            for (int positionIndex = 0; positionIndex < model.Positions.Length; positionIndex++) {
                float3 position = model.Positions[positionIndex];
                minX = Math.Min(minX, position.X);
                minY = Math.Min(minY, position.Y);
                maxX = Math.Max(maxX, position.X);
                maxY = Math.Max(maxY, position.Y);
            }
        }
    }
}
