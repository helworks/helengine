namespace helengine.editor.tests.testing {
    /// <summary>
    /// Minimal project-authored editor menu provider used to verify contributed menu discovery from loaded editor assemblies.
    /// </summary>
    public sealed class TestEditorMenuItemProvider : IEditorMenuItemProvider {
        /// <summary>
        /// Returns the deterministic demo menu contribution used by the host tests.
        /// </summary>
        /// <returns>One contributed demo menu item.</returns>
        public IReadOnlyList<EditorMenuItemDescriptor> GetMenuItems() {
            return [
                new EditorMenuItemDescriptor(
                    "demo",
                    "Demo",
                    100,
                    "demo.regenerate-main-menu",
                    "Regenerate Main Menu...",
                    100,
                    "menu.regenerate-demo-disc-main-menu")
            ];
        }
    }
}
