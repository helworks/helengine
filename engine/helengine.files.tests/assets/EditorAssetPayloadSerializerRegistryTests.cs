using helengine;
using helengine.files;
using Xunit;

namespace helengine.files.tests.assets {
    /// <summary>
    /// Locks the registration contract that replaced the three parallel dispatch chains of the editor asset format: every supported asset type resolves to exactly one payload serializer, and that serializer answers to the value kind stored in the header.
    /// </summary>
    public class EditorAssetPayloadSerializerRegistryTests {
        [Fact]
        public void FindByAsset_forEachSupportedAssetType_resolvesTheSerializerThatOwnsItsValueKind() {
            AssertResolvesToValueKind(new TextureAsset(), EditorAssetBinaryValueKind.TextureAsset);
            AssertResolvesToValueKind(new ModelAsset(), EditorAssetBinaryValueKind.ModelAsset);
            AssertResolvesToValueKind(new ShaderAsset(), EditorAssetBinaryValueKind.ShaderAsset);
            AssertResolvesToValueKind(new TextAsset(), EditorAssetBinaryValueKind.TextAsset);
            AssertResolvesToValueKind(new MaterialAsset(), EditorAssetBinaryValueKind.MaterialAsset);
            AssertResolvesToValueKind(new PlatformMaterialAsset(), EditorAssetBinaryValueKind.PlatformMaterialAsset);
            AssertResolvesToValueKind(new AnimationClipAsset(), EditorAssetBinaryValueKind.AnimationClipAsset);
            AssertResolvesToValueKind(new AudioAsset(), EditorAssetBinaryValueKind.AudioAsset);
            AssertResolvesToValueKind(new SceneAsset(), EditorAssetBinaryValueKind.SceneAsset);
            AssertResolvesToValueKind(new BlueprintAsset(), EditorAssetBinaryValueKind.BlueprintAsset);
        }

        [Fact]
        public void FindByAsset_whenAssetTypeDerivesFromARegisteredType_resolvesTheRegisteredBaseSerializer() {
            AssertResolvesToValueKind(new DerivedTestMaterialAsset(), EditorAssetBinaryValueKind.MaterialAsset);
        }

        [Fact]
        public void FindByAsset_whenAssetTypeIsNotRegistered_returnsNull() {
            Assert.Null(EditorAssetPayloadSerializerRegistry.FindByAsset(new UnregisteredTestAsset()));
        }

        [Fact]
        public void FindByValueKind_whenValueKindIsNotRegistered_returnsNull() {
            Assert.Null(EditorAssetPayloadSerializerRegistry.FindByValueKind((EditorAssetBinaryValueKind)9999));
        }

        /// <summary>
        /// Asserts that one asset instance resolves to the serializer registered for the expected value kind, in both lookup directions.
        /// </summary>
        /// <param name="asset">Asset instance to resolve.</param>
        /// <param name="expectedValueKind">Value kind the resolved serializer must own.</param>
        static void AssertResolvesToValueKind(Asset asset, EditorAssetBinaryValueKind expectedValueKind) {
            IEditorAssetPayloadSerializer payloadSerializer = EditorAssetPayloadSerializerRegistry.FindByAsset(asset);

            Assert.NotNull(payloadSerializer);
            Assert.Equal(expectedValueKind, payloadSerializer.ValueKind);
            Assert.Same(payloadSerializer, EditorAssetPayloadSerializerRegistry.FindByValueKind(expectedValueKind));
        }

        /// <summary>
        /// Material subtype standing in for the engine's own derived materials, which must keep being stored under the material value kind instead of being rejected as unknown.
        /// </summary>
        sealed class DerivedTestMaterialAsset : MaterialAsset {
        }

        /// <summary>
        /// Asset type that no payload serializer claims, used to prove an unknown asset is reported rather than silently written under someone else's value kind.
        /// </summary>
        sealed class UnregisteredTestAsset : Asset {
        }
    }
}
