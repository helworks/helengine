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
            Assert.Single(Core.Instance.ObjectManager.Entities);
            Assert.Empty(Core.Instance.ObjectManager.Drawables2D);
        }

        /// <summary>
        /// Ensures removing the component disposes the generated overlay subtree instead of leaving orphaned overlay entities registered.
        /// </summary>
        [Fact]
        public void RemoveComponent_WhenOverlayWasBuilt_DisposesOverlayEntities() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FPSComponent fps = new FPSComponent {
                Font = CreateFont()
            };
            entity.AddComponent(fps);

            Assert.Equal(4, Core.Instance.ObjectManager.Entities.Count);
            Assert.Equal(2, Core.Instance.ObjectManager.Drawables2D.Count);

            entity.RemoveComponent(fps);

            Assert.Empty(entity.Children);
            Assert.Single(Core.Instance.ObjectManager.Entities);
            Assert.Empty(Core.Instance.ObjectManager.Drawables2D);
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
        /// Ensures the FPS component prefers core-owned overlay metrics when the active runtime publishes custom diagnostics.
        /// </summary>
        [Fact]
        public void CoreUpdateAndDraw_WhenRendererProvidesOverlayRows_UsesRendererOwnedOverlayText() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FPSComponent fps = new FPSComponent {
                Font = CreateFont(),
                RefreshIntervalSeconds = 0d
            };

            entity.AddComponent(fps);
            Core.Instance.SetPerformanceOverlayMetrics(true, 7.0d, 1.5d, 0.5d, 8.0d, 0.8d, 0.2d, 12, 3);

            Core.Instance.Update(0.25d);
            Core.Instance.Draw();
            Core.Instance.Update(0.25d);

            Assert.Equal("Upd 4.0 Set 7.0 Prep 1.5 Emit 0.5", fps.UpdateFpsText);
            Assert.Equal("Rdr 4.0 Drw 0.0 Enc 8.0 Sub 0.8 Wt 0.2 Tri 12 Disp 3", fps.RenderFpsText);
        }

        /// <summary>
        /// Ensures the built overlay resolves core-owned metrics immediately instead of leaving placeholders visible until the first sample window completes.
        /// </summary>
        [Fact]
        public void ComponentAdded_WhenRendererProvidesOverlayRows_UsesRendererOwnedTextImmediatelyAfterOverlayBuild() {
            Core.Instance.SetPerformanceOverlayMetrics(true, 0d, 0d, 0d, 0d, 0d, 0d, 0, 0);

            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FPSComponent fps = new FPSComponent {
                Font = CreateFont()
            };

            entity.AddComponent(fps);

            Assert.Equal("Upd 0.0 Set 0.0 Prep 0.0 Emit 0.0", fps.UpdateFpsText);
            Assert.Equal("Rdr 0.0 Drw 0.0 Enc 0.0 Sub 0.0 Wt 0.0 Tri 0 Disp 0", fps.RenderFpsText);
        }

        /// <summary>
        /// Ensures the PS2 runtime always uses the performance-overlay row format even before the renderer publishes its first explicit overlay toggle.
        /// </summary>
        [Fact]
        public void ComponentAdded_WhenPlatformIsPs2_UsesPerformanceOverlayTextImmediatelyAfterOverlayBuild() {
            CoreInstance.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("ps2", "test-version"));

            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FPSComponent fps = new FPSComponent {
                Font = CreateFont()
            };

            entity.AddComponent(fps);

            Assert.StartsWith("Upd 0.0 Obj3D ", fps.UpdateFpsText, StringComparison.Ordinal);
            Assert.Contains(" Cam ", fps.UpdateFpsText, StringComparison.Ordinal);
            Assert.Contains(" Ent ", fps.UpdateFpsText, StringComparison.Ordinal);
            Assert.Equal("Rdr 0.0 Drw 0.0 Ovr 0 Tri 0 Disp 0", fps.RenderFpsText);
        }

        /// <summary>
        /// Ensures the FPS overlay metrics live on the core contract instead of relying on renderer virtual dispatch.
        /// </summary>
        [Fact]
        public void PerformanceOverlayContract_UsesCoreOwnedMetricsInsteadOfRendererVirtuals() {
            Assert.Null(typeof(RenderManager3D).GetMethod("UsesPerformanceOverlayMetrics"));
            Assert.Null(typeof(RenderManager3D).GetMethod("GetPerformanceOverlayTriangleSetupMilliseconds"));
            Assert.Null(typeof(RenderManager3D).GetMethod("GetPerformanceOverlayDispatchCount"));
            Assert.NotNull(typeof(Core).GetProperty("UsesPerformanceOverlayMetrics"));
            Assert.NotNull(typeof(Core).GetMethod("SetPerformanceOverlayMetrics"));
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

