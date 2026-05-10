using helengine;
using helengine.editor;
using Xunit;

namespace helengine.editor.tests.managers.gizmo {
    /// <summary>
    /// Verifies orientation solving for translation snap-preview grids.
    /// </summary>
    public class TransformTranslationSnapPreviewResolverTests {
        /// <summary>
        /// Tolerance used for normalized vector comparisons.
        /// </summary>
        const float FloatTolerance = 0.001f;

        /// <summary>
        /// Ensures plane-handle previews inherit the hovered handle's plane basis.
        /// </summary>
        [Fact]
        public void TryResolvePreviewOrientation_WhenPlaneHandleIsHovered_UsesHandlePlaneBasis() {
            InitializeCore();
            Entity planeHandle = CreatePlaneHandle(CreateXzPlaneOrientation());

            bool resolved = TransformTranslationSnapPreviewResolver.TryResolvePreviewOrientation(
                planeHandle,
                float3.Zero,
                new float3(0f, 3f, -8f),
                out float4 previewOrientation);

            Assert.True(resolved);

            float3 resolvedPrimary = Normalize(float4.RotateVector(new float3(1f, 0f, 0f), previewOrientation));
            float3 resolvedSecondary = Normalize(float4.RotateVector(new float3(0f, 1f, 0f), previewOrientation));
            AssertVectorClose(new float3(1f, 0f, 0f), resolvedPrimary);
            AssertVectorClose(new float3(0f, 0f, 1f), resolvedSecondary);
        }

        /// <summary>
        /// Ensures Y-axis previews choose the YZ companion plane when that plane faces the camera more directly.
        /// </summary>
        [Fact]
        public void TryResolvePreviewOrientation_WhenYAxisPreviewFacesCameraMoreThroughX_UsesYzPlane() {
            InitializeCore();
            Entity axisHandle = CreateAxisHandle(float4.Identity);
            float3 cameraPosition = new float3(8f, 4f, -1f);

            bool resolved = TransformTranslationSnapPreviewResolver.TryResolvePreviewOrientation(
                axisHandle,
                float3.Zero,
                cameraPosition,
                out float4 previewOrientation);

            Assert.True(resolved);

            float3 resolvedPrimary = Normalize(float4.RotateVector(new float3(1f, 0f, 0f), previewOrientation));
            float3 resolvedSecondary = Normalize(float4.RotateVector(new float3(0f, 1f, 0f), previewOrientation));
            AssertVectorClose(new float3(0f, 1f, 0f), resolvedPrimary);
            AssertVectorClose(new float3(0f, 0f, 1f), resolvedSecondary);
        }

        /// <summary>
        /// Ensures Y-axis previews choose the XY companion plane when that plane faces the camera more directly.
        /// </summary>
        [Fact]
        public void TryResolvePreviewOrientation_WhenYAxisPreviewFacesCameraMoreThroughZ_UsesXyPlane() {
            InitializeCore();
            Entity axisHandle = CreateAxisHandle(float4.Identity);
            float3 cameraPosition = new float3(1f, 4f, -8f);

            bool resolved = TransformTranslationSnapPreviewResolver.TryResolvePreviewOrientation(
                axisHandle,
                float3.Zero,
                cameraPosition,
                out float4 previewOrientation);

            Assert.True(resolved);

            float3 resolvedPrimary = Normalize(float4.RotateVector(new float3(1f, 0f, 0f), previewOrientation));
            float3 resolvedSecondary = Normalize(float4.RotateVector(new float3(0f, 1f, 0f), previewOrientation));
            AssertVectorClose(new float3(0f, 1f, 0f), resolvedPrimary);
            AssertVectorClose(new float3(1f, 0f, 0f), resolvedSecondary);
        }

