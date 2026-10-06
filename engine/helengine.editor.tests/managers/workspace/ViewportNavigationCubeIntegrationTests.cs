using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.workspace {
    /// <summary>
    /// Verifies workspace viewports own the navigation overlay and keep the complete camera stack synchronized.
    /// </summary>
    public sealed class ViewportNavigationCubeIntegrationTests {
        /// <summary>
        /// Ensures a workspace viewport owns an updating cube view and a viewport-local navigation controller.
        /// </summary>
        [Fact]
        public void ViewportCreation_AttachesNavigationViewAndControllerToViewport() {
            using EditorSessionWorkspaceTests.EditorSessionHarness harness = EditorSessionWorkspaceTests.EditorSessionHarness.Create();
            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            EditorWorkspacePanelInstance instance = Assert.Single(harness.Session.GetPanelInstancesForTest("viewport"));
            EditorViewportWorkspaceState state = harness.GetViewportControllerForTest(instance).ViewportState;

            EditorViewportNavigationCubeUpdateComponent updater = Assert.Single(state.Viewport.Components.OfType<EditorViewportNavigationCubeUpdateComponent>());
            Assert.Same(state.Viewport.NavigationCube, updater.View);
            Assert.Same(state.Viewport.NavigationController, state.NavigationController);
            Assert.Contains(updater, harness.OwnedCore.ObjectManager.Updateables);
            int cubeUpdateIndex = harness.OwnedCore.ObjectManager.Updateables.IndexOf(updater);
            int cameraUpdateIndex = harness.OwnedCore.ObjectManager.Updateables.IndexOf(state.CameraController);
            TransformTranslationGizmoDragComponent translationDrag = Assert.Single(
                state.SceneCameraEntity.Components.OfType<TransformTranslationGizmoDragComponent>());
            TransformRotationGizmoDragComponent rotationDrag = Assert.Single(
                state.SceneCameraEntity.Components.OfType<TransformRotationGizmoDragComponent>());
            TransformScaleGizmoDragComponent scaleDrag = Assert.Single(
                state.SceneCameraEntity.Components.OfType<TransformScaleGizmoDragComponent>());
            Assert.True(cubeUpdateIndex < cameraUpdateIndex);
            Assert.True(cubeUpdateIndex < harness.OwnedCore.ObjectManager.Updateables.IndexOf(translationDrag));
            Assert.True(cubeUpdateIndex < harness.OwnedCore.ObjectManager.Updateables.IndexOf(rotationDrag));
            Assert.True(cubeUpdateIndex < harness.OwnedCore.ObjectManager.Updateables.IndexOf(scaleDrag));
        }

        /// <summary>
        /// Ensures independent viewport stacks retain distinct pivots and projection modes.
        /// </summary>
        [Fact]
        public void NavigationState_WhenTwoViewportsAreOpen_RemainsViewportLocal() {
            using EditorSessionWorkspaceTests.EditorSessionHarness harness = EditorSessionWorkspaceTests.EditorSessionHarness.Create();
            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            IReadOnlyList<EditorWorkspacePanelInstance> instances = harness.Session.GetPanelInstancesForTest("viewport");
            EditorViewportWorkspaceState first = harness.GetViewportControllerForTest(instances[0]).ViewportState;
            EditorViewportWorkspaceState second = harness.GetViewportControllerForTest(instances[1]).ViewportState;
            first.Viewport.Size = new int2(640, 384);
            second.Viewport.Size = new int2(640, 384);
            float3 firstPivot = new float3(5f, 6f, 7f);
            float3 secondPivot = new float3(-9f, 2f, 3f);
            first.CameraController.SetViewPose(firstPivot, float4.Identity, 10.0);
            second.CameraController.SetViewPose(secondPivot, float4.Identity, 16.0);

            first.NavigationController.SelectTarget(new EditorViewportNavigationTarget(0, 1, 0));

            Assert.Equal(firstPivot, first.CameraController.GetOrbitTarget());
            Assert.Equal(secondPivot, second.CameraController.GetOrbitTarget());
            Assert.Equal(CameraProjectionMode.Orthographic, ((ICameraProjectionSettings)first.SceneCamera).ProjectionMode);
            Assert.Equal(CameraProjectionMode.Perspective, ((ICameraProjectionSettings)second.SceneCamera).ProjectionMode);
            Assert.NotEqual(((ICameraProjectionSettings)first.SceneCamera).OrthographicVerticalSpan,
                ((ICameraProjectionSettings)second.SceneCamera).OrthographicVerticalSpan);
        }

        /// <summary>
        /// Ensures clicking a face synchronizes scene, gizmo, and picker projection matrices immediately.
        /// </summary>
        [Fact]
        public void FaceNavigation_WhenProjectionChanges_SynchronizesAllCameraProjectionMatrices() {
            using EditorSessionWorkspaceTests.EditorSessionHarness harness = EditorSessionWorkspaceTests.EditorSessionHarness.Create();
            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            EditorWorkspacePanelInstance instance = Assert.Single(harness.Session.GetPanelInstancesForTest("viewport"));
            EditorViewportWorkspaceState state = harness.GetViewportControllerForTest(instance).ViewportState;
            state.Viewport.Size = new int2(640, 384);
            state.NavigationController.SelectTarget(new EditorViewportNavigationTarget(0, 0, 1));

            AssertCameraProjectionMatches(state.SceneCamera, state.GizmoCamera);
            AssertCameraProjectionMatches(state.SceneCamera, state.PickerCamera);
            Assert.Equal(state.SceneCameraEntity.Position, state.PickerCameraEntity.Position);
            Assert.Equal(state.SceneCameraEntity.Orientation, state.PickerCameraEntity.Orientation);
        }

        /// <summary>
        /// Ensures capturing during an active transition records the current displayed pose rather than the destination.
        /// </summary>
        [Fact]
        public void CaptureState_DuringNavigationTransition_StoresDisplayedCameraPose() {
            using EditorSessionWorkspaceTests.EditorSessionHarness harness = EditorSessionWorkspaceTests.EditorSessionHarness.Create();
            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            EditorWorkspacePanelInstance instance = Assert.Single(harness.Session.GetPanelInstancesForTest("viewport"));
            ViewportWorkspacePanelController controller = harness.GetViewportControllerForTest(instance);
            controller.ViewportState.Viewport.Size = new int2(640, 384);
            controller.ViewportState.NavigationController.SelectTarget(new EditorViewportNavigationTarget(1, 0, 0));
            controller.ViewportState.NavigationController.Advance(0.07);
            float3 displayedPosition = controller.ViewportState.SceneCameraEntity.Position;
            float4 displayedOrientation = controller.ViewportState.SceneCameraEntity.Orientation;
            float3 displayedPivot = controller.ViewportState.CameraController.GetOrbitTarget();

            ViewportWorkspacePanelStateDocument captured = Assert.IsType<ViewportWorkspacePanelStateDocument>(controller.CaptureState());

            Assert.True(controller.ViewportState.NavigationController.IsTransitioning);
            Assert.Equal(displayedPosition.X, captured.CameraPositionX);
            Assert.Equal(displayedPosition.Y, captured.CameraPositionY);
            Assert.Equal(displayedPosition.Z, captured.CameraPositionZ);
            Assert.Equal(displayedOrientation.X, captured.CameraOrientationX);
            Assert.Equal(displayedOrientation.Y, captured.CameraOrientationY);
            Assert.Equal(displayedOrientation.Z, captured.CameraOrientationZ);
            Assert.Equal(displayedOrientation.W, captured.CameraOrientationW);
            Assert.True(captured.HasOrbitPivot);
            Assert.Equal(displayedPivot.X, captured.OrbitPivotX);
            Assert.Equal(displayedPivot.Y, captured.OrbitPivotY);
            Assert.Equal(displayedPivot.Z, captured.OrbitPivotZ);
        }

        /// <summary>
        /// Ensures resize, minimize, and restore keep overlay hit regions aligned with the visible cube.
        /// </summary>
        [Fact]
        public void NavigationView_WhenViewportResizesOrMinimizes_UpdatesHitRegionVisibility() {
            using EditorSessionWorkspaceTests.EditorSessionHarness harness = EditorSessionWorkspaceTests.EditorSessionHarness.Create();
            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            EditorWorkspacePanelInstance instance = Assert.Single(harness.Session.GetPanelInstancesForTest("viewport"));
            EditorViewport viewport = (EditorViewport)instance.Dockable;
            viewport.Size = new int2(640, 384);
            EditorViewportNavigationCubeInteractionController interaction = viewport.NavigationCube.InteractionController;
            int2 visiblePosition = interaction.CubeScreenPosition;
            int2 visibleSize = interaction.CubeScreenSize;
            Assert.True(interaction.IsVisible);
            Assert.InRange(visiblePosition.X + visibleSize.X, 0, (int)viewport.Camera.Viewport.X + (int)viewport.Camera.Viewport.Z);
            Assert.True(visiblePosition.Y >= viewport.Camera.Viewport.Y);

            viewport.Size = new int2(80, 80);
            Assert.False(interaction.IsVisible);

            viewport.Size = new int2(640, 384);
            Assert.True(interaction.IsVisible);
        }

        /// <summary>
        /// Ensures live UI scaling and panel movement keep the rendered cube and projection button aligned with input bounds.
        /// </summary>
        [Fact]
        public void NavigationView_WhenUiScaleChangesAndViewportMoves_KeepsOverlayBoundsAlignedWithHitBounds() {
            using EditorSessionWorkspaceTests.EditorSessionHarness harness = EditorSessionWorkspaceTests.EditorSessionHarness.Create();
            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            EditorWorkspacePanelInstance instance = Assert.Single(harness.Session.GetPanelInstancesForTest("viewport"));
            EditorViewport viewport = (EditorViewport)instance.Dockable;
            viewport.Size = new int2(900, 700);
            viewport.Position = new float3(37f, 53f, 0f);
            int originalTitleBarHeight = viewport.TitleBarHeightPixels;

            harness.ApplyUiMetricsToViewportForTest(viewport, new EditorUiMetrics(2d));
            viewport.Position = new float3(61f, 79f, 0f);

            Assert.NotEqual(originalTitleBarHeight, viewport.TitleBarHeightPixels);
            EditorViewportNavigationCubeInteractionController interaction = viewport.NavigationCube.InteractionController;
            EditorEntity cubeRoot = Assert.Single(viewport.Children.OfType<EditorEntity>(), child => child.Name == "Navigation Cube Root");
            SpriteComponent cubeSprite = Assert.Single(cubeRoot.Components.OfType<SpriteComponent>());
            Assert.Equal(interaction.CubeScreenPosition, new int2((int)Math.Round(cubeRoot.Position.X), (int)Math.Round(cubeRoot.Position.Y)));
            Assert.Equal(interaction.CubeScreenSize, cubeSprite.Size);

            EditorEntity projectionRoot = Assert.Single(cubeRoot.Children.OfType<EditorEntity>(), child => child.Name == "Navigation Cube Projection Button");
            RoundedRectComponent projectionBackground = Assert.Single(projectionRoot.Components.OfType<RoundedRectComponent>());
            Assert.Equal(interaction.ProjectionControlScreenPosition,
                new int2((int)Math.Round(projectionRoot.Position.X), (int)Math.Round(projectionRoot.Position.Y)));
            Assert.Equal(interaction.ProjectionControlScreenSize, projectionBackground.Size);
        }

        /// <summary>
        /// Ensures scene, gizmo, and picker cameras synchronize clip ranges even when their old ranges are disjoint.
        /// </summary>
        /// <param name="nearPlane">Near clip plane copied from the scene camera.</param>
        /// <param name="farPlane">Far clip plane copied from the scene camera.</param>
        [Theory]
        [InlineData(150f, 2000f)]
        [InlineData(0.01f, 0.05f)]
        public void CameraSynchronization_WhenClipRangeMovesOutsidePreviousRange_MatchesAllCameraMatrices(float nearPlane, float farPlane) {
            using EditorSessionWorkspaceTests.EditorSessionHarness harness = EditorSessionWorkspaceTests.EditorSessionHarness.Create();
            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            EditorWorkspacePanelInstance instance = Assert.Single(harness.Session.GetPanelInstancesForTest("viewport"));
            EditorViewportWorkspaceState state = harness.GetViewportControllerForTest(instance).ViewportState;
            state.SceneCamera.FarPlaneDistance = farPlane;
            state.SceneCamera.NearPlaneDistance = nearPlane;

            EditorViewportCameraProjectionSynchronizer.Synchronize(state.SceneCamera, state.GizmoCamera);
            EditorViewportCameraProjectionSynchronizer.Synchronize(state.SceneCamera, state.PickerCamera);

            AssertCameraProjectionMatches(state.SceneCamera, state.GizmoCamera);
            AssertCameraProjectionMatches(state.SceneCamera, state.PickerCamera);
        }

        /// <summary>
        /// Ensures closing a viewport releases an active cube gesture and its viewport-wide input capture.
        /// </summary>
        [Fact]
        public void ViewportClose_DuringCubePointerCapture_ReleasesCaptureAndBlockers() {
            using EditorSessionWorkspaceTests.EditorSessionHarness harness = EditorSessionWorkspaceTests.EditorSessionHarness.Create();
            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            EditorWorkspacePanelInstance instance = Assert.Single(harness.Session.GetPanelInstancesForTest("viewport"));
            ViewportWorkspacePanelController controller = harness.GetViewportControllerForTest(instance);
            EditorViewport viewport = controller.ViewportState.Viewport;
            viewport.Size = new int2(640, 384);
            EditorViewportNavigationCubeInteractionController interaction = viewport.NavigationCube.InteractionController;
            int2 pointer = new int2(interaction.CubeScreenPosition.X + (interaction.CubeScreenSize.X / 2),
                interaction.CubeScreenPosition.Y + (interaction.CubeScreenSize.Y / 2));
            interaction.UpdatePointer(pointer, true, true, true, controller.ViewportState.SceneCameraEntity.Orientation, 0.0);
            Assert.True(interaction.IsPointerCaptured);
            Assert.True(harness.Interactions.InputCapture.IsPointerBlocked(pointer));

            viewport.ActivatePanelMenuActionForTest(DockableEntityPanelMenuAction.Close);

            Assert.False(interaction.IsPointerCaptured);
            Assert.False(harness.Interactions.InputCapture.IsPointerBlocked(pointer));
        }

        /// <summary>
        /// Ensures all cameras produce the same validated matrix when evaluated at their shared aspect ratio.
        /// </summary>
        /// <param name="source">Camera whose projection settings are authoritative.</param>
        /// <param name="destination">Camera expected to mirror the source settings.</param>
        static void AssertCameraProjectionMatches(CameraComponent source, CameraComponent destination) {
            Assert.Equal(source.FieldOfView, destination.FieldOfView);
            Assert.Equal(source.NearPlaneDistance, destination.NearPlaneDistance);
            Assert.Equal(source.FarPlaneDistance, destination.FarPlaneDistance);
            ICameraProjectionSettings sourceSettings = Assert.IsAssignableFrom<ICameraProjectionSettings>(source);
            ICameraProjectionSettings destinationSettings = Assert.IsAssignableFrom<ICameraProjectionSettings>(destination);
            Assert.Equal(sourceSettings.ProjectionMode, destinationSettings.ProjectionMode);
            Assert.Equal(sourceSettings.OrthographicVerticalSpan, destinationSettings.OrthographicVerticalSpan);
            Assert.Equal(CameraProjectionUtils.CreateProjection(source, 16f / 9f),
                CameraProjectionUtils.CreateProjection(destination, 16f / 9f));
        }
    }
}
