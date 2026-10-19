namespace helengine {
    /// <summary>Stores only original LayoutComponent layout fields in the optional typed cache.</summary>
    internal struct ViewportAnchorState {
        /// <summary>Borrowed participant whose lifetime is controlled by its entity.</summary>
        public LayoutComponent Component;
        /// <summary>Original AnchorDistances before owner layout processing.</summary>
        public float4 AnchorDistances;
        /// <summary>Captures this participant without allocating a record object.</summary>
        public ViewportAnchorState([NativeRetainsBorrow] LayoutComponent component) {
            Component = component;
            AnchorDistances = component.AnchorDistances;
        }
        /// <summary>Restores the retained baseline before absolute scaling.</summary>
        public void Restore() {
            Component.AnchorDistances = AnchorDistances;
        }
        /// <summary>Maps only deliberate preview edits back into authored space.</summary>
        public void RestoreEdited(ViewportAnchorState applied, in ViewportLayoutMetrics metrics) {
            float4 restored = Component.AnchorDistances;
            restored.X = AuthoredLayoutScaleUtils.UnscaleFloat(Component.AnchorDistances.X, applied.AnchorDistances.X, AnchorDistances.X, metrics.ScaleX);
            restored.Y = AuthoredLayoutScaleUtils.UnscaleFloat(Component.AnchorDistances.Y, applied.AnchorDistances.Y, AnchorDistances.Y, metrics.ScaleX);
            restored.Z = AuthoredLayoutScaleUtils.UnscaleFloat(Component.AnchorDistances.Z, applied.AnchorDistances.Z, AnchorDistances.Z, metrics.ScaleY);
            restored.W = AuthoredLayoutScaleUtils.UnscaleFloat(Component.AnchorDistances.W, applied.AnchorDistances.W, AnchorDistances.W, metrics.ScaleY);
            Component.AnchorDistances = restored;
        }
    }
}
