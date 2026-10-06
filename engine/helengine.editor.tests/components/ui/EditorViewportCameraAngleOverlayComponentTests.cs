using System.Reflection;
using helengine;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.components.ui {
    /// <summary>
    /// Verifies world-space transform-gizmo axis label behavior in the viewport camera-angle overlay.
    /// </summary>
    public class EditorViewportCameraAngleOverlayComponentTests : IDisposable {
        Core CoreValue;
        TestGeneratedAssetGraph GeneratedAssetGraph;
        /// <summary>
        /// Tolerance used for floating-point direction comparisons.
        /// </summary>
        const float FloatTolerance = 0.001f;

        /// <summary>
        /// Ensures the overlay preserves distinct X/Y/Z axis-label directions after yaw-facing rotation is applied.
        /// </summary>
        [Fact]
        public void ResolveAxisDirection_WhenYawFacingIsApplied_PreservesExpectedSignedAxes() {
            InitializeCore();
            CameraComponent sceneCamera = new CameraComponent();
            FontAsset font = CreateTestFont();
            var overlayComponent = new EditorViewportCameraAngleOverlayComponent(sceneCamera, font, 0, false, GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.RendererResources);

            float3 selectedPosition = float3.Zero;
            float3 cameraPosition = new float3(8f, 2f, 0f);
            float4 yawFacingOrientation = TransformGizmoYawSnapper.ComputeSnappedYawFacingOrientation(selectedPosition, cameraPosition);

            float3 xDirection = InvokeResolveAxisDirection(overlayComponent, 0, yawFacingOrientation);
            float3 yDirection = InvokeResolveAxisDirection(overlayComponent, 1, yawFacingOrientation);
            float3 zDirection = InvokeResolveAxisDirection(overlayComponent, 2, yawFacingOrientation);

            float3 expectedXDirection = float4.RotateVector(new float3(1f, 0f, 0f), yawFacingOrientation);
            float3 expectedYDirection = float4.RotateVector(new float3(0f, 1f, 0f), yawFacingOrientation);
            float3 expectedZDirection = float4.RotateVector(new float3(0f, 0f, 1f), yawFacingOrientation);

            AssertVectorEquals(expectedXDirection, xDirection);
            AssertVectorEquals(expectedYDirection, yDirection);
            AssertVectorEquals(expectedZDirection, zDirection);

            string xLabel = InvokeBuildAxisLabel(overlayComponent, xDirection);
            string yLabel = InvokeBuildAxisLabel(overlayComponent, yDirection);
            string zLabel = InvokeBuildAxisLabel(overlayComponent, zDirection);

            Assert.Equal(3, new HashSet<string>(StringComparer.Ordinal) { xLabel, yLabel, zLabel }.Count);
        }

        /// <summary>
        /// Ensures orthographic gizmo and label scaling stays constant on screen at different camera distances.
        /// </summary>
        [Fact]
        public void ComputeGizmoScale_WhenOrthographicCameraDistanceChanges_PreservesScreenSize() {
            InitializeCore();
            EditorViewportCameraComponent sceneCamera = new EditorViewportCameraComponent {
                Viewport = new float4(0f, 0f, 1280f, 720f),
                ProjectionMode = CameraProjectionMode.Orthographic,
                OrthographicVerticalSpan = 20f
            };
            FontAsset font = CreateTestFont();
            EditorViewportCameraAngleOverlayComponent overlay = new EditorViewportCameraAngleOverlayComponent(
                sceneCamera,
                font,
                0,
                false,
                GeneratedAssetGraph.ShaderLibrary,
                GeneratedAssetGraph.RendererResources);
            float3 gizmoOrigin = float3.Zero;

            double nearUnitsPerPixel = InvokeComputeWorldUnitsPerPixel(overlay, gizmoOrigin, new float3(0f, 0f, 10f));
            double farUnitsPerPixel = InvokeComputeWorldUnitsPerPixel(overlay, gizmoOrigin, new float3(0f, 0f, 100f));
            double nearGizmoScale = InvokeComputeGizmoScale(overlay, gizmoOrigin, new float3(0f, 0f, 10f));
            double farGizmoScale = InvokeComputeGizmoScale(overlay, gizmoOrigin, new float3(0f, 0f, 100f));

            Assert.Equal(20.0 / 720.0, nearUnitsPerPixel, 6);
            Assert.Equal(nearUnitsPerPixel, farUnitsPerPixel, 8);
            Assert.Equal(nearGizmoScale, farGizmoScale, 8);
        }

        /// <summary>Checks that finite coordinates remain projectable when float subtraction or squared distance would overflow.</summary>
        /// <param name="mode">Projection used for the gizmo and labels.</param>
        /// <param name="coordinate">Magnitude of the camera and selected positions on opposite sides of the origin.</param>
        [Theory]
        [InlineData(CameraProjectionMode.Perspective, 1e20f)]
        [InlineData(CameraProjectionMode.Perspective, 3e38f)]
        [InlineData(CameraProjectionMode.Orthographic, 1e20f)]
        [InlineData(CameraProjectionMode.Orthographic, 3e38f)]
        public void OverlayScale_WithLargeFiniteCoordinates_DoesNotOverflow(CameraProjectionMode mode, float coordinate) {
            InitializeCore();
            EditorViewportCameraComponent camera = new EditorViewportCameraComponent {
                Viewport = new float4(0f, 0f, 1280f, 720f),
                ProjectionMode = mode,
                OrthographicVerticalSpan = 20f
            };
            EditorViewportCameraAngleOverlayComponent overlay = new EditorViewportCameraAngleOverlayComponent(
                camera, CreateTestFont(), 0, false, GeneratedAssetGraph.ShaderLibrary, GeneratedAssetGraph.RendererResources);
            float3 origin = new float3(coordinate, coordinate, coordinate);
            float3 cameraPosition = new float3(-coordinate, -coordinate, -coordinate);
            double distance = 2.0 * coordinate * Math.Sqrt(3.0);
            double expectedUnits = CameraProjectionUtils.GetWorldUnitsPerPixel(camera, distance, 720.0);

            double units = InvokeComputeWorldUnitsPerPixel(overlay, origin, cameraPosition);
            double scale = InvokeComputeGizmoScale(overlay, origin, cameraPosition);
            Assert.True(double.IsFinite(units));
            Assert.True(double.IsFinite(scale));
            Assert.InRange(Math.Abs(units / expectedUnits - 1.0), 0.0, 1e-12);
        }

        /// <summary>
        /// Initializes a fresh core so camera components can be constructed in isolation tests.
        /// </summary>
        void InitializeCore() {
            CoreValue = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            CoreValue.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"), new CoreInitializationOptions {
                ContentStreamSource = new FakeContentStreamSource()
            });
            GeneratedAssetGraph = new TestGeneratedAssetGraph(CoreValue);
        }

        public void Dispose() {
            GeneratedAssetGraph?.Dispose();
            CoreValue?.Dispose();
        }

        /// <summary>
        /// Creates a minimal font atlas containing the glyphs needed by transform-gizmo axis labels.
        /// </summary>
        /// <returns>Font asset suitable for overlay component construction.</returns>
        static FontAsset CreateTestFont() {
            return new FontAsset(
                new FontInfo("TestAxisLabelFont", 16, 4f),
                new TestRuntimeTexture {
                    Width = 16,
                    Height = 16
                },
                new Dictionary<char, FontChar> {
                    ['x'] = new FontChar(new float4(0f, 0f, 0.1f, 0.1f), 0f, 10f, 0f, 0f),
                    ['y'] = new FontChar(new float4(0f, 0f, 0.1f, 0.1f), 0f, 10f, 0f, 0f),
                    ['z'] = new FontChar(new float4(0f, 0f, 0.1f, 0.1f), 0f, 10f, 0f, 0f),
                    ['+'] = new FontChar(new float4(0f, 0f, 0.1f, 0.1f), 0f, 10f, 0f, 0f),
                    ['-'] = new FontChar(new float4(0f, 0f, 0.1f, 0.1f), 0f, 10f, 0f, 0f)
                },
                16f,
                16,
                16);
        }

        /// <summary>
        /// Invokes the overlay component's private axis-direction resolver.
        /// </summary>
        /// <param name="overlayComponent">Overlay component to inspect.</param>
        /// <param name="axisIndex">Zero-based axis slot index.</param>
        /// <param name="yawFacingOrientation">Yaw-facing rotation to apply.</param>
        /// <returns>Resolved normalized world-space axis direction.</returns>
        static float3 InvokeResolveAxisDirection(EditorViewportCameraAngleOverlayComponent overlayComponent, int axisIndex, float4 yawFacingOrientation) {
            if (overlayComponent == null) {
                throw new ArgumentNullException(nameof(overlayComponent));
            }

            MethodInfo method = typeof(EditorViewportCameraAngleOverlayComponent).GetMethod(
                "ResolveAxisDirection",
                BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Expected ResolveAxisDirection method.");

            object result = method.Invoke(overlayComponent, new object[] { axisIndex, yawFacingOrientation }) ??
                            throw new InvalidOperationException("ResolveAxisDirection returned null.");
            return (float3)result;
        }

        /// <summary>
        /// Invokes the overlay component's private signed-axis label builder.
        /// </summary>
        /// <param name="overlayComponent">Overlay component to inspect.</param>
        /// <param name="axisDirection">World-space axis direction to translate into label text.</param>
        /// <returns>Signed axis label such as x+, y-, or z+.</returns>
        static string InvokeBuildAxisLabel(EditorViewportCameraAngleOverlayComponent overlayComponent, float3 axisDirection) {
            if (overlayComponent == null) {
                throw new ArgumentNullException(nameof(overlayComponent));
            }

            MethodInfo method = typeof(EditorViewportCameraAngleOverlayComponent).GetMethod(
                "BuildAxisLabel",
                BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Expected BuildAxisLabel method.");

            object result = method.Invoke(overlayComponent, new object[] { axisDirection }) ??
                            throw new InvalidOperationException("BuildAxisLabel returned null.");
            return (string)result;
        }

        /// <summary>
        /// Invokes the overlay's projection-aware world-units-per-pixel calculation.
        /// </summary>
        /// <param name="overlayComponent">Overlay whose camera scale is measured.</param>
        /// <param name="origin">World-space position of the label or gizmo.</param>
        /// <param name="cameraPosition">World-space camera position.</param>
        /// <returns>World-space size of one vertical viewport pixel.</returns>
        static double InvokeComputeWorldUnitsPerPixel(EditorViewportCameraAngleOverlayComponent overlayComponent, float3 origin, float3 cameraPosition) {
            MethodInfo method = typeof(EditorViewportCameraAngleOverlayComponent).GetMethod(
                "ComputeWorldUnitsPerPixel",
                BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Expected ComputeWorldUnitsPerPixel method.");
            return (double)method.Invoke(overlayComponent, new object[] { origin, cameraPosition });
        }

        /// <summary>
        /// Invokes the overlay's current translation-gizmo sizing calculation.
        /// </summary>
        /// <param name="overlayComponent">Overlay whose gizmo scale is measured.</param>
        /// <param name="origin">World-space gizmo origin.</param>
        /// <param name="cameraPosition">World-space camera position.</param>
        /// <returns>World scale required to preserve the intended screen size.</returns>
        static double InvokeComputeGizmoScale(EditorViewportCameraAngleOverlayComponent overlayComponent, float3 origin, float3 cameraPosition) {
            MethodInfo method = typeof(EditorViewportCameraAngleOverlayComponent).GetMethod(
                "ComputeGizmoScale",
                BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Expected ComputeGizmoScale method.");
            return (double)method.Invoke(overlayComponent, new object[] { origin, cameraPosition });
        }

        /// <summary>
        /// Asserts that two vectors match within the standard floating-point tolerance for axis-label tests.
        /// </summary>
        /// <param name="expected">Expected vector value.</param>
        /// <param name="actual">Actual vector value.</param>
        static void AssertVectorEquals(float3 expected, float3 actual) {
            Assert.InRange(Math.Abs(expected.X - actual.X), 0f, FloatTolerance);
            Assert.InRange(Math.Abs(expected.Y - actual.Y), 0f, FloatTolerance);
            Assert.InRange(Math.Abs(expected.Z - actual.Z), 0f, FloatTolerance);
        }
    }
}
