using Xunit;

namespace helengine.editor.windows.tests.utils {
    /// <summary>
    /// Verifies the editor host resolves native cursors from docking, resize, and hovered interactable cursor state.
    /// </summary>
    public sealed class EditorHostCursorResolverTests {
        /// <summary>
        /// Ensures text-hovered interactables resolve to the native I-beam cursor when no higher-priority override is active.
        /// </summary>
        [Fact]
        public void Resolve_WhenHoverCursorRequestsText_ReturnsIBeam() {
            Cursor cursor = EditorHostCursorResolver.Resolve(
                DockingCursorState.Default,
                PointerCursorKind.Text,
                false,
                Cursors.Default);

            Assert.Same(Cursors.IBeam, cursor);
        }

        /// <summary>
        /// Ensures resize cursors override hovered interactable cursor requests.
        /// </summary>
        [Fact]
        public void Resolve_WhenResizeCursorIsAvailable_ReturnsResizeCursor() {
            Cursor cursor = EditorHostCursorResolver.Resolve(
                DockingCursorState.Default,
                PointerCursorKind.Text,
                true,
                Cursors.SizeWE);

            Assert.Same(Cursors.SizeWE, cursor);
        }
    }
}
