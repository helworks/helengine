namespace helengine {
    /// <summary>Stores only original InteractableComponent layout fields in the optional typed cache.</summary>
    internal struct ViewportInteractableState {
        /// <summary>Borrowed participant whose lifetime is controlled by its entity.</summary>
        public InteractableComponent Component;
        /// <summary>Original Size before owner layout processing.</summary>
        public int2 Size;
        /// <summary>Captures this participant without allocating a record object.</summary>
        public ViewportInteractableState([NativeRetainsBorrow] InteractableComponent component) {
            Component = component;
            Size = component.Size;
        }
        /// <summary>Restores the retained baseline before absolute scaling.</summary>
        public void Restore() {
            Component.Size = Size;
        }
        /// <summary>Maps only deliberate preview edits back into authored space.</summary>
        public void RestoreEdited(ViewportInteractableState applied, in ViewportLayoutMetrics metrics) {
            Component.Size = AuthoredLayoutScaleUtils.UnscaleInt2(Component.Size, applied.Size, Size, metrics.ScaleX, metrics.ScaleY);
        }
    }
}
