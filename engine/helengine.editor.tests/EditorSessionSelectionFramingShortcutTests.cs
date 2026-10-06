using System.Reflection;
using helengine.editor.tests.testing;

namespace helengine.editor.tests {
    /// <summary>Exercises F through actual session input rather than calling the viewport framing callback directly.</summary>
    public sealed class EditorSessionSelectionFramingShortcutTests {
        /// <summary>Frames a selected 1280x720 menu from hierarchy or toolbar focus, preserving text entry and the last focused viewport.</summary>
        [Theory]
        [InlineData("hierarchy", CameraProjectionMode.Perspective)]
        [InlineData("hierarchy", CameraProjectionMode.Orthographic)]
        [InlineData("toolbar", CameraProjectionMode.Perspective)]
        [InlineData("viewport", CameraProjectionMode.Perspective)]
        [InlineData("transition", CameraProjectionMode.Perspective)]
        [InlineData("viewport-transition", CameraProjectionMode.Perspective)]
        [InlineData("duplicate", CameraProjectionMode.Perspective)]
        [InlineData("text", CameraProjectionMode.Perspective)]
        [InlineData("navigation", CameraProjectionMode.Perspective)]
        public void UpdateFrame_FFramesMenuSelectionAcrossFocusContexts(string context, CameraProjectionMode mode) {
            using RealEditorSessionFixture fixture = new RealEditorSessionFixture();
            EditorSession session = fixture.Session;
            EditorSessionInteractionServices interactions = session.InteractionServices;
            ViewportWorkspacePanelController primary = (ViewportWorkspacePanelController)Assert.Single(session.GetPanelInstancesForTest("viewport")).Controller;
            ViewportWorkspacePanelController controller = primary;
            if (context == "duplicate") {
                session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
                controller = (ViewportWorkspacePanelController)session.GetPanelInstancesForTest("viewport")[1].Controller;
                session.UpdateFrame(1280, 720);
            }
            EditorViewportWorkspaceState state = controller.ViewportState;
            ((ICameraProjectionSettings)state.SceneCamera).ProjectionMode = mode;
            EditorEntity menu = new EditorEntity(fixture.Core, interactions) {
                Name = "DemoDiscMenuRoot", IsSceneOwned = true, LayerMask = EditorLayerMasks.SceneObjects
            };
            menu.Components.OfType<EntitySaveComponent>().Single().EntityId = fixture.Core.SceneEntityIdAllocator.Allocate();
            menu.AddComponent(new ViewportComponent { BindingMode = ViewportComponent.FixedBindingMode, FixedSize = new int2(1280, 720) });
            menu.InitializeHierarchy();
            interactions.Selection.SetSelectedEntity(menu);
            session.RefreshHierarchy();
            SceneHierarchyPanel hierarchy = (SceneHierarchyPanel)Assert.Single(session.GetPanelInstancesForTest("scene-hierarchy")).Dockable;
            SceneHierarchyRow row = GetField<List<SceneHierarchyRow>>(hierarchy, "rows").Single(candidate => ReferenceEquals(candidate.NodeEntity, menu));
            if (context == "duplicate") {
                interactions.KeyboardFocus.SetFocusedTarget(GetField<EditorFocusTarget>(state.Viewport, "ViewportContentFocusTarget"));
                interactions.KeyboardFocus.SetFocusedTarget(row.FocusTarget);
            } else if (context == "toolbar") {
                interactions.KeyboardFocus.SetFocusedTarget(GetField<EditorFocusTarget[]>(state.Viewport, "ToolButtonFocusTargets")[0]);
            } else if (context == "viewport" || context == "viewport-transition") {
                interactions.KeyboardFocus.SetFocusedTarget(GetField<EditorFocusTarget>(state.Viewport, "ViewportContentFocusTarget"));
            } else {
                interactions.KeyboardFocus.SetFocusedTarget(row.FocusTarget);
            }
            if (context == "transition" || context == "viewport-transition") {
                state.NavigationController.SelectTarget(new EditorViewportNavigationTarget(0, 0, 1));
                Assert.True(state.NavigationController.IsTransitioning);
            }
            TextBoxComponent textEntry = null;
            if (context == "text") {
                EditorEntity owner = new EditorEntity(fixture.Core, interactions) { InternalEntity = true };
                textEntry = new TextBoxComponent(new int2(180, 28), fixture.Font, "Name");
                owner.AddComponent(textEntry);
                owner.InitializeHierarchy();
                textEntry.IsFocused = true;
            }
            float3 before = state.SceneCameraEntity.Position;
            float3 primaryBefore = primary.ViewportState.SceneCameraEntity.Position;
            if (context == "navigation") {
                fixture.Input.SetMouseState(new MouseState { RightButton = ButtonState.Pressed });
            }
            fixture.Input.SetKeyboardState(new KeyboardState(Keys.F));
            session.UpdateFrame(1280, 720);
            if (context == "text" || context == "navigation") {
                Assert.Equal(before, state.SceneCameraEntity.Position);
                if (textEntry != null) {
                    textEntry.IsFocused = false;
                }
                return;
            }
            Assert.NotEqual(before, state.SceneCameraEntity.Position);
            Assert.Equal(new float3(640, -360, 0), state.CameraController.GetOrbitTarget());
            Assert.False(state.NavigationController.IsTransitioning);
            Assert.Equal(state.SceneCameraEntity.Position, state.PickerCameraEntity.Position);
            if (context == "duplicate") {
                Assert.Equal(primaryBefore, primary.ViewportState.SceneCameraEntity.Position);
            }
            float3 framed = state.SceneCameraEntity.Position;
            fixture.Input.SetKeyboardState(new KeyboardState());
            session.UpdateFrame(1280, 720);
            Assert.Equal(framed, state.SceneCameraEntity.Position);
        }

        /// <summary>Reads a registered presentation target or row pool so tests can assign ordinary keyboard focus precisely.</summary>
        /// <param name="owner">Panel that owns the registered focus target.</param>
        /// <param name="name">Field containing the target or row pool.</param>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <returns>Panel-owned target or row pool.</returns>
        static T GetField<T>(object owner, string name) {
            return (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        }
    }
}
