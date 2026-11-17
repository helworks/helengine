using helengine.ui.format;
using helengine.editor.tests.testing;
using helengine.core.tests.managers.rendering;
namespace helengine.ui.tests {
    /// <summary>Exercises the same library bytes used in the browser against real engine components.</summary>
    public static class Program {
        /// <summary>Loads a supplied fixture without compiling or launching the app.</summary>
        public static int Main(string[] args) {
            if (args.Length != 1) { throw new ArgumentException("Expected the standalone library.heui fixture path."); }
            byte[] bytes = File.ReadAllBytes(args[0]);
            UiDocument document;
            CheckStreams(bytes);
            var renderer = new TestRenderManager2D { SystemTextBackend = new SystemTextTestBackend(new SystemTextTestLayout(null)) };
            using var input = new TestTouchInputBackend();
            var source = new FakeContentStreamSource(); source.Register("ui/library.heui", bytes);
            using var core = new Core(new CoreInitializationOptions { ContentStreamSource = source });
            core.Initialize(null, renderer, input, new PlatformInfo("ui-adapter-test", "1"));
            Entity root = core.EntityFactory.Create("Root"); root.LayerMask = 16384; root.InitChildren(); root.InitComponents();
            document = core.ContentManager.Load<UiDocument>("ui/library.heui", new UiContentProcessor());
            Require(source.RequestedAssetPaths.Contains("ui/library.heui"), "Binary asset uses the existing content source.");
            var bindings = new Bindings();
            foreach (float scale in new float[] { 1, 2, 3 }) {
                foreach (UiTemplate template in document.Templates) {
                    using var view = UiView.Create(root, document, template.Name, bindings, scale);
                    Require(view.Root.LayerMask == 16384, "View root inherits the parent camera layer.");
                    if (template.Name == "artwork") {
                        foreach (string name in new[] { "Open", "Cover", "Thumbnail" }) {
                            Require(view.Get(name).LayerMask == 16384, "Artwork nodes inherit the parent camera layer.");
                        }
                        RoundedRectComponent cover = view.Get("Cover").Components.OfType<RoundedRectComponent>().Single();
                        Require(cover.Size.X == 230 * scale && cover.Size.Y == cover.Size.X, "Square artwork at explicit density.");
                        TextComponent label = view.Get("Name").Components.OfType<TextComponent>().Single();
                        Require(label.Text == "<Arizona>" && label.FontScale == scale && label.WrapText, "Literal text, density and wrapping.");
                        Require(view.GetAccessibleLabel("Open") == "<Arizona>", "Accessibility binding retained for platform integration.");
                        view.Get("Open").Components.OfType<ButtonComponent>().Single().ActivateFromKey(Keys.Enter);
                    }
                }
                Require(root.Children.Count == 0, "Disposed views leave no child entities.");
            }
            Require(bindings.Calls == 3, "Exactly one native action per activation.");
            CheckAsyncThumbnail(root, document, bindings);
            CheckImagesAndLimits(root, document, bindings);
            bindings.RejectTheme = true; bool failed = false;
            try { using var bad = UiView.Create(root, document, "artwork", bindings, 2); } catch (FormatException) { failed = true; }
            Require(failed && root.Children.Count == 0 && core.ObjectManager.Entities.Count == 1, "Binding failure rolls back all created entities.");
            document.Dispose();
            root.Dispose(); Require(core.ObjectManager.Entities.Count == 0, "View bookkeeping retains no owned entities.");
            Console.WriteLine("PASS: binary library creates real engine components at scales 1/2/3; camera layers, asynchronous thumbnails, clipping, actions, metadata, rollback and disposal verified.");
            return 0;
        }
        /// <summary>Verifies non-seekable input, exact bytes, bounds, truncation and caller-owned stream lifetime.</summary>
        static void CheckStreams(byte[] bytes) {
            var processor = new UiContentProcessor();
            using var input = new PartialReadStream(bytes);
            Require(UiBinaryCodec.Write(processor.Read(input)).SequenceEqual(bytes), "Partial input yields identical document bytes.");
            Require(!input.WasDisposed, "Content processor must not close a borrowed source.");
            foreach (byte[] invalid in new byte[][] { bytes[..^1], new byte[UiFormatLimits.Bytes + 1] }) {
                using var stream = new MemoryStream(invalid); bool failed = false;
                try { processor.Read(stream); } catch (FormatException) { failed = true; }
                Require(failed && stream.CanRead, "Bad stream rejected without consuming ownership.");
            }
        }
        /// <summary>Checks empty contain images retain a stable child and fixed clipping across asynchronous texture changes.</summary>
        static void CheckAsyncThumbnail(Entity root, UiDocument document, Bindings bindings) {
            using var texture = new TestRuntimeTexture { Width = 100, Height = 200 };
            foreach (float scale in new float[] { 1, 2, 3 }) {
                bindings.Texture = null;
                using var view = UiView.Create(root, document, "artwork", bindings, scale);
                Entity thumbnail = view.Get("Thumbnail");
                Require(thumbnail.Children.Count == 1, "An absent contain texture still creates exactly one image child.");
                Entity image = thumbnail.Children[0];
                SpriteComponent sprite = image.Components.OfType<SpriteComponent>().Single();
                ClipRectComponent clip = thumbnail.Components.OfType<ClipRectComponent>().Single();
                Require(sprite.Texture == null && sprite.Size.X == 210 * scale && sprite.Size.Y == 210 * scale, "Empty thumbnail preserves authored image bounds.");
                Require(image.LayerMask == 16384, "The asynchronous image child inherits the camera layer.");
                Require(clip.Size.X == 210 * scale && clip.Size.Y == 210 * scale, "Empty thumbnail keeps its authored clip at drawable density.");

                // The app supplies a borrowed thumbnail later and fits only the existing image child.
                sprite.Texture = texture;
                sprite.Size = new int2((int)(105 * scale), (int)(210 * scale));
                image.LocalPosition = new float3(52.5f * scale, 0, 0);
                Require(ReferenceEquals(thumbnail.Children[0], image) && ReferenceEquals(image.Components.OfType<SpriteComponent>().Single(), sprite), "Asynchronous thumbnail binding reuses the image entity and component.");
                Require(sprite.Texture == texture && clip.Size.X == 210 * scale && clip.Size.Y == 210 * scale, "Binding a texture leaves the authored clip unchanged.");
                sprite.Texture = null;
                Require(ReferenceEquals(thumbnail.Children[0], image) && clip.Size.X == 210 * scale && clip.Size.Y == 210 * scale, "Clearing a thumbnail preserves its stable child and authored clip.");
            }
            Require(!texture.IsDisposed, "Views borrow asynchronously supplied textures.");
        }
        /// <summary>Checks borrowed image fitting, noncontiguous ids, invisibility and explicit native limitations.</summary>
        static void CheckImagesAndLimits(Entity root, UiDocument document, Bindings bindings) {
            using var texture = new TestRuntimeTexture { Width = 100, Height = 200 }; bindings.Texture = texture;
            UiTemplate artwork = document.Templates.Single(t => t.Name == "artwork");
            artwork.Nodes[0].Id = 90;
            foreach (UiNode node in artwork.Nodes.Skip(1)) { node.ParentId = 90; }
            using (var view = UiView.Create(root, document, "artwork", bindings, 2)) {
                Entity thumbnail = view.Get("Thumbnail"); SpriteComponent sprite = thumbnail.Children[0].Components.OfType<SpriteComponent>().Single();
                Require(sprite.Texture == texture && sprite.Size.X == 210 && sprite.Size.Y == 420, "Contain preserves aspect ratio with borrowed texture.");
                Require(thumbnail.Children[0].LocalPosition.X == 105 && thumbnail.LocalPosition.X == 20, "Contain offset applies density exactly once.");
            }
            Require(!texture.IsDisposed, "View disposal leaves the application texture alive."); bindings.Texture = null;
            artwork.Nodes[0].Visible = false;
            using (var hidden = UiView.Create(root, document, "artwork", bindings, 1)) { Require(!hidden.Get("Name").IsHierarchyEnabled, "Invisible nodes disable descendants."); }
            artwork.Nodes[0].Visible = true;
            using UiDocument unsupported = UiBinaryCodec.Read(UiBinaryCodec.Write(document));
            UiComponent unsupportedButton = unsupported.Templates.Single(t => t.Name == "artwork").Nodes[0].Components[0];
            UiProperty[] copy = unsupportedButton.Properties;
            UiProperty[] extended = copy.Append(new UiProperty { Id = UiPropertyId.Alignment, Kind = UiValueKind.Integer, Integer = 0 }).ToArray();
            Array.Clear(copy); // Transfer the elements, then release the old array.
            unsupportedButton.Properties = extended;
            bool failed = false; try { using var invalid = UiView.Create(root, unsupported, "artwork", bindings, 1); } catch (NotSupportedException) { failed = true; }
            Require(failed && root.Children.Count == 0, "Unsupported native button options fail with rollback.");
            UiProperty color = artwork.Nodes[3].Components[0].Properties.Single(p => p.Id == UiPropertyId.TextColor);
            color.Kind = UiValueKind.Color; color.Color = 0xff220080;
            using (var view = UiView.Create(root, document, "artwork", bindings, 1)) { byte4 rgba = view.Get("Name").Components.OfType<TextComponent>().Single().Color; Require(rgba.X == 255 && rgba.Y == 34 && rgba.Z == 0 && rgba.W == 128, "Packed RRGGBBAA maps to native channels."); }
        }
        /// <summary>Fails the focused executable with the violated behavior.</summary>
        static void Require(bool value, string message) { if (!value) { throw new InvalidOperationException(message); } }
    }
}
