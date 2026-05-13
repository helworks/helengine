using Xunit;

namespace helengine.editor.tests.menu {
    /// <summary>
    /// Verifies the core menu definition keeps optional overlay layout data without changing existing authored menu fields.
    /// </summary>
    public class MenuDefinitionTests {
        /// <summary>
        /// Ensures the optional platform-info overlay descriptor can be passed through the menu definition unchanged.
        /// </summary>
        [Fact]
        public void MenuDefinition_WhenPlatformInfoOverlayIsProvided_PreservesIt() {
            MenuPlatformInfoDefinition platformInfoOverlay = new MenuPlatformInfoDefinition(32, 44, 12);

            MenuDefinition definition = new MenuDefinition(
                string.Empty,
                string.Empty,
                "main",
                "Fonts/DemoDiscTitle.ttf",
                "Fonts/DemoDiscBody.ttf",
                new byte4(30, 17, 41, 255),
                new byte4(60, 41, 76, 232),
                new byte4(135, 94, 163, 255),
                new byte4(201, 147, 255, 255),
                new byte4(118, 219, 209, 255),
                new byte4(249, 243, 255, 255),
                new byte4(211, 198, 228, 255),
                new[] {
                    new MenuPanelDefinition(
                        "main",
                        "Main",
                        string.Empty,
                        1,
                        new[] {
                            new MenuItemDefinition("back", "Back", string.Empty, true, new MenuActionDefinition(MenuActionKind.Back, string.Empty))
                        })
                },
                null,
                platformInfoOverlay);

            Assert.Same(platformInfoOverlay, definition.PlatformInfoOverlay);
        }
    }
}
