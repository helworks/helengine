namespace helengine.editor.tests.testing {
    /// <summary>
    /// Deterministic menu-definition provider used by resolver and runtime menu-host tests.
    /// </summary>
    internal sealed class TestMenuDefinitionProvider : IMenuDefinitionProvider {
        /// <summary>
        /// Builds one deterministic test menu definition.
        /// </summary>
        /// <returns>Reusable menu definition with three panels and stable colors.</returns>
        public MenuDefinition CreateMenuDefinition() {
            return new MenuDefinition(
                "Demo Disc",
                "A playable city sampler",
                "main",
                "fonts/title.hefont",
                "fonts/body.hefont",
                new byte4(34, 19, 49, 255),
                new byte4(63, 42, 83, 230),
                new byte4(120, 86, 153, 255),
                new byte4(194, 138, 255, 255),
                new byte4(129, 214, 211, 255),
                new byte4(250, 244, 255, 255),
                new byte4(215, 201, 232, 255),
                new[] {
                    new MenuPanelDefinition(
                        "main",
                        "Main Menu",
                        "Choose a destination.",
                        6,
                        new[] {
                            new MenuItemDefinition("select-scene", "Select Scene", "Browse the curated scene list.", true, new MenuActionDefinition(MenuActionKind.OpenPanel, "scene-select")),
                            new MenuItemDefinition("options", "Options", "Preview the upcoming settings shell.", true, new MenuActionDefinition(MenuActionKind.OpenPanel, "options"))
                        }),
                    new MenuPanelDefinition(
                        "scene-select",
                        "Select Scene",
                        "Launch a packaged scene.",
                        6,
                        new[] {
                            new MenuItemDefinition("scene-one", "Downtown Morning", "Opens the sample city scene.", true, new MenuActionDefinition(MenuActionKind.LoadScene, "TestPlayableScene")),
                            new MenuItemDefinition("scene-back", "Back", "Returns to the main menu.", true, new MenuActionDefinition(MenuActionKind.Back, string.Empty))
                        }),
                    new MenuPanelDefinition(
                        "options",
                        "Options",
                        "Coming soon.",
                        6,
                        new[] {
                            new MenuItemDefinition("options-back", "Back", "Returns to the main menu.", true, new MenuActionDefinition(MenuActionKind.Back, string.Empty))
                        })
                });
        }
    }
}
