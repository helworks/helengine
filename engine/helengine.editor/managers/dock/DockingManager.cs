namespace helengine.editor {
    /// <summary>
    /// Coordinates dock layout, resize handles, and dock preview overlays.
    /// </summary>
    public class DockingManager {
        /// <summary>
        /// Layout engine used to arrange docked panels.
        /// </summary>
        readonly DockLayoutEngine layout;
        /// <summary>
        /// Overlay used to preview docking targets during drag operations.
        /// </summary>
        readonly DockPreviewOverlay previewOverlay;
        /// <summary>
        /// Last dockable entity that was dragged, used to finalize docking.
        /// </summary>
        DockableEntity? lastDragging;
        /// <summary>
        /// Tracks whether a docking hint is currently valid.
        /// </summary>
        bool dockHintValid;
        /// <summary>
        /// Stores the active docking hint during drag operations.
        /// </summary>
        DockHint dockHint;
        /// <summary>
        /// Tracks the last left mouse button state to gate resize start.
        /// </summary>
        ButtonState lastLeftButtonState = ButtonState.Released;
        /// <summary>
        /// Tracks the current cursor state requested by the docking system.
        /// </summary>
        DockingCursorState cursorState = DockingCursorState.Default;

        /// <summary>
        /// Initializes a new docking manager with optional padding and gap settings.
        /// </summary>
        /// <param name="padding">Space to leave around the host bounds.</param>
        /// <param name="gap">Space inserted between docked panels.</param>
        public DockingManager(int padding = 0, int gap = 0) {
            layout = new DockLayoutEngine(padding, gap);
            previewOverlay = new DockPreviewOverlay();
        }

        /// <summary>
        /// Gets the layout engine used for docking operations.
        /// </summary>
        public DockLayoutEngine Layout => layout;

        /// <summary>
        /// Gets the overlay used for docking previews.
        /// </summary>
        public DockPreviewOverlay PreviewOverlay => previewOverlay;

        /// <summary>
        /// Gets the cursor state requested by the docking system.
        /// </summary>
        public DockingCursorState CursorState => cursorState;

        /// <summary>
        /// Updates docking interactions and returns whether layout should refresh.
        /// </summary>
        /// <param name="pointer">Pointer position in screen or host coordinates.</param>
        /// <param name="leftButton">Current left mouse button state.</param>
        /// <param name="hostSize">Size of the host area.</param>
        /// <param name="origin">Origin of the host area.</param>
        /// <returns>True when the layout should be recomputed.</returns>
        public bool Update(int2 pointer, ButtonState leftButton, int2 hostSize, float3 origin) {
            bool layoutDirty = false;
            bool isDraggingDockable = IsDraggingDockable();

            layoutDirty |= UpdateResize(pointer, leftButton, hostSize, origin, isDraggingDockable);
            layoutDirty |= UpdateDockPreview(pointer, hostSize, origin);

            lastLeftButtonState = leftButton;
            return layoutDirty;
        }

        /// <summary>
        /// Handles resize interactions on dock split lines.
        /// </summary>
        /// <param name="pointer">Pointer position in screen or host coordinates.</param>
        /// <param name="leftButton">Current left mouse button state.</param>
        /// <param name="hostSize">Size of the host area.</param>
        /// <param name="origin">Origin of the host area.</param>
        /// <param name="isDraggingDockable">True when a dockable is actively being dragged.</param>
        /// <returns>True when the layout should refresh.</returns>
        bool UpdateResize(int2 pointer, ButtonState leftButton, int2 hostSize, float3 origin, bool isDraggingDockable) {
            bool layoutDirty = false;
            bool isNewPress = leftButton == ButtonState.Pressed && lastLeftButtonState == ButtonState.Released;

            if (layout.IsResizing) {
                layout.UpdateResize(pointer);
                layoutDirty = true;
                cursorState = layout.ActiveResizeIsVertical ? DockingCursorState.VerticalSplit : DockingCursorState.HorizontalSplit;
                if (leftButton == ButtonState.Released) {
                    layout.EndResize();
                }
                return layoutDirty;
            }

            if (isDraggingDockable) {
                cursorState = DockingCursorState.Default;
                return false;
            }

            if (isNewPress && layout.TryBeginResize(pointer, hostSize, origin, out bool isVertical)) {
                cursorState = isVertical ? DockingCursorState.VerticalSplit : DockingCursorState.HorizontalSplit;
                return false;
            }

            if (layout.TryGetResizeAxis(pointer, hostSize, origin, out bool hoverVertical)) {
                cursorState = hoverVertical ? DockingCursorState.VerticalSplit : DockingCursorState.HorizontalSplit;
            } else {
                cursorState = DockingCursorState.Default;
            }

            return false;
        }

        /// <summary>
        /// Handles docking previews and finalizes dock inserts.
        /// </summary>
        /// <param name="pointer">Pointer position in screen or host coordinates.</param>
        /// <param name="hostSize">Size of the host area.</param>
        /// <param name="origin">Origin of the host area.</param>
        /// <returns>True when the layout should refresh.</returns>
        bool UpdateDockPreview(int2 pointer, int2 hostSize, float3 origin) {
            if (layout.IsResizing) {
                previewOverlay.Hide();
                dockHintValid = false;
                lastDragging = null;
                return false;
            }

            bool layoutDirty = false;
            DockableEntity? dragging = null;
            var dockables = layout.Dockables;
            for (int i = 0; i < dockables.Count; i++) {
                var de = dockables[i];
                if (de.IsDragging) {
                    dragging = de;
                    break;
                }
            }

            if (dragging == null) {
                if (lastDragging != null && dockHintValid) {
                    ApplyDockHint(lastDragging);
                    layoutDirty = true;
                }
                previewOverlay.Hide();
                lastDragging = null;
                dockHintValid = false;
                return layoutDirty;
            }

            if (dragging.ConsumeUndockRequest()) {
                layout.Undock(dragging);
                layoutDirty = true;
            }

            bool fillOnly = !layout.HasDocked;

            if (layout.TryGetDockHint(pointer, hostSize, origin, fillOnly, out var hint)) {
                previewOverlay.Show(hint.Position, hint.Size);
                dockHintValid = true;
                dockHint = hint;
                lastDragging = dragging;
            } else {
                previewOverlay.Hide();
                dockHintValid = false;
                lastDragging = dragging;
            }

            return layoutDirty;
        }

        /// <summary>
        /// Applies the pending dock hint to a floating dockable entity.
        /// </summary>
        /// <param name="entity">Dockable entity to dock.</param>
        void ApplyDockHint(DockableEntity entity) {
            if (!dockHintValid) {
                return;
            }

            layout.Dock(entity, dockHint);
            dockHintValid = false;
            previewOverlay.Hide();
        }

        /// <summary>
        /// Checks whether any dockable entity is currently being dragged.
        /// </summary>
        /// <returns>True when a dockable is being dragged; otherwise false.</returns>
        bool IsDraggingDockable() {
            var dockables = layout.Dockables;
            for (int i = 0; i < dockables.Count; i++) {
                if (dockables[i].IsDragging) {
                    return true;
                }
            }

            return false;
        }
    }
}
