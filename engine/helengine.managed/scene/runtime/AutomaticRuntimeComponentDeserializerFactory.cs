namespace helengine {
    /// <summary>
    /// Supplies managed hosts with reflected component readers for persisted types beyond explicit portable registrations.
    /// </summary>
    public sealed class AutomaticRuntimeComponentDeserializerFactory : IRuntimeComponentDeserializerFactory {
        /// <summary>
        /// Resolves the persisted component type and transfers a new reflected reader to the requesting registry.
        /// </summary>
        /// <param name="componentTypeId">Stable serialized component type identifier.</param>
        /// <returns>An owned reader for a resolved component type, or null when the identifier is blank, unresolved or not a component.</returns>
        [NativeOwnedReturn]
        public IRuntimeComponentDeserializer Create(string componentTypeId) {
            if (string.IsNullOrWhiteSpace(componentTypeId)) {
                return null;
            }

            Type componentType = PersistedComponentTypeResolver.TryResolve(componentTypeId);
            if (componentType == null) {
                return null;
            }
            if (!typeof(Component).IsAssignableFrom(componentType)) {
                return null;
            }

            return new AutomaticScriptComponentRuntimeDeserializer(componentTypeId, componentType);
        }
    }
}
