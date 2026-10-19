using helengine;
using helengine.core.tests.managers.rendering;

namespace helengine.core.tests.components {
    /// <summary>
    /// Verifies viewport scaling keeps text effects proportional and restores authored values after resizing.
    /// </summary>
    public sealed class ViewportLayoutCacheTextTests {
        /// <summary>
        /// Keeps glyphs and effects proportional to the canvas scalar across aspect changes, repeated resizing and restoration.
        /// </summary>
        /// <param name="stretchCanvas">Whether other layout components stretch independently by axis.</param>
        /// <param name="viewportWidth">Live width available to the authored text layout.</param>
        /// <param name="viewportHeight">Live height available to the authored text layout.</param>
        /// <param name="expectedScale">Expected text and effect scale for the supplied viewport bounds.</param>
        [Theory]
        [InlineData(false, 320, 360, 0.25f)]
        [InlineData(true, 320, 360, 0.5f)]
        [InlineData(false, 640, 480, 0.5f)]
        [InlineData(true, 640, 480, 0.6666667f)]
        [InlineData(true, 1280, 720, 1f)]
        [InlineData(true, 2560, 720, 1f)]
        [InlineData(true, 640, 1440, 2f)]
        [InlineData(true, 2560, 360, 0.5f)]
        public void Apply_WhenViewportResizes_ScalesTextEffectsFromAuthoredValues(bool stretchCanvas, int viewportWidth, int viewportHeight, float expectedScale) {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, renderer, null, new PlatformInfo("test", "test-version"));
            Entity entity = new Entity(core);
            entity.InitComponents();
            TextComponent text = new TextComponent {
                Size = new int2(400, 100),
                FontScale = 2f,
                OutlineScale = 2f,
                ShadowOffset = new float2(-4f, 6f)
            };
            entity.AddComponent(text);
            ViewportLayoutCache cache = ViewportLayoutCache.Capture(entity);
            ViewportLayoutMetrics reduced = new ViewportLayoutMetrics { ScaleX = (double)viewportWidth / 1280, ScaleY = (double)viewportHeight / 720, GlyphScale = stretchCanvas ? (double)viewportHeight / 720 : DisplayAspectFitUtils.ResolveGlyphScale((double)viewportWidth / 1280, (double)viewportHeight / 720, 1) };

            double selectedExpectedScale = RealRepresentation.StorageBits == 64 && RealRepresentation.ArithmeticBits == 64
                ? (stretchCanvas ? (double)viewportHeight / 720 : Math.Min((double)viewportWidth / 1280, (double)viewportHeight / 720))
                : expectedScale;

            cache.Apply(reduced);
            cache.Apply(reduced);

            Assert.Equal(2f * selectedExpectedScale, text.FontScale);
            Assert.Equal(2f * selectedExpectedScale, text.OutlineScale);
            Assert.Equal(-4f * selectedExpectedScale, text.ShadowOffset.X);
            Assert.Equal(6f * selectedExpectedScale, text.ShadowOffset.Y);
            Assert.Equal((int)Math.Round(400d * viewportWidth / 1280d), text.Size.X);
            Assert.Equal((int)Math.Round(100d * viewportHeight / 720d), text.Size.Y);

            cache.Apply(new ViewportLayoutMetrics { ScaleX = 1, ScaleY = 1, GlyphScale = 1 });
            cache.Release();

            Assert.Equal(2f, text.FontScale);
            Assert.Equal(2f, text.OutlineScale);
            Assert.Equal(new float2(-4f, 6f), text.ShadowOffset);
            Assert.Equal(new int2(400, 100), text.Size);
        }
    }
}
