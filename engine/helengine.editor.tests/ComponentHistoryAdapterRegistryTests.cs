using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies component history adapters can be registered ergonomically and resolved for exact or assignable component types.
    /// </summary>
    public sealed class ComponentHistoryAdapterRegistryTests {
        /// <summary>
        /// Ensures the generic registration overload resolves the supplied adapter for the exact component type.
        /// </summary>
        [Fact]
        public void Register_generic_overload_resolves_the_adapter_for_the_exact_component_type() {
            ComponentHistoryAdapterRegistry registry = new ComponentHistoryAdapterRegistry();
            DefaultComponentHistoryAdapter adapter = new DefaultComponentHistoryAdapter();

            registry.Register<SpriteComponent>(adapter);

            IComponentHistoryAdapter resolvedAdapter = registry.Resolve(new SpriteComponent());

            Assert.Same(adapter, resolvedAdapter);
        }

        /// <summary>
        /// Ensures registrations on a base component type also resolve for derived component instances.
        /// </summary>
        [Fact]
        public void Resolve_when_only_a_base_component_type_is_registered_uses_the_assignable_adapter() {
            ComponentHistoryAdapterRegistry registry = new ComponentHistoryAdapterRegistry();
            DefaultComponentHistoryAdapter adapter = new DefaultComponentHistoryAdapter();

            registry.Register<Component>(adapter);

            IComponentHistoryAdapter resolvedAdapter = registry.Resolve(new SpriteComponent());

            Assert.Same(adapter, resolvedAdapter);
        }

        /// <summary>
        /// Ensures the built-in fallback adapter is returned when no explicit registration matches the component.
        /// </summary>
        [Fact]
        public void Resolve_without_a_matching_registration_returns_the_default_adapter() {
            ComponentHistoryAdapterRegistry registry = new ComponentHistoryAdapterRegistry();
            IComponentHistoryAdapter resolvedAdapter = registry.Resolve(new SpriteComponent());

            Assert.IsType<DefaultComponentHistoryAdapter>(resolvedAdapter);
        }
    }
}
