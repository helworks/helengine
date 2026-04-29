namespace helengine {
    /// <summary>
    /// Maintains an entity's position relative to window edges and updates on resize.
    /// </summary>
    public class AnchorComponent : Component {
        private AnchorData anchorData;
        
        /// <summary>
        /// Gets a value indicating whether anchoring is currently enabled.
        /// </summary>
        public bool IsAnchored => anchorData != null;
        
        /// <summary>
        /// Enable anchoring with specific sides. Entity will maintain its distance from the specified screen edges.
        /// </summary>
        /// <param name="left">Anchor to left edge of screen</param>
        /// <param name="right">Anchor to right edge of screen</param>
        /// <param name="top">Anchor to top edge of screen</param>
        /// <param name="bottom">Anchor to bottom edge of screen</param>
        public void EnableAnchoring(bool left = false, bool right = false, bool top = false, bool bottom = false) {
            if (!left && !right && !top && !bottom) {
                DisableAnchoring();
                return;
            }
            
            var windowSize = Core.Instance.RenderManager3D.MainWindowSize;
            anchorData = new AnchorData {
                LeftDistance = left ? Parent.Position.X : null,
                RightDistance = right ? windowSize.X - Parent.Position.X : null,
                TopDistance = top ? Parent.Position.Y : null,
                BottomDistance = bottom ? windowSize.Y - Parent.Position.Y : null
            };
            
            // Subscribe to window resize events
            Core.Instance.RenderManager3D.WindowResized += OnWindowResized;
        }
        
        /// <summary>
        /// Disable anchoring and stop responding to window resize events
        /// </summary>
        public void DisableAnchoring() {
            if (anchorData != null) {
                Core.Instance.RenderManager3D.WindowResized -= OnWindowResized;
                anchorData = null;
            }
        }
        
        /// <summary>
        /// Set anchor distances manually (useful for runtime adjustments)
        /// </summary>
        public void SetAnchorDistances(Nullable<float> left = null, Nullable<float> right = null, Nullable<float> top = null, Nullable<float> bottom = null) {
            if (anchorData == null) {
                anchorData = new AnchorData();
                Core.Instance.RenderManager3D.WindowResized += OnWindowResized;
            }
            
            anchorData.LeftDistance = left;
            anchorData.RightDistance = right;
            anchorData.TopDistance = top;
            anchorData.BottomDistance = bottom;
            
            // If all distances are null, disable anchoring
            if (!left.HasValue && !right.HasValue && !top.HasValue && !bottom.HasValue) {
                DisableAnchoring();
            }
        }
        
        /// <summary>
        /// Cleans up anchoring resources when the component is removed from its parent entity.
        /// </summary>
        /// <param name="entity">Entity the component was attached to.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);
            DisableAnchoring();
        }
        
        /// <summary>
        /// Handles window resize events to reposition anchored entities.
        /// </summary>
        private void OnWindowResized(IntPtr handle, int newWidth, int newHeight) {
            if (anchorData == null || Parent == null) return;
            
            var pos = Parent.Position;
            
            // Handle horizontal anchoring (left takes priority over right)
            if (anchorData.LeftDistance.HasValue) {
                pos.X = anchorData.LeftDistance.Value;
            } else if (anchorData.RightDistance.HasValue) {
                pos.X = newWidth - anchorData.RightDistance.Value;
            }
            
            // Handle vertical anchoring (top takes priority over bottom)
            if (anchorData.TopDistance.HasValue) {
                pos.Y = anchorData.TopDistance.Value;
            } else if (anchorData.BottomDistance.HasValue) {
                pos.Y = newHeight - anchorData.BottomDistance.Value;
            }
            
            Parent.Position = pos;
        }
        
        /// <summary>
        /// Get current anchor configuration as a readable string
        /// </summary>
        public string GetAnchorInfo() {
            if (!IsAnchored) return "Not anchored";
            
            var info = "Anchored to: ";
            var anchors = new List<string>();
            
            if (anchorData.LeftDistance.HasValue) anchors.Add($"Left ({anchorData.LeftDistance.Value:F1}px)");
            if (anchorData.RightDistance.HasValue) anchors.Add($"Right ({anchorData.RightDistance.Value:F1}px)");
            if (anchorData.TopDistance.HasValue) anchors.Add($"Top ({anchorData.TopDistance.Value:F1}px)");
            if (anchorData.BottomDistance.HasValue) anchors.Add($"Bottom ({anchorData.BottomDistance.Value:F1}px)");
            
            return info + string.Join(", ", anchors);
        }
        
        /// <summary>
        /// Internal data structure to store anchor distances. Only allocated when anchoring is enabled.
        /// </summary>
        private class AnchorData {
            /// <summary>
            /// Distance from the left edge of the window in pixels.
            /// </summary>
            public Nullable<float> LeftDistance { get; set; }
            /// <summary>
            /// Distance from the right edge of the window in pixels.
            /// </summary>
            public Nullable<float> RightDistance { get; set; }
            /// <summary>
            /// Distance from the top edge of the window in pixels.
            /// </summary>
            public Nullable<float> TopDistance { get; set; }
            /// <summary>
            /// Distance from the bottom edge of the window in pixels.
            /// </summary>
            public Nullable<float> BottomDistance { get; set; }
        }
    }
}
