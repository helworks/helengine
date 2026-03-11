namespace helengine.editor {
    /// <summary>
    /// Holds the runtime textures used by the viewport toolbar controls.
    /// </summary>
    public class EditorViewportToolbarIconSet {
        /// <summary>
        /// Initializes a new icon set with textures for each primary toolbar control.
        /// </summary>
        /// <param name="translateIcon">Texture used by the translate tool button.</param>
        /// <param name="rotateIcon">Texture used by the rotate tool button.</param>
        /// <param name="scaleIcon">Texture used by the scale tool button.</param>
        /// <param name="snapIncreaseIcon">Texture used by the snap increase button.</param>
        /// <param name="snapDecreaseIcon">Texture used by the snap decrease button.</param>
        /// <param name="magnetIcon">Texture used by snap-slot labels to indicate snapping.</param>
        /// <param name="ctrlKeyIcon">Texture used by the first snap-slot label.</param>
        /// <param name="shiftKeyIcon">Texture used by the second snap-slot label.</param>
        public EditorViewportToolbarIconSet(
            RuntimeTexture translateIcon,
            RuntimeTexture rotateIcon,
            RuntimeTexture scaleIcon,
            RuntimeTexture snapIncreaseIcon,
            RuntimeTexture snapDecreaseIcon,
            RuntimeTexture magnetIcon,
            RuntimeTexture ctrlKeyIcon,
            RuntimeTexture shiftKeyIcon) {
            TranslateIcon = translateIcon ?? throw new ArgumentNullException(nameof(translateIcon));
            RotateIcon = rotateIcon ?? throw new ArgumentNullException(nameof(rotateIcon));
            ScaleIcon = scaleIcon ?? throw new ArgumentNullException(nameof(scaleIcon));
            SnapIncreaseIcon = snapIncreaseIcon ?? throw new ArgumentNullException(nameof(snapIncreaseIcon));
            SnapDecreaseIcon = snapDecreaseIcon ?? throw new ArgumentNullException(nameof(snapDecreaseIcon));
            MagnetIcon = magnetIcon ?? throw new ArgumentNullException(nameof(magnetIcon));
            CtrlKeyIcon = ctrlKeyIcon ?? throw new ArgumentNullException(nameof(ctrlKeyIcon));
            ShiftKeyIcon = shiftKeyIcon ?? throw new ArgumentNullException(nameof(shiftKeyIcon));
        }

        /// <summary>
        /// Gets the texture used by the translate tool button.
        /// </summary>
        public RuntimeTexture TranslateIcon { get; }
        /// <summary>
        /// Gets the texture used by the rotate tool button.
        /// </summary>
        public RuntimeTexture RotateIcon { get; }
        /// <summary>
        /// Gets the texture used by the scale tool button.
        /// </summary>
        public RuntimeTexture ScaleIcon { get; }
        /// <summary>
        /// Gets the texture used by the snap increase button.
        /// </summary>
        public RuntimeTexture SnapIncreaseIcon { get; }
        /// <summary>
        /// Gets the texture used by the snap decrease button.
        /// </summary>
        public RuntimeTexture SnapDecreaseIcon { get; }
        /// <summary>
        /// Gets the texture used by snap-slot labels to indicate snapping behavior.
        /// </summary>
        public RuntimeTexture MagnetIcon { get; }
        /// <summary>
        /// Gets the texture used by the first snap-slot modifier label.
        /// </summary>
        public RuntimeTexture CtrlKeyIcon { get; }
        /// <summary>
        /// Gets the texture used by the second snap-slot modifier label.
        /// </summary>
        public RuntimeTexture ShiftKeyIcon { get; }

        /// <summary>
        /// Resolves the toolbar icon texture for the provided viewport tool mode.
        /// </summary>
        /// <param name="toolMode">Tool mode whose icon should be returned.</param>
        /// <returns>Runtime texture associated with the requested tool mode.</returns>
        public RuntimeTexture GetIcon(EditorViewportToolMode toolMode) {
            switch (toolMode) {
                case EditorViewportToolMode.Translate:
                    return TranslateIcon;
                case EditorViewportToolMode.Rotate:
                    return RotateIcon;
                case EditorViewportToolMode.Scale:
                    return ScaleIcon;
                default:
                    throw new InvalidOperationException("Toolbar icon is not defined for the requested tool mode.");
            }
        }

        /// <summary>
        /// Resolves the toolbar icon texture for one snap adjustment button.
        /// </summary>
        /// <param name="isIncreaseButton">True for the increase button; false for the decrease button.</param>
        /// <returns>Runtime texture associated with the requested snap adjustment button.</returns>
        public RuntimeTexture GetSnapButtonIcon(bool isIncreaseButton) {
            return isIncreaseButton ? SnapIncreaseIcon : SnapDecreaseIcon;
        }

        /// <summary>
        /// Resolves the toolbar keycap icon used by one snap-slot label.
        /// </summary>
        /// <param name="snapSlot">Snap slot whose modifier key icon should be returned.</param>
        /// <returns>Runtime texture associated with the requested snap slot.</returns>
        public RuntimeTexture GetSnapModifierIcon(TransformGizmoSnapSlot snapSlot) {
            switch (snapSlot) {
                case TransformGizmoSnapSlot.Snap1:
                    return CtrlKeyIcon;
                case TransformGizmoSnapSlot.Snap2:
                    return ShiftKeyIcon;
                default:
                    throw new InvalidOperationException("Toolbar snap label icon is not defined for the requested snap slot.");
            }
        }
    }
}
