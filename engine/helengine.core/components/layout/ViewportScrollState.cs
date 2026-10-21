#if !HELENGINE_RUNTIME_COOKED_VIEWPORT_LAYOUT
namespace helengine {
    /// <summary>Stores only original ScrollComponent layout fields in the optional typed cache.</summary>
    internal struct ViewportScrollState {
        /// <summary>Borrowed participant whose lifetime is controlled by its entity.</summary>
        public ScrollComponent Component;
        /// <summary>Original Size before owner layout processing.</summary>
        public int2 Size;
        /// <summary>Original ItemExtent before owner layout processing.</summary>
        public int ItemExtent;
        /// <summary>Captures this participant without allocating a record object.</summary>
        public ViewportScrollState([NativeRetainsBorrow] ScrollComponent component) {
            Component = component;
            Size = component.Size;
            ItemExtent = component.ItemExtent;
        }
        /// <summary>Restores the retained baseline before absolute scaling.</summary>
        public void Restore() {
            Component.Size = Size;
            Component.ItemExtent = ItemExtent;
        }
        /// <summary>Maps only deliberate preview edits back into authored space.</summary>
        public void RestoreEdited(ViewportScrollState applied, in ViewportLayoutMetrics metrics) {
            Component.Size = AuthoredLayoutScaleUtils.UnscaleInt2(Component.Size, applied.Size, Size, metrics.ScaleX, metrics.ScaleY);
            Component.ItemExtent = AuthoredLayoutScaleUtils.UnscaleInt(Component.ItemExtent, applied.ItemExtent, ItemExtent, metrics.GlyphScale);
        }
    }
}
#endif
