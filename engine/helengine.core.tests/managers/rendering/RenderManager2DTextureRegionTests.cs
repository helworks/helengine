using System.Reflection;
using helengine;

namespace helengine.core.tests.managers.rendering {
    /// <summary>
    /// Verifies the common validation and forwarding contract for runtime texture-region updates.
    /// </summary>
    public sealed class RenderManager2DTextureRegionTests {
        [Fact]
        public void UpdateTextureRegion_WhenTextureIsNull_ThrowsArgumentNullException() {
            using RecordingRenderManager2D renderer = new RecordingRenderManager2D();

            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
                () => renderer.UpdateTextureRegion(null, 0, 0, 1, 1, new byte[4], 4));

            Assert.Equal("texture", exception.ParamName);
            AssertNoHookEntry(renderer);
        }

        [Fact]
        public void UpdateTextureRegion_WhenTextureIsDisposed_ThrowsObjectDisposedException() {
            using RecordingRenderManager2D renderer = new RecordingRenderManager2D();
            RuntimeTexture texture = renderer.CreateTexture(1, 1);
            texture.Dispose();

            ObjectDisposedException exception = Assert.Throws<ObjectDisposedException>(
                () => renderer.UpdateTextureRegion(texture, 0, 0, 1, 1, new byte[4], 4));

            Assert.Equal("texture", exception.ObjectName);
            AssertNoHookEntry(renderer);
        }

        [Fact]
        public void UpdateTextureRegion_WhenSourceDataIsNull_ThrowsArgumentNullException() {
            using RecordingRenderManager2D renderer = new RecordingRenderManager2D();
            RuntimeTexture texture = renderer.CreateTexture(1, 1);

            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
                () => renderer.UpdateTextureRegion(texture, 0, 0, 1, 1, null, 4));

