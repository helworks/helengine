namespace helengine {
    /// <summary>
    /// Stores authored camera settings while the editor suppresses scene-camera rendering at runtime.
    /// </summary>
    public class EditorSceneCameraSuppressionComponent : Component, IEditorHiddenComponent {
        /// <summary>
        /// Cached authored render settings preserved while the live scene camera remains suppressed in the editor.
        /// </summary>
        CameraRenderSettings RenderSettingsValue;

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
            RenderSettingsValue = new CameraRenderSettings(renderSettings);
        }

        /// <summary>
        /// Gets or sets the authored camera draw order captured before editor suppression was applied.
        /// </summary>
        public byte CameraDrawOrder { get; set; }

        /// <summary>
        /// Gets or sets the authored camera layer mask captured before editor suppression was applied.
        /// </summary>
        public ushort LayerMask { get; set; }

        /// <summary>
        /// Gets or sets the authored camera viewport captured before editor suppression was applied.
        /// </summary>
        public float4 Viewport { get; set; }

        /// <summary>
        /// Gets or sets the authored camera clear settings captured before editor suppression was applied.
        /// </summary>
        public CameraClearSettings ClearSettings { get; set; }

        /// <summary>
        /// Gets or sets the authored camera render settings captured before editor suppression was applied.
        /// </summary>
        public CameraRenderSettings RenderSettings {
            get { return RenderSettingsValue; }
            set { RenderSettingsValue = value ?? throw new ArgumentNullException(nameof(value)); }
        }
    }
}
