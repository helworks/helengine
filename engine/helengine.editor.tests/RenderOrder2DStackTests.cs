using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the explicit editor 2D render stack for dockables and overlays.
    /// </summary>
    public class RenderOrder2DStackTests {
        /// <summary>
        /// Ensures floating dockables sit above docked panels while remaining below overlay menus.
        /// </summary>
        [Fact]
        public void FloatingDockable_AppliesExplicitBiasAboveDockedPanels() {
            InitializeCore();

            FontAsset font = CreateFont();
            DockableEntity docked = new DockableEntity(font);
            DockableEntity floating = new DockableEntity(font);
            DockLayoutEngine layout = new DockLayoutEngine();

            layout.DockAsRoot(docked);

            SpriteComponent dockedTitleBar = FindTitleBarSprite(docked);
            SpriteComponent floatingTitleBar = FindTitleBarSprite(floating);

            Assert.True(floatingTitleBar.RenderOrder2D > dockedTitleBar.RenderOrder2D);
            Assert.True(floatingTitleBar.RenderOrder2D < RenderOrder2D.OverlayBackground);
        }

        /// <summary>
        /// Initializes the core services required for render-order tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(null, new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Creates a deterministic font asset for dockable layout tests.
        /// </summary>
        /// <returns>Font asset used by the dockables created in this test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['T'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f)
            };

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                16f,
                64,
                64);
        }

        /// <summary>
        /// Finds the title-bar sprite registered on a dockable entity.
        /// </summary>
        /// <param name="dockable">Dockable whose title-bar sprite should be returned.</param>
        /// <returns>Title-bar sprite component for the supplied dockable.</returns>
        SpriteComponent FindTitleBarSprite(DockableEntity dockable) {
            for (int i = 0; i < dockable.Components.Count; i++) {
                if (dockable.Components[i] is SpriteComponent sprite && sprite.Size.Y == DockableEntity.TitleBarHeight) {
                    return sprite;
                }
            }

            throw new InvalidOperationException("Expected the dockable title-bar sprite to exist.");
        }
    }
}
