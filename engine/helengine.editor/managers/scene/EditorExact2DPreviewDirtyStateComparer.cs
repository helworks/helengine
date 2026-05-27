namespace helengine.editor {
    /// <summary>
    /// Captures and compares texture-affecting state for editor-only exact 2D world previews.
    /// </summary>
    public static class EditorExact2DPreviewDirtyStateComparer {
        /// <summary>
        /// Captures the texture-affecting preview state for one authored text component.
        /// </summary>
        /// <param name="sourceEntity">Authored source entity whose transform is intentionally ignored for dirty-state purposes.</param>
        /// <param name="sourceComponent">Authored text component whose visible data should be captured.</param>
        /// <returns>Render-state snapshot used to decide whether a texture recapture is required.</returns>
        public static EditorExact2DPreviewRenderState CaptureTextState(Entity sourceEntity, TextComponent sourceComponent) {
            if (sourceEntity == null) {
                throw new ArgumentNullException(nameof(sourceEntity));
            } else if (sourceComponent == null) {
                throw new ArgumentNullException(nameof(sourceComponent));
            }

            return new EditorExact2DPreviewRenderState(nameof(TextComponent)) {
                Texture = sourceComponent.Texture,
                Font = sourceComponent.Font,
                Text = sourceComponent.Text ?? string.Empty,
                SourceRect = sourceComponent.SourceRect,
                Size = sourceComponent.Size,
                Color = sourceComponent.Color,
                WrapText = sourceComponent.WrapText,
                FontScale = sourceComponent.FontScale,
                Alignment = sourceComponent.Alignment,
                Rotation = sourceComponent.Rotation
            };
        }

        /// <summary>
        /// Captures the texture-affecting preview state for one authored rounded-rectangle component.
        /// </summary>
        /// <param name="sourceEntity">Authored source entity whose transform is intentionally ignored for dirty-state purposes.</param>
        /// <param name="sourceComponent">Authored rounded-rectangle component whose visible data should be captured.</param>
        /// <returns>Render-state snapshot used to decide whether a texture recapture is required.</returns>
        public static EditorExact2DPreviewRenderState CaptureRoundedRectState(Entity sourceEntity, RoundedRectComponent sourceComponent) {
            if (sourceEntity == null) {
                throw new ArgumentNullException(nameof(sourceEntity));
            } else if (sourceComponent == null) {
                throw new ArgumentNullException(nameof(sourceComponent));
            }

            return new EditorExact2DPreviewRenderState(nameof(RoundedRectComponent)) {
                SourceRect = sourceComponent.SourceRect,
                Size = sourceComponent.Size,
                Color = sourceComponent.Color,
                Rotation = sourceComponent.Rotation,
                Radius = sourceComponent.Radius,
                BorderThickness = sourceComponent.BorderThickness,
                FillColor = sourceComponent.FillColor,
                BorderColor = sourceComponent.BorderColor,
                Corners = sourceComponent.Corners
            };
        }

        /// <summary>
        /// Determines whether the preview texture must be recaptured because the texture-affecting state changed.
        /// </summary>
        /// <param name="previousState">Previously captured preview state.</param>
        /// <param name="nextState">Newly captured preview state.</param>
        /// <returns>True when the preview texture must be regenerated.</returns>
        public static bool RequiresRecapture(EditorExact2DPreviewRenderState previousState, EditorExact2DPreviewRenderState nextState) {
            if (previousState == null) {
                throw new ArgumentNullException(nameof(previousState));
            } else if (nextState == null) {
                throw new ArgumentNullException(nameof(nextState));
            }

            if (!string.Equals(previousState.PreviewSourceTypeName, nextState.PreviewSourceTypeName, StringComparison.Ordinal)) {
                return true;
            } else if (!ReferenceEquals(previousState.Texture, nextState.Texture)) {
                return true;
            } else if (!ReferenceEquals(previousState.Font, nextState.Font)) {
                return true;
            } else if (!string.Equals(previousState.Text, nextState.Text, StringComparison.Ordinal)) {
                return true;
            } else if (!previousState.SourceRect.Equals(nextState.SourceRect)) {
                return true;
            } else if (!previousState.Size.Equals(nextState.Size)) {
                return true;
            } else if (!previousState.Color.Equals(nextState.Color)) {
                return true;
            } else if (previousState.WrapText != nextState.WrapText) {
                return true;
            } else if (previousState.FontScale != nextState.FontScale) {
                return true;
            } else if (previousState.Alignment != nextState.Alignment) {
                return true;
            } else if (previousState.Rotation != nextState.Rotation) {
                return true;
            } else if (previousState.Radius != nextState.Radius) {
                return true;
            } else if (previousState.BorderThickness != nextState.BorderThickness) {
                return true;
            } else if (!previousState.FillColor.Equals(nextState.FillColor)) {
                return true;
            } else if (!previousState.BorderColor.Equals(nextState.BorderColor)) {
                return true;
            }

            return previousState.Corners != nextState.Corners;
        }
    }
}
