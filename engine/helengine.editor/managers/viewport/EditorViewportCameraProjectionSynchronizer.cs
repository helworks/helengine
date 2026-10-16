namespace helengine.editor {
    /// <summary>
    /// Copies projection inputs between editor cameras that participate in one viewport render and picking stack.
    /// </summary>
    internal static class EditorViewportCameraProjectionSynchronizer {
        /// <summary>
        /// Copies field of view, clip planes, and optional projection settings from one camera to another.
        /// </summary>
        /// <param name="sourceCamera">Camera whose projection should be matched.</param>
        /// <param name="destinationCamera">Camera that receives matching projection settings.</param>
        public static void Synchronize(CameraComponent sourceCamera, CameraComponent destinationCamera) {
            if (sourceCamera == null) {
                throw new ArgumentNullException(nameof(sourceCamera));
            }
            if (destinationCamera == null) {
                throw new ArgumentNullException(nameof(destinationCamera));
            }

            destinationCamera.FieldOfView = sourceCamera.FieldOfView;
            destinationCamera.NearPlaneDistance = Math.Min(
                destinationCamera.NearPlaneDistance,
                sourceCamera.NearPlaneDistance);
            destinationCamera.FarPlaneDistance = sourceCamera.FarPlaneDistance;
            destinationCamera.NearPlaneDistance = sourceCamera.NearPlaneDistance;

            ICameraProjectionSettings sourceSettings = sourceCamera as ICameraProjectionSettings;
            ICameraProjectionSettings destinationSettings = destinationCamera as ICameraProjectionSettings;
            if (sourceSettings == null) {
                if (destinationSettings != null) {
                    destinationSettings.ProjectionMode = CameraProjectionMode.Perspective;
                }
                return;
            }
            if (destinationSettings == null) {
                throw new InvalidOperationException("The destination camera does not support the source camera's optional projection settings.");
            }

            destinationSettings.OrthographicVerticalSpan = sourceSettings.OrthographicVerticalSpan;
            destinationSettings.ProjectionMode = sourceSettings.ProjectionMode;
        }
    }
}
