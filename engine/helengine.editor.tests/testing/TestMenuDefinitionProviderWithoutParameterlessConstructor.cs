namespace helengine.editor.tests.testing {
    /// <summary>
    /// Menu-definition provider test double that intentionally lacks a public parameterless constructor.
    /// </summary>
    internal sealed class TestMenuDefinitionProviderWithoutParameterlessConstructor : IMenuDefinitionProvider {
        /// <summary>
        /// Initializes the provider with an unused value so resolver tests can verify strict constructor requirements.
        /// </summary>
        /// <param name="name">Unused constructor argument.</param>
        public TestMenuDefinitionProviderWithoutParameterlessConstructor(string name) {
            Name = name ?? string.Empty;
        }

        /// <summary>
        /// Gets the constructor argument recorded by the test double.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Builds one minimal menu definition.
        /// </summary>
        /// <returns>Minimal menu definition for tests that instantiate the provider directly.</returns>
        public MenuDefinition CreateMenuDefinition() {
            return new MenuDefinition(
                "Invalid",
                string.Empty,
                "main",
                "fonts/title.hefont",
                "fonts/body.hefont",
                new byte4(0, 0, 0, 255),
                new byte4(0, 0, 0, 255),
                new byte4(0, 0, 0, 255),
                new byte4(0, 0, 0, 255),
                new byte4(0, 0, 0, 255),
                new byte4(255, 255, 255, 255),
                new byte4(128, 128, 128, 255),
                new[] {
                    new MenuPanelDefinition(
                        "main",
                        "Main",
                        string.Empty,
                        1,
                        new[] {
                            new MenuItemDefinition("noop", "Noop", string.Empty, true, new MenuActionDefinition(MenuActionKind.None, string.Empty))
                        })
                });
        }
    }
}
