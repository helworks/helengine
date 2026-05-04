using helengine;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the dedicated tab wrapper keeps tab-style defaults centralized.
    /// </summary>
    public sealed class TabComponentTests {
        /// <summary>
        /// Ensures the tab wrapper starts with top corners and updates its selected state explicitly.
        /// </summary>
        [Fact]
        public void Constructor_UsesTopCornersAndTracksSelectionState() {
            TabComponent tab = new TabComponent("Windows", new int2(96, 24), null, null);

            Assert.Equal(RoundedRectCorners.TopLeft | RoundedRectCorners.TopRight, tab.Corners);
            Assert.False(tab.IsSelected);
            Assert.False(tab.IsKeyboardFocused);

            tab.SetSelected(true);

            Assert.True(tab.IsSelected);
            Assert.True(tab.IsKeyboardFocused);
        }
    }
}
