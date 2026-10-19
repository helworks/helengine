using helengine;
using helengine.core.tests.managers.rendering;
using Xunit;

namespace helengine.core.tests {
    /// <summary>
    /// Verifies native-pixel text keeps its outline and shadow copies on whole target pixels after viewport scaling.
    /// </summary>
    /// <remarks>
    /// The demodisc menu authors an outline of 2 on a 1280x720 canvas. On the PS2's 640x448 framebuffer that scaled to
    /// 1.244 pixels, which the GS quantized into a one-pixel left/top and two-pixel right/bottom outline.
    /// </remarks>
    public sealed class TextNativePixelEffectSnapTests {
        /// <summary>
        /// Ensures each mode rounds fractional radii its own way, enabled outlines never vanish and disabled outlines stay disabled.
        /// </summary>
        /// <param name="mode">Native-pixel rounding mode.</param>
        /// <param name="scaled">Outline radius after viewport scaling.</param>
        /// <param name="expected">Whole-pixel radius the mode must produce.</param>
        [Theory]
        [InlineData(TextNativePixelMode.RoundNearest, 1.2444445f, 1f)]
        [InlineData(TextNativePixelMode.RoundNearest, 1.5f, 2f)]
        [InlineData(TextNativePixelMode.RoundNearest, 0.3f, 1f)]
        [InlineData(TextNativePixelMode.RoundDown, 1.9f, 1f)]
        [InlineData(TextNativePixelMode.RoundDown, 0.3f, 1f)]
        [InlineData(TextNativePixelMode.RoundUp, 1.2444445f, 2f)]
        [InlineData(TextNativePixelMode.RoundUp, 2f, 2f)]
        [InlineData(TextNativePixelMode.RoundUp, 0f, 0f)]
        public void SnapOutlineScale_RoundsToWholePixelsPerMode(TextNativePixelMode mode, float scaled, float expected) {
            Assert.Equal(expected, TextNativePixelEffectUtils.SnapOutlineScale(scaled, mode));
        }

        /// <summary>
        /// Ensures snapping refuses the disabled mode instead of silently picking a rounding rule.
        /// </summary>
        [Fact]
        public void SnapOutlineScale_WithDisabledMode_Throws() {
            Assert.Throws<ArgumentOutOfRangeException>(() => TextNativePixelEffectUtils.SnapOutlineScale(1.5f, TextNativePixelMode.Disabled));
        }

        /// <summary>
        /// Ensures each shadow axis keeps its direction, stays at least one pixel away and leaves a zero axis untouched.
        /// </summary>
        [Fact]
        public void SnapShadowOffset_RoundsEachAxisAndKeepsDirection() {
            float2 snapped = TextNativePixelEffectUtils.SnapShadowOffset(new float2(-0.62f, 1.24f), TextNativePixelMode.RoundNearest);

            Assert.Equal(-1f, snapped.X);
            Assert.Equal(1f, snapped.Y);
            Assert.Equal(0f, TextNativePixelEffectUtils.SnapShadowOffset(new float2(0f, 2.4f), TextNativePixelMode.RoundNearest).X);
        }

        /// <summary>
        /// Ensures a stretched viewport snaps the menu's effects for native-pixel text and leaves scaled text fractional.
        /// </summary>
        /// <param name="mode">Native-pixel mode authored on the text.</param>
        /// <param name="expectedOutline">Outline radius expected on the 640x448 target.</param>
        [Theory]
        [InlineData(TextNativePixelMode.RoundNearest, 1f)]
        [InlineData(TextNativePixelMode.RoundUp, 2f)]
        [InlineData(TextNativePixelMode.Disabled, 1.2444445f)]
        public void ViewportSnapshot_OnPs2Framebuffer_SnapsOnlyNativePixelText(TextNativePixelMode mode, float expectedOutline) {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            TextComponent text = CreateMenuText(core, mode, out Entity entity);
            ViewportLayoutCache snapshot = ViewportLayoutCache.Capture(entity);

            snapshot.Apply(ViewportLayoutMath.Resolve(2, new int2(1280, 720), new int2(640, 448), 1, 1, 0, 0));
            snapshot.Release();

            Assert.Equal((double)(expectedOutline),(double)(text.OutlineScale), 5);
        }

        /// <summary>
        /// Ensures the reference-canvas fit path applies the same native-pixel snapping as the viewport path.
        /// </summary>
        [Fact]
        public void SharedFitCache_OnPs2Framebuffer_SnapsNativePixelText() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            TextComponent text = CreateMenuText(core, TextNativePixelMode.RoundNearest, out Entity entity);
            ViewportLayoutCache snapshot = ViewportLayoutCache.Capture(entity);

            snapshot.Apply(ViewportLayoutMath.Resolve(1, new int2(1280, 720), new int2(640, 448), 1, 1, 0, 0));
            snapshot.Release();

            Assert.Equal(1f, text.OutlineScale);
            Assert.Equal(-1f, text.ShadowOffset.X);
            Assert.Equal(1f, text.ShadowOffset.Y);
        }

        /// <summary>
        /// Creates an initialized core that can own the detached test entity.
        /// </summary>
        /// <param name="renderer">Recording 2D renderer used by the core.</param>
        /// <returns>The caller-owned core.</returns>
        static Core CreateCore(Batch2DTestRenderManager renderer) {
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory) });
            core.Initialize(null, renderer, null, new PlatformInfo("test", "test"));
            return core;
        }

        /// <summary>
        /// Builds menu-style text with the authored outline of 2 and a small diagonal shadow on a detached entity.
        /// </summary>
        /// <param name="core">Core that owns the entity.</param>
        /// <param name="mode">Native-pixel mode authored on the text.</param>
        /// <param name="entity">Receives the entity that owns the text.</param>
        /// <returns>The attached text component.</returns>
        static TextComponent CreateMenuText(Core core, TextNativePixelMode mode, out Entity entity) {
            entity = new Entity(core);
            entity.InitComponents();
            entity.InitChildren();
            TextComponent text = new TextComponent {
                OutlineScale = 2f,
                ShadowOffset = new float2(-1f, 2f),
                NativeFontPixels = mode
            };
            entity.AddComponent(text);
            return text;
        }
    }
}
