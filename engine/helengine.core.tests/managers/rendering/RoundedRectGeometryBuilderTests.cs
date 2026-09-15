using helengine;

namespace helengine.core.tests.managers.rendering {
    /// <summary>
    /// Verifies that <see cref="RoundedRectGeometryBuilder"/> reproduces the rounded-rect boundary
    /// and nine-slice tile geometry previously computed inline (with <c>MathF</c>-based local
    /// functions) by <c>DirectX11Renderer2D</c>. Each test implements the original single-precision
    /// formulas as an independent oracle and compares them against the builder's double-precision
    /// output across a spread of sizes, radii, and segment counts.
    /// </summary>
    public sealed class RoundedRectGeometryBuilderTests {
        /// <summary>
        /// Maximum allowed absolute difference between the builder's output and the oracle's
        /// output, accounting only for float-rounding of the builder's double-precision math. At
        /// the shape sizes exercised here (up to a few hundred units), a single float32 ULP is
        /// already close to 2e-5, so this allows a couple of ULPs of accumulated rounding without
        /// masking a real formula mismatch, which manifests as differences many orders of
        /// magnitude larger.
        /// </summary>
        const float Tolerance = 5e-5f;

        [Theory]
        [InlineData(100f, 60f, 12f, 0f, 0f, 32)]
        [InlineData(200f, 200f, 40f, 100f, 100f, 32)]
        [InlineData(50f, 120f, 10f, -25f, 60f, 16)]
        [InlineData(1f, 1f, 0.4f, 0f, 0f, 8)]
        [InlineData(300f, 80f, 40f, 12.5f, -8.25f, 64)]
        public void RingPointAt_ForSpreadOfShapes_MatchesOracle(float width, float height, float radius, float centerX, float centerY, int steps) {
            RoundedRectGeometryBuilder builder = new RoundedRectGeometryBuilder();

            for (int i = 0; i < steps; i++) {
                float angle = (i / (float)steps) * MathF.PI * 2.0f;

                float2 expected = OracleOuterAt(angle, width, height, radius, centerX, centerY);
                float2 actual = builder.RingPointAt(angle, width, height, radius, centerX, centerY);

                AssertClose(expected, actual, width, height, radius, centerX, centerY, angle);
            }
        }

        [Theory]
        [InlineData(100f, 60f, 12f, 0f, 0f, 32)]
        [InlineData(200f, 200f, 40f, 100f, 100f, 32)]
        [InlineData(50f, 120f, 10f, -25f, 60f, 16)]
        [InlineData(300f, 80f, 40f, 12.5f, -8.25f, 64)]
        public void BuildFillRingVertices_ForSpreadOfShapes_MatchesOracle(float width, float height, float radius, float centerX, float centerY, int steps) {
            RoundedRectGeometryBuilder builder = new RoundedRectGeometryBuilder();

            float2[] actual = builder.BuildFillRingVertices(steps, width, height, radius, centerX, centerY);

            Assert.Equal(steps * 3, actual.Length);

            for (int i = 0; i < steps; i++) {
                float angle0 = (i / (float)steps) * MathF.PI * 2.0f;
                float angle1 = ((i + 1) % steps) / (float)steps * MathF.PI * 2.0f;

                float2 expectedOuter0 = OracleOuterAt(angle0, width, height, radius, centerX, centerY);
                float2 expectedOuter1 = OracleOuterAt(angle1, width, height, radius, centerX, centerY);

                int baseIndex = i * 3;
                AssertClose(new float2(centerX, centerY), actual[baseIndex], width, height, radius, centerX, centerY, angle0);
                AssertClose(expectedOuter0, actual[baseIndex + 1], width, height, radius, centerX, centerY, angle0);
                AssertClose(expectedOuter1, actual[baseIndex + 2], width, height, radius, centerX, centerY, angle1);
            }
        }

        [Theory]
        [InlineData(100f, 60f, 12f, 6f, 0f, 0f, 32)]
        [InlineData(200f, 200f, 40f, 15f, 100f, 100f, 32)]
        [InlineData(50f, 120f, 10f, 3f, -25f, 60f, 16)]
        [InlineData(300f, 80f, 40f, 40f, 12.5f, -8.25f, 64)]
        public void BuildBorderRingVertices_ForSpreadOfShapes_MatchesOracle(float width, float height, float radius, float borderThickness, float centerX, float centerY, int steps) {
            RoundedRectGeometryBuilder builder = new RoundedRectGeometryBuilder();

            float innerRadius = Math.Max(0f, radius - borderThickness);
            float innerWidth = Math.Max(0f, width - borderThickness * 2f);
            float innerHeight = Math.Max(0f, height - borderThickness * 2f);

            float2[] actual = builder.BuildBorderRingVertices(steps, width, height, radius, innerWidth, innerHeight, innerRadius, centerX, centerY);

            Assert.Equal(steps * 6, actual.Length);

            for (int i = 0; i < steps; i++) {
                float angle0 = (i / (float)steps) * MathF.PI * 2.0f;
                float angle1 = ((i + 1) % steps) / (float)steps * MathF.PI * 2.0f;

                float2 outer0 = OracleOuterAt(angle0, width, height, radius, centerX, centerY);
                float2 outer1 = OracleOuterAt(angle1, width, height, radius, centerX, centerY);
                float2 inner0 = OracleOuterAt(angle0, innerWidth, innerHeight, innerRadius, centerX, centerY);
                float2 inner1 = OracleOuterAt(angle1, innerWidth, innerHeight, innerRadius, centerX, centerY);

                int baseIndex = i * 6;
                AssertClose(outer0, actual[baseIndex], width, height, radius, centerX, centerY, angle0);
                AssertClose(outer1, actual[baseIndex + 1], width, height, radius, centerX, centerY, angle1);
                AssertClose(inner1, actual[baseIndex + 2], width, height, radius, centerX, centerY, angle1);
                AssertClose(outer0, actual[baseIndex + 3], width, height, radius, centerX, centerY, angle0);
                AssertClose(inner1, actual[baseIndex + 4], width, height, radius, centerX, centerY, angle1);
                AssertClose(inner0, actual[baseIndex + 5], width, height, radius, centerX, centerY, angle0);
            }
        }

