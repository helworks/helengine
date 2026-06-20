using helengine.editor;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies the scene-map component now flows through the shared automatic scene persistence and runtime loading systems.
    /// </summary>
    public sealed class SceneMapComponentPersistenceDescriptorTests {
        /// <summary>
        /// Ensures the scene-map component round-trips through the shared automatic editor persistence descriptor.
        /// </summary>
        [Fact]
        public void SerializeComponent_WhenSceneMapUsesAutomaticPersistence_RoundTripsInitialSceneIdAndMappings() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            SceneMapComponent sceneMapComponent = new SceneMapComponent {
                InitialSceneId = "MainMenuScene"
            };
            sceneMapComponent.Mappings.Add("MainMenuScene", "AlternateMainMenuScene");
            sceneMapComponent.Mappings.Add("OptionsMenu", "OptionsMenuScene");

            SceneComponentAssetRecord record = descriptor.SerializeComponent(sceneMapComponent, 3, null);
            SceneMapComponent loadedComponent = Assert.IsType<SceneMapComponent>(descriptor.DeserializeComponent(record, null, null));

            Assert.Equal(AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(SceneMapComponent)), record.ComponentTypeId);
            Assert.Equal(3, record.ComponentIndex);
            Assert.Equal("MainMenuScene", loadedComponent.InitialSceneId);
            Assert.Equal("AlternateMainMenuScene", loadedComponent.Mappings["MainMenuScene"]);
            Assert.Equal("OptionsMenuScene", loadedComponent.Mappings["OptionsMenu"]);
        }

        /// <summary>
        /// Ensures the component persistence registry resolves scene-map components through the automatic reflected descriptor instead of one explicit bespoke registration.
        /// </summary>
        [Fact]
        public void GetDescriptor_WhenSceneMapComponentIsRequested_ReturnsAutomaticDescriptor() {
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();

            IComponentPersistenceDescriptor descriptor = registry.GetDescriptor(new SceneMapComponent());

            Assert.IsType<AutomaticScriptComponentPersistenceDescriptor>(descriptor);
        }

        /// <summary>
        /// Ensures packaged automatic runtime payloads can restore scene-map mappings through the shared automatic runtime deserializer.
        /// </summary>
        [Fact]
        public void Deserialize_WhenAutomaticRuntimeSceneMapPayloadIsValid_LoadsMappings() {
            AutomaticScriptComponentRuntimeDeserializer deserializer = new AutomaticScriptComponentRuntimeDeserializer(
                AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(SceneMapComponent)),
                typeof(SceneMapComponent));
            SceneComponentAssetRecord record = new SceneComponentAssetRecord {
                ComponentTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(SceneMapComponent)),
                ComponentIndex = 0,
                Payload = WriteAutomaticRuntimePayload("MainMenuScene", CreateMapping("MainMenu", "MainMenuScene"))
            };

            SceneMapComponent loadedComponent = Assert.IsType<SceneMapComponent>(deserializer.Deserialize(record, null));

            Assert.Equal("MainMenuScene", loadedComponent.InitialSceneId);
            Assert.Equal("MainMenuScene", loadedComponent.Mappings["MainMenu"]);
        }

        /// <summary>
        /// Creates one deterministic mapping entry used by the scene-map automatic persistence tests.
        /// </summary>
        /// <param name="sourceSceneId">Logical source scene id.</param>
        /// <param name="targetSceneId">Resolved runtime target scene id.</param>
        /// <returns>One deterministic mapping entry.</returns>
        static KeyValuePair<string, string> CreateMapping(string sourceSceneId, string targetSceneId) {
            return new KeyValuePair<string, string>(sourceSceneId, targetSceneId);
        }

        /// <summary>
        /// Writes one strict automatic runtime payload for the supplied scene-map values.
        /// </summary>
        /// <param name="initialSceneId">Initial scene id to encode.</param>
        /// <param name="mappings">Scene-id mappings to encode.</param>
        /// <returns>Automatic runtime payload bytes.</returns>
        static byte[] WriteAutomaticRuntimePayload(string initialSceneId, params KeyValuePair<string, string>[] mappings) {
            SceneMapComponent component = new SceneMapComponent {
                InitialSceneId = initialSceneId ?? string.Empty
            };
            for (int index = 0; index < mappings.Length; index++) {
                component.Mappings.Add(mappings[index].Key, mappings[index].Value);
            }

            ScriptComponentReflectionSchemaBuilder schemaBuilder = new ScriptComponentReflectionSchemaBuilder();
            ScriptComponentReflectionSchema schema = schemaBuilder.Build(typeof(SceneMapComponent));
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion);
            writer.WriteInt32(schema.Members.Count);
            for (int index = 0; index < schema.Members.Count; index++) {
                ScriptComponentReflectionMember member = schema.Members[index];
                AutomaticScriptComponentPersistenceDescriptor.WriteSupportedMemberValue(writer, member, component, null);
            }

            return stream.ToArray();
        }
    }
}
