#if !HELENGINE_RUNTIME_COOKED_VIEWPORT_LAYOUT
namespace helengine {
    /// <summary>Stores only original TextComponent layout fields in the optional typed cache.</summary>
    internal struct ViewportTextState {
        /// <summary>Borrowed participant whose lifetime is controlled by its entity.</summary>
        public TextComponent Component;
        /// <summary>Original Size before owner layout processing.</summary>
        public int2 Size;
        /// <summary>Original FontScale before owner layout processing.</summary>
        public Real FontScale;
        /// <summary>Original OutlineScale before owner layout processing.</summary>
        public Real OutlineScale;
        /// <summary>Original ShadowOffset before owner layout processing.</summary>
        public float2 ShadowOffset;
        /// <summary>Captures this participant without allocating a record object.</summary>
        public ViewportTextState([NativeRetainsBorrow] TextComponent component) {
            Component = component;
            Size = component.Size;
            FontScale = component.FontScale;
            OutlineScale = component.OutlineScale;
            ShadowOffset = component.ShadowOffset;
        }
        /// <summary>Restores the retained baseline before absolute scaling.</summary>
        public void Restore() {
            Component.Size = Size;
            Component.FontScale = FontScale;
            Component.OutlineScale = OutlineScale;
            Component.ShadowOffset = ShadowOffset;
        }
        /// <summary>Maps only deliberate preview edits back into authored space.</summary>
        public void RestoreEdited(ViewportTextState applied, in ViewportLayoutMetrics metrics) {
            Component.Size = AuthoredLayoutScaleUtils.UnscaleInt2(Component.Size, applied.Size, Size, metrics.ScaleX, metrics.ScaleY);
            Component.FontScale = AuthoredLayoutScaleUtils.UnscaleFloat(Component.FontScale, applied.FontScale, FontScale, Component.NativeFontPixels == TextNativePixelMode.Disabled ? metrics.GlyphScale : 1d);
            Component.OutlineScale = AuthoredLayoutScaleUtils.UnscaleFloat(Component.OutlineScale, applied.OutlineScale, OutlineScale, metrics.GlyphScale);
            Component.ShadowOffset = new float2(AuthoredLayoutScaleUtils.UnscaleFloat(Component.ShadowOffset.X, applied.ShadowOffset.X, ShadowOffset.X, metrics.GlyphScale), AuthoredLayoutScaleUtils.UnscaleFloat(Component.ShadowOffset.Y, applied.ShadowOffset.Y, ShadowOffset.Y, metrics.GlyphScale));
        }
    }
}
#endif
