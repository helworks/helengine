namespace helengine.editor {
    /// <summary>Tracks translation-gizmo follow components for one editor graph.</summary>
    public sealed class EditorTranslationGizmoFollowRegistry : IDisposable {
        readonly Dictionary<CameraComponent, TransformTranslationGizmoFollowComponent> FollowComponentsByCamera = new Dictionary<CameraComponent, TransformTranslationGizmoFollowComponent>();
        bool IsDisposed;

        /// <summary>Gets the follow component registered for one session camera.</summary>
        public TransformTranslationGizmoFollowComponent GetForCamera(CameraComponent camera) {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            ThrowIfDisposed();
            FollowComponentsByCamera.TryGetValue(camera, out TransformTranslationGizmoFollowComponent followComponent);
            return followComponent;
        }

        /// <summary>Registers one session-owned follow component.</summary>
        public void Register(CameraComponent camera, TransformTranslationGizmoFollowComponent followComponent) {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (followComponent == null) throw new ArgumentNullException(nameof(followComponent));
            ThrowIfDisposed();
            FollowComponentsByCamera[camera] = followComponent;
        }

        /// <summary>Removes one follow component when it is detached.</summary>
        public void Unregister(CameraComponent camera, TransformTranslationGizmoFollowComponent followComponent) {
            if (camera == null || followComponent == null || IsDisposed) return;
            if (FollowComponentsByCamera.TryGetValue(camera, out TransformTranslationGizmoFollowComponent current) && ReferenceEquals(current, followComponent)) {
                FollowComponentsByCamera.Remove(camera);
            }
        }

        /// <inheritdoc />
        public void Dispose() {
            if (IsDisposed) return;
            FollowComponentsByCamera.Clear();
            IsDisposed = true;
        }

        void ThrowIfDisposed() {
            if (IsDisposed) throw new ObjectDisposedException(nameof(EditorTranslationGizmoFollowRegistry));
        }
    }
}
