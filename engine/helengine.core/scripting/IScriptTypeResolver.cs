namespace helengine {
    /// <summary>
    /// Resolves persisted script type names against the currently loaded module assemblies.
    /// </summary>
    public interface IScriptTypeResolver {
        /// <summary>
        /// Resolves one assembly-qualified script type name against the loaded module assemblies.
        /// </summary>
        /// <param name="assemblyQualifiedTypeName">Assembly-qualified script type name.</param>
        /// <returns>Resolved script type.</returns>
        Type Resolve(string assemblyQualifiedTypeName);
    }
}
