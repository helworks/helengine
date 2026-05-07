namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Minimal script type resolver used to emulate project-script type lookup from collectible gameplay assemblies.
    /// </summary>
    public sealed class FakeScriptTypeResolver : IScriptTypeResolver {
        /// <summary>
        /// Resolved script type returned for every lookup.
        /// </summary>
        readonly Type ResolvedType;

        /// <summary>
        /// Initializes one fake resolver that always returns the supplied type.
        /// </summary>
        /// <param name="resolvedType">Script component type returned for every lookup.</param>
        public FakeScriptTypeResolver(Type resolvedType) {
            ResolvedType = resolvedType ?? throw new ArgumentNullException(nameof(resolvedType));
        }

        /// <summary>
        /// Resolves one script type name to the configured script component type.
        /// </summary>
        /// <param name="assemblyQualifiedTypeName">Assembly-qualified script type name.</param>
        /// <returns>The configured resolved script type.</returns>
        public Type Resolve(string assemblyQualifiedTypeName) {
            if (string.IsNullOrWhiteSpace(assemblyQualifiedTypeName)) {
                throw new ArgumentException("Assembly-qualified type name must be provided.", nameof(assemblyQualifiedTypeName));
            }

            return ResolvedType;
        }
    }
}
