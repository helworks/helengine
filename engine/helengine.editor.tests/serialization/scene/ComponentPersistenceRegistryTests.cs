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
        /// Ensures eligible built-in engine components without explicit descriptors also resolve through the automatic reflected fallback.
        /// </summary>
        [Fact]
        public void GetDescriptor_WhenEngineComponentHasNoExplicitDescriptor_ReturnsAutomaticFallback() {
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            LineRendererComponent component = new LineRendererComponent();

            IComponentPersistenceDescriptor descriptorByComponent = registry.GetDescriptor(component);
            IComponentPersistenceDescriptor descriptorByTypeId = registry.GetDescriptor(
                AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(LineRendererComponent)));

            Assert.IsType<AutomaticScriptComponentPersistenceDescriptor>(descriptorByComponent);
            Assert.Same(descriptorByComponent, descriptorByTypeId);
        }

        /// <summary>
        /// Ensures unresolved serialized component type ids fail with a clear error instead of being silently ignored.
        /// </summary>
        [Fact]
        public void GetDescriptor_WhenComponentTypeIsUnsupported_ThrowsClearError() {
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => registry.GetDescriptor("helengine.MissingComponent"));

            Assert.Contains("helengine.MissingComponent", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures removed engine-owned directional-shadow component ids are no longer exposed through the registry.
        /// </summary>
        [Fact]
        public void GetDescriptor_WhenDirectionalShadowEngineTypeIdsAreRequested_ThrowsClearError() {
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();

            InvalidOperationException cameraException = Assert.Throws<InvalidOperationException>(() => registry.GetDescriptor("helengine.DirectionalShadowCameraOrbitComponent"));
            InvalidOperationException orbitException = Assert.Throws<InvalidOperationException>(() => registry.GetDescriptor("helengine.DirectionalShadowOrbitComponent"));
            InvalidOperationException sunSweepException = Assert.Throws<InvalidOperationException>(() => registry.GetDescriptor("helengine.DirectionalShadowSunSweepComponent"));
            InvalidOperationException towerSpinException = Assert.Throws<InvalidOperationException>(() => registry.GetDescriptor("helengine.DirectionalShadowTowerSpinComponent"));

            Assert.Contains("helengine.DirectionalShadowCameraOrbitComponent", cameraException.Message, StringComparison.Ordinal);
            Assert.Contains("helengine.DirectionalShadowOrbitComponent", orbitException.Message, StringComparison.Ordinal);
            Assert.Contains("helengine.DirectionalShadowSunSweepComponent", sunSweepException.Message, StringComparison.Ordinal);
            Assert.Contains("helengine.DirectionalShadowTowerSpinComponent", towerSpinException.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the editor session persistence-registry factory wires the script type resolver so project script components can load from scene files.
        /// </summary>
        [Fact]
        public void EditorSessionPersistenceRegistryFactory_WhenScriptResolverIsProvided_UsesItForAutomaticScriptComponentResolution() {
            FakeScriptTypeResolver scriptTypeResolver = new FakeScriptTypeResolver(typeof(TestScriptSerializableComponent));
            var factoryMethod = typeof(EditorSession).GetMethod("CreateComponentPersistenceRegistry", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            Assert.NotNull(factoryMethod);

            ComponentPersistenceRegistry registry = Assert.IsType<ComponentPersistenceRegistry>(factoryMethod.Invoke(null, new object[] { scriptTypeResolver }));

            IComponentPersistenceDescriptor descriptor = registry.GetDescriptor("city.rendering.DirectionalShadowCameraOrbitComponent, gameplay");

            Assert.IsType<AutomaticScriptComponentPersistenceDescriptor>(descriptor);
        }
    }
}
