namespace helengine.editor.tests.testing {
    /// <summary>
    /// Records the camera count visible to the 3D renderer when one draw call begins.
    /// </summary>
    internal sealed class CameraCountRecordingRenderManager3D : TestRenderManager3D {
        /// <summary>
        /// Gets the camera count observed during the most recent draw invocation.
        /// </summary>
        public int LastObservedCameraCount { get; private set; }

        /// <summary>
        /// Captures the current registered camera count before delegating to the base test renderer behavior.
        /// </summary>
        public override void Draw() {
            if (Core.Instance == null || Core.Instance.ObjectManager == null) {
                LastObservedCameraCount = -1;
            } else {
                LastObservedCameraCount = Core.Instance.ObjectManager.Cameras.Count;
            }

            base.Draw();
        }
    }
}
