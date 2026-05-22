using helengine.editor;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies scene persistence for the authored scene-map component.
    /// </summary>
    public sealed class SceneMapComponentPersistenceDescriptorTests {
        /// <summary>
        /// Ensures the scene-map descriptor round-trips authored mappings through the tagged editor payload.
        /// </summary>
        [Fact]
        public void SerializeComponent_WhenMappingsExist_WritesDictionaryEntries() {
            SceneMapComponentPersistenceDescriptor descriptor = new SceneMapComponentPersistenceDescriptor();
            SceneMapComponent sceneMapComponent = new SceneMapComponent();
            sceneMapComponent.Mappings.Add("MainMenu", "MainMenuScene");
            sceneMapComponent.Mappings.Add("OptionsMenu", "OptionsMenuDs");

            SceneComponentAssetRecord record = descriptor.SerializeComponent(sceneMapComponent, 3, null);
            SceneMapComponent loadedComponent = Assert.IsType<SceneMapComponent>(descriptor.DeserializeComponent(record, null, null));

            Assert.Equal(SceneMapComponent.SerializedComponentTypeId, record.ComponentTypeId);
            Assert.Equal(3, record.ComponentIndex);
            Assert.Equal("MainMenuScene", loadedComponent.Mappings["MainMenu"]);
            Assert.Equal("OptionsMenuDs", loadedComponent.Mappings["OptionsMenu"]);
        }

        /// <summary>
        /// Ensures the scene-map descriptor round-trips one authored initial scene id alongside the mapping table.
        /// </summary>
        [Fact]
        public void SerializeComponent_WhenInitialSceneIdExists_RoundTripsInitialSceneIdAndMappings() {
            SceneMapComponentPersistenceDescriptor descriptor = new SceneMapComponentPersistenceDescriptor();
            SceneMapComponent sceneMapComponent = new SceneMapComponent {
                InitialSceneId = "MainMenuScene"
            };
            sceneMapComponent.Mappings.Add("MainMenuScene", "AlternateMainMenuScene");

            SceneComponentAssetRecord record = descriptor.SerializeComponent(sceneMapComponent, 3, null);
            SceneMapComponent loadedComponent = Assert.IsType<SceneMapComponent>(descriptor.DeserializeComponent(record, null, null));

            Assert.Equal("MainMenuScene", loadedComponent.InitialSceneId);
            Assert.Equal("AlternateMainMenuScene", loadedComponent.Mappings["MainMenuScene"]);
        }

        /// <summary>
        /// Ensures the scene-map descriptor restores mappings from the tolerant tagged editor payload shape.
        /// </summary>
        [Fact]
        public void DeserializeComponent_WhenPayloadUsesTaggedEditorLayout_LoadsMappings() {
            SceneMapComponentPersistenceDescriptor descriptor = new SceneMapComponentPersistenceDescriptor();
            SceneComponentAssetRecord record = new SceneComponentAssetRecord {
                ComponentTypeId = SceneMapComponent.SerializedComponentTypeId,
                ComponentIndex = 0,
                Payload = WriteTaggedEditorPayload(
                    CreateMapping("MainMenu", "MainMenuScene"),
                    CreateMapping("OptionsMenu", "OptionsMenuDs"))
            };

            SceneMapComponent loadedComponent = Assert.IsType<SceneMapComponent>(descriptor.DeserializeComponent(record, null, null));

            Assert.Equal(2, loadedComponent.Mappings.Count);
            Assert.Equal("MainMenuScene", loadedComponent.Mappings["MainMenu"]);
            Assert.Equal("OptionsMenuDs", loadedComponent.Mappings["OptionsMenu"]);
        }

        /// <summary>
        /// Ensures the scene-map descriptor accepts the strict cooked runtime payload shape used by packaged builds.
        /// </summary>
        [Fact]
        public void DeserializeComponent_WhenPayloadUsesCookedRuntimeLayout_LoadsMappings() {
            SceneMapComponentPersistenceDescriptor descriptor = new SceneMapComponentPersistenceDescriptor();
            SceneComponentAssetRecord record = new SceneComponentAssetRecord {
                ComponentTypeId = SceneMapComponent.SerializedComponentTypeId,
                ComponentIndex = 0,
                Payload = WriteCookedRuntimePayload(SceneMapComponent.CurrentVersion, string.Empty, CreateMapping("MainMenu", "AlternateMainMenuScene"))
            };

            SceneMapComponent loadedComponent = Assert.IsType<SceneMapComponent>(descriptor.DeserializeComponent(record, null, null));

            Assert.Single(loadedComponent.Mappings);
            Assert.Equal("AlternateMainMenuScene", loadedComponent.Mappings["MainMenu"]);
        }

        /// <summary>
        /// Ensures the scene-map descriptor rejects unsupported cooked payload versions.
        /// </summary>
        [Fact]
        public void DeserializeComponent_WhenPayloadVersionIsUnsupported_ThrowsInvalidOperationException() {
            SceneMapComponentPersistenceDescriptor descriptor = new SceneMapComponentPersistenceDescriptor();
            SceneComponentAssetRecord record = new SceneComponentAssetRecord {
                ComponentTypeId = SceneMapComponent.SerializedComponentTypeId,
                ComponentIndex = 0,
                Payload = WriteCookedRuntimePayload(99, string.Empty, CreateMapping("MainMenu", "AlternateMainMenuScene"))
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => descriptor.DeserializeComponent(record, null, null));

            Assert.Contains("Unsupported scene map component payload version", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the runtime scene-map deserializer restores cooked mappings directly for player builds.
        /// </summary>
        [Fact]
        public void Deserialize_WhenRuntimeSceneMapPayloadIsValid_LoadsMappings() {
            RuntimeSceneMapComponentDeserializer deserializer = new RuntimeSceneMapComponentDeserializer();
            SceneComponentAssetRecord record = new SceneComponentAssetRecord {
                ComponentTypeId = SceneMapComponent.SerializedComponentTypeId,
                ComponentIndex = 0,
                Payload = WriteCookedRuntimePayload(SceneMapComponent.CurrentVersion, "MainMenuScene", CreateMapping("MainMenu", "MainMenuScene"))
            };

            SceneMapComponent loadedComponent = Assert.IsType<SceneMapComponent>(deserializer.Deserialize(record, null));

            Assert.Equal("MainMenuScene", loadedComponent.InitialSceneId);
            Assert.Equal("MainMenuScene", loadedComponent.Mappings["MainMenu"]);
        }

        /// <summary>
        /// Writes one tagged editor payload for the supplied mappings.
        /// </summary>
        /// <param name="mappings">Mappings to encode into the payload.</param>
        /// <returns>Tagged editor payload bytes.</returns>
        KeyValuePair<string, string> CreateMapping(string sourceSceneId, string targetSceneId) {
            return new KeyValuePair<string, string>(sourceSceneId, targetSceneId);
        }

        /// <summary>
        /// Writes one tagged editor payload for the supplied mappings.
        /// </summary>
        /// <param name="mappings">Mappings to encode into the payload.</param>
        /// <returns>Tagged editor payload bytes.</returns>
        byte[] WriteTaggedEditorPayload(params KeyValuePair<string, string>[] mappings) {
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField("InitialSceneId", fieldWriter => fieldWriter.WriteString(string.Empty));
            writer.WriteField("MappingCount", fieldWriter => fieldWriter.WriteInt32(mappings.Length));

            for (int index = 0; index < mappings.Length; index++) {
                KeyValuePair<string, string> mapping = mappings[index];
                writer.WriteField($"MappingSource{index}", fieldWriter => fieldWriter.WriteString(mapping.Key));
                writer.WriteField($"MappingTarget{index}", fieldWriter => fieldWriter.WriteString(mapping.Value));
            }

            return writer.BuildPayload();
        }

        /// <summary>
        /// Writes one strict cooked runtime payload for the supplied mappings.
        /// </summary>
        /// <param name="version">Cooked runtime payload version to encode.</param>
        /// <param name="mappings">Mappings to encode into the payload.</param>
        /// <returns>Cooked runtime payload bytes.</returns>
        byte[] WriteCookedRuntimePayload(byte version, string initialSceneId, params KeyValuePair<string, string>[] mappings) {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(version);
            if (version >= 2) {
                writer.WriteString(initialSceneId);
            }
            writer.WriteInt32(mappings.Length);

            for (int index = 0; index < mappings.Length; index++) {
                writer.WriteString(mappings[index].Key);
                writer.WriteString(mappings[index].Value);
            }

            return stream.ToArray();
        }
    }
}
