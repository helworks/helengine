namespace helengine.core.tests.scene.runtime {
    /// <summary>Verifies portable registry strictness, fallback caching and explicit-reader precedence without managed reflection.</summary>
    public sealed class RuntimeComponentRegistryFactoryTests {
        /// <summary>Uses a fixture identity absent from all built-in and generated component registrations.</summary>
        const string UnregisteredComponentTypeId = "tests.registry.unregistered-component";

        /// <summary>Default portable registries fail explicitly when no host supplies a reader for an unknown component.</summary>
        [Fact]
        public void CreateDefault_WithoutFactory_RejectsUnknownComponentTypeId() {
            using RuntimeComponentRegistry registry = RuntimeComponentRegistry.CreateDefault();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => registry.GetDeserializer(UnregisteredComponentTypeId));

            Assert.Contains(UnregisteredComponentTypeId, exception.Message);
            Assert.False(registry.TryGet(UnregisteredComponentTypeId, out _));
        }

        /// <summary>A host returning no reader leaves the requested component unsupported rather than registering a null entry.</summary>
        [Fact]
        public void CreateDefault_WhenFactoryReturnsNull_RejectsUnknownComponentTypeId() {
            RuntimeComponentRegistryTestFactory factory = new RuntimeComponentRegistryTestFactory(false);
            using RuntimeComponentRegistry registry = RuntimeComponentRegistry.CreateDefault(factory);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => registry.GetDeserializer(UnregisteredComponentTypeId));

            Assert.Contains(UnregisteredComponentTypeId, exception.Message);
            Assert.Equal(1, factory.CreateCount);
            Assert.Equal(UnregisteredComponentTypeId, factory.LastRequestedTypeId);
            Assert.False(registry.TryGet(UnregisteredComponentTypeId, out _));
        }

        /// <summary>A successful fallback transfers one reader to the registry and subsequent lookups borrow that same cached reader.</summary>
        [Fact]
        public void GetDeserializer_WithFallbackReader_CreatesOnceAndCachesReader() {
            RuntimeComponentRegistryTestFactory factory = new RuntimeComponentRegistryTestFactory(true);
            using RuntimeComponentRegistry registry = new RuntimeComponentRegistry(factory);

            IRuntimeComponentDeserializer firstReader = registry.GetDeserializer(UnregisteredComponentTypeId);
            IRuntimeComponentDeserializer secondReader = registry.GetDeserializer(UnregisteredComponentTypeId);

            Assert.NotNull(firstReader);
            Assert.Equal(UnregisteredComponentTypeId, firstReader.ComponentTypeId);
            Assert.Same(firstReader, secondReader);
            Assert.True(registry.TryGet(UnregisteredComponentTypeId, out IRuntimeComponentDeserializer cachedReader));
            Assert.Same(firstReader, cachedReader);
            Assert.Equal(1, factory.CreateCount);
            Assert.Equal(UnregisteredComponentTypeId, factory.LastRequestedTypeId);
        }

        /// <summary>Explicit registration satisfies lookups before the host fallback factory is consulted.</summary>
        [Fact]
        public void GetDeserializer_WithExplicitReader_DoesNotCallFallbackFactory() {
            RuntimeComponentRegistryTestFactory factory = new RuntimeComponentRegistryTestFactory(true);
            using RuntimeComponentRegistry registry = RuntimeComponentRegistry.CreateDefault(factory);
            IRuntimeComponentDeserializer explicitReader = new ScenePreparationProbeDeserializer(UnregisteredComponentTypeId);
            registry.Register(explicitReader);

            IRuntimeComponentDeserializer resolvedReader = registry.GetDeserializer(UnregisteredComponentTypeId);

            Assert.Same(explicitReader, resolvedReader);
            Assert.Equal(0, factory.CreateCount);
            Assert.Null(factory.LastRequestedTypeId);
        }
    }
}