            Assert.Equal("rgba8", exception.ParamName);
            AssertNoHookEntry(renderer);
        }

        [Fact]
        public void UpdateTextureRegion_WhenTextureBelongsToDifferentRenderer_ThrowsArgumentException() {
            using RecordingRenderManager2D renderer = new RecordingRenderManager2D();
            using RecordingRenderManager2D foreignRenderer = new RecordingRenderManager2D();
            RuntimeTexture foreignTexture = foreignRenderer.CreateTexture(1, 1);

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => renderer.UpdateTextureRegion(foreignTexture, 0, 0, 1, 1, new byte[4], 4));

            Assert.Equal("texture", exception.ParamName);
            Assert.Equal(1, renderer.UpdateTextureRegionAttemptCount);
            Assert.Equal(0, renderer.UpdateTextureRegionCallCount);
            AssertNoHookEntry(foreignRenderer);
        }

        [Theory]
        [InlineData(0, 1, "width")]
        [InlineData(1, 0, "height")]
        [InlineData(-1, 1, "width")]
        [InlineData(1, -1, "height")]
        public void UpdateTextureRegion_WhenDimensionsAreNotPositive_ThrowsArgumentOutOfRangeException(
            int width,
            int height,
            string parameterName) {
            using RecordingRenderManager2D renderer = new RecordingRenderManager2D();
            RuntimeTexture texture = renderer.CreateTexture(4, 4);

            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => renderer.UpdateTextureRegion(texture, 0, 0, width, height, new byte[4], 4));

            Assert.Equal(parameterName, exception.ParamName);
            AssertNoHookEntry(renderer);
        }

        [Theory]
        [InlineData(-1, 0, "x")]
        [InlineData(0, -1, "y")]
        public void UpdateTextureRegion_WhenOriginIsNegative_ThrowsArgumentOutOfRangeException(
            int x,
            int y,
            string parameterName) {
            using RecordingRenderManager2D renderer = new RecordingRenderManager2D();
            RuntimeTexture texture = renderer.CreateTexture(4, 4);

            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => renderer.UpdateTextureRegion(texture, x, y, 1, 1, new byte[4], 4));

            Assert.Equal(parameterName, exception.ParamName);
            AssertNoHookEntry(renderer);
        }

        [Theory]
        [InlineData(4, 0, 1, 1, "x")]
        [InlineData(0, 4, 1, 1, "y")]
        [InlineData(3, 0, 2, 1, "width")]
        [InlineData(0, 3, 1, 2, "height")]
        public void UpdateTextureRegion_WhenRectangleExceedsTextureBounds_ThrowsArgumentOutOfRangeException(
            int x,
            int y,
            int width,
            int height,
            string parameterName) {
            using RecordingRenderManager2D renderer = new RecordingRenderManager2D();
            RuntimeTexture texture = renderer.CreateTexture(4, 4);

            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => renderer.UpdateTextureRegion(texture, x, y, width, height, new byte[8], 8));

            Assert.Equal(parameterName, exception.ParamName);
            AssertNoHookEntry(renderer);
        }

        [Theory]
        [InlineData(int.MaxValue, 0, 1, 1, "x")]
        [InlineData(0, int.MaxValue, 1, 1, "y")]
        [InlineData(1, 0, int.MaxValue, 1, "width")]
        [InlineData(0, 1, 1, int.MaxValue, "height")]
        public void UpdateTextureRegion_WhenRectangleArithmeticWouldOverflow_ThrowsArgumentOutOfRangeException(
            int x,
            int y,
            int width,
            int height,
            string parameterName) {
            using RecordingRenderManager2D renderer = new RecordingRenderManager2D();
            RuntimeTexture texture = renderer.CreateTexture(4, 4);

            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => renderer.UpdateTextureRegion(texture, x, y, width, height, new byte[4], 4));

            Assert.Equal(parameterName, exception.ParamName);
            AssertNoHookEntry(renderer);
        }

        [Fact]
        public void UpdateTextureRegion_WhenRequiredRowBytesOverflow_ThrowsArgumentOutOfRangeException() {
            using RecordingRenderManager2D renderer = new RecordingRenderManager2D();
            RuntimeTexture texture = renderer.CreateTexture(int.MaxValue, 1);

            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => renderer.UpdateTextureRegion(texture, 0, 0, int.MaxValue, 1, Array.Empty<byte>(), int.MaxValue));

            Assert.Equal("width", exception.ParamName);
            AssertNoHookEntry(renderer);
        }

        [Fact]
        public void UpdateTextureRegion_WhenRequiredBytesOverflow_ThrowsArgumentOutOfRangeException() {
            using RecordingRenderManager2D renderer = new RecordingRenderManager2D();
            RuntimeTexture texture = renderer.CreateTexture(int.MaxValue, int.MaxValue);
            int sourceRowPitch = int.MaxValue - 3;

            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => renderer.UpdateTextureRegion(
                    texture,
                    0,
                    0,
                    1,
                    int.MaxValue,
                    Array.Empty<byte>(),
                    sourceRowPitch));

            Assert.Equal("sourceRowPitch", exception.ParamName);
            AssertNoHookEntry(renderer);
        }

        [Fact]
        public void UpdateTextureRegion_WhenSourceRowPitchIsBelowRequiredRowBytes_ThrowsArgumentOutOfRangeException() {
            using RecordingRenderManager2D renderer = new RecordingRenderManager2D();
            RuntimeTexture texture = renderer.CreateTexture(2, 1);

            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => renderer.UpdateTextureRegion(texture, 0, 0, 2, 1, new byte[7], 7));

            Assert.Equal("sourceRowPitch", exception.ParamName);
            AssertNoHookEntry(renderer);
        }

        [Fact]
        public void UpdateTextureRegion_WhenSourceRowPitchIsNotDivisibleByFour_ThrowsArgumentException() {
            using RecordingRenderManager2D renderer = new RecordingRenderManager2D();
            RuntimeTexture texture = renderer.CreateTexture(2, 1);

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => renderer.UpdateTextureRegion(texture, 0, 0, 2, 1, new byte[9], 9));

            Assert.Equal("sourceRowPitch", exception.ParamName);
            AssertNoHookEntry(renderer);
        }

        [Fact]
        public void UpdateTextureRegion_WhenSourceDataIsShort_ThrowsArgumentException() {
            using RecordingRenderManager2D renderer = new RecordingRenderManager2D();
            RuntimeTexture texture = renderer.CreateTexture(2, 2);

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => renderer.UpdateTextureRegion(texture, 0, 0, 2, 2, new byte[15], 8));

            Assert.Equal("rgba8", exception.ParamName);
            AssertNoHookEntry(renderer);
        }

        [Fact]
        public void UpdateTextureRegion_WhenPaddedSourceDataIsShort_ThrowsArgumentException() {
            using RecordingRenderManager2D renderer = new RecordingRenderManager2D();
            RuntimeTexture texture = renderer.CreateTexture(2, 2);

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => renderer.UpdateTextureRegion(texture, 0, 0, 2, 2, new byte[19], 12));

            Assert.Equal("rgba8", exception.ParamName);
            AssertNoHookEntry(renderer);
        }

        [Fact]
        public void UpdateTextureRegion_WhenSourceDataIsTightlyPacked_DispatchesExactArgumentsOnce() {
            using RecordingRenderManager2D renderer = new RecordingRenderManager2D();
            RuntimeTexture texture = renderer.CreateTexture(4, 4);
            byte[] pixels = new byte[16];

            renderer.UpdateTextureRegion(texture, 1, 1, 2, 2, pixels, 8);

            AssertRecordedCall(renderer, texture, 1, 1, 2, 2, pixels, 8);
        }

        [Fact]
        public void UpdateTextureRegion_WhenSourceDataHasPaddedRows_DispatchesExactArgumentsOnce() {
            using RecordingRenderManager2D renderer = new RecordingRenderManager2D();
            RuntimeTexture texture = renderer.CreateTexture(4, 4);
            byte[] pixels = new byte[20];

            renderer.UpdateTextureRegion(texture, 1, 1, 2, 2, pixels, 12);

            AssertRecordedCall(renderer, texture, 1, 1, 2, 2, pixels, 12);
        }

        [Fact]
        public void UpdateTextureRegion_WhenRegionEndsAtTextureEdges_DispatchesExactArgumentsOnce() {
            using RecordingRenderManager2D renderer = new RecordingRenderManager2D();
            RuntimeTexture texture = renderer.CreateTexture(4, 4);
            byte[] pixels = new byte[16];

            renderer.UpdateTextureRegion(texture, 2, 2, 2, 2, pixels, 8);

            AssertRecordedCall(renderer, texture, 2, 2, 2, 2, pixels, 8);
        }

        [Fact]
        public void UpdateTextureRegion_Rgba8ParametersHaveNativeNoEscapeAttribute() {
            MethodInfo publicMethod = typeof(RenderManager2D).GetMethod(
                "UpdateTextureRegion",
                BindingFlags.Instance | BindingFlags.Public);
            MethodInfo coreMethod = typeof(RenderManager2D).GetMethod(
                "UpdateTextureRegionCore",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(publicMethod);
            Assert.NotNull(coreMethod);
            AssertNativeNoEscapeOnRgba8(publicMethod);
            AssertNativeNoEscapeOnRgba8(coreMethod);
        }

        static void AssertNativeNoEscapeOnRgba8(MethodInfo method) {
            ParameterInfo rgba8Parameter = Assert.Single(
                method.GetParameters(),
                parameter => parameter.Name == "rgba8");

            Assert.True(rgba8Parameter.IsDefined(typeof(NativeNoEscapeAttribute), false));
        }

        static void AssertRecordedCall(
            RecordingRenderManager2D renderer,
            RuntimeTexture texture,
            int x,
            int y,
            int width,
            int height,
            byte[] pixels,
            int sourceRowPitch) {
            Assert.Equal(1, renderer.UpdateTextureRegionAttemptCount);
            Assert.Equal(1, renderer.UpdateTextureRegionCallCount);
            Assert.Same(texture, renderer.LastTexture);
            Assert.Equal(x, renderer.LastX);
            Assert.Equal(y, renderer.LastY);
            Assert.Equal(width, renderer.LastWidth);
            Assert.Equal(height, renderer.LastHeight);
            Assert.Same(pixels, renderer.LastPixels);
            Assert.Equal(sourceRowPitch, renderer.LastSourceRowPitch);
        }

        static void AssertNoHookEntry(RecordingRenderManager2D renderer) {
            Assert.Equal(0, renderer.UpdateTextureRegionAttemptCount);
            Assert.Equal(0, renderer.UpdateTextureRegionCallCount);
        }

        sealed class RecordingRenderManager2D : RenderManager2D {
            readonly HashSet<RuntimeTexture> OwnedTextures = new HashSet<RuntimeTexture>();

            public int UpdateTextureRegionAttemptCount { get; private set; }

            public int UpdateTextureRegionCallCount { get; private set; }

            public RuntimeTexture LastTexture { get; private set; }

            public int LastX { get; private set; }

            public int LastY { get; private set; }

            public int LastWidth { get; private set; }

            public int LastHeight { get; private set; }

            public byte[] LastPixels { get; private set; }

            public int LastSourceRowPitch { get; private set; }

            public RuntimeTexture CreateTexture(int width, int height) {
                RuntimeTexture texture = new TestRuntimeTexture {
                    Width = width,
                    Height = height
                };
                OwnedTextures.Add(texture);
                return texture;
            }

            protected override void UpdateTextureRegionCore(
                RuntimeTexture texture,
                int x,
                int y,
                int width,
                int height,
                [NativeNoEscape] byte[] rgba8,
                int sourceRowPitch) {
                UpdateTextureRegionAttemptCount++;
                if (!OwnedTextures.Contains(texture)) {
                    throw new ArgumentException("Texture was not created by this renderer.", nameof(texture));
                }

                UpdateTextureRegionCallCount++;
                LastTexture = texture;
                LastX = x;
                LastY = y;
                LastWidth = width;
                LastHeight = height;
                LastPixels = rgba8;
                LastSourceRowPitch = sourceRowPitch;
            }

            public override RuntimeTexture BuildTextureFromRaw(TextureAsset data) {
                throw new NotSupportedException();
            }

            public override void DrawSprite(ISpriteDrawable2D sprite) {
            }

            public override void DrawText(ITextDrawable2D text) {
            }

            public override void DrawRoundedRect(IRoundedRectDrawable2D shape) {
            }
        }

        sealed class TestRuntimeTexture : RuntimeTexture {
        }
    }
}
