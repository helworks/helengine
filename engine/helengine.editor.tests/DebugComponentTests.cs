using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the runtime debug overlay component builds valid text drawables with the assigned font.
    /// </summary>
    public class DebugComponentTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the runtime test harness.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Core instance configured for deterministic debug-overlay tests.
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
        /// Ensures the debug overlay builds one five-row text hierarchy using the assigned font.
        /// </summary>
        [Fact]
        public void ComponentAdded_WhenFontIsAssigned_BuildsOverlayTextWithAssignedFont() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FontAsset font = CreateFont();
            DebugComponent debug = new DebugComponent {
                Font = font
            };

            entity.AddComponent(debug);

            Entity overlayHost = Assert.Single(entity.Children);
            Assert.Equal(5, overlayHost.Children.Count);
            Assert.Equal(5, Core.Instance.ObjectManager.Drawables2D.Count);

            for (int index = 0; index < overlayHost.Children.Count; index++) {
                TextComponent textComponent = Assert.Single(overlayHost.Children[index].Components.OfType<TextComponent>());
                Assert.Same(font, textComponent.Font);
            }
        }

        /// <summary>
        /// Ensures steady-state overlay refreshes do not rely on the allocating full diagnostics snapshot path.
        /// </summary>
        [Fact]
        public void Update_WhenRefreshIntervalElapses_DoesNotUseFullDiagnosticsSnapshotCapturePath() {
            RuntimeMemoryDiagnosticsSnapshot snapshot = new RuntimeMemoryDiagnosticsSnapshot {
                ResidentBytes = 1024u,
                CommittedBytes = 2048u
            };
            FakeRuntimeDiagnosticsProvider provider = new FakeRuntimeDiagnosticsProvider(snapshot);
            TestClockDrivenCore core = new TestClockDrivenCore(new CoreInitializationOptions {
                ContentRootPath = TempRootPath,
                RuntimeDiagnosticsProvider = provider
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));

            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            DebugComponent debug = new DebugComponent {
                Font = CreateFont(),
                RefreshIntervalSeconds = 0.5d
            };

            entity.AddComponent(debug);

            core.Update(0.6d);

            Assert.Equal(0, provider.SnapshotCaptureCount);
        }

        /// <summary>
        /// Ensures the debug overlay tears down stale child references when the overlay subtree is disposed before the owning component receives its disable callback.
        /// </summary>
        [Fact]
        public void ParentEnabledChange_WhenOverlaySubtreeWasDisposedExternally_TearsDownStaleOverlayReferences() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            DebugComponent debug = new DebugComponent {
                Font = CreateFont()
            };

            entity.AddComponent(debug);

            Entity overlayHost = Assert.IsType<Entity>(GetPrivateFieldValue(debug, "OverlayHost"));
            overlayHost.Dispose();

            debug.ParentEnabledChange(false);

            Assert.False((bool)GetPrivateFieldValue(debug, "Initialized"));
            Assert.Null(GetPrivateFieldValue(debug, "OverlayHost"));
            Assert.Null(GetPrivateFieldValue(debug, "RenderFpsTextComponent"));
            Assert.Null(GetPrivateFieldValue(debug, "ResidentMemoryTextComponent"));
            Assert.Null(GetPrivateFieldValue(debug, "CommittedMemoryTextComponent"));
            Assert.Null(GetPrivateFieldValue(debug, "Drawables2DTextComponent"));
            Assert.Null(GetPrivateFieldValue(debug, "Drawables3DTextComponent"));
        }

        /// <summary>
        /// Creates one runtime font asset with glyph metrics that cover the debug overlay text set.
        /// </summary>
        /// <returns>Font asset suitable for overlay-component tests.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['R'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['F'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['M'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['C'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['w'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
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
                24f,
                64,
                64);
        }

        /// <summary>
        /// Reads one private instance field value from the supplied test subject.
        /// </summary>
        /// <param name="target">Object that owns the requested field.</param>
        /// <param name="fieldName">Exact private field name to read.</param>
        /// <returns>Current field value.</returns>
        static object GetPrivateFieldValue(object target, string fieldName) {
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            } else if (string.IsNullOrWhiteSpace(fieldName)) {
                throw new ArgumentException("Field name must be provided.", nameof(fieldName));
            }

            Type currentType = target.GetType();
            while (currentType != null) {
                var field = currentType.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field != null) {
                    return field.GetValue(target);
                }

                currentType = currentType.BaseType;
            }

            throw new InvalidOperationException($"Field '{fieldName}' was not found on type '{target.GetType().FullName}'.");
        }
    }
}
