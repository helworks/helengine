namespace helengine {
    /// <summary>
    /// Stores override payloads keyed by scope path and answers deepest-authored-prefix lookups.
    /// </summary>
    /// <typeparam name="T">Override payload type.</typeparam>
    internal sealed class EditorOverrideScopeMap<T> {
        readonly Dictionary<EditorOverrideScope, T> ValuesByScope;

        /// <summary>
        /// Initializes an empty map.
        /// </summary>
        public EditorOverrideScopeMap() {
            ValuesByScope = new Dictionary<EditorOverrideScope, T>();
        }

        /// <summary>Stores one payload at the supplied scope.</summary>
        public void Set(EditorOverrideScope scope, T value) {
            ValuesByScope[scope] = value;
        }

        /// <summary>Gets or creates one payload at the supplied scope.</summary>
        public T GetOrCreate(EditorOverrideScope scope, Func<T> valueFactory) {
            if (!ValuesByScope.TryGetValue(scope, out T value)) {
                value = valueFactory();
                ValuesByScope.Add(scope, value);
            }

            return value;
        }

        /// <summary>Attempts to resolve the payload authored exactly at the supplied scope.</summary>
        public bool TryGet(EditorOverrideScope scope, out T value) {
            return ValuesByScope.TryGetValue(scope, out value);
        }

        /// <summary>
        /// Resolves the payload whose scope is the longest prefix of <paramref name="target"/>, Common included.
        /// </summary>
        public bool TryGetDeepestPrefix(EditorOverrideScope target, out T value, out EditorOverrideScope matched) {
            bool found = false;
            int bestDepth = -1;
            value = default;
            matched = EditorOverrideScope.Common;
            foreach (KeyValuePair<EditorOverrideScope, T> entry in ValuesByScope) {
                if (entry.Key.Depth <= bestDepth || !entry.Key.IsPrefixOf(target)) {
                    continue;
                }

                found = true;
                bestDepth = entry.Key.Depth;
                value = entry.Value;
                matched = entry.Key;
            }

            return found;
        }

        /// <summary>Removes one payload.</summary>
        public bool Remove(EditorOverrideScope scope) {
            return ValuesByScope.Remove(scope);
        }

        /// <summary>Enumerates every payload in unspecified order.</summary>
        public IEnumerable<T> EnumerateValues() {
            return ValuesByScope.Values;
        }

        /// <summary>Enumerates every authored scope in unspecified order.</summary>
        public IEnumerable<EditorOverrideScope> EnumerateScopes() {
            return ValuesByScope.Keys;
        }
    }
}
