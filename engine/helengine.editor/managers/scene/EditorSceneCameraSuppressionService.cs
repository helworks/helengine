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
