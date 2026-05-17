using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the runtime debug overlay component builds and disposes its fixed text hierarchy.
    /// </summary>
    public class DebugComponentTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the runtime test harness.
        /// </summary>
        readonly string TempRootPath;
        /// <summary>
        /// Core instance configured for deterministic debug overlay tests.
        /// </summary>
        readonly TestClockDrivenCore CoreInstance;

        /// <summary>
        /// Initializes the runtime services required by the component tests.
        /// </summary>
        public DebugComponentTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-debug-component-tests", Guid.NewGuid().ToString("N"));
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
        /// Ensures the component remains inert until a font is assigned.
        /// </summary>
        [Fact]
        public void ComponentAdded_WhenFontIsMissing_DoesNotBuildOverlayChildren() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            DebugComponent debug = new DebugComponent();

            entity.AddComponent(debug);

            Assert.Empty(entity.Children);
            Assert.Null(debug.Font);
        }

        /// <summary>
        /// Ensures assigning a font after attachment builds a fixed five-row overlay hierarchy.
        /// </summary>
        [Fact]
        public void FontProperty_WhenAssignedAfterAttachment_BuildsFiveOverlayRows() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            DebugComponent debug = new DebugComponent();
            entity.AddComponent(debug);

            FontAsset font = CreateFont(24f);
            debug.Font = font;

            Entity overlayHost = Assert.Single(entity.Children);

            Assert.Equal(5, overlayHost.Children.Count);
            for (int index = 0; index < overlayHost.Children.Count; index++) {
                TextComponent textComponent = Assert.Single(overlayHost.Children[index].Components.OfType<TextComponent>());
                Assert.Same(font, textComponent.Font);
            }

            Assert.Equal(font.LineHeight * 4f, overlayHost.Children[4].LocalPosition.Y);
        }

        /// <summary>
        /// Ensures removing the component disposes the generated overlay subtree instead of leaving orphaned entities registered.
        /// </summary>
        [Fact]
        public void RemoveComponent_WhenOverlayWasBuilt_DisposesOverlayEntities() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            DebugComponent debug = new DebugComponent {
                Font = CreateFont()
            };
            entity.AddComponent(debug);

            Assert.Equal(7, Core.Instance.ObjectManager.Entities.Count);
            Assert.Equal(5, Core.Instance.ObjectManager.Drawables2D.Count);

            entity.RemoveComponent(debug);

            Assert.Empty(entity.Children);
            Assert.Single(Core.Instance.ObjectManager.Entities);
            Assert.Empty(Core.Instance.ObjectManager.Drawables2D);
        }

        /// <summary>
        /// Ensures the overlay formats live sampled metrics after update and draw ticks have been recorded.
        /// </summary>
        [Fact]
        public void CoreUpdateAndDraw_WhenSamplingFrames_FormatsAllRows() {
            RuntimeMemoryDiagnosticsSnapshot snapshot = new RuntimeMemoryDiagnosticsSnapshot {
                ResidentBytes = 128UL * 1024UL * 1024UL + 512UL * 1024UL,
                CommittedBytes = 192UL * 1024UL * 1024UL
            };
            TestClockDrivenCore core = new TestClockDrivenCore(new CoreInitializationOptions {
                ContentRootPath = TempRootPath,
                RuntimeDiagnosticsProvider = new FakeRuntimeDiagnosticsProvider(snapshot)
            });
            TestRenderManager3D renderManager = new TestRenderManager3D();
            core.Initialize(renderManager, new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
            renderManager.QueueDrawCallCounts(new[] { 23 });
            core.QueueMeasuredDrawMilliseconds(new[] { 12.3d });

            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            DebugComponent debug = new DebugComponent {
                Font = CreateFont(),
                RefreshIntervalSeconds = 0d
            };
            entity.AddComponent(debug);

            Core.Instance.Update(0.25d);
            Core.Instance.Draw();
            Core.Instance.Update(0.25d);

            Assert.Equal("Render FPS: 4.0", debug.RenderFpsText);
            Assert.Equal("Memory Res: 128.5 MB", debug.ResidentMemoryText);
            Assert.Equal("Memory Com: 192.0 MB", debug.CommittedMemoryText);
            Assert.Equal("Drawables 2D: 5", debug.Drawables2DText);
            Assert.Equal("Drawables 3D: 0 DrawCalls: 23", debug.Drawables3DText);
        }

        /// <summary>
        /// Ensures memory rows stay on placeholder text when no runtime diagnostics provider was configured.
        /// </summary>
        [Fact]
        public void ComponentAdded_WhenRuntimeDiagnosticsProviderIsMissing_UsesMemoryPlaceholders() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            DebugComponent debug = new DebugComponent {
                Font = CreateFont(),
                RefreshIntervalSeconds = 0d
            };
            entity.AddComponent(debug);

            Assert.Equal("Memory Res: --", debug.ResidentMemoryText);
            Assert.Equal("Memory Com: --", debug.CommittedMemoryText);
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
                ['R'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                [' '] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['F'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                [':'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['-'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['M'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['C'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['w'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['D'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['2'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['3'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['0'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['1'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['4'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['5'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['8'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['9'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['.'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['B'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f)
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
