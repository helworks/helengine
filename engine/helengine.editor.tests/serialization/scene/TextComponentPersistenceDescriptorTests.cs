using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies scene persistence for the runtime text component descriptor.
    /// </summary>
    public class TextComponentPersistenceDescriptorTests {
        /// <summary>
        /// Ensures text persistence round-trips the assigned font reference and authored text settings.
        /// </summary>
        [Fact]
        public void SerializeAndDeserialize_WhenTextUsesFontAndLayout_RoundTripsTheComponent() {
            TextComponentPersistenceDescriptor descriptor = new TextComponentPersistenceDescriptor();
            TextComponent textComponent = new TextComponent {
                Font = CreateFont("Primary"),
                Text = "Hello world",
                WrapText = true,
                Size = new int2(320, 64),
                Color = new byte4(12, 34, 56, 78),
                SourceRect = new float4(0.1f, 0.2f, 0.3f, 0.4f),
                Rotation = 0.25f,
                RenderOrder2D = 19,
                LayerMask = 7,
                SelectionEnabled = true
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            SceneAssetReference fontReference = BuildFontReference("fonts/primary.hefont", "fonts", "primary");
            saveState.SetAssetReference("Font", fontReference);

            SceneComponentAssetRecord record = descriptor.SerializeComponent(textComponent, 0, saveState);

            TestSceneAssetReferenceResolver resolver = new TestSceneAssetReferenceResolver();
            FontAsset loadedFont = CreateFont("Loaded");
            resolver.RegisterFont(fontReference, loadedFont);

            EntitySaveComponent loadedSaveComponent = new EntitySaveComponent();
            TextComponent loadedComponent = Assert.IsType<TextComponent>(descriptor.DeserializeComponent(record, loadedSaveComponent, resolver));

            Assert.Same(loadedFont, loadedComponent.Font);
            Assert.Equal("Hello world", loadedComponent.Text);
            Assert.True(loadedComponent.WrapText);
            Assert.Equal(new int2(320, 64), loadedComponent.Size);
            Assert.Equal(new byte4(12, 34, 56, 78), loadedComponent.Color);
            Assert.Equal(new float4(0.1f, 0.2f, 0.3f, 0.4f), loadedComponent.SourceRect);
            Assert.Equal(0.25f, loadedComponent.Rotation);
            Assert.Equal((byte)19, loadedComponent.RenderOrder2D);
            Assert.Equal((byte)7, loadedComponent.LayerMask);
            Assert.True(loadedComponent.SelectionEnabled);
            Assert.True(loadedSaveComponent.TryGetComponentState(loadedComponent, out EntityComponentSaveState loadedState));
            Assert.True(loadedState.TryGetAssetReference("Font", out SceneAssetReference loadedReference));
            Assert.Equal(fontReference.RelativePath, loadedReference.RelativePath);
            Assert.Equal(fontReference.ProviderId, loadedReference.ProviderId);
            Assert.Equal(fontReference.AssetId, loadedReference.AssetId);
        }

        /// <summary>
        /// Creates a stable runtime font asset used by the descriptor round-trip test.
        /// </summary>
        /// <param name="name">Friendly font name.</param>
        /// <returns>Runtime font asset with deterministic metrics.</returns>
        FontAsset CreateFont(string name) {
            return new FontAsset(
                new FontInfo(name, 16, 4f),
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
        /// Builds one stable font reference used by the descriptor tests.
        /// </summary>
        /// <param name="relativePath">Project-relative path for the font.</param>
        /// <param name="providerId">Generated provider identifier.</param>
        /// <param name="assetId">Provider-local asset identifier.</param>
        /// <returns>Stable scene asset reference.</returns>
        SceneAssetReference BuildFontReference(string relativePath, string providerId, string assetId) {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = relativePath,
                ProviderId = providerId,
                AssetId = assetId
            };
        }
    }
}
