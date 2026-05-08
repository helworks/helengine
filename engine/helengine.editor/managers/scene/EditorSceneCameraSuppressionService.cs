namespace helengine.editor {
    /// <summary>
    /// Suppresses user-authored scene cameras while the editor has no dedicated game-view camera pipeline.
    /// </summary>
    public static class EditorSceneCameraSuppressionService {
        /// <summary>
        /// Ensures one scene entity with a camera keeps its authored settings in hidden editor state and stays suppressed at runtime.
        /// </summary>
        /// <param name="entity">Scene entity that may own one authored camera.</param>
        /// <returns>True when a camera was found and is now suppressed; otherwise false.</returns>
        public static bool AttachAndSuppress(EditorEntity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            CameraComponent cameraComponent = FindComponent<CameraComponent>(entity);
            if (cameraComponent == null) {
                return false;
            }

            EditorSceneCameraSuppressionComponent suppressionComponent = FindComponent<EditorSceneCameraSuppressionComponent>(entity);
            if (suppressionComponent == null) {
                suppressionComponent = new EditorSceneCameraSuppressionComponent(
                    cameraComponent.CameraDrawOrder,
                    cameraComponent.LayerMask,
                    cameraComponent.Viewport,
                    cameraComponent.NearPlaneDistance,
                    cameraComponent.FarPlaneDistance,
                    cameraComponent.ClearSettings,
                    cameraComponent.RenderSettings);
                entity.AddComponent(suppressionComponent);
            }

            ApplySuppressedRuntimeState(cameraComponent);
            return true;
        }

        /// <summary>
        /// Resolves the hidden authored state for one scene camera when the entity has been suppressed in the editor.
        /// </summary>
        /// <param name="cameraComponent">Live scene camera component.</param>
        /// <returns>Hidden authored state when present; otherwise null.</returns>
        public static EditorSceneCameraSuppressionComponent GetSuppressionState(CameraComponent cameraComponent) {
            if (cameraComponent == null) {
                throw new ArgumentNullException(nameof(cameraComponent));
            }
            if (cameraComponent.Parent is not EditorEntity editorEntity) {
                return null;
            }

            return FindComponent<EditorSceneCameraSuppressionComponent>(editorEntity);
        }

        /// <summary>
        /// Attempts to read one authored camera property value from suppression metadata instead of the live suppressed camera.
        /// </summary>
        /// <param name="cameraComponent">Suppressed scene camera whose authored state should be queried.</param>
        /// <param name="propertyName">Name of the authored camera property.</param>
        /// <param name="value">Resolved authored value when suppression metadata owns the property.</param>
        /// <returns>True when the property is backed by suppression metadata; otherwise false.</returns>
        public static bool TryGetAuthoredPropertyValue(CameraComponent cameraComponent, string propertyName, out object value) {
            if (cameraComponent == null) {
                throw new ArgumentNullException(nameof(cameraComponent));
            }
            if (string.IsNullOrWhiteSpace(propertyName)) {
                throw new ArgumentException("Property name must be provided.", nameof(propertyName));
            }

            EditorSceneCameraSuppressionComponent suppressionState = GetSuppressionState(cameraComponent);
            if (suppressionState == null) {
                value = null;
                return false;
            }

            if (string.Equals(propertyName, nameof(CameraComponent.CameraDrawOrder), StringComparison.Ordinal)) {
                value = suppressionState.CameraDrawOrder;
                return true;
            }
            if (string.Equals(propertyName, nameof(CameraComponent.LayerMask), StringComparison.Ordinal)) {
                value = suppressionState.LayerMask;
                return true;
            }
            if (string.Equals(propertyName, nameof(CameraComponent.Viewport), StringComparison.Ordinal)) {
                value = suppressionState.Viewport;
                return true;
            }
            if (string.Equals(propertyName, nameof(CameraComponent.NearPlaneDistance), StringComparison.Ordinal)) {
                value = suppressionState.NearPlaneDistance;
                return true;
            }
            if (string.Equals(propertyName, nameof(CameraComponent.FarPlaneDistance), StringComparison.Ordinal)) {
                value = suppressionState.FarPlaneDistance;
                return true;
            }
            if (string.Equals(propertyName, nameof(CameraComponent.ClearSettings), StringComparison.Ordinal)) {
                value = suppressionState.ClearSettings;
                return true;
            }
            if (string.Equals(propertyName, nameof(CameraComponent.RenderSettings), StringComparison.Ordinal)) {
                value = suppressionState.RenderSettings;
                return true;
            }

            value = null;
            return false;
        }

        /// <summary>
        /// Attempts to write one authored camera property value into suppression metadata instead of mutating the live suppressed camera.
        /// </summary>
        /// <param name="cameraComponent">Suppressed scene camera whose authored state should be updated.</param>
        /// <param name="propertyName">Name of the authored camera property.</param>
        /// <param name="value">Authored value to store.</param>
        /// <returns>True when the property is backed by suppression metadata; otherwise false.</returns>
        public static bool TrySetAuthoredPropertyValue(CameraComponent cameraComponent, string propertyName, object value) {
            if (cameraComponent == null) {
                throw new ArgumentNullException(nameof(cameraComponent));
            }
            if (string.IsNullOrWhiteSpace(propertyName)) {
                throw new ArgumentException("Property name must be provided.", nameof(propertyName));
            }

            EditorSceneCameraSuppressionComponent suppressionState = GetSuppressionState(cameraComponent);
            if (suppressionState == null) {
                return false;
            }

            if (string.Equals(propertyName, nameof(CameraComponent.CameraDrawOrder), StringComparison.Ordinal)) {
                suppressionState.CameraDrawOrder = (byte)value;
                return true;
            }
            if (string.Equals(propertyName, nameof(CameraComponent.LayerMask), StringComparison.Ordinal)) {
                suppressionState.LayerMask = (ushort)value;
                return true;
            }
            if (string.Equals(propertyName, nameof(CameraComponent.Viewport), StringComparison.Ordinal)) {
                suppressionState.Viewport = (float4)value;
                return true;
            }
            if (string.Equals(propertyName, nameof(CameraComponent.NearPlaneDistance), StringComparison.Ordinal)) {
                suppressionState.NearPlaneDistance = (float)value;
                return true;
            }
            if (string.Equals(propertyName, nameof(CameraComponent.FarPlaneDistance), StringComparison.Ordinal)) {
                suppressionState.FarPlaneDistance = (float)value;
                return true;
            }
            if (string.Equals(propertyName, nameof(CameraComponent.ClearSettings), StringComparison.Ordinal)) {
                suppressionState.ClearSettings = (CameraClearSettings)value;
                return true;
            }
            if (string.Equals(propertyName, nameof(CameraComponent.RenderSettings), StringComparison.Ordinal)) {
                suppressionState.RenderSettings = (CameraRenderSettings)value;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Applies the inert editor runtime state that prevents a scene camera from rendering into editor views.
        /// </summary>
        /// <param name="cameraComponent">Scene camera whose runtime state should be suppressed.</param>
        static void ApplySuppressedRuntimeState(CameraComponent cameraComponent) {
            if (cameraComponent == null) {
                throw new ArgumentNullException(nameof(cameraComponent));
            }

            cameraComponent.LayerMask = 0;
            cameraComponent.ClearSettings = new CameraClearSettings(false, new float4(0f, 0f, 0f, 0f), false, 1.0f, false, 0);
        }

        /// <summary>
        /// Finds the first component of the requested type attached to one editor entity.
        /// </summary>
        /// <typeparam name="T">Concrete component type to locate.</typeparam>
        /// <param name="entity">Entity whose component list should be searched.</param>
        /// <returns>Matching component instance when present; otherwise null.</returns>
        static T FindComponent<T>(EditorEntity entity) where T : Component {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (entity.Components == null) {
                return null;
            }

            for (int i = 0; i < entity.Components.Count; i++) {
                if (entity.Components[i] is T component) {
                    return component;
                }
            }

            return null;
        }
    }
}
