using helengine;

namespace helengine.core.tests.managers.rendering {
    /// <summary>
    /// Verifies the shared pixel-space geometry and camera projection conventions.
    /// </summary>
    public sealed class Batch2DGeometryTests {
        /// <summary>
        /// Confirms the camera maps a viewport with a nonzero origin to the full clip-space rectangle.
        /// </summary>
        [Fact]
        public void CreateProjection_WhenViewportHasOffset_MapsItsCornersToClipSpace() {
            float4x4 projection = Batch2DCameraProjection.Create(new float4(10f, 20f, 30f, 40f));

            Assert.Equal(-1f, projection.M41 + 10f * projection.M11, 5);
            Assert.Equal(1f, projection.M42 + 20f * projection.M22, 5);
            Assert.Equal(1f, projection.M41 + 40f * projection.M11, 5);
            Assert.Equal(-1f, projection.M42 + 60f * projection.M22, 5);
        }

        /// <summary>
        /// Rejects a finite input viewport whose projection coefficients cannot be represented as finite floats.
        /// </summary>
        [Fact]
        public void CreateProjection_WhenViewportProducesInfiniteCoefficients_ThrowsArgumentException() {
            Assert.Throws<ArgumentException>(() => Batch2DCameraProjection.Create(
                new float4(float.MaxValue, 0f, float.Epsilon, 1f)));
        }

        /// <summary>
        /// Confirms a positive quarter turn preserves the DirectX Y-up shader rotation when expressed in Y-down pixels.
        /// </summary>
        [Fact]
        public void CreateQuad_WhenRotatedNinetyDegrees_UsesDirectXScreenSpaceSign() {
            Batch2DGeometry.CreateQuad(10d, 20d, 30d, 40d, 0d, Math.PI / 2d,
                new float4(0f, 0f, 1f, 1f), new float4(1f, 1f, 1f, 1f),
                out Batch2DVertex topLeft, out Batch2DVertex topRight,
                out Batch2DVertex bottomRight, out Batch2DVertex bottomLeft);

            Assert.Equal(5f, topLeft.Position.X, 4);
            Assert.Equal(55f, topLeft.Position.Y, 4);
            Assert.Equal(5f, topRight.Position.X, 4);
            Assert.Equal(25f, topRight.Position.Y, 4);
            Assert.Equal(0f, topLeft.TexLocal.Z);
            Assert.Equal(0f, bottomRight.TexLocal.W);
            Assert.Equal(0f, bottomLeft.Shape.X);
        }

        /// <summary>
        /// Confirms a borderless plain fill keeps a zero border width and straight-alpha fill channels.
        /// </summary>
        [Fact]
        public void CreateRoundedQuad_WhenBorderIsZero_StoresPlainStraightAlphaFill() {
            Batch2DGeometry.CreateRoundedQuad(0d, 0d, 20d, 10d, 0d, 0d, 0d, 0d,
                new float4(0f, 0f, 0f, 0f), new float4(0.25f, 0.5f, 0.75f, 0.5f),
                new float4(0f, 0f, 0f, 0f), out Batch2DVertex topLeft,
                out Batch2DVertex topRight, out Batch2DVertex bottomRight,
                out Batch2DVertex bottomLeft);

            Assert.Equal(0f, topLeft.Shape.W);
            Assert.Equal(0.5f, topLeft.Color.W);
            Assert.Equal(topLeft.Color, topRight.Color);
            Assert.Equal(topLeft.Color, bottomRight.Color);
            Assert.Equal(topLeft.Color, bottomLeft.Color);
        }

        /// <summary>
        /// Rejects non-finite radius inputs before finite clamping could hide their invalid values.
        /// </summary>
        [Theory]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        [InlineData(float.NegativeInfinity)]
        public void CreateRoundedQuad_WhenRadiusIsNonFinite_ThrowsArgumentException(float radius) {
            Assert.Throws<ArgumentException>(() => Batch2DGeometry.CreateRoundedQuad(
                0d, 0d, 20d, 10d, 0d, 0d, radius, 0d,
                new float4(1f, 1f, 1f, 1f), new float4(1f, 1f, 1f, 1f),
                new float4(0f, 0f, 0f, 0f), out Batch2DVertex topLeft,
                out Batch2DVertex topRight, out Batch2DVertex bottomRight,
                out Batch2DVertex bottomLeft));
        }

        /// <summary>
        /// Rejects non-finite border inputs before finite clamping could hide their invalid values.
        /// </summary>
        [Theory]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        [InlineData(float.NegativeInfinity)]
        public void CreateRoundedQuad_WhenBorderWidthIsNonFinite_ThrowsArgumentException(float borderWidth) {
            Assert.Throws<ArgumentException>(() => Batch2DGeometry.CreateRoundedQuad(
                0d, 0d, 20d, 10d, 0d, 0d, 0d, borderWidth,
                new float4(1f, 1f, 1f, 1f), new float4(1f, 1f, 1f, 1f),
                new float4(0f, 0f, 0f, 0f), out Batch2DVertex topLeft,
                out Batch2DVertex topRight, out Batch2DVertex bottomRight,
                out Batch2DVertex bottomLeft));
        }

        /// <summary>
        /// Keeps finite negative radius and border values as zero after the explicit finite check.
        /// </summary>
        [Fact]
        public void CreateRoundedQuad_WhenRadiusAndBorderAreFiniteNegative_ClampsThemToZero() {
            Batch2DGeometry.CreateRoundedQuad(0d, 0d, 20d, 10d, 0d, 0d, -1d, -2d,
                new float4(1f, 1f, 1f, 1f), new float4(1f, 1f, 1f, 1f),
                new float4(0f, 0f, 0f, 0f), out Batch2DVertex topLeft,
                out Batch2DVertex topRight, out Batch2DVertex bottomRight,
                out Batch2DVertex bottomLeft);

            Assert.Equal(0f, topLeft.Shape.Z);
            Assert.Equal(0f, topLeft.Shape.W);
        }
    }
}
