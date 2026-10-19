namespace helengine {
    /// <summary>Stores only original RoundedRectComponent layout fields in the optional typed cache.</summary>
    internal struct ViewportRectangleState {
        /// <summary>Borrowed participant whose lifetime is controlled by its entity.</summary>
        public RoundedRectComponent Component;
        /// <summary>Original Size before owner layout processing.</summary>
        public int2 Size;
        /// <summary>Original Radius before owner layout processing.</summary>
        public Real Radius;
        /// <summary>Original BorderThickness before owner layout processing.</summary>
        public Real BorderThickness;
        /// <summary>Captures this participant without allocating a record object.</summary>
        public ViewportRectangleState([NativeRetainsBorrow] RoundedRectComponent component) {
            Component = component;
            Size = component.Size;
            Radius = component.Radius;
            BorderThickness = component.BorderThickness;
        }
        /// <summary>Restores the retained baseline before absolute scaling.</summary>
        public void Restore() {
            Component.Size = Size;
            Component.Radius = Radius;
            Component.BorderThickness = BorderThickness;
        }
        /// <summary>Maps only deliberate preview edits back into authored space.</summary>
        public void RestoreEdited(ViewportRectangleState applied, in ViewportLayoutMetrics metrics) {
            Component.Size = AuthoredLayoutScaleUtils.UnscaleInt2(Component.Size, applied.Size, Size, metrics.ScaleX, metrics.ScaleY);
            Component.Radius = AuthoredLayoutScaleUtils.UnscaleFloat(Component.Radius, applied.Radius, Radius, metrics.GlyphScale);
            Component.BorderThickness = AuthoredLayoutScaleUtils.UnscaleFloat(Component.BorderThickness, applied.BorderThickness, BorderThickness, metrics.GlyphScale);
        }
    }
}
