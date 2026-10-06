namespace helengine.editor {
    /// <summary>
    /// Camera used only by editor viewports and their synchronized picking passes.
    /// </summary>
    [RunInEditor]
    public class EditorViewportCameraComponent : CameraComponent, ICameraProjectionSettings {
        /// <summary>
        /// Projection mode selected for this editor viewport camera.
        /// </summary>
        CameraProjectionMode ProjectionModeValue;

        /// <summary>
        /// Full vertical world-space extent used by orthographic projection.
        /// </summary>
        float OrthographicVerticalSpanValue;

        /// <summary>
        /// Creates an editor viewport camera with a valid default orthographic span and perspective mode.
        /// </summary>
        public EditorViewportCameraComponent() {
            ProjectionModeValue = CameraProjectionMode.Perspective;
            OrthographicVerticalSpanValue = 10f;
            FilterEditorHiddenEntities = true;
        }

        /// <summary>
        /// Gets or sets the projection mode selected for this viewport camera.
        /// </summary>
        [EditorPropertyHidden]
        [ScenePersistenceIgnore]
        public CameraProjectionMode ProjectionMode {
            get { return ProjectionModeValue; }
            set {
                if (!Enum.IsDefined(typeof(CameraProjectionMode), value)) {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "The camera projection mode is not supported.");
                }
                ProjectionModeValue = value;
            }
        }

        /// <summary>
        /// Gets or sets the full vertical world-space extent used by orthographic projection.
        /// </summary>
        [EditorPropertyHidden]
        [ScenePersistenceIgnore]
        public float OrthographicVerticalSpan {
            get { return OrthographicVerticalSpanValue; }
            set {
                if (!float.IsFinite(value) || value < CameraProjectionUtils.MinimumOrthographicVerticalSpan) {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Orthographic vertical span must be finite and at least the configured minimum.");
                }
                OrthographicVerticalSpanValue = value;
            }
        }
    }
}
