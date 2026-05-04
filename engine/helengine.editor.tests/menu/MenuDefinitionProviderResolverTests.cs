using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.menu {
    /// <summary>
    /// Verifies strict menu-provider resolution rules used by serialized menu-host components.
    /// </summary>
    public class MenuDefinitionProviderResolverTests {
        /// <summary>
        /// Ensures the resolver instantiates one provider from its assembly-qualified type name.
        /// </summary>
        [Fact]
        public void Resolve_WhenProviderTypeIsValid_ReturnsProviderInstance() {
            MenuDefinitionProviderResolver resolver = new MenuDefinitionProviderResolver();

            IMenuDefinitionProvider provider = resolver.Resolve(typeof(TestMenuDefinitionProvider).AssemblyQualifiedName);
            MenuDefinition definition = provider.CreateMenuDefinition();

            Assert.IsType<TestMenuDefinitionProvider>(provider);
            Assert.Equal("Demo Disc", definition.Title);
            Assert.Equal("main", definition.InitialPanelId);
            Assert.Equal(3, definition.Panels.Length);
        }

        /// <summary>
        /// Ensures missing provider types fail with a clear error.
        /// </summary>
        [Fact]
        public void Resolve_WhenProviderTypeCannotBeResolved_Throws() {
            MenuDefinitionProviderResolver resolver = new MenuDefinitionProviderResolver();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => resolver.Resolve("helengine.DoesNotExist, helengine.DoesNotExist"));

            Assert.Contains("helengine.DoesNotExist", exception.Message);
        }

        /// <summary>
        /// Ensures types that do not implement the provider contract fail fast.
        /// </summary>
        [Fact]
        public void Resolve_WhenProviderTypeDoesNotImplementInterface_Throws() {
            MenuDefinitionProviderResolver resolver = new MenuDefinitionProviderResolver();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => resolver.Resolve(typeof(TestMenuDefinitionProviderWrongType).AssemblyQualifiedName));

            Assert.Contains(nameof(IMenuDefinitionProvider), exception.Message);
        }

        /// <summary>
        /// Ensures provider types must expose a public parameterless constructor.
        /// </summary>
        [Fact]
        public void Resolve_WhenProviderTypeHasNoPublicParameterlessConstructor_Throws() {
            MenuDefinitionProviderResolver resolver = new MenuDefinitionProviderResolver();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => resolver.Resolve(typeof(TestMenuDefinitionProviderWithoutParameterlessConstructor).AssemblyQualifiedName));

            Assert.Contains("public parameterless constructor", exception.Message);
        }
    }
}
