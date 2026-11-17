using System;

namespace helengine.ui.format {
    /// <summary>Validates a complete portable model and computes its exact serialized size.</summary>
    public static class UiValidation {
        /// <summary>Checks semantics and limits before the writer allocates its fixed-size buffer.</summary>
        public static int Measure(UiDocument document) {
            if (document == null || document.Metrics == null || document.Templates == null) { throw new FormatException("Incomplete UI document."); }
            Count(document.Metrics.Length, UiFormatLimits.Metrics); Count(document.Templates.Length, UiFormatLimits.Templates);
            int size = 20; int nodes = 0;
            for (int i = 0; i < document.Metrics.Length; i++) {
                UiMetric metric = document.Metrics[i]; if (metric == null) { throw new FormatException("Null UI metric."); }
                Name(metric.Name); Finite(metric.Value);
                for (int j = 0; j < i; j++) { if (document.Metrics[j].Name == metric.Name) { throw new FormatException("Duplicate UI metric."); } }
                size += StringSize(metric.Name) + 4;
            }
            for (int t = 0; t < document.Templates.Length; t++) {
                UiTemplate template = document.Templates[t]; if (template == null || template.Nodes == null) { throw new FormatException("Incomplete UI template."); }
                Name(template.Name); Count(template.Nodes.Length, UiFormatLimits.Nodes - nodes); nodes += template.Nodes.Length;
                for (int j = 0; j < t; j++) { if (document.Templates[j].Name == template.Name) { throw new FormatException("Duplicate UI template."); } }
                size += StringSize(template.Name) + 4;
                for (int n = 0; n < template.Nodes.Length; n++) {
                    UiNode node = template.Nodes[n]; if (node == null || node.Components == null || node.Id <= 0 || node.ParentId < -1 || node.ParentId == 0) { throw new FormatException("Invalid UI node."); }
                    Integer(node.Id, 1, 2147483647d); Integer(node.ParentId, -1, 2147483647d);
                    Name(node.Name); Finite(node.X); Finite(node.Y); Finite(node.Z); Finite(node.Width); Finite(node.Height);
                    if (node.Width < 0 || node.Height < 0) { throw new FormatException("Negative UI size."); }
                    bool parent = node.ParentId == -1;
                    for (int j = 0; j < n; j++) {
                        UiNode previous = template.Nodes[j];
                        if (previous.Id == node.Id || previous.Name == node.Name) { throw new FormatException("Duplicate UI node identity."); }
                        if (previous.Id == node.ParentId) { parent = true; }
                    }
                    if (!parent) { throw new FormatException("UI parent must precede child in the same template."); }
                    Count(node.Components.Length, UiFormatLimits.Components); size += 31 + StringSize(node.Name);
                    for (int c = 0; c < node.Components.Length; c++) {
                        UiComponent component = node.Components[c];
                        if (component == null || component.Properties == null || component.Kind < UiComponentKind.Rectangle || component.Kind > UiComponentKind.Button) { throw new FormatException("Unsupported UI component."); }
                        for (int j = 0; j < c; j++) { if (node.Components[j].Kind == component.Kind) { throw new FormatException("Duplicate UI component kind."); } }
                        Count(component.Properties.Length, UiFormatLimits.Properties); size += 4;
                        bool required = component.Kind == UiComponentKind.Rectangle || component.Kind == UiComponentKind.Clip;
                        for (int p = 0; p < component.Properties.Length; p++) {
                            UiProperty property = component.Properties[p]; UiPropertyRules.Validate(component.Kind, property);
                            for (int j = 0; j < p; j++) { if (component.Properties[j].Id == property.Id) { throw new FormatException("Duplicate UI property."); } }
                            if ((component.Kind == UiComponentKind.Text && property.Id == UiPropertyId.Text) || (component.Kind == UiComponentKind.Image && property.Id == UiPropertyId.Image) || (component.Kind == UiComponentKind.Button && property.Id == UiPropertyId.Action)) { required = true; }
                            size += 3 + (UiPropertyRules.IsText(property.Kind) ? StringSize(property.Text) : property.Kind == UiValueKind.Boolean ? 1 : 4);
                            if (size > UiFormatLimits.Bytes) { throw new FormatException("UI document exceeds byte limit."); }
                        }
                        if (!required) { throw new FormatException("Missing required UI component property."); }
                    }
                    if (size > UiFormatLimits.Bytes) { throw new FormatException("UI document exceeds byte limit."); }
                }
            }
            if (size > UiFormatLimits.Bytes) { throw new FormatException("UI document exceeds byte limit."); }
            return size;
        }
        /// <summary>Requires a finite single-precision value.</summary>
        public static void Finite(float value) { if (!(value >= -3.4028234663852886e38 && value <= 3.4028234663852886e38)) { throw new FormatException("Non-finite or overflowing UI float32."); } }
        /// <summary>Enforces integer storage domains even when generated TypeScript receives a JS number.</summary>
        public static void Integer(double value, double minimum, double maximum) { if (!(value >= minimum && value <= maximum) || Math.Floor(value) != value) { throw new FormatException("Invalid UI integer."); } }
        /// <summary>Rejects collection sizes before allocation or iteration.</summary>
        public static void Count(int count, int maximum) { if (count < 0 || count > maximum) { throw new FormatException("UI count exceeds limit."); } }
        /// <summary>Checks nonempty identifiers without platform-specific normalization.</summary>
        static void Name(string text) { if (text == null || text.Length == 0) { throw new FormatException("Empty UI name."); } UiUtf8.Length(text); }
        /// <summary>Counts an explicit uint32 UTF-8 byte length and its payload.</summary>
        static int StringSize(string text) { return 4 + UiUtf8.Length(text); }
    }
}
