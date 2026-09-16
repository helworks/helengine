using helengine;
using Xunit;

namespace helengine.core.tests {
    /// <summary>
    /// Verifies the camera owns its vertical field of view.
    /// </summary>
    /// <remarks>
    /// Every platform renderer used to invent this value, so one authored scene framed
    /// differently on PlayStation 1, Dreamcast and Xbox 360 for no reason an author could
    /// see or change. The default is the 45 degrees all three happened to choose, so
    /// existing scenes frame exactly as they did.
    /// </remarks>
    public sealed class CameraFieldOfViewTests {
        /// <summary>
        /// Ensures a new camera starts at the shared default rather than zero.
        /// </summary>
        [Fact]
        public void FieldOfView_OnANewCamera_IsTheSharedDefault() {
            CameraComponent camera = new CameraComponent();

            Assert.Equal(CameraProjectionUtils.DefaultFieldOfView, camera.FieldOfView);
        }

        /// <summary>
        /// Ensures an authored value survives unchanged when it is already legal.
        /// </summary>
        [Fact]
        public void FieldOfView_WhenSetInRange_KeepsTheValue() {
            CameraComponent camera = new CameraComponent();

            camera.FieldOfView = 1.2f;

            Assert.Equal(1.2f, camera.FieldOfView);
        }

        /// <summary>
        /// Ensures values the projection builder would reject are clamped instead of throwing
        /// deep inside a renderer on some other platform.
        /// </summary>
        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(3.2f)]
        [InlineData(100f)]
        public void FieldOfView_WhenSetOutOfRange_IsClampedToALegalValue(float requested) {
            CameraComponent camera = new CameraComponent();

            camera.FieldOfView = requested;

            Assert.InRange(camera.FieldOfView, CameraProjectionUtils.MinimumFieldOfView,
                CameraProjectionUtils.MaximumFieldOfView);
        }

        /// <summary>
        /// Ensures the projection built for a camera uses the camera's own field of view.
        /// </summary>
        [Fact]
        public void CreatePerspectiveProjection_UsesTheCameraFieldOfView() {
            CameraComponent camera = new CameraComponent();
            camera.FieldOfView = 1.0f;

            float4x4 fromCamera = CameraProjectionUtils.CreatePerspectiveProjection(camera, 16f / 9f);
            float4x4 fromExplicitValue = CameraProjectionUtils.CreatePerspectiveProjection(camera, 1.0f, 16f / 9f);

            Assert.Equal(fromExplicitValue.M11, fromCamera.M11, 5);
            Assert.Equal(fromExplicitValue.M22, fromCamera.M22, 5);
        }

        /// <summary>
        /// Ensures a different field of view actually produces a different projection, so the
        /// value is read rather than quietly ignored.
        /// </summary>
        [Fact]
        public void CreatePerspectiveProjection_WhenFieldOfViewChanges_ChangesTheProjection() {
            CameraComponent narrow = new CameraComponent();
            narrow.FieldOfView = 0.6f;
            CameraComponent wide = new CameraComponent();
            wide.FieldOfView = 1.4f;

            float4x4 narrowProjection = CameraProjectionUtils.CreatePerspectiveProjection(narrow, 1f);
            float4x4 wideProjection = CameraProjectionUtils.CreatePerspectiveProjection(wide, 1f);

            // A wider field of view scales the image down, so its vertical scale is smaller.
            Assert.True(wideProjection.M22 < narrowProjection.M22);
        }
    }
}