        [Theory]
        [InlineData(0f, 0f, 100f, 60f, 12)]
        [InlineData(10f, 20f, 200f, 200f, 40)]
        [InlineData(-15f, 8f, 50f, 120f, 10)]
        [InlineData(5f, 5f, 21f, 21f, 8)]
        public void BuildNineSliceTileRects_ForSpreadOfShapes_MatchesOracle(float x, float y, float width, float height, int cornerSize) {
            RoundedRectGeometryBuilder builder = new RoundedRectGeometryBuilder();

            float4[] actual = builder.BuildNineSliceTileRects(x, y, width, height, cornerSize);
            float4[] expected = OracleNineSliceTileRects(x, y, width, height, cornerSize);

            Assert.Equal(9, actual.Length);
            for (int i = 0; i < 9; i++) {
                Assert.True(MathF.Abs(expected[i].X - actual[i].X) <= Tolerance, $"tile {i} X mismatch: expected {expected[i].X}, actual {actual[i].X}");
                Assert.True(MathF.Abs(expected[i].Y - actual[i].Y) <= Tolerance, $"tile {i} Y mismatch: expected {expected[i].Y}, actual {actual[i].Y}");
                Assert.True(MathF.Abs(expected[i].Z - actual[i].Z) <= Tolerance, $"tile {i} width mismatch: expected {expected[i].Z}, actual {actual[i].Z}");
                Assert.True(MathF.Abs(expected[i].W - actual[i].W) <= Tolerance, $"tile {i} height mismatch: expected {expected[i].W}, actual {actual[i].W}");
            }
        }

        /// <summary>
        /// Asserts that two boundary points match within <see cref="Tolerance"/>, printing the
        /// input parameters on failure to make a mismatch easy to diagnose.
        /// </summary>
        static void AssertClose(float2 expected, float2 actual, float width, float height, float radius, float centerX, float centerY, float angle) {
            string context = $"width={width}, height={height}, radius={radius}, centerX={centerX}, centerY={centerY}, angle={angle}";
            Assert.True(MathF.Abs(expected.X - actual.X) <= Tolerance, $"X mismatch: expected {expected.X}, actual {actual.X} ({context})");
            Assert.True(MathF.Abs(expected.Y - actual.Y) <= Tolerance, $"Y mismatch: expected {expected.Y}, actual {actual.Y} ({context})");
        }

        /// <summary>
        /// Oracle reimplementation of the original <c>OuterAt</c>/<c>InnerAt</c> local functions
        /// removed from <c>DirectX11Renderer2D</c>, using the exact same single-precision
        /// <c>MathF</c>-based formulas, so the builder's double-precision output can be verified
        /// against the previously shipped behavior.
        /// </summary>
        static float2 OracleOuterAt(float angle, float w, float h, float r, float cx, float cy) {
            float x = MathF.Cos(angle);
            float y = MathF.Sin(angle);
            float ox = MathF.Sign(x) * MathF.Max(MathF.Abs(w * 0.5f - r), MathF.Abs(w * 0.5f * x)) + cx;
            float oy = MathF.Sign(y) * MathF.Max(MathF.Abs(h * 0.5f - r), MathF.Abs(h * 0.5f * y)) + cy;
            if (MathF.Abs(ox - cx) > (w * 0.5f - r) && MathF.Abs(oy - cy) > (h * 0.5f - r)) {
                float cornerCx = cx + MathF.Sign(x) * (w * 0.5f - r);
                float cornerCy = cy + MathF.Sign(y) * (h * 0.5f - r);
                ox = cornerCx + r * MathF.Sign(x) * MathF.Abs(x);
                oy = cornerCy + r * MathF.Sign(y) * MathF.Abs(y);
            }
            return new float2(ox, oy);
        }

        /// <summary>
        /// Oracle reimplementation of the original <c>DrawTile</c>/<c>DrawBorderTile</c> nine-slice
        /// destination rectangle math removed from <c>DirectX11Renderer2D</c>.
        /// </summary>
        static float4[] OracleNineSliceTileRects(float x, float y, float w, float h, int s) {
            int lw = s;
            int rw = s;
            int mw = Math.Max(1, (int)w - lw - rw);
            int th = s;
            int bh = s;
            int mh = Math.Max(1, (int)h - th - bh);

            return new float4[] {
                new float4(x, y, lw, th),
                new float4(x + lw, y, mw, th),
                new float4(x + lw + mw, y, rw, th),
                new float4(x, y + th, lw, mh),
                new float4(x + lw, y + th, mw, mh),
                new float4(x + lw + mw, y + th, rw, mh),
                new float4(x, y + th + mh, lw, bh),
                new float4(x + lw, y + th + mh, mw, bh),
                new float4(x + lw + mw, y + th + mh, rw, bh)
            };
        }
    }
}
