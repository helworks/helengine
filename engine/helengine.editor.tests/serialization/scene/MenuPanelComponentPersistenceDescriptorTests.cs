using helengine.editor;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies scene persistence for the baked demo menu panel component descriptor.
    /// </summary>
    public class MenuPanelComponentPersistenceDescriptorTests {
        /// <summary>
        /// Ensures menu-panel deserialization accepts the strict runtime payload shape written by cooked scene packaging.
        /// </summary>
        [Fact]
        public void DeserializeComponent_WhenPayloadUsesCookedRuntimeLayout_LoadsTheComponent() {
            MenuPanelComponentPersistenceDescriptor descriptor = new MenuPanelComponentPersistenceDescriptor();
            SceneComponentAssetRecord record = new SceneComponentAssetRecord {
                ComponentTypeId = descriptor.ComponentTypeId,
                ComponentIndex = 0,
                Payload = WriteCookedRuntimePayload()
            };

            MenuPanelComponent loadedComponent = Assert.IsType<MenuPanelComponent>(descriptor.DeserializeComponent(record, null, null));

            Assert.Equal("main", loadedComponent.PanelId);
        }

        /// <summary>
        /// Writes one strict runtime menu-panel payload matching the cooked scene layout used by packaged builds.
        /// </summary>
        /// <returns>Serialized cooked-runtime payload.</returns>
        byte[] WriteCookedRuntimePayload() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(MenuPanelComponent.CurrentVersion);
            writer.WriteString("main");
            return stream.ToArray();
        }
    }
}
