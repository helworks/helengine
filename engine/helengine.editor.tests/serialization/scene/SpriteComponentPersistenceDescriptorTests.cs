using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies scene persistence for the runtime sprite component descriptor.
    /// </summary>
    public class SpriteComponentPersistenceDescriptorTests {
        /// <summary>
        /// Ensures sprite deserialization accepts the strict runtime payload shape written by cooked scene packaging.
        /// </summary>
        [Fact]
        public void DeserializeComponent_WhenPayloadUsesCookedRuntimeLayout_LoadsTheComponent() {
            SpriteComponentPersistenceDescriptor descriptor = new SpriteComponentPersistenceDescriptor();
            SceneAssetReference textureReference = BuildTextureReference("images/menu/logo.png", "images", "logo");
            TestSceneAssetReferenceResolver resolver = new TestSceneAssetReferenceResolver();
            TestRuntimeTexture loadedTexture = new TestRuntimeTexture {
                Width = 128,
                Height = 64
            };
            resolver.RegisterTexture(textureReference, loadedTexture);

            SceneComponentAssetRecord record = new SceneComponentAssetRecord {
                ComponentTypeId = descriptor.ComponentTypeId,
                ComponentIndex = 0,
                Payload = WriteCookedRuntimePayload(textureReference)
            };

            EntitySaveComponent loadedSaveComponent = new EntitySaveComponent();
            SpriteComponent loadedComponent = Assert.IsType<SpriteComponent>(descriptor.DeserializeComponent(record, loadedSaveComponent, resolver));

            Assert.Same(loadedTexture, loadedComponent.Texture);
            Assert.Equal(new float4(0.1f, 0.2f, 0.7f, 0.8f), loadedComponent.SourceRect);
            Assert.Equal(new int2(96, 32), loadedComponent.Size);
            Assert.Equal(new byte4(20, 40, 60, 255), loadedComponent.Color);
            Assert.Equal(0.35f, loadedComponent.Rotation);
            Assert.Equal((byte)4, loadedComponent.RenderOrder2D);
            Assert.Equal((byte)2, loadedComponent.LayerMask);
            Assert.True(loadedSaveComponent.TryGetComponentState(loadedComponent, out EntityComponentSaveState loadedState));
            Assert.True(loadedState.TryGetAssetReference(TextureAssetScenePersistenceSupport.TextureReferenceName, out SceneAssetReference loadedReference));
            Assert.Equal(textureReference.RelativePath, loadedReference.RelativePath);
            Assert.Equal(textureReference.ProviderId, loadedReference.ProviderId);
            Assert.Equal(textureReference.AssetId, loadedReference.AssetId);
        }

        /// <summary>
        /// Builds one stable texture reference used by the descriptor tests.
        /// </summary>
        /// <param name="relativePath">Project-relative path for the texture.</param>
        /// <param name="providerId">Generated provider identifier.</param>
        /// <param name="assetId">Provider-local asset identifier.</param>
        /// <returns>Stable scene asset reference.</returns>
        SceneAssetReference BuildTextureReference(string relativePath, string providerId, string assetId) {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = relativePath,
                ProviderId = providerId,
                AssetId = assetId
            };
        }

        /// <summary>
        /// Writes one strict runtime sprite payload matching the cooked scene layout used by packaged builds.
        /// </summary>
        /// <param name="textureReference">Texture reference encoded into the runtime payload.</param>
        /// <returns>Serialized cooked-runtime payload.</returns>
        byte[] WriteCookedRuntimePayload(SceneAssetReference textureReference) {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            SceneComponentBinaryFieldEncoding.WriteOptionalReference(writer, textureReference);
            writer.WriteFloat4(new float4(0.1f, 0.2f, 0.7f, 0.8f));
            writer.WriteInt2(new int2(96, 32));
            SceneComponentBinaryFieldEncoding.WriteByte4(writer, new byte4(20, 40, 60, 255));
            writer.WriteSingle(0.35f);
            writer.WriteByte(4);
            writer.WriteByte(2);
            return stream.ToArray();
        }
    }
}
