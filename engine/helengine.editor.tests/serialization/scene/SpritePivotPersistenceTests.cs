using helengine.editor;

namespace helengine.editor.tests.serialization.scene {
    public sealed class SpritePivotPersistenceTests {
        [Fact]
        public void MissingTaggedFieldsKeepLegacyOrigin_AndNewFieldsRoundTrip() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new(new ScriptComponentReflectionSchemaBuilder());
            EditorTaggedSceneComponentFieldWriter oldWriter = new();
            oldWriter.WriteField(nameof(SpriteComponent.Size), writer => writer.WriteInt2(new int2(37, 19)));
            oldWriter.WriteField(nameof(SpriteComponent.SourceRect), writer => writer.WriteFloat4(new float4(0.1f, 0.2f, 0.3f, 0.4f)));
            SceneComponentAssetRecord record = new() {
                ComponentTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(SpriteComponent)),
                Payload = oldWriter.BuildPayload()
            };
            SpriteComponent legacy = Assert.IsType<SpriteComponent>(descriptor.DeserializeComponent(record, null, null));
            Assert.Equal(new float2(0f, 0f), legacy.Pivot);
            Assert.Equal(0f, legacy.RotationRadians);
            Assert.Equal(new int2(37, 19), legacy.Size);
            Assert.Equal(new float4(0.1f, 0.2f, 0.3f, 0.4f), legacy.SourceRect);
            legacy.Pivot = new float2(0.5f, 0.25f);
            legacy.RotationRadians = 0.73f;
            SpriteComponent restored = Assert.IsType<SpriteComponent>(descriptor.DeserializeComponent(descriptor.SerializeComponent(legacy, 0, new EntityComponentSaveState()), null, null));
            Assert.Equal(legacy.Pivot, restored.Pivot);
            Assert.Equal(legacy.RotationRadians, restored.RotationRadians);
            Assert.Equal(legacy.Size, restored.Size);
        }
    }
}
