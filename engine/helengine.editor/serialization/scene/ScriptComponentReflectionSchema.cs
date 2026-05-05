namespace helengine.editor {
    /// <summary>
    /// Captures the deterministic reflected persistence schema for one scripted component type.
    /// </summary>
    public sealed class ScriptComponentReflectionSchema {
        /// <summary>
        /// Initializes one reflected script-component schema.
        /// </summary>
        /// <param name="componentType">Scripted component type described by the schema.</param>
        /// <param name="members">Deterministically ordered persisted members.</param>
        public ScriptComponentReflectionSchema(Type componentType, IReadOnlyList<ScriptComponentReflectionMember> members) {
            if (componentType == null) {
                throw new ArgumentNullException(nameof(componentType));
            }
            if (members == null) {
                throw new ArgumentNullException(nameof(members));
            }

            ComponentType = componentType;
            Members = members;
        }

        /// <summary>
        /// Gets the scripted component type described by the schema.
        /// </summary>
        public Type ComponentType { get; }

        /// <summary>
        /// Gets the deterministically ordered persisted members.
        /// </summary>
        public IReadOnlyList<ScriptComponentReflectionMember> Members { get; }
    }
}
