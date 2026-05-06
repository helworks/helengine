namespace helengine.editor.tests.testing {
    /// <summary>
    /// Contributes one duplicate menu item descriptor when explicitly enabled for validation tests.
    /// </summary>
    public sealed class DuplicateTestEditorMenuItemProvider : IEditorMenuItemProvider {
        /// <summary>
        /// Environment variable that enables the duplicate contribution for one targeted test.
        /// </summary>
        public const string EnabledEnvironmentVariableName = "HELENGINE_TEST_ENABLE_DUPLICATE_MENU_PROVIDER";

        /// <summary>
        /// Returns one duplicate descriptor only when the matching test explicitly enables it.
        /// </summary>
        /// <returns>Either one duplicate descriptor or an empty list.</returns>
        public IReadOnlyList<EditorMenuItemDescriptor> GetMenuItems() {
            if (!string.Equals(Environment.GetEnvironmentVariable(EnabledEnvironmentVariableName), "1", StringComparison.Ordinal)) {
                return Array.Empty<EditorMenuItemDescriptor>();
            }

            return [
                new EditorMenuItemDescriptor(
                    "demo",
                    "Demo",
                    100,
                    "demo.regenerate-main-menu",
                    "Regenerate Main Menu...",
                    200,
                    "menu.regenerate-demo-disc-main-menu")
            ];
        }
    }
}
