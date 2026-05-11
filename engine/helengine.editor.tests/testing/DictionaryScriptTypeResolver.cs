namespace helengine.editor.tests.testing {
    /// <summary>
    /// Resolves persisted script type ids through one explicit in-memory lookup table.
    /// </summary>
    internal sealed class DictionaryScriptTypeResolver : IScriptTypeResolver {
        /// <summary>
        /// Runtime types keyed by their persisted assembly-qualified script type ids.
        /// </summary>
        readonly Dictionary<string, Type> TypesById;

        /// <summary>
        /// Initializes one empty explicit script-type lookup table.
        /// </summary>
        public DictionaryScriptTypeResolver() {
            TypesById = new Dictionary<string, Type>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Registers one runtime type for the supplied persisted script type id.
        /// </summary>
        /// <param name="assemblyQualifiedTypeName">Persisted script type id.</param>
        /// <param name="type">Runtime type that should satisfy the id.</param>
        public void Register(string assemblyQualifiedTypeName, Type type) {
            if (string.IsNullOrWhiteSpace(assemblyQualifiedTypeName)) {
                throw new ArgumentException("Assembly-qualified type name must be provided.", nameof(assemblyQualifiedTypeName));
            } else if (type == null) {
                throw new ArgumentNullException(nameof(type));
            }

            TypesById[assemblyQualifiedTypeName] = type;
        }

        /// <summary>
        /// Resolves one persisted script type id through the explicit lookup table.
        /// </summary>
        /// <param name="assemblyQualifiedTypeName">Persisted script type id.</param>
        /// <returns>Registered runtime type.</returns>
        public Type Resolve(string assemblyQualifiedTypeName) {
            if (string.IsNullOrWhiteSpace(assemblyQualifiedTypeName)) {
                throw new ArgumentException("Assembly-qualified type name must be provided.", nameof(assemblyQualifiedTypeName));
            }
            if (!TypesById.TryGetValue(assemblyQualifiedTypeName, out Type type)) {
                throw new InvalidOperationException($"Script type '{assemblyQualifiedTypeName}' was not registered for this test.");
            }

            return type;
        }
    }
}
