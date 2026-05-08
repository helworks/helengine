using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies shared platform tab strip behavior.
    /// </summary>
    public sealed class PlatformTabStripViewTests : IDisposable {
        /// <summary>
        /// Initializes the core services required by the shared platform tab strip tests.
        /// </summary>
        public PlatformTabStripViewTests() {
            Core core = new Core();
            core.Initialize(null, new TestRenderManager2D(), null);
            EditorKeyboardFocusService.Reset();
        }

        /// <summary>
        /// Ensures one tab is created for each supplied platform id.
        /// </summary>
        [Fact]
        public void SetPlatforms_WhenPlatformsAreProvided_CreatesOneTabPerPlatform() {
            PlatformTabStripView view = new PlatformTabStripView(CreateFont(), 1, 88, 24, 6, 24);

            view.SetPlatforms(["windows", "ps2", "linux"], "windows", _ => { });

            Assert.Equal(3, view.TabCount);
            Assert.Equal("windows", view.SelectedPlatformId);
        }

        /// <summary>
        /// Ensures overflow arrows activate when the tabs exceed the available width.
        /// </summary>
        [Fact]
        public void UpdateLayout_WhenTabsOverflow_EnablesOverflowState() {
            PlatformTabStripView view = new PlatformTabStripView(CreateFont(), 1, 88, 24, 6, 24);

            view.SetPlatforms(["windows", "ps2", "linux", "gamecube", "wii"], "windows", _ => { });
            view.UpdateLayout(0, 0, 220);

            Assert.True(view.HasOverflow);
            Assert.True(view.CanScrollRight);
            Assert.False(view.CanScrollLeft);
        }

        /// <summary>
        /// Ensures selecting a clipped tab scrolls the strip until the tab becomes fully visible.
        /// </summary>
        [Fact]
        public void SetSelectedPlatform_WhenSelectedTabIsClipped_RevealsTheTab() {
            PlatformTabStripView view = new PlatformTabStripView(CreateFont(), 1, 88, 24, 6, 24);

            view.SetPlatforms(["windows", "ps2", "linux", "gamecube", "wii"], "windows", _ => { });
            view.UpdateLayout(0, 0, 220);

            view.SetSelectedPlatform("wii");

            Assert.True(view.HorizontalScrollOffset > 0);
            Assert.True(view.IsPlatformFullyVisible("wii"));
        }

        /// <summary>
        /// Ensures keyboard navigation moves selection to the adjacent platform and reveals it if needed.
        /// </summary>
        [Fact]
        public void KeyboardFocus_WhenRightArrowIsActivated_MovesSelectionToTheNextPlatform() {
            PlatformTabStripView view = new PlatformTabStripView(CreateFont(), 1, 88, 24, 6, 24);
            string selectedPlatformId = string.Empty;

            view.SetPlatforms(["windows", "ps2", "linux", "gamecube", "wii"], "windows", platformId => selectedPlatformId = platformId);
            view.UpdateLayout(0, 0, 220);

            List<EditorFocusTarget> focusTargets = GetPrivateField<List<EditorFocusTarget>>(view, "TabFocusTargets");
            EditorKeyboardFocusService.SetFocusedTarget(focusTargets[0]);
            focusTargets[0].ActivateFromKey(Keys.Right);

            Assert.Equal("ps2", view.SelectedPlatformId);
            Assert.Equal("ps2", selectedPlatformId);
            Assert.True(view.IsPlatformFullyVisible("ps2"));
        }

        /// <summary>
        /// Clears shared keyboard focus state after each test.
        /// </summary>
        public void Dispose() {
            EditorKeyboardFocusService.Reset();
        }

        /// <summary>
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) {
                throw new InvalidOperationException("Expected private field was not found.");
            }

            return Assert.IsType<T>(field.GetValue(target));
        }

        /// <summary>
        /// Creates a deterministic font asset for shared tab-strip layout tests.
        /// </summary>
        /// <returns>Font asset used by the test strip instances.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['2'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['w'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['x'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
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
    }
}
