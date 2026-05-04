namespace helengine {
    /// <summary>
    /// Stores authored camera settings while the editor suppresses scene-camera rendering at runtime.
    /// </summary>
    public class EditorSceneCameraSuppressionComponent : Component, IEditorHiddenComponent {
        /// <summary>
        /// Initializes a new hidden suppression-state component from authored camera settings.
        /// </summary>
        /// <param name="cameraDrawOrder">Authored camera draw order.</param>
        /// <param name="layerMask">Authored camera layer mask.</param>
        /// <param name="viewport">Authored camera viewport.</param>
        /// <param name="clearSettings">Authored camera clear settings.</param>
        /// <param name="renderSettings">Authored camera render settings.</param>
        public EditorSceneCameraSuppressionComponent(
            byte cameraDrawOrder,
            ushort layerMask,
            float4 viewport,
            CameraClearSettings clearSettings,
            CameraRenderSettings renderSettings) {
            if (renderSettings == null) {
                throw new ArgumentNullException(nameof(renderSettings));
            }

            CameraDrawOrder = cameraDrawOrder;
            LayerMask = layerMask;
            Viewport = viewport;
            ClearSettings = clearSettings;
            RenderSettings = new CameraRenderSettings(renderSettings);
        }

        /// <summary>
        /// Gets the authored camera draw order captured before editor suppression was applied.
        /// </summary>
        public byte CameraDrawOrder { get; }

        /// <summary>
        /// Gets the authored camera layer mask captured before editor suppression was applied.
        /// </summary>
        public ushort LayerMask { get; }

        /// <summary>
        /// Gets the authored camera viewport captured before editor suppression was applied.
        /// </summary>
        public float4 Viewport { get; }

        /// <summary>
        /// Gets the authored camera clear settings captured before editor suppression was applied.
        /// </summary>
        public CameraClearSettings ClearSettings { get; }

        /// <summary>
        /// Gets the authored camera render settings captured before editor suppression was applied.
        /// </summary>
        public CameraRenderSettings RenderSettings { get; }
    }
}
