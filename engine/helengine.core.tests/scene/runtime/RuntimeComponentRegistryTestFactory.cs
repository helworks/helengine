namespace helengine.core.tests.scene.runtime {
    /// <summary>Records fallback requests and optionally creates portable readers so registry tests can detect repeated factory calls.</summary>
    internal sealed class RuntimeComponentRegistryTestFactory : IRuntimeComponentDeserializerFactory {
        /// <summary>Controls whether each request creates a reader or reports an unsupported component.</summary>
        readonly bool CreatesReaders;

        /// <summary>Configures successful-reader or unsupported-component behavior without resolving reflected types.</summary>
        /// <param name="createsReaders">True to create a distinct reader on every call; false to return null.</param>
        public RuntimeComponentRegistryTestFactory(bool createsReaders) {
            CreatesReaders = createsReaders;
        }

        /// <summary>Gets the number of fallback reader requests received from the registry.</summary>
        public int CreateCount { get; private set; }

        /// <summary>Gets the last requested serialized component identity, or null before the first request.</summary>
        public string LastRequestedTypeId { get; private set; }

        /// <summary>Records one request and creates a new registry-owned reader when this fixture supports the request.</summary>
        /// <param name="componentTypeId">Serialized component identity requested by the registry.</param>
        /// <returns>A new portable fixture reader, or null when the fixture reports unsupported types.</returns>
        [NativeOwnedReturn]
        public IRuntimeComponentDeserializer Create(string componentTypeId) {
            CreateCount++;
            LastRequestedTypeId = componentTypeId;
            return CreatesReaders ? new ScenePreparationProbeDeserializer(componentTypeId) : null;
        }
    }
}
