namespace helengine.editor {
    /// <summary>
    /// Resolves the currently active transform-gizmo snap value from keyboard modifier state.
    /// </summary>
    public static class TransformGizmoActiveSnapValueResolver {
        /// <summary>
        /// Reads the active snap value for a specific gizmo tool mode from the input manager.
        /// </summary>
        /// <param name="input">Input manager that provides keyboard modifier state.</param>
        /// <param name="toolMode">Tool mode whose active snap value should be resolved.</param>
        /// <returns>Configured snap value for the active modifier slot, or zero when no snap modifier is held.</returns>
        public static double ResolveActiveSnapValue(InputManager input, EditorViewportToolMode toolMode) {
            if (input == null) {
                throw new ArgumentNullException(nameof(input));
            }

            bool isControlDown = input.IsKeyDown(Keys.LeftControl) || input.IsKeyDown(Keys.RightControl);
            bool isShiftDown = input.IsKeyDown(Keys.LeftShift) || input.IsKeyDown(Keys.RightShift);
            return TransformGizmoSnapSettingsService.GetActiveSnapValue(toolMode, isControlDown, isShiftDown);
        }
    }
}
