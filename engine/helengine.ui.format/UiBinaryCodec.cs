using System;
using System.IO;
using System.Text;

namespace helengine.ui.format {
    /// <summary>Version-one portable UI wire codec; the same C# implementation is exported to TypeScript.</summary>
    public static class UiBinaryCodec {
        /// <summary>Distinct UI payload family in the standard HELE header.</summary>
        public const ushort FormatId = 0x5549;
        /// <summary>First strict UI payload schema.</summary>
        public const byte Version = 1;
        /// <summary>Validates and serializes into an exactly sized buffer, preserving declaration order.</summary>
        [NativeOwnedReturn] public static byte[] Write([NativeNoEscape] UiDocument document) {
            int length = UiValidation.Measure(document);
            using UiBinaryOutput output = new UiBinaryOutput(length);
            BinaryWriter writer = output.Writer;
            writer.Write((byte)72); writer.Write((byte)69); writer.Write((byte)76); writer.Write((byte)69);
            writer.Write((byte)1); writer.Write(Version); writer.Write(FormatId); writer.Write((ushort)1); writer.Write((ushort)1); writer.Write((uint)(length - 16));
            writer.Write((ushort)document.Metrics.Length);
            for (int i = 0; i < document.Metrics.Length; i++) { Text(writer, document.Metrics[i].Name); writer.Write(document.Metrics[i].Value); }
            writer.Write((ushort)document.Templates.Length);
            for (int t = 0; t < document.Templates.Length; t++) {
                UiTemplate template = document.Templates[t]; Text(writer, template.Name); writer.Write((uint)template.Nodes.Length);
                for (int n = 0; n < template.Nodes.Length; n++) {
                    UiNode node = template.Nodes[n]; writer.Write(node.Id); writer.Write(node.ParentId); Text(writer, node.Name);
                    writer.Write(node.X); writer.Write(node.Y); writer.Write(node.Z); writer.Write(node.Width); writer.Write(node.Height); writer.Write((byte)(node.Visible ? 1 : 0)); writer.Write((ushort)node.Components.Length);
                    for (int c = 0; c < node.Components.Length; c++) {
                        UiComponent component = node.Components[c]; writer.Write((ushort)component.Kind); writer.Write((ushort)component.Properties.Length);
                        for (int p = 0; p < component.Properties.Length; p++) { Property(writer, component.Properties[p]); }
                    }
                }
            }
            if (output.Position != length) { throw new InvalidOperationException("UI measurement and encoding disagree."); }
            return output.ToArray();
        }
        /// <summary>Reads a bounded document, validates every semantic reference and rejects trailing bytes.</summary>
        [NativeOwnedReturn] public static UiDocument Read([NativeNoEscape] byte[] bytes) {
            if (bytes == null || bytes.Length < 20 || bytes.Length > UiFormatLimits.Bytes) { throw new FormatException("Invalid UI document length."); }
            using UiBinaryInput reader = new UiBinaryInput(bytes);
            if (reader.Byte() != 72 || reader.Byte() != 69 || reader.Byte() != 76 || reader.Byte() != 69 || reader.Byte() != 1 || reader.Byte() != Version || reader.UInt16() != FormatId || reader.UInt16() != 1 || reader.UInt16() != 1) { throw new FormatException("Unsupported UI binary header."); }
            if (reader.UInt32() != bytes.Length - 16) { throw new FormatException("UI payload length mismatch."); }
            UiDocument result = new UiDocument();
            try {
                int metrics = reader.Count(reader.UInt16(), UiFormatLimits.Metrics, 8); result.Metrics = new UiMetric[metrics];
                for (int i = 0; i < metrics; i++) {
                    result.Metrics[i] = new UiMetric(); UiMetric metric = result.Metrics[i]; metric.Name = reader.Text(); metric.Value = reader.Single();
                }
                int templates = reader.Count(reader.UInt16(), UiFormatLimits.Templates, 8); result.Templates = new UiTemplate[templates]; int totalNodes = 0;
                for (int t = 0; t < templates; t++) {
                    result.Templates[t] = new UiTemplate(); UiTemplate template = result.Templates[t]; template.Name = reader.Text();
                    int nodes = reader.Count(reader.UInt32(), UiFormatLimits.Nodes - totalNodes, 35); totalNodes += nodes; template.Nodes = new UiNode[nodes];
                    for (int n = 0; n < nodes; n++) {
                        template.Nodes[n] = new UiNode(); UiNode node = template.Nodes[n];
                        node.Id = reader.Int32(); node.ParentId = reader.Int32(); node.Name = reader.Text(); node.X = reader.Single(); node.Y = reader.Single(); node.Z = reader.Single(); node.Width = reader.Single(); node.Height = reader.Single(); node.Visible = reader.Boolean();
                        int components = reader.Count(reader.UInt16(), UiFormatLimits.Components, 4); node.Components = new UiComponent[components];
                        for (int c = 0; c < components; c++) {
                            node.Components[c] = new UiComponent(); UiComponent component = node.Components[c]; component.Kind = (UiComponentKind)reader.UInt16();
                            int properties = reader.Count(reader.UInt16(), UiFormatLimits.Properties, 4); component.Properties = new UiProperty[properties];
                            for (int p = 0; p < properties; p++) { component.Properties[p] = Property(reader); }
                        }
                    }
                }
                if (reader.Remaining != 0 || UiValidation.Measure(result) != bytes.Length) { throw new FormatException("Unexpected trailing UI bytes."); }
                return result;
            } catch { NativeOwnership.DisposeAndRelease(ref result); throw; }
        }
        /// <summary>Writes explicit UTF-8 byte lengths instead of platform BinaryWriter string conventions.</summary>
        static void Text(BinaryWriter writer, string text) { byte[] bytes = Encoding.UTF8.GetBytes(text); try { writer.Write((uint)bytes.Length); for (int i = 0; i < bytes.Length; i++) { writer.Write(bytes[i]); } } finally { NativeOwnership.Release(ref bytes); } }
        /// <summary>Writes only the payload selected by a validated property tag.</summary>
        static void Property(BinaryWriter writer, UiProperty property) {
            writer.Write((ushort)property.Id); writer.Write((byte)property.Kind);
            if (UiPropertyRules.IsText(property.Kind)) { Text(writer, property.Text); }
            else if (property.Kind == UiValueKind.Number) { writer.Write(property.Number); }
            else if (property.Kind == UiValueKind.Integer) { writer.Write(property.Integer); }
            else if (property.Kind == UiValueKind.Boolean) { writer.Write((byte)(property.Boolean ? 1 : 0)); }
            else if (property.Kind == UiValueKind.Color) { writer.Write(property.Color); }
            else { throw new FormatException("Unsupported UI value tag."); }
        }
        /// <summary>Rejects unknown payload tags rather than guessing their encoded length.</summary>
        [NativeOwnedReturn] static UiProperty Property(UiBinaryInput reader) {
            UiProperty property = new UiProperty();
            try {
            property.Id = (UiPropertyId)reader.UInt16(); property.Kind = (UiValueKind)reader.Byte();
            if (UiPropertyRules.IsText(property.Kind)) { property.Text = reader.Text(); }
            else if (property.Kind == UiValueKind.Number) { property.Number = reader.Single(); }
            else if (property.Kind == UiValueKind.Integer) { property.Integer = reader.Int32(); }
            else if (property.Kind == UiValueKind.Boolean) { property.Boolean = reader.Boolean(); }
            else if (property.Kind == UiValueKind.Color) { property.Color = reader.UInt32(); }
            else { throw new FormatException("Unsupported UI value tag."); }
            return property;
            } catch { NativeOwnership.Release(ref property); throw; }
        }
    }
}
