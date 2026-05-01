using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the runtime FPS overlay component builds and updates its own text hierarchy.
    /// </summary>
    public class FPSComponentTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the runtime test harness.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the runtime services required by the component tests.
        /// </summary>
        public FPSComponentTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-fps-component-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });

            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputManager());
        }

        /// <summary>
        /// Deletes temporary test content after each run.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the component creates a top-left overlay host with two text children.
        /// </summary>
        [Fact]
        public void ComponentAdded_WhenAttached_BuildsTwoTextChildrenAtTopLeft() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FontAsset font = CreateFont();
            Core.Instance.DefaultFontAsset = font;
            FPSComponent fps = new FPSComponent();

            entity.AddComponent(fps);

            Entity overlayHost = Assert.Single(entity.Children);
            Assert.Equal(new float3(8f, 6f, 0f), overlayHost.LocalPosition);
            Assert.Equal(2, overlayHost.Children.Count);

            Entity updateRow = overlayHost.Children[0];
            Entity renderRow = overlayHost.Children[1];

            TextComponent updateText = Assert.Single(updateRow.Components.OfType<TextComponent>());
            TextComponent renderText = Assert.Single(renderRow.Components.OfType<TextComponent>());

            Assert.Equal("Update FPS: --", updateText.Text);
            Assert.Equal("Render FPS: --", renderText.Text);
            Assert.Equal(0f, updateRow.LocalPosition.Y);
            Assert.Equal(font.LineHeight, renderRow.LocalPosition.Y);
        }

        /// <summary>
        /// Ensures update and render frame ticks refresh both visible lines.
        /// </summary>
        [Fact]
        public void CoreUpdateAndDraw_WhenFrameTicksAdvance_RefreshesBothTextLines() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FontAsset font = CreateFont();
            Core.Instance.DefaultFontAsset = font;
            FPSComponent fps = new FPSComponent();
            fps.RefreshIntervalSeconds = 0d;

            entity.AddComponent(fps);

            Core.Instance.Update();
            Core.Instance.Draw();
            Core.Instance.Update();
            Core.Instance.Draw();

            Entity overlayHost = Assert.Single(entity.Children);
            TextComponent updateText = Assert.Single(overlayHost.Children[0].Components.OfType<TextComponent>());
            TextComponent renderText = Assert.Single(overlayHost.Children[1].Components.OfType<TextComponent>());

            Assert.StartsWith("Update FPS:", updateText.Text);
            Assert.StartsWith("Render FPS:", renderText.Text);
            Assert.NotEqual("Update FPS: --", updateText.Text);
            Assert.NotEqual("Render FPS: --", renderText.Text);
        }

        /// <summary>
        /// Ensures disabling the parent entity removes the text drawables from render participation.
        /// </summary>
        [Fact]
        public void ParentEntity_WhenDisabled_RemovesOverlayDrawablesFromRenderLists() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            Core.Instance.DefaultFontAsset = CreateFont();
            FPSComponent fps = new FPSComponent();

            entity.AddComponent(fps);

            Entity overlayHost = Assert.Single(entity.Children);
            TextComponent updateText = Assert.Single(overlayHost.Children[0].Components.OfType<TextComponent>());
            TextComponent renderText = Assert.Single(overlayHost.Children[1].Components.OfType<TextComponent>());

            Assert.Contains(updateText, Core.Instance.ObjectManager.Drawables2D);
            Assert.Contains(renderText, Core.Instance.ObjectManager.Drawables2D);

            entity.Enabled = false;

            Assert.DoesNotContain(updateText, Core.Instance.ObjectManager.Drawables2D);
            Assert.DoesNotContain(renderText, Core.Instance.ObjectManager.Drawables2D);
            Assert.False(overlayHost.IsHierarchyEnabled);
        }

        /// <summary>
        /// Ensures the parameterless constructor uses the configured default font asset.
        /// </summary>
        [Fact]
        public void Constructor_WhenCoreDefaultFontIsConfigured_AssignsThatFont() {
            FontAsset font = CreateFont();
            Core.Instance.DefaultFontAsset = font;

            FPSComponent fps = new FPSComponent();

            Assert.Same(font, fps.Font);
        }

        /// <summary>
        /// Ensures changing the font after attachment updates the rendered overlay text.
        /// </summary>
        [Fact]
        public void FontProperty_WhenChangedAfterAttachment_UpdatesOverlayTextFonts() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FontAsset initialFont = CreateFont(16f);
            FontAsset replacementFont = CreateFont(24f);
            Core.Instance.DefaultFontAsset = initialFont;

            FPSComponent fps = new FPSComponent();
            entity.AddComponent(fps);
            fps.Font = replacementFont;

            Entity overlayHost = Assert.Single(entity.Children);
            Entity updateRow = overlayHost.Children[0];
            Entity renderRow = overlayHost.Children[1];
            TextComponent updateText = Assert.Single(updateRow.Components.OfType<TextComponent>());
            TextComponent renderText = Assert.Single(renderRow.Components.OfType<TextComponent>());

            Assert.Same(replacementFont, fps.Font);
            Assert.Same(replacementFont, updateText.Font);
            Assert.Same(replacementFont, renderText.Font);
            Assert.Equal(replacementFont.LineHeight, renderRow.LocalPosition.Y);
        }

        /// <summary>
        /// Creates a deterministic font asset containing the glyphs needed by the overlay labels.
        /// </summary>
        /// <returns>Font asset with stable glyph metrics for the tests.</returns>
        FontAsset CreateFont() {
            return CreateFont(16f);
        }

        /// <summary>
        /// Creates a deterministic font asset containing the glyphs needed by the overlay labels.
        /// </summary>
        /// <param name="lineHeight">Line height assigned to the generated test font.</param>
        /// <returns>Font asset with stable glyph metrics for the tests.</returns>
        FontAsset CreateFont(float lineHeight) {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['U'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['F'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['R'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                [':'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                [' '] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['-'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['0'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['1'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['2'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['3'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['4'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['5'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['6'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['7'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['8'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['9'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['.'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f)
            };

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                lineHeight,
                64,
                64);
        }
    }
}