        /// <summary>
        /// Ensures axis-handle previews still resolve when the camera direction is parallel to the active axis.
        /// </summary>
        [Fact]
        public void TryResolvePreviewOrientation_WhenCameraIsParallelToAxis_UsesStableFallbackPlane() {
            InitializeCore();
            Entity axisHandle = CreateAxisHandle(float4.Identity);

            bool resolved = TransformTranslationSnapPreviewResolver.TryResolvePreviewOrientation(
                axisHandle,
                float3.Zero,
                new float3(0f, 8f, 0f),
                out float4 previewOrientation);

            Assert.True(resolved);

            float3 resolvedPrimary = Normalize(float4.RotateVector(new float3(1f, 0f, 0f), previewOrientation));
            float3 resolvedSecondary = Normalize(float4.RotateVector(new float3(0f, 1f, 0f), previewOrientation));
            AssertVectorClose(new float3(0f, 1f, 0f), resolvedPrimary);
            Assert.InRange(Math.Abs(float3.Dot(resolvedPrimary, resolvedSecondary)), 0f, FloatTolerance);
        }

        /// <summary>
        /// Initializes a minimal core so test entities can be constructed safely.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(null, null, null);
        }

        /// <summary>
        /// Creates an axis handle with the supplied orientation.
        /// </summary>
        /// <param name="orientation">World-space handle orientation.</param>
        /// <returns>Configured axis handle entity.</returns>
        Entity CreateAxisHandle(float4 orientation) {
            var handleEntity = new EditorEntity {
                Orientation = orientation
            };
            handleEntity.AddComponent(new TransformGizmoHandleComponent(new float3(0f, 1f, 0f)));
            return handleEntity;
        }

        /// <summary>
        /// Creates a plane handle with the supplied orientation.
        /// </summary>
        /// <param name="orientation">World-space handle orientation.</param>
        /// <returns>Configured plane handle entity.</returns>
        Entity CreatePlaneHandle(float4 orientation) {
            var handleEntity = new EditorEntity {
                Orientation = orientation
            };
            handleEntity.AddComponent(new TransformGizmoHandleComponent(new float3(1f, 0f, 0f), new float3(0f, 1f, 0f)));
            return handleEntity;
        }

        /// <summary>
        /// Asserts that two normalized vectors are equal within the configured tolerance.
        /// </summary>
        /// <param name="expected">Expected normalized vector.</param>
        /// <param name="actual">Actual normalized vector.</param>
        void AssertVectorClose(float3 expected, float3 actual) {
            Assert.InRange(Math.Abs(expected.X - actual.X), 0f, FloatTolerance);
            Assert.InRange(Math.Abs(expected.Y - actual.Y), 0f, FloatTolerance);
            Assert.InRange(Math.Abs(expected.Z - actual.Z), 0f, FloatTolerance);
        }

        /// <summary>
        /// Normalizes a vector for test comparisons.
        /// </summary>
        /// <param name="value">Vector to normalize.</param>
        /// <returns>Normalized vector.</returns>
        float3 Normalize(float3 value) {
            double length = Math.Sqrt((value.X * value.X) + (value.Y * value.Y) + (value.Z * value.Z));
            if (length <= 0.0) {
                throw new InvalidOperationException("Cannot normalize a zero-length test vector.");
            }

            double inverseLength = 1.0 / length;
            return new float3(
                (float)(value.X * inverseLength),
                (float)(value.Y * inverseLength),
                (float)(value.Z * inverseLength));
        }

        /// <summary>
        /// Creates the quaternion that maps +Y geometry into +X direction.
        /// </summary>
        /// <returns>Quaternion rotating +Y to +X.</returns>
        float4 CreateXAxisOrientation() {
            float3 zAxis = new float3(0f, 0f, 1f);
            float4 orientation;
            float4.CreateFromAxisAngle(ref zAxis, (float)(-Math.PI * 0.5), out orientation);
            return orientation;
        }

        /// <summary>
        /// Creates the plane rotation that maps local XY plane to world XZ plane.
        /// </summary>
        /// <returns>Quaternion rotating local +Y to world +Z.</returns>
        float4 CreateXzPlaneOrientation() {
            float3 xAxis = new float3(1f, 0f, 0f);
            float4 orientation;
            float4.CreateFromAxisAngle(ref xAxis, (float)(Math.PI * 0.5), out orientation);
            return orientation;
        }
    }
}
