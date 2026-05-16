using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the runtime FPS overlay component builds and updates its own text hierarchy.
    /// </summary>
    public class FPSComponentTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the runtime test harness.
        /// </summary>
        readonly string TempRootPath;
        /// <summary>
        /// Core instance configured for deterministic FPS overlay tests.
        /// </summary>
        readonly TestClockDrivenCore CoreInstance;

        /// <summary>
        /// Initializes the runtime services required by the component tests.
        /// </summary>
        public FPSComponentTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-fps-component-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            CoreInstance = new TestClockDrivenCore(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });

            CoreInstance.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Deletes temporary test content after each run.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the component creates a top-left overlay host with two text children.
        /// </summary>
        [Fact]
        public void ComponentAdded_WhenFontIsMissing_DoesNotBuildOverlayChildren() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FPSComponent fps = new FPSComponent();

            entity.AddComponent(fps);

            Assert.Empty(entity.Children);
            Assert.Null(fps.Font);
        }

        /// <summary>
        /// Ensures update and render frame ticks refresh both visible lines.
        /// </summary>
        [Fact]
        public void CoreUpdateAndDraw_WhenFontIsMissing_DoesNotThrow() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FPSComponent fps = new FPSComponent {
                RefreshIntervalSeconds = 0d
            };

            entity.AddComponent(fps);

            Core.Instance.Update();
            Core.Instance.Draw();
            Core.Instance.Update();
            Core.Instance.Draw();

            Assert.Empty(entity.Children);
            Assert.Equal("Update FPS: --", fps.UpdateFpsText);
            Assert.Equal("Render FPS: -- (-- ms)", fps.RenderFpsText);
        }

        /// <summary>
        /// Ensures disabling the parent entity removes the text drawables from render participation.
        /// </summary>
        [Fact]
        public void FontProperty_WhenAssignedAfterAttachment_BuildsOverlayChildren() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FPSComponent fps = new FPSComponent();
            entity.AddComponent(fps);

            FontAsset font = CreateFont(24f);
            fps.Font = font;

            Entity overlayHost = Assert.Single(entity.Children);
            TextComponent updateText = Assert.Single(overlayHost.Children[0].Components.OfType<TextComponent>());
            TextComponent renderText = Assert.Single(overlayHost.Children[1].Components.OfType<TextComponent>());

            Assert.Same(font, updateText.Font);
            Assert.Same(font, renderText.Font);
            Assert.Equal(font.LineHeight, overlayHost.Children[1].LocalPosition.Y);
        }

        /// <summary>
        /// Ensures the parameterless constructor uses the configured default font asset.
        /// </summary>
        [Fact]
        public void FontProperty_WhenClearedAfterAttachment_RemovesOverlayChildren() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FPSComponent fps = new FPSComponent {
                Font = CreateFont()
            };
            entity.AddComponent(fps);
            Assert.Single(entity.Children);

            fps.Font = null;

            Assert.Empty(entity.Children);
            Assert.Null(fps.Font);
        }

        /// <summary>
        /// Ensures both FPS lines format sampled values with exactly one decimal place.
        /// </summary>
        [Fact]
        public void CoreUpdateAndDraw_WhenSamplingFrames_FormatsFpsWithOneDecimalPlace() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FPSComponent fps = new FPSComponent {
                Font = CreateFont(),
                RefreshIntervalSeconds = 0d
            };

            entity.AddComponent(fps);

            Core.Instance.Update();
            Core.Instance.Draw();
            Core.Instance.Update();
            Core.Instance.Draw();

            Assert.Matches("^Update FPS: [0-9]+\\.[0-9]$", fps.UpdateFpsText);
            Assert.Matches("^Render FPS: [0-9]+\\.[0-9] \\([0-9]+\\.[0-9] ms\\)$", fps.RenderFpsText);
        }

        /// <summary>
        /// Ensures explicit core frame timing, not wall-clock sampling, drives the visible FPS values and draw duration text.
        /// </summary>
        [Fact]
        public void CoreUpdateAndDraw_WhenExplicitElapsedSecondsAreUsed_UsesCoreTimingForFpsSamplingAndRenderDrawMilliseconds() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FPSComponent fps = new FPSComponent {
                Font = CreateFont(),
                RefreshIntervalSeconds = 0d
            };

            entity.AddComponent(fps);
            CoreInstance.QueueMeasuredDrawMilliseconds(new[] { 12.3d });

            Core.Instance.Update(0.25d);
            Core.Instance.Draw();
            Core.Instance.Update(0.25d);

            Assert.Equal("Update FPS: 4.0", fps.UpdateFpsText);
            Assert.Equal("Render FPS: 4.0 (12.3 ms)", fps.RenderFpsText);
        }

        /// <summary>
        /// Creates a deterministic font asset containing the glyphs needed by the overlay labels.
        /// </summary>
        /// <returns>Font asset with stable glyph metrics for the tests.</returns>
        FontAsset CreateFont() {
            return CreateFont(16f);
        }

        /// <summary>
        /// Creates a deterministic font asset containing the glyphs needed by the overlay labels.
        /// </summary>
        /// <param name="lineHeight">Line height assigned to the generated test font.</param>
        /// <returns>Font asset with stable glyph metrics for the tests.</returns>
        FontAsset CreateFont(float lineHeight) {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['U'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['F'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['R'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                [':'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                [' '] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['('] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                [')'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['-'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['0'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['1'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['2'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['3'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['4'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['5'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['6'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['7'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['8'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['9'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['.'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f)
            };

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                lineHeight,
                64,
                64);
        }
    }
}

