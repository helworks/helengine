using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies preview panel lifecycle behavior.
    /// </summary>
    public class PreviewPanelTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the panel tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by the preview panel tests.
        /// </summary>
        public PreviewPanelTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-preview-panel-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Deletes temporary content after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures replacing the preview source disposes the previous source.
        /// </summary>
        [Fact]
        public void SetPreviewSource_WhenNewSourceIsAssigned_DisposesThePreviousSource() {
            PreviewPanel panel = new PreviewPanel(CreateFont());
            TestPreviewSource first = new TestPreviewSource(new TestRuntimeTexture {
                Width = 32,
                Height = 32
            });
            TestPreviewSource second = new TestPreviewSource(new TestRuntimeTexture {
                Width = 64,
                Height = 64
            });

            panel.SetPreviewSource(first);
            panel.SetPreviewSource(second);

            Assert.True(first.IsDisposed);
            Assert.Same(second, panel.ActivePreviewSource);
        }

        /// <summary>
        /// Ensures scaled dock metrics move the preview content below the scaled title bar and increase content padding.
        /// </summary>
        [Fact]
        public void SetPreviewSource_WithScaledMetrics_UsesScaledTitleBarOffsetAndPadding() {
            EditorUiMetrics metrics = new EditorUiMetrics(1.5d);
            PreviewPanel panel = new PreviewPanel(CreateFont(), metrics) {
                Size = new int2(300, 240)
            };
            TestPreviewSource source = new TestPreviewSource(new TestRuntimeTexture {
                Width = 100,
                Height = 50
            });

            panel.SetPreviewSource(source);

            EditorEntity contentRoot = GetPrivateField<EditorEntity>(panel, "contentRoot");
            EditorEntity textureHost = GetPrivateField<EditorEntity>(panel, "textureHost");
            SpriteComponent textureSprite = GetPrivateField<SpriteComponent>(panel, "textureSprite");

            Assert.Equal(30f, contentRoot.Position.Y);
            Assert.Equal(12f, textureHost.Position.X);
            Assert.Equal(66f, textureHost.Position.Y);
            Assert.Equal(new int2(276, 138), textureSprite.Size);
        }

        /// <summary>
        /// Creates a small font asset that can satisfy dockable layout requirements.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['v'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['w'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f)
            };

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                16f,
                64,
                64);
        }

        /// <summary>
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            System.Reflection.FieldInfo field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return Assert.IsType<T>(field.GetValue(target));
        }
    }
}
