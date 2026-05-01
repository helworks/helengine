namespace helengine.editor {
    /// <summary>
    /// Attaches the hidden editor camera-visual component to scene entities that own a camera.
    /// </summary>
    public static class EditorCameraVisualAttachmentService {
        /// <summary>
        /// Ensures one scene entity with a camera component also owns the hidden editor camera-visual component.
        /// </summary>
        /// <param name="entity">Scene entity that may represent a camera.</param>
        /// <returns>True when the visual component was added; otherwise false.</returns>
        public static bool Attach(EditorEntity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            if (!HasComponent<CameraComponent>(entity)) {
                return false;
            }

            if (HasComponent<EditorCameraVisualComponent>(entity)) {
                return false;
            }

            entity.AddComponent(new EditorCameraVisualComponent());
            return true;
        }

        /// <summary>
        /// Determines whether one entity already owns a component of the requested type.
        /// </summary>
        /// <typeparam name="T">Concrete component type to locate.</typeparam>
        /// <param name="entity">Entity whose component list should be searched.</param>
        /// <returns>True when a matching component is present.</returns>
        static bool HasComponent<T>(EditorEntity entity) where T : Component {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            if (entity.Components == null) {
                return false;
            }

            for (int i = 0; i < entity.Components.Count; i++) {
                if (entity.Components[i] is T) {
                    return true;
                }
            }

            return false;
        }
    }
}
