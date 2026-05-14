using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies scene persistence for the runtime FPS overlay component descriptor.
    /// </summary>
    public class FPSComponentPersistenceDescriptorTests : IDisposable {
        /// <summary>
        /// Temporary project root used by the descriptor tests.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Initializes the core runtime services required by the descriptor tests.
        /// </summary>
        public FPSComponentPersistenceDescriptorTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-fps-component-persistence-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempProjectRootPath);

            EditorCore core = new EditorCore(null);
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"), new CoreInitializationOptions {
                ContentRootPath = TempProjectRootPath
            });
            core.SetDefaultFontAssetForEditor(CreateFont());
        }

        /// <summary>
        /// Deletes temporary test content after each run.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures FPS overlay settings round-trip through the scene persistence descriptor.
        /// </summary>
        [Fact]
        public void SerializeAndDeserialize_WhenFpsOverlayUsesCustomSettings_RoundTripsTheComponent() {
            FPSComponentPersistenceDescriptor descriptor = new FPSComponentPersistenceDescriptor();
            TestSceneAssetReferenceResolver referenceResolver = new TestSceneAssetReferenceResolver();
            referenceResolver.RegisterFont(BuildEditorFontReference(), ((EditorCore)Core.Instance).DefaultFontAssetForEditor);
            FPSComponent fpsComponent = new FPSComponent {
                Font = ((EditorCore)Core.Instance).DefaultFontAssetForEditor,
                RefreshIntervalSeconds = 1.25d,
                Padding = new int2(13, 21),
                RenderOrder2D = 243
            };

            SceneComponentAssetRecord record = descriptor.SerializeComponent(fpsComponent, 0, null);
            FPSComponent loadedComponent = Assert.IsType<FPSComponent>(descriptor.DeserializeComponent(record, null, referenceResolver));

            Assert.Equal(1.25d, loadedComponent.RefreshIntervalSeconds);
            Assert.Equal(new int2(13, 21), loadedComponent.Padding);
            Assert.Equal((byte)243, loadedComponent.RenderOrder2D);
            Assert.Same(((EditorCore)Core.Instance).DefaultFontAssetForEditor, loadedComponent.Font);
        }

        /// <summary>
        /// Ensures missing font references deserialize into an inert FPS component.
        /// </summary>
        [Fact]
        public void DeserializeComponent_WhenFontReferenceIsMissing_LeavesFontNull() {
            FPSComponentPersistenceDescriptor descriptor = new FPSComponentPersistenceDescriptor();

            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField("RefreshIntervalSeconds", fieldWriter => fieldWriter.WriteInt64(BitConverter.DoubleToInt64Bits(1.25d)));
            writer.WriteField("Padding", fieldWriter => fieldWriter.WriteInt2(new int2(13, 21)));
            writer.WriteField("RenderOrder2D", fieldWriter => fieldWriter.WriteByte(243));

            SceneComponentAssetRecord record = new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.FPSComponent",
                ComponentIndex = 0,
                Payload = writer.BuildPayload()
            };

            FPSComponent loadedComponent = Assert.IsType<FPSComponent>(descriptor.DeserializeComponent(record, null, new TestSceneAssetReferenceResolver()));

            Assert.Null(loadedComponent.Font);
            Assert.Equal(1.25d, loadedComponent.RefreshIntervalSeconds);
            Assert.Equal(new int2(13, 21), loadedComponent.Padding);
            Assert.Equal((byte)243, loadedComponent.RenderOrder2D);
        }

        /// <summary>
        /// Creates a deterministic font asset used by the default FPS component constructor.
        /// </summary>
        /// <returns>Font asset with stable metrics for the current test.</returns>
        FontAsset CreateFont() {
            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 1,
                    Height = 1
                },
                new Dictionary<char, FontChar>(),
                16f,
                1,
                1);
        }

        /// <summary>
        /// Builds the stable editor-font reference used by FPS component serialization.
        /// </summary>
        /// <returns>Stable scene asset reference.</returns>
        SceneAssetReference BuildEditorFontReference() {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = "generated/editor/fonts/ui.hefont",
                ProviderId = "editor",
                AssetId = "ui-font"
            };
        }
    }
}

