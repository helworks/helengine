using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies keyboard-focus behavior for asset-browser rows and toolbar controls.
    /// </summary>
    public class AssetBrowserViewKeyboardFocusTests : IDisposable {
        /// <summary>
        /// Temporary project roots created by the current test instance.
        /// </summary>
        readonly List<string> TemporaryProjectRoots = new List<string>();

        /// <summary>
        /// Deletes temporary project state and clears shared keyboard-focus registrations.
        /// </summary>
        public void Dispose() {
            EditorKeyboardFocusService.Reset();

            for (int i = 0; i < TemporaryProjectRoots.Count; i++) {
                string path = TemporaryProjectRoots[i];
                if (Directory.Exists(path)) {
                    Directory.Delete(path, true);
                }
            }
        }

        /// <summary>
        /// Ensures supplying a dock focus group wires the up button and visible rows into keyboard traversal.
        /// </summary>
        [Fact]
        public void AssetBrowserView_WhenDockGroupIsSupplied_RegistersTheUpButtonAndVisibleRows() {
            string projectRoot = CreateProjectRoot();
            Directory.CreateDirectory(Path.Combine(projectRoot, "assets", "Folder"));
            File.WriteAllText(Path.Combine(projectRoot, "assets", "alpha.txt"), "content");

            InitializeCore(projectRoot);

            TestFocusGroup focusGroup = new TestFocusGroup(null, 0, 0, 0, 400, 300);
            AssetBrowserView browserView = new AssetBrowserView(
                CreateFont(),
                projectRoot,
                EditorLayerMasks.EditorUi,
                1,
                2,
                3,
                4,
                true,
                focusGroup.FocusGroup);

            browserView.UpdateLayout(320, 240);

            ButtonComponent upButton = GetPrivateField<ButtonComponent>(browserView, "UpButton");
            List<AssetBrowserRow> rows = GetPrivateField<List<AssetBrowserRow>>(browserView, "Rows");

            Assert.Same(focusGroup.FocusGroup, upButton.FocusGroup);
            Assert.Equal(0, upButton.TabIndex);
            Assert.Same(focusGroup.FocusGroup, rows[0].FocusTarget.FocusGroup);
            Assert.Equal(1, rows[0].FocusTarget.TabIndex);
            Assert.True(rows[0].Entity.Enabled);
            Assert.Equal(3, GetRegisteredTargetCount());
        }

        /// <summary>
        /// Ensures keyboard activation on one row reuses the same navigation path used by pointer release.
        /// </summary>
        [Fact]
        public void AssetBrowserView_WhenEnterActivatesARow_UsesTheSameNavigationOrAssetActionAsMouseRelease() {
            string projectRoot = CreateProjectRoot();
            Directory.CreateDirectory(Path.Combine(projectRoot, "assets", "Folder"));
            File.WriteAllText(Path.Combine(projectRoot, "assets", "alpha.txt"), "content");

            InitializeCore(projectRoot);

            AssetBrowserView browserView = new AssetBrowserView(
                CreateFont(),
                projectRoot,
                EditorLayerMasks.EditorUi,
                1,
                2,
                3,
                4);

            browserView.UpdateLayout(320, 240);

            List<AssetBrowserRow> rows = GetPrivateField<List<AssetBrowserRow>>(browserView, "Rows");

            Assert.Equal("Folder", rows[0].Entry.Name);

            rows[0].FocusTarget.ActivateFromKey(Keys.Enter);

            Assert.EndsWith(Path.Combine("assets", "Folder"), browserView.CurrentDirectoryPath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures shrinking the visible entry list keeps pooled row targets registered but no longer focusable.
        /// </summary>
        [Fact]
        public void AssetBrowserView_WhenRowsShrink_KeepsPooledTargetsRegisteredButUnfocusable() {
            string projectRoot = CreateProjectRoot();
            Directory.CreateDirectory(Path.Combine(projectRoot, "assets", "Empty"));
            File.WriteAllText(Path.Combine(projectRoot, "assets", "alpha.txt"), "content");

            InitializeCore(projectRoot);

            TestFocusGroup focusGroup = new TestFocusGroup(null, 0, 0, 0, 400, 300);
            AssetBrowserView browserView = new AssetBrowserView(
                CreateFont(),
                projectRoot,
                EditorLayerMasks.EditorUi,
                1,
                2,
                3,
                4,
                true,
                focusGroup.FocusGroup);

            browserView.UpdateLayout(320, 240);

            Assert.Equal(3, GetRegisteredTargetCount());
            Assert.True(browserView.TryNavigateTo("Empty"));

            List<AssetBrowserRow> rows = GetPrivateField<List<AssetBrowserRow>>(browserView, "Rows");

            Assert.Equal(3, GetRegisteredTargetCount());
            Assert.False(rows[0].Entity.Enabled);
            Assert.False(rows[0].FocusTarget.CanReceiveFocus);
            Assert.False(rows[1].Entity.Enabled);
            Assert.False(rows[1].FocusTarget.CanReceiveFocus);
        }

        /// <summary>
        /// Ensures the browser derives the visible item count from the viewport and uses the scroll component as the clip region.
        /// </summary>
        [Fact]
        public void AssetBrowserView_WhenEntriesExceedViewport_ScrollsAutomatically() {
            string projectRoot = CreateProjectRoot();
            Directory.CreateDirectory(Path.Combine(projectRoot, "assets", "buffer"));
            Directory.CreateDirectory(Path.Combine(projectRoot, "assets", "folder"));
            Directory.CreateDirectory(Path.Combine(projectRoot, "assets", "home"));
            Directory.CreateDirectory(Path.Combine(projectRoot, "assets", "logger"));
            Directory.CreateDirectory(Path.Combine(projectRoot, "assets", "model"));
            Directory.CreateDirectory(Path.Combine(projectRoot, "assets", "output"));
            Directory.CreateDirectory(Path.Combine(projectRoot, "assets", "robot"));
            Directory.CreateDirectory(Path.Combine(projectRoot, "assets", "ruler"));

            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(projectRoot)
            });
            TestInputBackend input = new TestInputBackend();
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), input, new PlatformInfo("test", "test-version"));

            AssetBrowserView browserView = new AssetBrowserView(
                CreateFont(),
                projectRoot,
                EditorLayerMasks.EditorUi,
                1,
                2,
                3,
                4);

            int browserHeight = AssetBrowserView.ToolbarHeight + (AssetBrowserView.RowHeight * 4) - 1;
            browserView.UpdateLayout(320, browserHeight);

            ScrollComponent scrollComponent = GetPrivateField<ScrollComponent>(browserView, "ListScrollComponent");
            EditorEntity listRoot = GetPrivateField<EditorEntity>(browserView, "ListRoot");
            float4 clipRect = scrollComponent.GetClipRect();

            Assert.Equal(320f, clipRect.Z);
            Assert.Equal((float)AssetBrowserView.ToolbarHeight, clipRect.Y);
            Assert.Equal((float)(AssetBrowserView.RowHeight * 4 - 1), clipRect.W);
            Assert.Equal(4, scrollComponent.VisibleItemCount);
            Assert.Equal(0, scrollComponent.ScrollOffset);
            Assert.Equal(0f, listRoot.LocalPosition.Y);

            input.SetMouseState(new MouseState(40, 60, -120, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            Core.Instance.Update();

            Assert.Equal(1, scrollComponent.ScrollOffset);
            Assert.Equal(-AssetBrowserView.RowHeight, listRoot.LocalPosition.Y);
        }

        /// <summary>
        /// Creates a temporary project root with an assets directory.
        /// </summary>
        /// <returns>Path to the new project root.</returns>
        string CreateProjectRoot() {
            string projectRoot = Path.Combine(Path.GetTempPath(), "helengine-asset-browser-focus-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(projectRoot, "assets"));
            TemporaryProjectRoots.Add(projectRoot);
            return projectRoot;
        }

        /// <summary>
        /// Initializes the core services required by asset-browser keyboard-focus tests.
        /// </summary>
        /// <param name="projectRoot">Project root used as the content path.</param>
        void InitializeCore(string projectRoot) {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(projectRoot)
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
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

            object value = field.GetValue(target);
            return Assert.IsType<T>(value);
        }

        /// <summary>
        /// Reads the number of currently registered keyboard-focus targets.
        /// </summary>
        /// <returns>Registered focus-target count.</returns>
        int GetRegisteredTargetCount() {
            FieldInfo field = typeof(EditorKeyboardFocusService).GetField("RegisteredTargets", BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null) {
                throw new InvalidOperationException("Expected RegisteredTargets field was not found.");
            }

            object value = field.GetValue(null);
            List<IFocusTarget> targets = Assert.IsType<List<IFocusTarget>>(value);
            return targets.Count;
        }

        /// <summary>
        /// Creates a deterministic font asset for asset-browser layout tests.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['E'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['F'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
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

