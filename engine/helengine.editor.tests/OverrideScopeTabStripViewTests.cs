using helengine.editor.tests.testing;
using System.Reflection;

namespace helengine.editor.tests {
    public sealed class OverrideScopeTabStripViewTests : IDisposable {
        public OverrideScopeTabStripViewTests() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new FakeContentStreamSource()
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }

        public void Dispose() {
            Core.Instance?.Dispose();
        }

        [Fact]
        public void SetPlatforms_HidesEnvironmentTabsUntilPlusIsPressed() {
            OverrideScopeTabStripView view = new OverrideScopeTabStripView(Core.Instance, new helengine.editor.EditorSessionInteractionServices(), CreateFont(), 1, 120, 24, 0, 24);

            view.SetPlatforms(["windows"], "windows", ["debug", "release"], "release");

            Assert.False(view.EnvironmentTabsVisible);
            typeof(PlatformTabStripView).GetMethod("HandleEnvironmentAddClicked", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view.PlatformTabs, null);
            Assert.True(view.EnvironmentTabsVisible);
            Assert.Equal("release", view.SelectedEnvironmentId);
        }

        FontAsset CreateFont() {
            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                new Dictionary<char, FontChar> {
                    ['a'] = new FontChar(new float4(0f, 0f, 8f, 16f), 0f, 8f, 0f, 0f)
                },
                16f,
                64,
                64);
        }
    }
}
