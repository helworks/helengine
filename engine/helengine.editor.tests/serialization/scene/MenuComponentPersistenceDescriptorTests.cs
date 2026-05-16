using helengine.editor;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies scene persistence for the baked demo menu root component descriptor.
    /// </summary>
    public class MenuComponentPersistenceDescriptorTests {
        /// <summary>
        /// Ensures menu-root deserialization accepts the strict runtime payload shape written by cooked scene packaging.
        /// </summary>
        [Fact]
        public void DeserializeComponent_WhenPayloadUsesCookedRuntimeLayout_LoadsTheComponent() {
            MenuComponentPersistenceDescriptor descriptor = new MenuComponentPersistenceDescriptor();
            SceneComponentAssetRecord record = new SceneComponentAssetRecord {
                ComponentTypeId = descriptor.ComponentTypeId,
                ComponentIndex = 0,
                Payload = WriteCookedRuntimePayload()
            };

            MenuComponent loadedComponent = Assert.IsType<MenuComponent>(descriptor.DeserializeComponent(record, null, null));

            Assert.Equal("city.ui.DemoDiscMenuProvider, gameplay", loadedComponent.ProviderTypeName);
            Assert.Equal("main", loadedComponent.InitialPanelId);
        }

        /// <summary>
        /// Writes one strict runtime menu-root payload matching the cooked scene layout used by packaged builds.
        /// </summary>
        /// <returns>Serialized cooked-runtime payload.</returns>
        byte[] WriteCookedRuntimePayload() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(MenuComponent.CurrentVersion);
            writer.WriteString("city.ui.DemoDiscMenuProvider, gameplay");
            writer.WriteString("main");
            return stream.ToArray();
        }
    }
}
