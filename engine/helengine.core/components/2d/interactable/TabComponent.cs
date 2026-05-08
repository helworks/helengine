namespace helengine {
    /// <summary>
    /// Specialized button used to render and manage tab-style selections with rounded top corners and explicit active-state styling.
    /// </summary>
    public class TabComponent : ButtonComponent {
        /// <summary>
        /// Gets a value indicating whether the tab is currently selected.
        /// </summary>
        public bool IsSelected => IsKeyboardFocused;

        /// <summary>
        /// Initializes a new tab button with the standard tab styling defaults.
        /// </summary>
        /// <param name="text">Label text displayed on the tab.</param>
        /// <param name="size">Tab dimensions.</param>
        /// <param name="font">Font used to render the label.</param>
        /// <param name="onClickAction">Optional callback invoked when the tab is clicked.</param>
        /// <param name="borderThickness">Border thickness in pixels.</param>
        public TabComponent(
            string text,
            int2 size,
            FontAsset font,
            Action onClickAction = null,
            float borderThickness = 2f) : base(text, size, font, onClickAction, borderThickness) {
            UseTopCorners();
            SetCornerRadius((float)(size.Y * 0.3d));
            SetTextColor(ThemeManager.Colors.AccentQuaternary);
            SetVisualPalette(
                ThemeManager.Colors.SurfacePrimary,
                ThemeManager.Colors.AccentSecondary,
                ThemeManager.Colors.AccentTertiary,
                ThemeManager.Colors.SurfaceInput,
                ThemeManager.Colors.AccentTertiary,
                ThemeManager.Colors.AccentTertiary);
        }

        /// <summary>
        /// Updates the active-state styling for the tab.
        /// </summary>
        /// <param name="isSelected">True when the tab should render as selected.</param>
        public void SetSelected(bool isSelected) {
            SetTargetFocused(isSelected);
        }
    }
}
