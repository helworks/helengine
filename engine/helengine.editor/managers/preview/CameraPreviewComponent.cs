namespace helengine.editor {
    /// <summary>Renders the camera preview at panel resolution while retaining the scene's authored 2D canvas.</summary>
    [RunInEditor]
    public sealed class CameraPreviewComponent : CameraComponent, ICamera2DProjectionSettings {
        /// <summary>Stores the authored canvas dimensions independently of the physical viewport.</summary>
        int2 LogicalViewportSizeValue = new int2(1, 1);

        /// <summary>Gets or sets the logical canvas used to project 2D preview content into the current target.</summary>
        [ScenePersistenceIgnore]
        [EditorPropertyHidden]
        public int2 LogicalViewportSize {
            get { return LogicalViewportSizeValue; }
            set {
                if (value.X <= 0 || value.Y <= 0) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Logical preview dimensions must be positive.");
                }
                LogicalViewportSizeValue = value;
            }
        }
    }
}
