#if !HELENGINE_RUNTIME_COOKED_VIEWPORT_LAYOUT
namespace helengine {
    /// <summary>Stores only original FPSComponent layout fields in the optional typed cache.</summary>
    internal struct ViewportFpsState {
        /// <summary>Borrowed participant whose lifetime is controlled by its entity.</summary>
        public FPSComponent Component;
        /// <summary>Original FontScale before owner layout processing.</summary>
        public Real FontScale;
        /// <summary>Original Padding before owner layout processing.</summary>
        public int2 Padding;
        /// <summary>Captures this participant without allocating a record object.</summary>
        public ViewportFpsState([NativeRetainsBorrow] FPSComponent component) {
            Component = component;
            FontScale = component.FontScale;
            Padding = component.Padding;
        }
        /// <summary>Restores the retained baseline before absolute scaling.</summary>
        public void Restore() {
            Component.FontScale = FontScale;
            Component.Padding = Padding;
        }
        /// <summary>Maps only deliberate preview edits back into authored space.</summary>
        public void RestoreEdited(ViewportFpsState applied, in ViewportLayoutMetrics metrics) {
            Component.FontScale = AuthoredLayoutScaleUtils.UnscaleFloat(Component.FontScale, applied.FontScale, FontScale, Component.NativeFontPixels == TextNativePixelMode.Disabled ? metrics.GlyphScale : 1d);
            Component.Padding = AuthoredLayoutScaleUtils.UnscaleInt2(Component.Padding, applied.Padding, Padding, metrics.ScaleX, metrics.ScaleY);
        }
    }
}
#endif
