namespace helengine.editor {
    /// <summary>
    /// Reusable clipped scroll body for editor panels: one fixed clip host plus one scrolling content root, so
    /// panels stop hand-rolling overflow clipping and scroll translation separately.
    /// </summary>
    public sealed class EditorClippedScrollBody {
        /// <summary>
        /// Clip owner attached to the fixed host so overflow content clips against the visible body instead of the scrolling child.
        /// </summary>
        readonly ClipRectComponent ClipComponent;

        /// <summary>
        /// Body size applied by the most recent layout update.
        /// </summary>
        int2 BodySizePixels;

        /// <summary>
        /// Vertical scroll translation applied by the most recent scroll update.
        /// </summary>
        int VerticalScrollPixels;

        /// <summary>
        /// Gets the fixed host entity that should be added as a child of the owning panel.
        /// </summary>
        public EditorEntity HostEntity { get; }

        /// <summary>
        /// Gets the scrolling content root that panel content should be parented under.
        /// </summary>
        public EditorEntity ContentRoot { get; }

        /// <summary>
        /// Initializes one clipped scroll body on the supplied render layer.
        /// </summary>
        /// <param name="layerMask">Render layer shared with the owning panel.</param>
        public EditorClippedScrollBody(Core ownerCore, EditorSessionInteractionServices interactionServices, ushort layerMask) {
            HostEntity = new EditorEntity(ownerCore, interactionServices);
            HostEntity.LayerMask = layerMask;
            HostEntity.Position = float3.Zero;

            ClipComponent = new ClipRectComponent();
            HostEntity.AddComponent(ClipComponent);

            ContentRoot = new EditorEntity(ownerCore, interactionServices);
            ContentRoot.LayerMask = layerMask;
            ContentRoot.Position = float3.Zero;
            HostEntity.AddChild(ContentRoot);
        }

        /// <summary>
        /// Positions the fixed host inside the owning panel and resizes the clip region to the visible body.
        /// </summary>
        /// <param name="hostLocalPosition">Host position relative to the owning panel.</param>
        /// <param name="bodySizePixels">Visible body size in pixels.</param>
        public void UpdateLayout(float3 hostLocalPosition, int2 bodySizePixels) {
            if (bodySizePixels.X < 0 || bodySizePixels.Y < 0) {
                throw new ArgumentOutOfRangeException(nameof(bodySizePixels), "Scroll body size must not be negative.");
            }

            HostEntity.Position = hostLocalPosition;
            ClipComponent.Size = bodySizePixels;
            BodySizePixels = bodySizePixels;
        }

        /// <summary>
        /// Applies one vertical scroll translation to the content root.
        /// </summary>
        /// <param name="scrollPixels">Scrolled distance in pixels; positive values scroll content upward.</param>
        public void SetVerticalScrollPixels(int scrollPixels) {
            VerticalScrollPixels = scrollPixels;
            ContentRoot.Position = new float3(0f, -scrollPixels, 0f);
        }

        /// <summary>
        /// Returns true when one content row intersects the visible body under the current scroll translation.
        /// </summary>
        /// <param name="rowTopPixels">Row top edge in content-root pixels.</param>
        /// <param name="rowHeightPixels">Row height in pixels.</param>
        /// <returns>True when any part of the row is visible.</returns>
        public bool IsRowVisible(int rowTopPixels, int rowHeightPixels) {
            if (rowHeightPixels <= 0) {
                return false;
            }

            int visibleTop = rowTopPixels - VerticalScrollPixels;
            return visibleTop + rowHeightPixels > 0 && visibleTop < BodySizePixels.Y;
        }
    }
}
