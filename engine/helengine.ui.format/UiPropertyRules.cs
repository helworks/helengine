using System;

namespace helengine.ui.format {
    /// <summary>Version-one component schema, shared by both serialization directions.</summary>
    public static class UiPropertyRules {
        /// <summary>Validates property applicability, payload kind and value domain.</summary>
        public static void Validate(UiComponentKind component, UiProperty p) {
            if (p == null) { throw new FormatException("Null UI property."); }
            bool shape = component == UiComponentKind.Rectangle || component == UiComponentKind.Button;
            bool text = component == UiComponentKind.Text;
            bool button = component == UiComponentKind.Button;
            bool image = component == UiComponentKind.Image;
            bool color = p.Kind == UiValueKind.Color || p.Kind == UiValueKind.Theme;
            bool literal = p.Kind == UiValueKind.String || p.Kind == UiValueKind.TextBinding;
            bool valid = false;
            switch (p.Id) {
                case UiPropertyId.Fill: case UiPropertyId.Border: valid = shape && color; break;
                case UiPropertyId.BorderWidth: case UiPropertyId.Radius: valid = shape && p.Kind == UiValueKind.Number && p.Number >= 0; break;
                case UiPropertyId.Corners: valid = shape && p.Kind == UiValueKind.Integer && p.Integer >= 0 && p.Integer <= 15; break;
                case UiPropertyId.Text: valid = (text || button) && literal; break;
                case UiPropertyId.TextColor: valid = (text || button) && color; break;
                case UiPropertyId.FontSize: valid = (text || button) && p.Kind == UiValueKind.Number && p.Number >= 1 && p.Number <= 256; break;
                case UiPropertyId.FontWeight: valid = (text || button) && p.Kind == UiValueKind.Integer && p.Integer >= 0 && p.Integer <= 3; break;
                case UiPropertyId.Alignment: valid = (text || button) && p.Kind == UiValueKind.Integer && p.Integer >= 0 && p.Integer <= 2; break;
                case UiPropertyId.Wrap: valid = (text || button) && p.Kind == UiValueKind.Boolean; break;
                case UiPropertyId.Image: valid = image && p.Kind == UiValueKind.ResourceBinding; break;
                case UiPropertyId.ImageFit: valid = image && p.Kind == UiValueKind.Integer && p.Integer >= 0 && p.Integer <= 1; break;
                case UiPropertyId.Action: valid = button && p.Kind == UiValueKind.ActionBinding; break;
                case UiPropertyId.Hover: case UiPropertyId.Pressed: case UiPropertyId.Focused: case UiPropertyId.FocusBorder: valid = button && color; break;
                case UiPropertyId.AccessibleLabel: valid = button && literal; break;
            }
            if (!valid) { throw new FormatException("Unsupported UI property or value kind."); }
            if (p.Kind == UiValueKind.Number) { UiValidation.Finite(p.Number); }
            if (p.Kind == UiValueKind.Integer) { UiValidation.Integer(p.Integer, -2147483648d, 2147483647d); }
            if (p.Kind == UiValueKind.Color) { UiValidation.Integer(p.Color, 0, 4294967295d); }
            if (IsText(p.Kind)) {
                UiUtf8.Length(p.Text);
                if (p.Kind != UiValueKind.String && p.Text.Length == 0) { throw new FormatException("Empty UI binding key."); }
            }
        }
        /// <summary>Identifies value kinds whose wire payload is a bounded UTF-8 string.</summary>
        public static bool IsText(UiValueKind kind) { return kind == UiValueKind.String || kind == UiValueKind.TextBinding || kind == UiValueKind.ResourceBinding || kind == UiValueKind.ActionBinding || kind == UiValueKind.Theme; }
    }
}
