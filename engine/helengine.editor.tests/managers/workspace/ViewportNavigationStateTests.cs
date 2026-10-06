using System.Text.Json;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.workspace {
    /// <summary>
    /// Verifies per-viewport navigation state capture and migration from older workspace documents.
    /// </summary>
    public sealed class ViewportNavigationStateTests {
        /// <summary>
        /// Ensures capture serializes the active projection, orthographic span, and explicit orbit pivot.
        /// </summary>
        [Fact]
        public void CaptureState_WhenProjectionAndPivotAreSet_PersistsNavigationFields() {
            using EditorSessionWorkspaceTests.EditorSessionHarness harness = EditorSessionWorkspaceTests.EditorSessionHarness.Create();
            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            EditorWorkspacePanelInstance instance = Assert.Single(harness.Session.GetPanelInstancesForTest("viewport"));
            ViewportWorkspacePanelController controller = harness.GetViewportControllerForTest(instance);
            EditorViewportWorkspaceState viewportState = controller.ViewportState;
            viewportState.Viewport.Size = new int2(640, 384);
            float3 pivot = new float3(12.5f, -3.25f, 8.75f);
            viewportState.CameraController.SetViewPose(pivot, float4.Identity, 14.0);
            viewportState.CameraController.SetProjectionMode(CameraProjectionMode.Orthographic);

            object capturedState = controller.CaptureState();
            JsonElement serialized = JsonSerializer.SerializeToElement(capturedState, new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Assert.Equal((int)CameraProjectionMode.Orthographic, serialized.GetProperty("projectionMode").GetInt32());
            Assert.True(serialized.GetProperty("orthographicVerticalSpan").GetSingle() > 0f);
            Assert.True(serialized.GetProperty("hasOrbitPivot").GetBoolean());
            Assert.Equal(pivot.X, serialized.GetProperty("orbitPivotX").GetSingle());
            Assert.Equal(pivot.Y, serialized.GetProperty("orbitPivotY").GetSingle());
            Assert.Equal(pivot.Z, serialized.GetProperty("orbitPivotZ").GetSingle());
        }

        /// <summary>
        /// Ensures a legacy workspace document defaults to perspective and reconstructs a pivot and span from its camera pose.
        /// </summary>
        [Fact]
        public void RestoreState_FromLegacyDocument_UsesPerspectiveAndDerivesOrbitState() {
            using EditorSessionWorkspaceTests.EditorSessionHarness harness = EditorSessionWorkspaceTests.EditorSessionHarness.Create();
            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            EditorWorkspacePanelInstance instance = Assert.Single(harness.Session.GetPanelInstancesForTest("viewport"));
            ViewportWorkspacePanelController controller = harness.GetViewportControllerForTest(instance);
            controller.ViewportState.Viewport.Size = new int2(640, 384);
            using JsonDocument legacyState = JsonDocument.Parse("""
                {
                  "cameraPositionX": 0.0,
                  "cameraPositionY": 0.0,
                  "cameraPositionZ": 10.0,
                  "cameraOrientationX": 0.0,
                  "cameraOrientationY": 0.0,
                  "cameraOrientationZ": 0.0,
                  "cameraOrientationW": 1.0,
                  "toolMode": 0,
                  "nearPlaneDistance": 1.0,
                  "farPlaneDistance": 5000.0
                }
                """);

            controller.RestoreState(legacyState.RootElement);

            ICameraProjectionSettings projection = Assert.IsAssignableFrom<ICameraProjectionSettings>(controller.ViewportState.SceneCamera);
            Assert.Equal(CameraProjectionMode.Perspective, projection.ProjectionMode);
            Assert.Equal(new float3(0f, 0f, 0f), controller.ViewportState.CameraController.GetOrbitTarget());
            Assert.InRange(projection.OrthographicVerticalSpan, 8.284f, 8.285f);
        }

        /// <summary>
        /// Ensures a current orthographic workspace document restores its pivot and vertical span exactly.
        /// </summary>
        [Fact]
        public void RestoreState_FromOrthographicDocument_RestoresProjectionSpanAndZeroPivot() {
            using EditorSessionWorkspaceTests.EditorSessionHarness harness = EditorSessionWorkspaceTests.EditorSessionHarness.Create();
            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            EditorWorkspacePanelInstance instance = Assert.Single(harness.Session.GetPanelInstancesForTest("viewport"));
            ViewportWorkspacePanelController controller = harness.GetViewportControllerForTest(instance);
            controller.ViewportState.Viewport.Size = new int2(640, 384);
            using JsonDocument state = JsonDocument.Parse("""
                {
                  "cameraPositionX": 0.0,
                  "cameraPositionY": 0.0,
                  "cameraPositionZ": 10.0,
                  "cameraOrientationX": 0.0,
                  "cameraOrientationY": 0.0,
                  "cameraOrientationZ": 0.0,
                  "cameraOrientationW": 1.0,
                  "toolMode": 0,
                  "nearPlaneDistance": 1.0,
                  "farPlaneDistance": 5000.0,
                  "projectionMode": 1,
                  "orthographicVerticalSpan": 20.0,
                  "hasOrbitPivot": true,
                  "orbitPivotX": 0.0,
                  "orbitPivotY": 0.0,
                  "orbitPivotZ": 0.0
                }
                """);

            controller.RestoreState(state.RootElement);

            ICameraProjectionSettings projection = Assert.IsAssignableFrom<ICameraProjectionSettings>(controller.ViewportState.SceneCamera);
            Assert.Equal(CameraProjectionMode.Orthographic, projection.ProjectionMode);
            Assert.Equal(20f, projection.OrthographicVerticalSpan);
            Assert.Equal(new float3(0f, 0f, 0f), controller.ViewportState.CameraController.GetOrbitTarget());
            Assert.Equal(new float3(0f, 0f, 10f), controller.ViewportState.SceneCameraEntity.Position);
        }

        /// <summary>
        /// Ensures non-finite pivot and span values are rejected instead of being persisted as a broken viewport state.
        /// </summary>
        [Fact]
        public void RestoreState_WithInvalidNavigationNumbers_RejectsDocument() {
            using EditorSessionWorkspaceTests.EditorSessionHarness harness = EditorSessionWorkspaceTests.EditorSessionHarness.Create();
            harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
            EditorWorkspacePanelInstance instance = Assert.Single(harness.Session.GetPanelInstancesForTest("viewport"));
            ViewportWorkspacePanelController controller = harness.GetViewportControllerForTest(instance);
            ViewportWorkspacePanelStateDocument invalidState = new ViewportWorkspacePanelStateDocument {
                CameraOrientationW = 1f,
                ProjectionMode = CameraProjectionMode.Orthographic,
                OrthographicVerticalSpan = float.NaN,
                HasOrbitPivot = true,
                OrbitPivotX = float.PositiveInfinity
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => controller.RestoreState(invalidState));
        }
    }
}
