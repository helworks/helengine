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
            Assert.Equal("windows", loadedOverride.PlatformId);
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

            Assert.Equal("windows", loadedOverride.PlatformId);
            Assert.Equal("debug", loadedOverride.EnvironmentId);
            Assert.Equal(new byte[] { 9, 8, 7 }, loadedOverride.Payload);
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
