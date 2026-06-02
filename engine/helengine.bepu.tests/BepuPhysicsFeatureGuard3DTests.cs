namespace helengine.bepu.tests {
    /// <summary>
    /// Verifies unsupported physics features fail explicitly during migration.
    /// </summary>
    public sealed class BepuPhysicsFeatureGuard3DTests : IDisposable {
        /// <summary>
        /// Initializes the minimal core services required for entity-backed guard tests.
        /// </summary>
        public BepuPhysicsFeatureGuard3DTests() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Ends one guard test lifecycle.
        /// </summary>
        public void Dispose() {
        }

        /// <summary>
        /// Ensures capsule colliders are rejected in the first replacement pass.
        /// </summary>
        [Fact]
        public void ValidateSupportedCollider_WithCapsuleCollider_ThrowsNotSupportedException() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.AddComponent(new CapsuleCollider3DComponent());

            Assert.Throws<NotSupportedException>(() => BepuPhysicsFeatureGuard3D.ValidateEntity(entity));
        }
    }
}
