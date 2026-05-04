using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies menu-host component scene persistence round-trips the provider type reference.
    /// </summary>
    public class MenuHostComponentPersistenceDescriptorTests {
        /// <summary>
        /// Ensures menu-host component persistence preserves the provider type name.
        /// </summary>
        [Fact]
        public void SerializeAndDeserialize_WhenProviderTypeNameIsConfigured_RoundTripsTheValue() {
            MenuHostComponentPersistenceDescriptor descriptor = new MenuHostComponentPersistenceDescriptor();
            MenuHostComponent component = new MenuHostComponent {
                ProviderTypeName = typeof(TestMenuDefinitionProvider).AssemblyQualifiedName
            };

            SceneComponentAssetRecord record = descriptor.SerializeComponent(component, 0, null);
            MenuHostComponent loadedComponent = Assert.IsType<MenuHostComponent>(descriptor.DeserializeComponent(record, null, new TestSceneAssetReferenceResolver()));

            Assert.Equal(MenuHostComponent.SerializedComponentTypeId, record.ComponentTypeId);
            Assert.Equal(component.ProviderTypeName, loadedComponent.ProviderTypeName);
        }
    }
}
