using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the standalone asset picker modal layout and scaling behavior.
    /// </summary>
    public class AssetPickerModalTests : IDisposable {
        /// <summary>
        /// Temporary project root used by the modal tests.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Initializes the core services required by the asset picker modal tests.
        /// </summary>
        public AssetPickerModalTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-asset-picker-modal-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
            EditorInputCaptureService.Reset();

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = ProjectRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Deletes temporary modal state after each test.
        /// </summary>
        public void Dispose() {
            EditorInputCaptureService.Reset();
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the asset picker scales its panel, header, and close button with shared metrics.
        /// </summary>
        [Fact]
        public void UpdateLayout_WithScaledMetrics_UsesScaledPanelAndHeaderControls() {
            EditorUiMetrics metrics = new EditorUiMetrics(1.5d);
            AssetPickerModal modal = new AssetPickerModal(CreateFont(), metrics, ProjectRootPath);

            modal.Show(_ => { }, ".obj");
            modal.UpdateLayout(1280, 720);

            RoundedRectComponent panelBackground = GetPrivateField<RoundedRectComponent>(modal, "PanelBackground");
            SpriteComponent headerBackground = GetPrivateField<SpriteComponent>(modal, "HeaderBackground");
            ButtonComponent closeButton = GetPrivateField<ButtonComponent>(modal, "CloseButton");
            SpriteComponent backdropTopSurface = GetPrivateField<SpriteComponent>(modal, "BackdropTopSurface");

            Assert.Equal(new int2(1232, 672), panelBackground.Size);
            Assert.Equal(48, headerBackground.Size.Y);
            Assert.Equal(new int2(108, 33), closeButton.Size);
            Assert.Equal(54, backdropTopSurface.Size.Y);
        }

        /// <summary>
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return Assert.IsType<T>(field.GetValue(target));
        }

        /// <summary>
        /// Creates a small font asset that can satisfy modal layout requirements.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['A'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['k'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['X'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f)
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
    }
}
