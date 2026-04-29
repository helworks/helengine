using helengine;
using helengine.editor;
using Xunit;

namespace helengine.editor.tests.managers.gizmo {
    /// <summary>
    /// Verifies rotation snap-preview orientation resolution for gizmo rings.
    /// </summary>
    public class TransformRotationSnapPreviewResolverTests {
        /// <summary>
        /// Tolerance used for axis-direction comparisons.
        /// </summary>
        const float FloatTolerance = 0.001f;

        /// <summary>
        /// Ensures the preview orientation for the Y ring produces a disc normal aligned to the world Y axis.
        /// </summary>
        [Fact]
        public void TryResolvePreviewOrientation_ForYRing_AlignsPreviewNormalToYAxis() {
            InitializeCore();
            EditorEntity handleEntity = CreateRingEntity(float4.Identity);

            bool resolved = TransformRotationSnapPreviewResolver.TryResolvePreviewOrientation(handleEntity, out float4 previewOrientation);

            Assert.True(resolved);
            AssertVectorClose(new float3(0f, 1f, 0f), float4.RotateVector(new float3(0f, 0f, 1f), previewOrientation));
        }

        /// <summary>
        /// Ensures the preview orientation for the X ring produces a disc normal aligned to the world X axis.
        /// </summary>
        [Fact]
        public void TryResolvePreviewOrientation_ForXRing_AlignsPreviewNormalToXAxis() {
            InitializeCore();
            EditorEntity handleEntity = CreateRingEntity(CreateXAxisOrientation());

            bool resolved = TransformRotationSnapPreviewResolver.TryResolvePreviewOrientation(handleEntity, out float4 previewOrientation);

            Assert.True(resolved);
            AssertVectorClose(new float3(1f, 0f, 0f), float4.RotateVector(new float3(0f, 0f, 1f), previewOrientation));
        }

        /// <summary>
        /// Ensures the resolver rejects entities that do not expose a transform-gizmo handle component.
        /// </summary>
        [Fact]
        public void TryResolvePreviewOrientation_WithoutHandleComponent_ReturnsFalse() {
            InitializeCore();
            bool resolved = TransformRotationSnapPreviewResolver.TryResolvePreviewOrientation(new EditorEntity(), out float4 previewOrientation);

            Assert.False(resolved);
            Assert.Equal(float4.Identity, previewOrientation);
        }

        /// <summary>
        /// Initializes a fresh core with an object manager for entity-based tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(null, null, null);
        }

        /// <summary>
        /// Creates a rotation-ring entity for preview-orientation tests.
        /// </summary>
        /// <param name="orientation">Ring orientation relative to the gizmo root.</param>
        /// <returns>Configured ring entity.</returns>
        EditorEntity CreateRingEntity(float4 orientation) {
            EditorEntity ringEntity = new EditorEntity();
            ringEntity.Orientation = orientation;
            ringEntity.AddComponent(new TransformGizmoHandleComponent(new float3(0f, 1f, 0f)));
            return ringEntity;
        }

        /// <summary>
        /// Creates the quaternion that maps a local Y-normal ring into an X-normal ring.
        /// </summary>
        /// <returns>Quaternion rotating +Y to +X.</returns>
        float4 CreateXAxisOrientation() {
            float3 zAxis = new float3(0f, 0f, 1f);
            float4 orientation;
            float4.CreateFromAxisAngle(ref zAxis, (float)(-Math.PI * 0.5), out orientation);
            return orientation;
        }

        /// <summary>
        /// Asserts that two direction vectors are equal within a small tolerance.
        /// </summary>
        /// <param name="expected">Expected direction.</param>
        /// <param name="actual">Actual direction.</param>
        void AssertVectorClose(float3 expected, float3 actual) {
            Assert.InRange(Math.Abs(expected.X - actual.X), 0f, FloatTolerance);
            Assert.InRange(Math.Abs(expected.Y - actual.Y), 0f, FloatTolerance);
            Assert.InRange(Math.Abs(expected.Z - actual.Z), 0f, FloatTolerance);
        }
    }
}
