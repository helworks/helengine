namespace helengine {
    /// <summary>
    /// Lets a host provide component readers beyond the portable runtime's explicit registrations.
    /// </summary>
    public interface IRuntimeComponentDeserializerFactory {
        /// <summary>
        /// Creates an owned reader for a serialized component type supported by this host.
        /// </summary>
        /// <param name="componentTypeId">Stable serialized component type identifier.</param>
        /// <returns>A new reader transferred to the registry, or null when the type is unsupported.</returns>
        [NativeOwnedReturn]
        IRuntimeComponentDeserializer Create(string componentTypeId);
    }
}
