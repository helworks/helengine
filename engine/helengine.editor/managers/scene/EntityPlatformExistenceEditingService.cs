namespace helengine.editor {
    /// <summary>
    /// Manages per-platform entity existence overrides stored on the hidden editor save component.
    /// </summary>
    public sealed class EntityPlatformExistenceEditingService : IDisposable {
        /// <summary>
        /// Stable platform id used by the shared common entity state.
        /// </summary>
        public const string CommonPlatformId = ComponentPlatformEditingService.CommonPlatformId;

        /// <summary>
        /// Raised after one entity existence override changes so viewport suppression can re-resolve event-driven.
        /// </summary>
        public event Action ExistenceChanged;

        /// <inheritdoc />
        public void Dispose() {
            ExistenceChanged = null;
        }

        /// <summary>
        /// Resolves whether one entity exists on the default-order path for a platform.
        /// </summary>
        public bool ResolveExists(EntitySaveComponent saveComponent, string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            return ResolveExists(saveComponent, EditorOverrideScope.ForPlatform(platformId));
        }

        /// <summary>
        /// Resolves existence at one path: the deepest authored prefix wins, Common included; nothing authored means the entity exists.
        /// </summary>
        public bool ResolveExists(EntitySaveComponent saveComponent, EditorOverrideScope scope) {
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }

            if (saveComponent.TryGetDeepestExistencePlatformOverride(scope, out SceneEntityPlatformExistenceOverrideAsset overrideState)) {
                return overrideState.Exists;
            }

            return true;
        }

        /// <summary>
        /// Returns whether one path stores its own existence override.
        /// </summary>
        public bool HasExistenceOverride(EntitySaveComponent saveComponent, string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            return HasExistenceOverride(saveComponent, EditorOverrideScope.ForPlatform(platformId));
        }

        /// <summary>
        /// Returns whether one path stores its own existence override.
        /// </summary>
        public bool HasExistenceOverride(EntitySaveComponent saveComponent, EditorOverrideScope scope) {
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }

            return saveComponent.TryGetExistencePlatformOverride(scope, out _);
        }

        /// <summary>
        /// Stores existence for the default-order path of a platform.
        /// </summary>
        public void SetExists(EntitySaveComponent saveComponent, string platformId, bool exists) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            SetExists(saveComponent, EditorOverrideScope.ForPlatform(platformId), exists);
        }

        /// <summary>
        /// Stores a sparse override: the entry is removed when it equals what the parent path already resolves to.
        /// Common's parent value is the implicit "exists".
        /// </summary>
        public void SetExists(EntitySaveComponent saveComponent, EditorOverrideScope scope, bool exists) {
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }

            bool parentExists = scope.IsCommon ? true : ResolveExists(saveComponent, scope.Parent);
            if (exists == parentExists) {
                saveComponent.RemoveExistencePlatformOverride(scope);
                ExistenceChanged?.Invoke();
                return;
            }

            saveComponent.SetExistencePlatformOverride(scope, new SceneEntityPlatformExistenceOverrideAsset {
                Scope = scope.ToSteps(),
                Exists = exists
            });
            ExistenceChanged?.Invoke();
        }
    }
}
