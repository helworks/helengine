using helengine.ui.format;

namespace helengine.ui.format.tests {
    /// <summary>Executable conformance checks; requires explicit build authorization before running.</summary>
    public static class Program {
        /// <summary>Checks exact bytes, round trips and hostile input before reporting success.</summary>
        public static void Main(string[] args) {
            if (args.Length == 2 && args[0] == "--emit") { File.WriteAllBytes(args[1], UiBinaryCodec.Write(Fixture())); return; }
            if (args.Length == 2 && args[0] == "--verify") { Equal(UiBinaryCodec.Write(Fixture()), UiBinaryCodec.Write(UiBinaryCodec.Read(File.ReadAllBytes(args[1])))); return; }
            if (args.Length == 2 && args[0] == "--emit-rich") { File.WriteAllBytes(args[1], UiBinaryCodec.Write(RichFixture())); return; }
            if (args.Length == 2 && args[0] == "--verify-rich") { Equal(UiBinaryCodec.Write(RichFixture()), UiBinaryCodec.Write(UiBinaryCodec.Read(File.ReadAllBytes(args[1])))); return; }
            if (args.Length != 0) { throw new ArgumentException("Expected no arguments, --emit/--emit-rich path or --verify/--verify-rich path."); }
            UiDocument document = Fixture();
            Require((object)document is IDisposable, "UI document owns a disposable native tree");
            byte[] bytes = UiBinaryCodec.Write(document);
            Equal(bytes, Convert.FromHexString(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "card-v1.hex")).Trim()));
            Require(UiUtf8.Length("\ufeff") == 3, "UTF-8 BOM remains literal text");
            foreach (byte[] bad in new[] { new byte[] { 0xc0, 0x80 }, new byte[] { 0xed, 0xa0, 0x80 }, new byte[] { 0xf4, 0x90, 0x80, 0x80 }, new byte[] { 0xe2, 0x82 } }) {
                bool rejected = false; try { UiUtf8.Validate(bad); } catch (FormatException) { rejected = true; } Require(rejected, "malformed UTF-8 accepted");
            }
            UiDocument loaded = UiBinaryCodec.Read(bytes);
            Equal(bytes, UiBinaryCodec.Write(loaded));
            Require(loaded.Templates[0].Nodes[0].Name == "ação 🖌", "UTF-8 text");
            Require(loaded.Templates[0].Nodes[0].X == -1.25f, "signed float32");
            byte[] empty = UiBinaryCodec.Write(new UiDocument());
            Equal(empty, Convert.FromHexString("48454c4501014955010001000400000000000000"));
            for (int count = 0; count < bytes.Length; count++) {
                byte[] cut = new byte[count]; Array.Copy(bytes, cut, count);
                Reject(cut);
            }
            byte[] trailing = new byte[bytes.Length + 1]; Array.Copy(bytes, trailing, bytes.Length); Reject(trailing);
            byte[] badBoolean = (byte[])bytes.Clone(); badBoolean[86] = 2; Reject(badBoolean);
            byte[] badUtf8 = (byte[])bytes.Clone(); badUtf8[56] = 0xc0; Reject(badUtf8);
            byte[] version = (byte[])bytes.Clone(); version[5] = 2; Reject(version);
            byte[] oversized = (byte[])empty.Clone(); oversized[16] = 255; oversized[17] = 255; Reject(oversized);
            UiNode node = document.Templates[0].Nodes[0]; node.ParentId = node.Id; Invalid(document); node.ParentId = -1;
            node.Width = float.NaN; Invalid(document); node.Width = 230;
            node.Name = "bad" + (char)0xd800; Invalid(document); node.Name = "ação 🖌";
            UiComponent component = node.Components[0];
            component.Properties = new[] { new UiProperty { Id = UiPropertyId.Fill, Kind = UiValueKind.Color }, new UiProperty { Id = UiPropertyId.Fill, Kind = UiValueKind.Color } }; Invalid(document);
            document = Fixture(); node = document.Templates[0].Nodes[0];
            node.Components[0].Properties[0].Kind = UiValueKind.Boolean; Invalid(document);
            document = Fixture(); node = document.Templates[0].Nodes[0];
            node.Components[0].Kind = (UiComponentKind)65535; Invalid(document);
            document = Fixture(); document.Templates[0].Nodes = new[] { new UiNode { Id = 1, Name = "first" }, new UiNode { Id = 1, Name = "second" } }; Invalid(document);
            document = RichFixture(); node = document.Templates[0].Nodes[0];
            byte[] tagged = UiBinaryCodec.Write(document); UiDocument taggedRead = UiBinaryCodec.Read(tagged); Equal(tagged, UiBinaryCodec.Write(taggedRead));
            Require(taggedRead.Templates[0].Nodes[0].Components[0].Properties[0].Text == "\ufeffliteral", "BOM preservation");
            Require(taggedRead.Templates[0].Nodes[0].Components[0].Properties[3].Color == 0xff2200ff, "unsigned color");
            UiDocument tree = UiBinaryCodec.Read(UiBinaryCodec.Write(document));
            Require(tree.Templates[0].Nodes[1].ParentId == 1 && BitConverter.SingleToInt32Bits(tree.Templates[0].Nodes[1].X) == unchecked((int)0x80000000), "parent and negative zero");
            document.Metrics = new UiMetric[UiFormatLimits.Metrics + 1]; Invalid(document);
            foreach (int offset in new[] { 43, 89, 93, 95 }) { byte[] bad = (byte[])bytes.Clone(); bad[offset] = offset == 43 ? (byte)0 : (byte)255; Reject(bad); }
            CheckOwnership();
            Console.WriteLine("PASS: UI binary format conformance.");
        }
        /// <summary>Checks cascading cleanup, replacement, self-assignment and partially populated owners.</summary>
        static void CheckOwnership() {
            UiDocument document = Fixture(); UiTemplate template = document.Templates[0];
            UiNode node = template.Nodes[0]; UiComponent component = node.Components[0];
            document.Templates = document.Templates;
            Require(template.Nodes[0] == node, "Self-assignment retains the owned tree");
            document.Templates = new UiTemplate[2];
            Require(template.Nodes == null && node.Components == null && component.Properties == null, "Replacement disposes the previous subtree");
            document.Templates[1] = new UiTemplate { Nodes = new UiNode[2] };
            document.Templates[1].Nodes[1] = new UiNode { Components = new UiComponent[2] };
            document.Templates[1].Nodes[1].Components[1] = new UiComponent { Properties = new UiProperty[2] };
            document.Dispose(); document.Dispose();
            Require(document.Metrics == null && document.Templates == null, "Partial trees and repeated disposal are safe");
        }
        /// <summary>Creates data independent of cozy pencil, including Unicode and a negative coordinate.</summary>
        public static UiDocument Fixture() {
            return new UiDocument {
                Metrics = new[] { new UiMetric { Name = "gap", Value = 16 } },
                Templates = new[] { new UiTemplate { Name = "card", Nodes = new[] {
                    new UiNode { Id = 1, Name = "ação 🖌", X = -1.25f, Y = 2.5f, Width = 230, Height = 230,
                        Components = new[] { new UiComponent { Kind = UiComponentKind.Rectangle, Properties = new[] {
                            new UiProperty { Id = UiPropertyId.Fill, Kind = UiValueKind.Theme, Text = "Surface" },
                            new UiProperty { Id = UiPropertyId.Radius, Kind = UiValueKind.Number, Number = 16 }
                        } } } }
                } } }
            };
        }
        /// <summary>Exercises every remaining value tag plus hierarchy and signed zero in both runtimes.</summary>
        public static UiDocument RichFixture() {
            UiDocument document = Fixture();
            Require((object)document is IDisposable, "UI document owns a disposable native tree"); UiNode node = document.Templates[0].Nodes[0];
            node.Components = new[] {
                new UiComponent { Kind = UiComponentKind.Text, Properties = new[] {
                    new UiProperty { Id = UiPropertyId.Text, Kind = UiValueKind.String, Text = "\ufeffliteral" },
                    new UiProperty { Id = UiPropertyId.Wrap, Kind = UiValueKind.Boolean, Boolean = true },
                    new UiProperty { Id = UiPropertyId.FontWeight, Kind = UiValueKind.Integer, Integer = 3 },
                    new UiProperty { Id = UiPropertyId.TextColor, Kind = UiValueKind.Color, Color = 0xff2200ff }
                } },
                new UiComponent { Kind = UiComponentKind.Image, Properties = new[] {
                    new UiProperty { Id = UiPropertyId.Image, Kind = UiValueKind.ResourceBinding, Text = "cover" },
                    new UiProperty { Id = UiPropertyId.ImageFit, Kind = UiValueKind.Integer, Integer = 1 }
                } },
                new UiComponent { Kind = UiComponentKind.Button, Properties = new[] {
                    new UiProperty { Id = UiPropertyId.Action, Kind = UiValueKind.ActionBinding, Text = "open" },
                    new UiProperty { Id = UiPropertyId.AccessibleLabel, Kind = UiValueKind.TextBinding, Text = "name" }
                } },
                new UiComponent { Kind = UiComponentKind.Clip }
            };
            document.Templates[0].Nodes[0] = null; // Transfer the node out before replacing its owning array.
            document.Templates[0].Nodes = new[] { node, new UiNode { Id = 2, ParentId = 1, Name = "child", X = -0f } };
            return document;
        }
        /// <summary>Requires deterministic rejection at the public reader boundary.</summary>
        static void Reject(byte[] bytes) { bool failed = false; try { UiBinaryCodec.Read(bytes); } catch (FormatException) { failed = true; } Require(failed, "malformed bytes accepted"); }
        /// <summary>Requires the writer to reject invalid models before publishing output.</summary>
        static void Invalid(UiDocument document) { bool failed = false; try { UiBinaryCodec.Write(document); } catch (FormatException) { failed = true; } Require(failed, "invalid model accepted"); }
        /// <summary>Compares exact wire bytes without string or semantic normalization.</summary>
        static void Equal(byte[] a, byte[] b) { Require(a.Length == b.Length, "byte length"); for (int i = 0; i < a.Length; i++) { Require(a[i] == b[i], "byte mismatch at " + i); } }
        /// <summary>Stops at the first violated format invariant.</summary>
        static void Require(bool valid, string message) { if (!valid) { throw new InvalidOperationException(message); } }
    }
}
