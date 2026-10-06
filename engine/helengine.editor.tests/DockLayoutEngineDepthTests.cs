using System.Reflection;
using System.Linq;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies that docking layout keeps panel and chrome depth after repeated layout passes.
    /// </summary>
    public sealed class DockLayoutEngineDepthTests : IDisposable {
        readonly Core CoreValue;
        readonly EditorSessionInteractionServices InteractionServices = new EditorSessionInteractionServices();

        /// <summary>
        /// Initializes the renderer and object manager used by dock layout.
        /// </summary>
        public DockLayoutEngineDepthTests() {
            CoreValue = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            CoreValue.Initialize(null, new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
            CoreValue.SessionInteractionGraph = InteractionServices;
        }

        /// <summary>
        /// Disposes the test core after the dock layout assertions finish.
        /// </summary>
        public void Dispose() {
            InteractionServices.Dispose();
            CoreValue.Dispose();
        }

        /// <summary>
        /// Ensures subsequent layout passes preserve docked panel depth and keep tabs and separators above it.
        /// </summary>
        [Fact]
        public void Layout_RepeatedPasses_PreservePanelTabAndSeparatorDepth() {
            FontAsset font = CreateFont();
            DockableEntity first = new DockableEntity(CoreValue, InteractionServices, font);
            DockableEntity second = new DockableEntity(CoreValue, InteractionServices, font);
            DockableEntity third = new DockableEntity(CoreValue, InteractionServices, font);
            DockLayoutEngine layout = new DockLayoutEngine(CoreValue.RenderManager2D, CoreValue.ObjectManager);
            layout.DockAsRoot(first);
            layout.DockRelative(second, first, DockInsertDirection.Fill);
            layout.DockRelative(third, first, DockInsertDirection.Right);

            float3 origin = new float3(10f, 20f, 3f);
            layout.Layout(new int2(1200, 900), origin);
            layout.Layout(new int2(1201, 901), origin);

            Assert.Equal(19f, first.Position.Z);
            Assert.Equal(19f, second.Position.Z);
            Assert.Equal(19f, third.Position.Z);

            DockTabStrip tabStrip = CoreValue.ObjectManager.Entities.OfType<DockTabStrip>().Single();
            List<DockTabEntry> entries = GetPrivateField<List<DockTabEntry>>(tabStrip, "tabs");
            SpriteComponent titleBar = FindTitleBarSprite(first);
            SpriteComponent separator = FindDockSeparator();

            Assert.True(RenderDepthOrder2D.CompareDrawables(titleBar, entries[0].Background) < 0);
            Assert.True(RenderDepthOrder2D.CompareDrawables(entries[0].Label, separator) < 0);
        }

        /// <summary>
        /// Finds the split separator sprite created by the docking layout.
        /// </summary>
        /// <returns>The split separator sprite.</returns>
        SpriteComponent FindDockSeparator() {
            byte4 separatorColor = new byte4(255, 255, 255, 72);
            for (int entityIndex = 0; entityIndex < CoreValue.ObjectManager.Entities.Count; entityIndex++) {
                Entity entity = CoreValue.ObjectManager.Entities[entityIndex];
                for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                    if (entity.Components[componentIndex] is SpriteComponent sprite && sprite.Color.Equals(separatorColor)) {
                        return sprite;
                    }
                }
            }

            throw new InvalidOperationException("Expected a docking split separator sprite.");
        }

        /// <summary>
        /// Finds the title-bar background sprite for a dockable panel.
        /// </summary>
        /// <param name="dockable">Panel whose title-bar sprite should be returned.</param>
        /// <returns>The panel title-bar sprite.</returns>
        static SpriteComponent FindTitleBarSprite(DockableEntity dockable) {
            for (int i = 0; i < dockable.Components.Count; i++) {
                if (dockable.Components[i] is SpriteComponent sprite && sprite.Size.Y == DockableEntity.TitleBarHeight) {
                    return sprite;
                }
            }

            throw new InvalidOperationException("Expected a docked panel title-bar sprite.");
        }

        /// <summary>
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Private field name to read.</param>
        /// <returns>The requested private field value.</returns>
        static T GetPrivateField<T>(object target, string fieldName) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return Assert.IsType<T>(field.GetValue(target));
        }

        /// <summary>
        /// Creates a deterministic font asset for dock tab labels.
        /// </summary>
        /// <returns>A small test font.</returns>
        static FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f)
            };

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture { Width = 64, Height = 64 },
                characters,
                16f,
                64,
                64);
        }
    }
}
