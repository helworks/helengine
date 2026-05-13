using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.dock {
    /// <summary>
    /// Verifies dock layout snapshot and restore behavior.
    /// </summary>
    public sealed class DockLayoutEngineSnapshotTests {
        /// <summary>
        /// Ensures capture returns the expected split tree and preserves the authored split fraction.
        /// </summary>
        [Fact]
        public void CaptureSnapshot_WhenLayoutContainsSplitAndTabs_ReturnsTreeWithActiveTabAndFractions() {
            InitializeCore();
            DockLayoutEngine layout = new DockLayoutEngine();
            DockableEntity viewport = CreateDock("Viewport");
            DockableEntity logger = CreateDock("Logger");
            DockableEntity preview = CreateDock("Preview");

            layout.Add(viewport);
            layout.Add(logger);
            layout.Add(preview);
            layout.DockAsRoot(viewport);
            layout.DockRelative(logger, viewport, DockInsertDirection.Bottom, 0.7f);
            layout.DockRelative(preview, logger, DockInsertDirection.Fill, 0.5f);

            EditorWorkspaceDockSnapshot snapshot = layout.CaptureSnapshot(dock => dock.Title.ToLowerInvariant());

            EditorWorkspaceDockSplitSnapshot root = Assert.IsType<EditorWorkspaceDockSplitSnapshot>(snapshot.Root);
            Assert.Equal(0.7f, root.SplitFraction, 3);
            EditorWorkspaceDockLeafSnapshot second = Assert.IsType<EditorWorkspaceDockLeafSnapshot>(root.Second);
            Assert.Equal("preview", second.ActiveInstanceId);
            Assert.Equal(["logger", "preview"], second.InstanceIds);
        }

        /// <summary>
        /// Ensures restore rebuilds the visible traversal order from one persisted snapshot tree.
        /// </summary>
        [Fact]
        public void RestoreSnapshot_WhenDockablesAreProvided_RebuildsVisibleTraversalOrder() {
            InitializeCore();
            DockLayoutEngine layout = new DockLayoutEngine();
            DockableEntity viewport = CreateDock("Viewport");
            DockableEntity logger = CreateDock("Logger");

            EditorWorkspaceDockSnapshot snapshot = new EditorWorkspaceDockSnapshot {
                Root = new EditorWorkspaceDockSplitSnapshot {
                    IsVertical = false,
                    SplitFraction = 0.7f,
                    First = new EditorWorkspaceDockLeafSnapshot {
                        ActiveInstanceId = "viewport",
                        InstanceIds = ["viewport"]
                    },
                    Second = new EditorWorkspaceDockLeafSnapshot {
                        ActiveInstanceId = "logger",
                        InstanceIds = ["logger"]
                    }
                }
            };

            layout.Add(viewport);
            layout.Add(logger);
            layout.RestoreSnapshot(snapshot, instanceId => instanceId == "viewport" ? viewport : logger);

            IReadOnlyList<DockableEntity> visible = layout.GetVisibleDockablesInTraversalOrder();
            Assert.Equal(["Viewport", "Logger"], visible.Select(dock => dock.Title).ToArray());
        }

        /// <summary>
        /// Initializes the core services required by dock snapshot tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(null, new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
            EditorKeyboardFocusService.Reset();
        }

        /// <summary>
        /// Creates a dockable entity with the supplied title.
        /// </summary>
        /// <param name="title">Title shown by the dock.</param>
        /// <returns>Configured dockable entity.</returns>
        DockableEntity CreateDock(string title) {
            DockableEntity dock = new DockableEntity(CreateFont());
            dock.Title = title;
            return dock;
        }

        /// <summary>
        /// Creates a deterministic font asset for dock layout tests.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['V'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['L'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['v'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['w'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f)
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
