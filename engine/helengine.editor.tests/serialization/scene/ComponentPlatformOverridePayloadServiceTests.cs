using helengine.editor;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies the wrapped platform-override payload service persists only the current override payload format.
    /// </summary>
    public sealed class ComponentPlatformOverridePayloadServiceTests {
        /// <summary>
        /// Ensures wrapped component payloads round-trip their base payload and explicit platform overrides through the current format.
        /// </summary>
        [Fact]
        public void WrapAndReadOverrideStates_WhenOverridesExist_RoundTripsCurrentPayloadFormat() {
            ComponentPlatformOverridePayloadService service = new ComponentPlatformOverridePayloadService();
            SceneComponentAssetRecord baseRecord = new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.TestComponent",
                ComponentIndex = 3,
                Payload = new byte[] { 7, 8, 9 }
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            EntityComponentPlatformOverrideState overrideState = new EntityComponentPlatformOverrideState {
                Payload = new byte[] { 1, 2, 3, 4 }
            };
            overrideState.SetAssetReference("Font", CreateFileReference("fonts/default.hefont"));
            overrideState.SetPropertyOverride("Transform.Position");
            overrideState.SetMemberValue("BGLayer", "1");
            saveState.SetPlatformOverride("windows", overrideState);

            SceneComponentAssetRecord wrappedRecord = service.Wrap(baseRecord, saveState);
            SceneComponentAssetRecord unwrappedRecord = service.UnwrapBaseRecord(wrappedRecord);
            EntityComponentPlatformOverrideState loadedOverride = Assert.Single(service.ReadOverrideStates(wrappedRecord));

            Assert.Equal(baseRecord.ComponentTypeId, wrappedRecord.ComponentTypeId);
            Assert.Equal(baseRecord.ComponentIndex, wrappedRecord.ComponentIndex);
            Assert.Equal(baseRecord.Payload, unwrappedRecord.Payload);
            Assert.Equal("platform:windows", loadedOverride.Scope.ToString());
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, loadedOverride.Payload);
            Assert.True(loadedOverride.HasPropertyOverride("Transform.Position"));
            Assert.True(loadedOverride.TryGetMemberValue("BGLayer", out string bgLayerValue));
            Assert.Equal("1", bgLayerValue);
            Assert.True(loadedOverride.TryGetAssetReference("Font", out SceneAssetReference loadedReference));
            Assert.Equal("fonts/default.hefont", loadedReference.RelativePath);
        }

        /// <summary>
        /// Ensures component override payloads retain their nested environment scope independently of the platform payload.
        /// </summary>
        [Fact]
        public void WrapAndReadOverrideStates_WhenEnvironmentOverrideExists_RoundTripsNestedScope() {
            ComponentPlatformOverridePayloadService service = new ComponentPlatformOverridePayloadService();
            SceneComponentAssetRecord baseRecord = new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.TestComponent",
                Payload = new byte[] { 4, 5, 6 }
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetScopedPlatformOverride(new EditorOverrideScope("windows", "debug"), new EntityComponentPlatformOverrideState {
                Payload = new byte[] { 9, 8, 7 }
            });

            SceneComponentAssetRecord wrappedRecord = service.Wrap(baseRecord, saveState);
            EntityComponentPlatformOverrideState loadedOverride = Assert.Single(service.ReadOverrideStates(wrappedRecord));

            Assert.Equal("platform:windows/buildconfig:debug", loadedOverride.Scope.ToString());
            Assert.Equal(new byte[] { 9, 8, 7 }, loadedOverride.Payload);
        }

        /// <summary>
        /// Ensures a removed 2D draw-order override and its detached payload field are discarded for former built-in drawable components.
        /// </summary>
        [Fact]
        public void ReadOverrideStates_WhenLegacyRenderOrder2DOverrideExists_ForBuiltInDrawableDiscardsItAndResavesCleanPayload() {
            ComponentPlatformOverridePayloadService service = new ComponentPlatformOverridePayloadService();
            EditorTaggedSceneComponentFieldWriter fieldWriter = new EditorTaggedSceneComponentFieldWriter();
            fieldWriter.WriteField("Label", writer => writer.WriteString("Platform label"));
            fieldWriter.WriteField("RenderOrder2D", writer => writer.WriteByte(211));
            EntityComponentPlatformOverrideState overrideState = new EntityComponentPlatformOverrideState {
                Payload = fieldWriter.BuildPayload(),
                Scope = new EditorOverrideScope("windows")
            };
            overrideState.SetPropertyOverride("RenderOrder2D");
            overrideState.SetMemberValue("RenderOrder2D", "211");
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetScopedPlatformOverride(overrideState.Scope, overrideState);
            SceneComponentAssetRecord wrappedRecord = service.Wrap(new SceneComponentAssetRecord {
                ComponentTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(RoundedRectComponent)),
                ComponentIndex = 0,
                Payload = new byte[] { 1, 2, 3 }
            }, saveState);

            EntityComponentPlatformOverrideState loadedOverride = Assert.Single(service.ReadOverrideStates(wrappedRecord));
            SceneComponentAssetRecord resavedRecord = service.Wrap(service.UnwrapBaseRecord(wrappedRecord), CreateSaveStateWith(loadedOverride));
            EntityComponentPlatformOverrideState resavedOverride = Assert.Single(service.ReadOverrideStates(resavedRecord));
            EditorTaggedSceneComponentFieldReader resavedPayload = new EditorTaggedSceneComponentFieldReader(resavedOverride.Payload);

            Assert.False(loadedOverride.HasPropertyOverride("RenderOrder2D"));
            Assert.False(loadedOverride.HasMemberValue("RenderOrder2D"));
            Assert.False(resavedPayload.TryGetFieldReader("RenderOrder2D", out EngineBinaryReader obsoleteFieldReader));
            Assert.Null(obsoleteFieldReader);
        }

        /// <summary>
        /// Ensures same-named custom component override data is preserved instead of being mistaken for removed built-in drawable metadata.
        /// </summary>
        [Fact]
        public void ReadOverrideStates_WhenRenderOrder2DOverrideExists_ForCustomComponentPreservesIt() {
            ComponentPlatformOverridePayloadService service = new ComponentPlatformOverridePayloadService();
            EditorTaggedSceneComponentFieldWriter fieldWriter = new EditorTaggedSceneComponentFieldWriter();
            fieldWriter.WriteField("RenderOrder2D", writer => writer.WriteByte(173));
            EntityComponentPlatformOverrideState overrideState = new EntityComponentPlatformOverrideState {
                Payload = fieldWriter.BuildPayload(),
                Scope = new EditorOverrideScope("windows")
            };
            overrideState.SetPropertyOverride("RenderOrder2D");
            overrideState.SetMemberValue("RenderOrder2D", "173");
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetScopedPlatformOverride(overrideState.Scope, overrideState);
            SceneComponentAssetRecord wrappedRecord = service.Wrap(new SceneComponentAssetRecord {
                ComponentTypeId = "sample.gameplay.ColorComponent, gameplay",
                ComponentIndex = 0,
                Payload = new byte[] { 1, 2, 3 }
            }, saveState);

            EntityComponentPlatformOverrideState loadedOverride = Assert.Single(service.ReadOverrideStates(wrappedRecord));
            EditorTaggedSceneComponentFieldReader payloadReader = new EditorTaggedSceneComponentFieldReader(loadedOverride.Payload);
            Assert.True(loadedOverride.HasPropertyOverride("RenderOrder2D"));
            Assert.True(loadedOverride.TryGetMemberValue("RenderOrder2D", out string memberValue));
            Assert.Equal("173", memberValue);
            Assert.True(payloadReader.TryGetFieldReader("RenderOrder2D", out EngineBinaryReader fieldReader));
            using (fieldReader) {
                Assert.Equal(173, fieldReader.ReadByte());
            }
        }

        /// <summary>
        /// Ensures unordered override maps produce the same wrapped bytes regardless of insertion order.
        /// </summary>
        [Fact]
        public void Wrap_WhenOverrideMapsAreInsertedInReverseOrder_IsDeterministic() {
            ComponentPlatformOverridePayloadService service = new ComponentPlatformOverridePayloadService();
            SceneComponentAssetRecord baseRecord = new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.TestComponent",
                Payload = new byte[] { 1, 2, 3 }
            };

            byte[] first = service.Wrap(baseRecord, CreateUnorderedOverrideState(new[] { "z-slot", "a-slot" }, new[] { "z-property", "a-property" }, new[] { "z-member", "a-member" })).Payload;
            byte[] second = service.Wrap(baseRecord, CreateUnorderedOverrideState(new[] { "a-slot", "z-slot" }, new[] { "a-property", "z-property" }, new[] { "a-member", "z-member" })).Payload;

            Assert.Equal(first, second);
        }

        /// <summary>
        /// Ensures the removed version-3 wrapped override payload is rejected instead of being normalized to the current schema.
        /// </summary>
        [Fact]
        public void ReadOverrideStates_WhenPayloadUsesOlderVersion_ThrowsUnsupportedPayloadVersion() {
            ComponentPlatformOverridePayloadService service = new ComponentPlatformOverridePayloadService();
            SceneComponentAssetRecord record = new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.TestComponent",
                ComponentIndex = 0,
                Payload = WriteOlderVersionWrappedPayload()
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => service.ReadOverrideStates(record));
            Assert.Contains("Unsupported component platform override payload version", exception.Message);
        }

        /// <summary>
        /// Creates one file-system scene asset reference for the override payload tests.
        /// </summary>
        /// <param name="relativePath">Relative asset path stored by the reference.</param>
        /// <returns>File-system scene asset reference.</returns>
        static SceneAssetReference CreateFileReference(string relativePath) {
            return global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateCurrentFileSystem(relativePath);
        }

        /// <summary>
        /// Creates one override state whose unordered collections use the supplied insertion order.
        /// </summary>
        /// <param name="referenceNames">Reference names in insertion order.</param>
        /// <param name="propertyPaths">Property paths in insertion order.</param>
        /// <param name="memberNames">Detached member names in insertion order.</param>
        /// <returns>One populated override state.</returns>
        static EntityComponentSaveState CreateUnorderedOverrideState(
            IReadOnlyList<string> referenceNames,
            IReadOnlyList<string> propertyPaths,
            IReadOnlyList<string> memberNames) {
            EntityComponentPlatformOverrideState overrideState = new EntityComponentPlatformOverrideState {
                Payload = new byte[] { 4, 5, 6 }
            };
            for (int index = 0; index < referenceNames.Count; index++) {
                string referenceName = referenceNames[index];
                overrideState.SetAssetReference(referenceName, CreateFileReference("models/" + referenceName + ".hasset"));
            }
            for (int index = 0; index < propertyPaths.Count; index++) {
                overrideState.SetPropertyOverride(propertyPaths[index]);
            }
            for (int index = 0; index < memberNames.Count; index++) {
                overrideState.SetMemberValue(memberNames[index], memberNames[index] + "-value");
            }

            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetPlatformOverride("windows", overrideState);
            return saveState;
        }

        /// <summary>
        /// Wraps one restored override state in a save-state for the subsequent scene-save pass.
        /// </summary>
        /// <param name="overrideState">Restored override metadata to persist again.</param>
        /// <returns>Save-state containing the override on its original scope.</returns>
        static EntityComponentSaveState CreateSaveStateWith(EntityComponentPlatformOverrideState overrideState) {
            if (overrideState == null) {
                throw new ArgumentNullException(nameof(overrideState));
            }

            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetScopedPlatformOverride(overrideState.Scope, overrideState);
            return saveState;
        }

        /// <summary>
        /// Writes one removed version-3 wrapped override payload using its old no-environment entry shape.
        /// </summary>
        /// <returns>Serialized version-3 wrapped override payload.</returns>
        static byte[] WriteOlderVersionWrappedPayload() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte((byte)'C');
            writer.WriteByte((byte)'P');
            writer.WriteByte((byte)'O');
            writer.WriteByte((byte)'V');
            writer.WriteInt32(3);
            writer.WriteByteArray(new byte[] { 7, 8, 9 });
            writer.WriteInt32(1);
            writer.WriteString("windows");
            writer.WriteByteArray(new byte[] { 1, 2, 3, 4 });
            writer.WriteInt32(0);
            writer.WriteInt32(0);
            writer.WriteInt32(0);
            return stream.ToArray();
        }
    }
}
