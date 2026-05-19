namespace helengine.editor {
    /// <summary>
    /// Applies consistent viewport and item-count configuration to editor scroll components.
    /// </summary>
    public static class EditorScrollComponentLayout {
        /// <summary>
        /// Configures one scroll component so it resolves its visible item count from the current viewport height and item extent.
        /// </summary>
        /// <param name="scrollComponent">Scroll component to configure.</param>
        /// <param name="viewportSize">Viewport width and height used for clipping and automatic visible-count resolution.</param>
        /// <param name="itemExtent">Height of one logical item in viewport units.</param>
        /// <param name="itemCount">Total number of logical items available for scrolling.</param>
        public static void ConfigureAutomaticVisibleItems(ScrollComponent scrollComponent, int2 viewportSize, int itemExtent, int itemCount) {
            if (scrollComponent == null) {
                throw new ArgumentNullException(nameof(scrollComponent));
            }
            if (viewportSize.X < 0 || viewportSize.Y < 0) {
                throw new ArgumentOutOfRangeException(nameof(viewportSize), "Viewport size cannot be negative.");
            }
            if (itemExtent <= 0) {
                throw new ArgumentOutOfRangeException(nameof(itemExtent), "Item extent must be positive.");
            }
            if (itemCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(itemCount), "Item count cannot be negative.");
            }

            scrollComponent.Size = viewportSize;
            scrollComponent.ItemExtent = itemExtent;
            scrollComponent.ItemCount = itemCount;
            scrollComponent.VisibleItemCount = 0;
        }

        /// <summary>
        /// Configures one scroll component so it uses an explicit visible item count instead of deriving that count from the viewport height.
        /// </summary>
        /// <param name="scrollComponent">Scroll component to configure.</param>
        /// <param name="viewportSize">Viewport width and height used for clipping.</param>
        /// <param name="itemExtent">Height of one logical item in viewport units.</param>
        /// <param name="itemCount">Total number of logical items available for scrolling.</param>
        /// <param name="visibleItemCount">Explicit number of visible items that should fit inside the configured viewport.</param>
        public static void ConfigureExplicitVisibleItems(ScrollComponent scrollComponent, int2 viewportSize, int itemExtent, int itemCount, int visibleItemCount) {
            if (scrollComponent == null) {
                throw new ArgumentNullException(nameof(scrollComponent));
            }
            if (viewportSize.X < 0 || viewportSize.Y < 0) {
                throw new ArgumentOutOfRangeException(nameof(viewportSize), "Viewport size cannot be negative.");
            }
            if (itemExtent <= 0) {
                throw new ArgumentOutOfRangeException(nameof(itemExtent), "Item extent must be positive.");
            }
            if (itemCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(itemCount), "Item count cannot be negative.");
            }
            if (visibleItemCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(visibleItemCount), "Visible item count cannot be negative.");
            }

            scrollComponent.Size = viewportSize;
            scrollComponent.ItemExtent = itemExtent;
            scrollComponent.ItemCount = itemCount;
            scrollComponent.VisibleItemCount = visibleItemCount;
        }
    }
}
