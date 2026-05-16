using helengine.editor;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies scene persistence for the baked demo menu item component descriptor.
    /// </summary>
    public class MenuItemComponentPersistenceDescriptorTests {
        /// <summary>
        /// Ensures menu-item deserialization accepts the strict runtime payload shape written by cooked scene packaging.
        /// </summary>
        [Fact]
        public void DeserializeComponent_WhenPayloadUsesCookedRuntimeLayout_LoadsTheComponent() {
            MenuItemComponentPersistenceDescriptor descriptor = new MenuItemComponentPersistenceDescriptor();
            SceneComponentAssetRecord record = new SceneComponentAssetRecord {
                ComponentTypeId = descriptor.ComponentTypeId,
                ComponentIndex = 0,
                Payload = WriteCookedRuntimePayload()
            };

            MenuItemComponent loadedComponent = Assert.IsType<MenuItemComponent>(descriptor.DeserializeComponent(record, null, null));

            Assert.Equal("main", loadedComponent.PanelId);
            Assert.Equal("start", loadedComponent.ItemId);
            Assert.Equal("Launch the game.", loadedComponent.Description);
            Assert.Equal(MenuActionKind.OpenPanel, loadedComponent.ActionKind);
            Assert.Equal("play", loadedComponent.TargetId);
            Assert.Equal(new byte4(11, 22, 33, 255), loadedComponent.IdleFillColor);
            Assert.Equal(new byte4(44, 55, 66, 255), loadedComponent.IdleBorderColor);
            Assert.Equal(new byte4(77, 88, 99, 255), loadedComponent.SelectedFillColor);
            Assert.Equal(new byte4(111, 122, 133, 255), loadedComponent.SelectedBorderColor);
        }

        /// <summary>
        /// Writes one strict runtime menu-item payload matching the cooked scene layout used by packaged builds.
        /// </summary>
        /// <returns>Serialized cooked-runtime payload.</returns>
        byte[] WriteCookedRuntimePayload() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(MenuItemComponent.CurrentVersion);
            writer.WriteString("main");
            writer.WriteString("start");
            writer.WriteString("Launch the game.");
            writer.WriteByte((byte)MenuActionKind.OpenPanel);
            writer.WriteString("play");
            SceneComponentBinaryFieldEncoding.WriteByte4(writer, new byte4(11, 22, 33, 255));
            SceneComponentBinaryFieldEncoding.WriteByte4(writer, new byte4(44, 55, 66, 255));
            SceneComponentBinaryFieldEncoding.WriteByte4(writer, new byte4(77, 88, 99, 255));
            SceneComponentBinaryFieldEncoding.WriteByte4(writer, new byte4(111, 122, 133, 255));
            return stream.ToArray();
        }
    }
}
