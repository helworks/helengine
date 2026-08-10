namespace helengine {
    /// <summary>
    /// Stores override payloads in platform-keyed containers with environment entries nested beneath each platform.
    /// </summary>
    /// <typeparam name="T">Override payload type.</typeparam>
    internal sealed class EditorOverrideScopeMap<T> {
        readonly Dictionary<string, Dictionary<string, T>> ValuesByPlatformId;

        /// <summary>
        /// Initializes an empty nested scope map.
        /// </summary>
        public EditorOverrideScopeMap() {
            ValuesByPlatformId = new Dictionary<string, Dictionary<string, T>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Stores one payload at the supplied scope.
        /// </summary>
        public void Set(EditorOverrideScope scope, T value) {
            GetOrCreatePlatformValues(scope.PlatformId)[scope.EnvironmentId] = value;
        }

        /// <summary>
        /// Gets or creates one payload at the supplied scope.
        /// </summary>
        public T GetOrCreate(EditorOverrideScope scope, Func<T> valueFactory) {
            Dictionary<string, T> values = GetOrCreatePlatformValues(scope.PlatformId);
            if (!values.TryGetValue(scope.EnvironmentId, out T value)) {
                value = valueFactory();
                values.Add(scope.EnvironmentId, value);
            }

            return value;
        }

        /// <summary>
        /// Attempts to resolve one payload at the supplied scope.
        /// </summary>
        public bool TryGet(EditorOverrideScope scope, out T value) {
            if (ValuesByPlatformId.TryGetValue(scope.PlatformId, out Dictionary<string, T> values)) {
                return values.TryGetValue(scope.EnvironmentId, out value);
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Removes one payload at the supplied scope.
        /// </summary>
        public bool Remove(EditorOverrideScope scope) {
            if (!ValuesByPlatformId.TryGetValue(scope.PlatformId, out Dictionary<string, T> values)) {
                return false;
            }

            bool removed = values.Remove(scope.EnvironmentId);
            if (values.Count == 0) {
                ValuesByPlatformId.Remove(scope.PlatformId);
            }

            return removed;
        }

        /// <summary>
        /// Enumerates every payload in deterministic platform-then-environment insertion order.
        /// </summary>
        public IEnumerable<T> EnumerateValues() {
            foreach (Dictionary<string, T> values in ValuesByPlatformId.Values) {
                foreach (T value in values.Values) {
                    yield return value;
                }
            }
        }

        Dictionary<string, T> GetOrCreatePlatformValues(string platformId) {
            if (!ValuesByPlatformId.TryGetValue(platformId, out Dictionary<string, T> values)) {
                values = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
                ValuesByPlatformId.Add(platformId, values);
            }

            return values;
        }
    }
}
