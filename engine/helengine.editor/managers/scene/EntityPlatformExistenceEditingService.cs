namespace helengine.editor {
    /// <summary>
    /// Manages per-platform entity existence overrides stored on the hidden editor save component.
    /// </summary>
    public sealed class EntityPlatformExistenceEditingService {
        /// <summary>
        /// Stable platform id used by the shared common entity state.
        /// </summary>
        public const string CommonPlatformId = ComponentPlatformEditingService.CommonPlatformId;

        /// <summary>
        /// Raised after one entity existence override changes so viewport suppression can re-resolve event-driven.
        /// </summary>
        public static event Action ExistenceChanged;

        /// <summary>
        /// Resolves whether one entity should exist on the supplied platform.
        /// </summary>
        /// <param name="saveComponent">Hidden save component that owns the entity existence overrides.</param>
        /// <param name="platformId">Platform identifier to resolve.</param>
        /// <returns>True when the entity should exist on the supplied platform.</returns>
        public bool ResolveExists(EntitySaveComponent saveComponent, string platformId) {
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            return ResolveExists(saveComponent, new EditorOverrideScope(platformId));
        }

        /// <summary>
        /// Resolves common, platform, and nested environment existence state in order.
        /// </summary>
        public bool ResolveExists(EntitySaveComponent saveComponent, EditorOverrideScope scope) {
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }

            bool exists = true;
            if (saveComponent.TryGetExistencePlatformOverride(new EditorOverrideScope(scope.PlatformId), out SceneEntityPlatformExistenceOverrideAsset platformOverride)) {
                exists = platformOverride.Exists;
            }
            if (!scope.IsPlatformOnly
                && saveComponent.TryGetExistencePlatformOverride(scope, out SceneEntityPlatformExistenceOverrideAsset environmentOverride)) {
                exists = environmentOverride.Exists;
            }

            return exists;
        }

        /// <summary>
        /// Returns whether one platform stores an explicit entity existence override.
        /// </summary>
        /// <param name="saveComponent">Hidden save component that owns the entity existence overrides.</param>
        /// <param name="platformId">Platform identifier to query.</param>
        /// <returns>True when the platform stores an explicit entity existence override.</returns>
        public bool HasExistenceOverride(EntitySaveComponent saveComponent, string platformId) {
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            string normalizedPlatformId = NormalizePlatformId(platformId);
            if (IsCommonPlatformId(normalizedPlatformId)) {
                return false;
            }

            return saveComponent.TryGetExistencePlatformOverride(normalizedPlatformId, out _);
        }

        /// <summary>
        /// Stores the desired entity existence for one platform and removes redundant overrides that match common behavior.
        /// </summary>
        /// <param name="saveComponent">Hidden save component that owns the entity existence overrides.</param>
        /// <param name="platformId">Platform identifier whose effective entity existence should be stored.</param>
        /// <param name="exists">True when the entity should exist on the platform.</param>
        public void SetExists(EntitySaveComponent saveComponent, string platformId, bool exists) {
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            SetExists(saveComponent, new EditorOverrideScope(platformId), exists);
        }

        /// <summary>
        /// Stores a sparse platform or nested environment existence override relative to its parent scope.
        /// </summary>
        public void SetExists(EntitySaveComponent saveComponent, EditorOverrideScope scope, bool exists) {
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }

            if (IsCommonPlatformId(scope.PlatformId)) {
                return;
            }

            bool parentExists = scope.IsPlatformOnly
                ? true
                : ResolveExists(saveComponent, new EditorOverrideScope(scope.PlatformId));
            if (exists == parentExists) {
                saveComponent.RemoveExistencePlatformOverride(scope);
                ExistenceChanged?.Invoke();
                return;
            }

            saveComponent.SetExistencePlatformOverride(scope, new SceneEntityPlatformExistenceOverrideAsset {
                PlatformId = scope.PlatformId,
                EnvironmentId = scope.EnvironmentId,
                Exists = exists
            });
            ExistenceChanged?.Invoke();
        }

        /// <summary>
        /// Removes all existence-changed subscribers between tests or editor shutdown.
        /// </summary>
        public static void ResetExistenceChangedSubscribers() {
            ExistenceChanged = null;
        }

        /// <summary>
        /// Returns whether the supplied platform id points at shared common state.
        /// </summary>
        /// <param name="platformId">Platform identifier to inspect.</param>
        /// <returns>True when the identifier points at shared common state.</returns>
        static bool IsCommonPlatformId(string platformId) {
            return string.IsNullOrWhiteSpace(platformId)
                || string.Equals(platformId, CommonPlatformId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Normalizes one platform identifier for case-insensitive comparisons and dictionary lookups.
        /// </summary>
        /// <param name="platformId">Platform identifier to normalize.</param>
        /// <returns>Normalized platform identifier.</returns>
        static string NormalizePlatformId(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                return CommonPlatformId;
            }

            return platformId.Trim();
        }
    }
}
