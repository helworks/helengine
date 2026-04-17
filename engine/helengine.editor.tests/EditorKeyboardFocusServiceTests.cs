using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies keyboard-focus traversal, activation, and pointer synchronization for the editor focus service.
    /// </summary>
    public class EditorKeyboardFocusServiceTests {
        /// <summary>
        /// Ensures dock-local traversal follows subgroup order before target tab order.
        /// </summary>
        [Fact]
        public void HandleTab_WhenToolbarAndContentGroupsExist_MovesByGroupOrderThenTabIndex() {
            InitializeCore();
            DockableEntity viewportDock = CreateDock("Viewport", 0, 0, 400, 300);
            TestFocusGroup toolbarGroup = new TestFocusGroup(viewportDock, 1, 0, 0, 400, 20);
            TestFocusGroup contentGroup = new TestFocusGroup(viewportDock, 0, 0, 20, 400, 280);
            TestFocusTarget contentTarget = new TestFocusTarget(contentGroup, 0, false, 10, 40, 100, 100);
            TestFocusTarget translateTarget = new TestFocusTarget(toolbarGroup, 0, false, 10, 2, 20, 16);
            TestFocusTarget rotateTarget = new TestFocusTarget(toolbarGroup, 1, false, 34, 2, 20, 16);

            EditorKeyboardFocusService.Reset();
            EditorKeyboardFocusService.RegisterGroup(viewportDock);
            EditorKeyboardFocusService.RegisterGroup(toolbarGroup);
            EditorKeyboardFocusService.RegisterGroup(contentGroup);
            EditorKeyboardFocusService.RegisterTarget(contentTarget);
            EditorKeyboardFocusService.RegisterTarget(translateTarget);
            EditorKeyboardFocusService.RegisterTarget(rotateTarget);
            EditorKeyboardFocusService.SetDockOrder(new[] { viewportDock });
            EditorKeyboardFocusService.SetFocusedTarget(contentTarget);

            EditorKeyboardFocusService.HandleTab(true);

            Assert.True(translateTarget.IsFocused);
            Assert.False(contentTarget.IsFocused);
            Assert.False(rotateTarget.IsFocused);
        }

        /// <summary>
        /// Ensures backward dock-local traversal moves to the previous valid target inside the active dock.
        /// </summary>
        [Fact]
        public void HandleShiftTab_WhenTargetIsFocused_MovesBackwardWithinTheActiveRootDock() {
            InitializeCore();
            DockableEntity viewportDock = CreateDock("Viewport", 0, 0, 400, 300);
            TestFocusTarget firstTarget = new TestFocusTarget(viewportDock, 0, false, 10, 10, 20, 20);
            TestFocusTarget secondTarget = new TestFocusTarget(viewportDock, 1, false, 40, 10, 20, 20);

            EditorKeyboardFocusService.Reset();
            EditorKeyboardFocusService.RegisterGroup(viewportDock);
            EditorKeyboardFocusService.RegisterTarget(firstTarget);
            EditorKeyboardFocusService.RegisterTarget(secondTarget);
            EditorKeyboardFocusService.SetDockOrder(new[] { viewportDock });
            EditorKeyboardFocusService.SetFocusedTarget(secondTarget);

            EditorKeyboardFocusService.HandleTab(false);

            Assert.True(firstTarget.IsFocused);
            Assert.False(secondTarget.IsFocused);
        }

        /// <summary>
        /// Ensures control-tab activates the next dock and focuses that dock's default target.
        /// </summary>
        [Fact]
        public void HandleCtrlTab_WhenForwardIsTrue_ActivatesTheNextVisibleDockDefaultTarget() {
            InitializeCore();
            DockableEntity hierarchyDock = CreateDock("Hierarchy", 0, 0, 240, 300);
            DockableEntity viewportDock = CreateDock("Viewport", 240, 0, 400, 300);
            TestFocusTarget hierarchyTarget = new TestFocusTarget(hierarchyDock, 0, false, 10, 10, 40, 20);
            TestFocusTarget viewportTarget = new TestFocusTarget(viewportDock, 0, true, 260, 20, 100, 80);

            EditorKeyboardFocusService.Reset();
            EditorKeyboardFocusService.RegisterGroup(hierarchyDock);
            EditorKeyboardFocusService.RegisterGroup(viewportDock);
            EditorKeyboardFocusService.RegisterTarget(hierarchyTarget);
            EditorKeyboardFocusService.RegisterTarget(viewportTarget);
            EditorKeyboardFocusService.SetDockOrder(new[] { hierarchyDock, viewportDock });
            EditorKeyboardFocusService.SetFocusedTarget(hierarchyTarget);

            EditorKeyboardFocusService.HandleCtrlTab(true);

            Assert.True(viewportTarget.IsFocused);
            Assert.False(hierarchyTarget.IsFocused);
        }

        /// <summary>
        /// Ensures control-shift-tab activates the previous dock and focuses that dock's default target.
        /// </summary>
        [Fact]
        public void HandleCtrlTab_WhenForwardIsFalse_ActivatesThePreviousVisibleDockDefaultTarget() {
            InitializeCore();
            DockableEntity hierarchyDock = CreateDock("Hierarchy", 0, 0, 240, 300);
            DockableEntity viewportDock = CreateDock("Viewport", 240, 0, 400, 300);
            TestFocusTarget hierarchyTarget = new TestFocusTarget(hierarchyDock, 0, true, 10, 10, 40, 20);
            TestFocusTarget viewportTarget = new TestFocusTarget(viewportDock, 0, false, 260, 20, 100, 80);

            EditorKeyboardFocusService.Reset();
            EditorKeyboardFocusService.RegisterGroup(hierarchyDock);
            EditorKeyboardFocusService.RegisterGroup(viewportDock);
            EditorKeyboardFocusService.RegisterTarget(hierarchyTarget);
            EditorKeyboardFocusService.RegisterTarget(viewportTarget);
            EditorKeyboardFocusService.SetDockOrder(new[] { hierarchyDock, viewportDock });
            EditorKeyboardFocusService.SetFocusedTarget(viewportTarget);

            EditorKeyboardFocusService.HandleCtrlTab(false);

            Assert.True(hierarchyTarget.IsFocused);
            Assert.False(viewportTarget.IsFocused);
        }

        /// <summary>
        /// Ensures left-clicking a focusable target synchronizes the focused target immediately.
        /// </summary>
        [Fact]
        public void HandlePointerPressed_WhenLeftClickHitsTarget_FocusesTargetAndDock() {
            InitializeCore();
            DockableEntity viewportDock = CreateDock("Viewport", 0, 0, 400, 300);
            TestFocusTarget contentTarget = new TestFocusTarget(viewportDock, 0, true, 20, 40, 100, 100);

            EditorKeyboardFocusService.Reset();
            EditorKeyboardFocusService.RegisterGroup(viewportDock);
            EditorKeyboardFocusService.RegisterTarget(contentTarget);
            EditorKeyboardFocusService.SetDockOrder(new[] { viewportDock });

            EditorKeyboardFocusService.HandlePointerPressed(new int2(40, 60), false);

            Assert.True(contentTarget.IsFocused);
        }

        /// <summary>
        /// Ensures right-clicking empty dock space keeps the current target while still activating the dock.
        /// </summary>
        [Fact]
        public void HandlePointerPressed_WhenRightClickHitsRootWithoutTarget_ActivatesDockAndLeavesTargetUnchanged() {
            InitializeCore();
            DockableEntity viewportDock = CreateDock("Viewport", 0, 0, 400, 300);
            TestFocusTarget contentTarget = new TestFocusTarget(viewportDock, 0, true, 20, 40, 100, 100);

            EditorKeyboardFocusService.Reset();
            EditorKeyboardFocusService.RegisterGroup(viewportDock);
            EditorKeyboardFocusService.RegisterTarget(contentTarget);
            EditorKeyboardFocusService.SetDockOrder(new[] { viewportDock });
            EditorKeyboardFocusService.SetFocusedTarget(contentTarget);

            EditorKeyboardFocusService.HandlePointerPressed(new int2(300, 10), true);

            Assert.True(contentTarget.IsFocused);
        }

        /// <summary>
        /// Ensures unregistering the focused target causes the service to recover to the next valid target in the same dock.
        /// </summary>
        [Fact]
        public void Update_WhenFocusedTargetIsUnregistered_FallsBackToTheNextValidTargetInsideTheSameDock() {
            InitializeCore();
            DockableEntity hierarchyDock = CreateDock("Hierarchy", 0, 0, 240, 300);
            TestFocusTarget firstTarget = new TestFocusTarget(hierarchyDock, 0, false, 10, 10, 40, 20);
            TestFocusTarget secondTarget = new TestFocusTarget(hierarchyDock, 1, false, 10, 40, 40, 20);

            EditorKeyboardFocusService.Reset();
            EditorKeyboardFocusService.RegisterGroup(hierarchyDock);
            EditorKeyboardFocusService.RegisterTarget(firstTarget);
            EditorKeyboardFocusService.RegisterTarget(secondTarget);
            EditorKeyboardFocusService.SetDockOrder(new[] { hierarchyDock });
            EditorKeyboardFocusService.SetFocusedTarget(firstTarget);
            EditorKeyboardFocusService.UnregisterTarget(firstTarget);

            EditorKeyboardFocusService.Update();

            Assert.True(secondTarget.IsFocused);
            Assert.False(firstTarget.IsFocused);
        }

        /// <summary>
        /// Initializes the core services required by dock-backed focus tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(null, new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Creates a dockable test surface positioned for pointer hit testing.
        /// </summary>
        /// <param name="title">Title shown by the dock.</param>
        /// <param name="left">Left edge of the dock.</param>
        /// <param name="top">Top edge of the dock.</param>
        /// <param name="width">Width of the dock content area.</param>
        /// <param name="height">Height of the dock content area.</param>
        /// <returns>Configured dockable entity.</returns>
        DockableEntity CreateDock(string title, int left, int top, int width, int height) {
            DockableEntity dock = new DockableEntity(CreateFont());
            dock.Title = title;
            dock.Position = new float3(left, top, 0f);
            dock.Size = new int2(width, height);
            return dock;
        }

        /// <summary>
        /// Creates a deterministic font asset for dock and label layout in these tests.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['H'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['V'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['w'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
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
