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
            AssertResolvesToValueKind(new EffectAsset(), EditorAssetBinaryValueKind.EffectAsset);
            AssertResolvesToValueKind(new GraphicTemplateAsset(), EditorAssetBinaryValueKind.GraphicTemplateAsset);
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
        /// The timeline value kinds are only reserved in core: this assembly registers no serializer for them, the optional timeline modules do.
        /// </summary>
        [Fact]
        public void FindByValueKind_forTimelineKinds_isLeftToTheTimelineModules() {
            Assert.Equal((ushort)14, (ushort)EditorAssetBinaryValueKind.TimelineAsset);
            Assert.Equal((ushort)15, (ushort)EditorAssetBinaryValueKind.CookedTimelineAsset);
            Assert.Null(EditorAssetPayloadSerializerRegistry.FindByValueKind(EditorAssetBinaryValueKind.TimelineAsset));
            Assert.Null(EditorAssetPayloadSerializerRegistry.FindByValueKind(EditorAssetBinaryValueKind.CookedTimelineAsset));
        }

        /// <summary>
        /// A module serializer becomes resolvable in both directions once registered; registering the same type again is a no-op and a different type claiming the kind is rejected.
        /// </summary>
        [Fact]
        public void Register_moduleSerializer_resolvesOnceAndRejectsConflicts() {
            int before = EditorAssetPayloadSerializerRegistry.Count;

            EditorAssetPayloadSerializerRegistry.Register(new ModuleTestSerializer());
            EditorAssetPayloadSerializerRegistry.Register(new ModuleTestSerializer());

            Assert.Equal(before + 1, EditorAssetPayloadSerializerRegistry.Count);
            AssertResolvesToValueKind(new ModuleTestAsset(), ModuleTestSerializer.Kind);
            Assert.Throws<InvalidOperationException>(() => EditorAssetPayloadSerializerRegistry.Register(new ConflictingModuleTestSerializer()));
            Assert.Throws<InvalidOperationException>(() => EditorAssetPayloadSerializerRegistry.Register(new ConflictingBuiltInKindSerializer()));
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

        /// <summary>
        /// Asset type owned by a stand-in optional module.
        /// </summary>
        sealed class ModuleTestAsset : Asset {
        }

        /// <summary>
        /// Stand-in module serializer registered at runtime under a value kind no real asset uses.
        /// </summary>
        class ModuleTestSerializer : IEditorAssetPayloadSerializer {
            /// <summary>
            /// Value kind claimed by the stand-in module.
            /// </summary>
            public const EditorAssetBinaryValueKind Kind = (EditorAssetBinaryValueKind)900;

            /// <summary>
            /// Gets the stand-in module's value kind.
            /// </summary>
            public virtual EditorAssetBinaryValueKind ValueKind {
                get {
                    return Kind;
                }
            }

            /// <summary>
            /// Claims only the stand-in module asset.
            /// </summary>
            /// <param name="asset">Asset about to be serialized.</param>
            /// <returns>True for <see cref="ModuleTestAsset"/>.</returns>
            public bool Handles(Asset asset) {
                return asset is ModuleTestAsset;
            }

            /// <summary>
            /// Accepts every asset; the stand-in is never written.
            /// </summary>
            /// <param name="asset">Asset about to be serialized.</param>
            public void Validate(Asset asset) {
            }

            /// <summary>
            /// Writes nothing; the stand-in is never written.
            /// </summary>
            /// <param name="writer">Destination writer.</param>
            /// <param name="asset">Asset to write.</param>
            public void Write(EngineBinaryWriter writer, Asset asset) {
            }

            /// <summary>
            /// Returns an empty stand-in asset.
            /// </summary>
            /// <param name="reader">Source reader.</param>
            /// <returns>A new stand-in asset.</returns>
            public Asset Read(EngineBinaryReader reader) {
                return new ModuleTestAsset();
            }
        }

        /// <summary>
        /// A different serializer type that tries to claim the stand-in module's value kind.
        /// </summary>
        sealed class ConflictingModuleTestSerializer : ModuleTestSerializer {
        }

        /// <summary>
        /// A module serializer that tries to claim a built-in value kind.
        /// </summary>
        sealed class ConflictingBuiltInKindSerializer : ModuleTestSerializer {
            /// <summary>
            /// Gets the graphic template value kind, which a built-in serializer already owns.
            /// </summary>
            public override EditorAssetBinaryValueKind ValueKind {
                get {
                    return EditorAssetBinaryValueKind.GraphicTemplateAsset;
                }
            }
        }
    }
}
