using System.Reflection;
using helengine;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.components.ui {
    /// <summary>
    /// Verifies world-space transform-gizmo axis label behavior in the viewport camera-angle overlay.
    /// </summary>
    public class EditorViewportCameraAngleOverlayComponentTests {
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
            var overlayComponent = new EditorViewportCameraAngleOverlayComponent(sceneCamera, font, 0, false);

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
        /// Initializes a fresh core so camera components can be constructed in isolation tests.
        /// </summary>
        static void InitializeCore() {
            Core core = new Core();
            core.Initialize(null, null, new TestInputBackend(), new PlatformInfo("test", "test-version"));
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
