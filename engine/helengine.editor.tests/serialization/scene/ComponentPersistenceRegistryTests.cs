using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies explicit registration and lookup for scene component persistence descriptors.
    /// </summary>
    public class ComponentPersistenceRegistryTests {
        /// <summary>
        /// Ensures registered descriptors can be resolved by component instance and serialized type id.
        /// </summary>
        [Fact]
        public void Register_WhenDescriptorIsAdded_ResolvesItByComponentTypeAndTypeId() {
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            MeshComponentPersistenceDescriptor descriptor = new MeshComponentPersistenceDescriptor();
            MeshComponent meshComponent = new MeshComponent();

            registry.Register(descriptor);

            Assert.Same(descriptor, registry.GetDescriptor(meshComponent));
            Assert.Same(descriptor, registry.GetDescriptor(descriptor.ComponentTypeId));
        }

        /// <summary>
        /// Ensures eligible scripted components without explicit descriptors resolve through the automatic reflected fallback.
        /// </summary>
        [Fact]
        public void GetDescriptor_WhenScriptComponentHasNoExplicitDescriptor_ReturnsAutomaticFallback() {
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            TestScriptSerializableComponent component = new TestScriptSerializableComponent();

            IComponentPersistenceDescriptor descriptorByComponent = registry.GetDescriptor(component);
            IComponentPersistenceDescriptor descriptorByTypeId = registry.GetDescriptor(
                AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(TestScriptSerializableComponent)));

            Assert.IsType<AutomaticScriptComponentPersistenceDescriptor>(descriptorByComponent);
            Assert.Same(descriptorByComponent, descriptorByTypeId);
        }

        /// <summary>
        /// Ensures unsupported component types fail with a clear error instead of being silently ignored.
        /// </summary>
        [Fact]
        public void GetDescriptor_WhenComponentTypeIsUnsupported_ThrowsClearError() {
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => registry.GetDescriptor(new AnchorComponent()));

            Assert.Contains(nameof(AnchorComponent), exception.Message, StringComparison.Ordinal);
        }
    }
}
