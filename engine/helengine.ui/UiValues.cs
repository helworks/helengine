using helengine.ui.format;
namespace helengine.ui {
    /// <summary>Resolves typed properties and the existing prototype defaults without parsing another wire format.</summary>
    public static class UiValues {
        /// <summary>Returns an optional property borrowed from the caller's document.</summary>
        [NativeBorrowedReturn] public static UiProperty Find(UiComponent spec, UiPropertyId id) { for (int i = 0; i < spec.Properties.Length; i++) { if (spec.Properties[i].Id == id) { return spec.Properties[i]; } } return null; }
        /// <summary>Reads an optional float property with its documented render default.</summary>
        public static float Number(UiComponent spec, UiPropertyId id, float fallback) { UiProperty p = Find(spec, id); return p == null ? fallback : p.Number; }
        /// <summary>Reads an optional integer property with its documented render default.</summary>
        public static int Integer(UiComponent spec, UiPropertyId id, int fallback) { UiProperty p = Find(spec, id); return p == null ? fallback : p.Integer; }
        /// <summary>Reads an optional boolean property, defaulting to false.</summary>
        public static bool Boolean(UiComponent spec, UiPropertyId id) { UiProperty p = Find(spec, id); return p != null && p.Boolean; }
        /// <summary>Resolves literal strings independently of binding keys, including literal dollar signs.</summary>
        public static string Text(UiComponent spec, UiPropertyId id, IUiBindings bindings) { UiProperty p = Find(spec, id); if (p == null) { return ""; } string result = p.Kind == UiValueKind.String ? p.Text : bindings.GetText(p.Text); if (result == null) { throw new FormatException("Null UI text binding."); } return result; }
        /// <summary>Decodes numeric RRGGBBAA or requests a semantic role; missing colors are transparent.</summary>
        public static byte4 Color(UiComponent spec, UiPropertyId id, IUiBindings bindings) { UiProperty p = Find(spec, id); if (p == null) { return new byte4(0, 0, 0, 0); } if (p.Kind == UiValueKind.Theme) { return bindings.GetColor(p.Text); } return new byte4((byte)(p.Color >> 24), (byte)(p.Color >> 16), (byte)(p.Color >> 8), (byte)p.Color); }
        /// <summary>Maps wire weight ids explicitly instead of depending on an engine enum's storage.</summary>
        public static SystemTextWeight Weight(UiComponent spec) { switch (Integer(spec, UiPropertyId.FontWeight, 0)) { case 0: return SystemTextWeight.Regular; case 1: return SystemTextWeight.Medium; case 2: return SystemTextWeight.Semibold; case 3: return SystemTextWeight.Bold; default: throw new FormatException("Unsupported font weight."); } }
        /// <summary>Maps wire alignment ids explicitly.</summary>
        public static TextAlignment Alignment(UiComponent spec) { switch (Integer(spec, UiPropertyId.Alignment, 0)) { case 0: return TextAlignment.Left; case 1: return TextAlignment.Center; case 2: return TextAlignment.Right; default: throw new FormatException("Unsupported text alignment."); } }
        /// <summary>Applies one explicit framebuffer density with midpoint-to-even rounding.</summary>
        public static int Pixels(float value, float scale) { return (int)MathF.Round(value * scale); }
        /// <summary>Bounds the initial renderer subset before integer size conversion.</summary>
        public static float Bounded(float value, float minimum = 0) { if (!float.IsFinite(value) || value < minimum || value > 32768) { throw new FormatException("UI geometry exceeds the supported renderer range."); } return value; }
    }
}
