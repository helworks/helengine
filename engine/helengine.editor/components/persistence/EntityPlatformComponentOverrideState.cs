namespace helengine {
    /// <summary>
    /// Stores one platform's removed common components and detached platform-only components for an entity.
    /// </summary>
    public class EntityPlatformComponentOverrideState {
        /// <summary>
        /// Removed common component keys keyed by stable component key.
        /// </summary>
        readonly HashSet<string> RemovedComponentKeys;

        /// <summary>
        /// Platform-only added components keyed by stable component key.
        /// </summary>
        readonly Dictionary<string, EntityPlatformAddedComponentState> AddedComponentsByKey;

        /// <summary>
        /// Initializes a new empty component-existence override state container.
        /// </summary>
        public EntityPlatformComponentOverrideState() {
            RemovedComponentKeys = new HashSet<string>(StringComparer.Ordinal);
            AddedComponentsByKey = new Dictionary<string, EntityPlatformAddedComponentState>(StringComparer.Ordinal);
            PlatformId = string.Empty;
        }

        /// <summary>
        /// Gets or sets the platform identifier that owns the component existence overrides.
        /// </summary>
        public string PlatformId { get; set; }

        /// <summary>
        /// Gets a value indicating whether any removed common components are tracked for the platform.
        /// </summary>
        public bool HasRemovedComponents => RemovedComponentKeys.Count > 0;

        /// <summary>
        /// Gets a value indicating whether any platform-only added components are tracked for the platform.
        /// </summary>
        public bool HasAddedComponents => AddedComponentsByKey.Count > 0;

        /// <summary>
        /// Gets a value indicating whether this platform currently stores any component existence override data.
        /// </summary>
        public bool HasAnyOverrides => HasRemovedComponents || HasAddedComponents;

        /// <summary>
        /// Marks one common component key as removed for the platform.
        /// </summary>
        /// <param name="componentKey">Stable common component key to remove on the platform.</param>
        public void MarkComponentRemoved(string componentKey) {
            if (string.IsNullOrWhiteSpace(componentKey)) {
                throw new ArgumentException("Component key must be provided.", nameof(componentKey));
            }

            RemovedComponentKeys.Add(componentKey);
        }

        /// <summary>
        /// Clears one removed common component key from the platform.
        /// </summary>
        /// <param name="componentKey">Stable common component key to restore on the platform.</param>
        public void RestoreRemovedComponent(string componentKey) {
            if (string.IsNullOrWhiteSpace(componentKey)) {
                throw new ArgumentException("Component key must be provided.", nameof(componentKey));
            }

            RemovedComponentKeys.Remove(componentKey);
        }

        /// <summary>
        /// Returns whether one common component key is marked as removed for the platform.
        /// </summary>
        /// <param name="componentKey">Stable common component key to query.</param>
        /// <returns>True when the common component is removed for the platform.</returns>
        public bool IsComponentRemoved(string componentKey) {
            if (string.IsNullOrWhiteSpace(componentKey)) {
                throw new ArgumentException("Component key must be provided.", nameof(componentKey));
            }

            return RemovedComponentKeys.Contains(componentKey);
        }

        /// <summary>
        /// Adds or replaces one detached platform-only component state.
        /// </summary>
        /// <param name="addedComponentState">Detached platform-only component state to store.</param>
        public void SetAddedComponent(EntityPlatformAddedComponentState addedComponentState) {
            if (addedComponentState == null) {
                throw new ArgumentNullException(nameof(addedComponentState));
            }
            if (string.IsNullOrWhiteSpace(addedComponentState.ComponentKey)) {
                throw new InvalidOperationException("Added component states must define a component key.");
            }
            if (addedComponentState.Component == null) {
                throw new InvalidOperationException("Added component states must define a detached component.");
            }
            if (addedComponentState.SaveState == null) {
                throw new InvalidOperationException("Added component states must define a save state.");
            }

            AddedComponentsByKey[addedComponentState.ComponentKey] = addedComponentState;
        }

        /// <summary>
        /// Attempts to resolve one detached platform-only component state by stable key.
        /// </summary>
        /// <param name="componentKey">Stable component key to resolve.</param>
        /// <param name="addedComponentState">Resolved detached component state when one exists.</param>
        /// <returns>True when the platform owns one detached component for the supplied key.</returns>
        public bool TryGetAddedComponent(string componentKey, out EntityPlatformAddedComponentState addedComponentState) {
            if (string.IsNullOrWhiteSpace(componentKey)) {
                throw new ArgumentException("Component key must be provided.", nameof(componentKey));
            }

            return AddedComponentsByKey.TryGetValue(componentKey, out addedComponentState);
        }

        /// <summary>
        /// Removes one detached platform-only component state by stable key.
        /// </summary>
        /// <param name="componentKey">Stable component key to remove.</param>
        public void RemoveAddedComponent(string componentKey) {
            if (string.IsNullOrWhiteSpace(componentKey)) {
                throw new ArgumentException("Component key must be provided.", nameof(componentKey));
            }

            AddedComponentsByKey.Remove(componentKey);
        }

        /// <summary>
        /// Enumerates the stable keys for common components removed on the platform.
        /// </summary>
        /// <returns>Stable common component keys removed on the platform.</returns>
        public IEnumerable<string> EnumerateRemovedComponentKeys() {
            return RemovedComponentKeys;
        }

        /// <summary>
        /// Enumerates the detached platform-only components added on the platform.
        /// </summary>
        /// <returns>Detached platform-only component states.</returns>
        public IEnumerable<EntityPlatformAddedComponentState> EnumerateAddedComponents() {
            return AddedComponentsByKey.Values;
        }
    }
}
