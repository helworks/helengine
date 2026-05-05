namespace helengine.editor {
    /// <summary>
    /// Stores viewport-local simulated canvas settings used by the world-space 2D preview plane.
    /// </summary>
    public sealed class EditorViewportCanvasPreviewSettings {
        /// <summary>
        /// Default simulated canvas width in pixels.
        /// </summary>
        public const int DefaultCanvasWidth = 1280;
        /// <summary>
        /// Default simulated canvas height in pixels.
        /// </summary>
        public const int DefaultCanvasHeight = 720;
        /// <summary>
        /// Default number of pixels represented by one world unit on the preview plane.
        /// </summary>
        public const int DefaultPixelsPerWorldUnit = 100;

        /// <summary>
        /// Backing field for <see cref="CanvasWidth"/>.
        /// </summary>
        int CanvasWidthValue = DefaultCanvasWidth;
        /// <summary>
        /// Backing field for <see cref="CanvasHeight"/>.
        /// </summary>
        int CanvasHeightValue = DefaultCanvasHeight;
        /// <summary>
        /// Backing field for <see cref="PixelsPerWorldUnit"/>.
        /// </summary>
        int PixelsPerWorldUnitValue = DefaultPixelsPerWorldUnit;

        /// <summary>
        /// Raised whenever one simulated canvas setting changes value.
        /// </summary>
        public event Action SettingsChanged;

        /// <summary>
        /// Gets or sets the simulated canvas width in pixels.
        /// </summary>
        public int CanvasWidth {
            get => CanvasWidthValue;
            set {
                int clampedValue = Math.Max(1, value);
                if (CanvasWidthValue == clampedValue) {
                    return;
                }

                CanvasWidthValue = clampedValue;
                RaiseSettingsChanged();
            }
        }

        /// <summary>
        /// Gets or sets the simulated canvas height in pixels.
        /// </summary>
        public int CanvasHeight {
            get => CanvasHeightValue;
            set {
                int clampedValue = Math.Max(1, value);
                if (CanvasHeightValue == clampedValue) {
                    return;
                }

                CanvasHeightValue = clampedValue;
                RaiseSettingsChanged();
            }
        }

        /// <summary>
        /// Gets or sets the number of simulated canvas pixels represented by one world unit.
        /// </summary>
        public int PixelsPerWorldUnit {
            get => PixelsPerWorldUnitValue;
            set {
                int clampedValue = Math.Max(1, value);
                if (PixelsPerWorldUnitValue == clampedValue) {
                    return;
                }

                PixelsPerWorldUnitValue = clampedValue;
                RaiseSettingsChanged();
            }
        }

        /// <summary>
        /// Raises <see cref="SettingsChanged"/> when listeners are present.
        /// </summary>
        void RaiseSettingsChanged() {
            if (SettingsChanged != null) {
                SettingsChanged();
            }
        }
    }
}
