using helengine;
using helengine.core.tests.managers.rendering;
using Xunit;

namespace helengine.core.tests.components {
    /// <summary>
    /// Verifies the FPS overlay can draw its font at native pixel size like any other text in the demo scenes.
    /// </summary>
    public sealed class FpsNativeFontPixelsTests {
        /// <summary>
        /// Ensures enabling the mode pins the authored scale to one and rejects later resizing, while disabling restores scaling.
        /// </summary>
        [Fact]
        public void NativeFontPixels_PinsScaleToOneUntilDisabled() {
            FPSComponent fps = new FPSComponent { FontScale = 2f };

            fps.NativeFontPixels = TextNativePixelMode.RoundUp;

            Assert.Equal(1f, fps.FontScale);
            Assert.Throws<ArgumentOutOfRangeException>(() => fps.FontScale = 2f);
            fps.NativeFontPixels = TextNativePixelMode.Disabled;
            fps.FontScale = 2f;
            Assert.Equal(2f, fps.FontScale);
        }

        /// <summary>
        /// Ensures the mode is an appended persisted member so authored scenes keep it.
        /// </summary>
        [Fact]
        public void NativeFontPixels_IsAppendedPersistedMember() {
            System.Reflection.PropertyInfo member = typeof(FPSComponent).GetProperty(nameof(FPSComponent.NativeFontPixels));

            Assert.True(ScenePersistenceValueWalker.IsSerializableMember(member));
            Assert.Equal(typeof(TextNativePixelMode), ScenePersistenceValueWalker.GetMemberValueType(member));
            Assert.True(ScenePersistenceMemberOrdering.IsAppended(member));
        }

        /// <summary>
        /// Ensures every generated overlay row inherits the mode at unit scale, both when built and when the mode changes later.
        /// </summary>
        [Fact]
        public void OverlayRows_FollowTheComponentMode() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using Core core = CreateCore(renderer);
            using FontAsset font = new FontAsset(new FontInfo("fps native", 10, 3f), renderer.CreateTexture(16, 16),
                new Dictionary<char, FontChar> { ['0'] = new FontChar(new float4(0f, 0f, 0.25f, 0.5f), 2f, 5f, 0f, 0f) }, 10f, 16, 16);
            Entity entity = new Entity(core);
            entity.InitComponents();
            entity.InitChildren();
            FPSComponent fps = new FPSComponent { Font = font, FontScale = 2f };
            entity.AddComponent(fps);

            fps.NativeFontPixels = TextNativePixelMode.RoundUp;

            List<TextComponent> rows = CollectText(entity);
            Assert.NotEmpty(rows);
            Assert.All(rows, row => {
                Assert.Equal(TextNativePixelMode.RoundUp, row.NativeFontPixels);
                Assert.Equal(1f, row.FontScale);
            });
        }

        /// <summary>
        /// Gathers every text component below an entity.
        /// </summary>
        /// <param name="entity">Root entity to search.</param>
        /// <returns>All descendant text components.</returns>
        static List<TextComponent> CollectText(Entity entity) {
            List<TextComponent> found = new List<TextComponent>();
            foreach (Entity child in entity.Children) {
                foreach (Component component in child.Components) {
                    if (component is TextComponent text) {
                        found.Add(text);
                    }
                }
                found.AddRange(CollectText(child));
            }
            return found;
        }

        /// <summary>
        /// Creates the real headless runtime with a test renderer that owns its atlas textures.
        /// </summary>
        /// <param name="renderer">Renderer installed on the core.</param>
        /// <returns>An initialized core for the caller to dispose.</returns>
        static Core CreateCore(Batch2DTestRenderManager renderer) {
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory) });
            core.Initialize(null, renderer, null, new PlatformInfo("test", "test-version"));
            return core;
        }
    }
}
