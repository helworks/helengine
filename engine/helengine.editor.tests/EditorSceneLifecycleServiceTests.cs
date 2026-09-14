using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the scene-lifecycle service binds editor scenes to the active physics runtime
    /// and refuses runtimes that cannot own an editor scene instead of silently skipping them.
    /// </summary>
    public sealed class EditorSceneLifecycleServiceTests {
        /// <summary>
        /// Creates one scene-lifecycle service bound to a throwaway project root.
        /// </summary>
        /// <returns>Scene-lifecycle service under test.</returns>
        static EditorSceneLifecycleService CreateService() {
            return new EditorSceneLifecycleService(new EditorProjectSceneCatalogService(Path.GetTempPath()));
        }

        /// <summary>
        /// Ensures a scene-bindable runtime receives the exact root entities supplied by the caller.
        /// </summary>
        [Fact]
        public void BindSceneToPhysicsRuntime_WhenRuntimeIsSceneBindable_BindsSuppliedRoots() {
            EditorSceneLifecycleService service = CreateService();
            RecordingSceneBindablePhysicsRuntime runtime = new RecordingSceneBindablePhysicsRuntime();
            Entity[] rootEntities = Array.Empty<Entity>();

            service.BindSceneToPhysicsRuntime(runtime, rootEntities);

            Assert.Same(rootEntities, runtime.LastBoundRootEntities);
        }

        /// <summary>
        /// Ensures no physics runtime at all remains a supported editor configuration.
        /// </summary>
        [Fact]
        public void BindSceneToPhysicsRuntime_WhenNoRuntimeIsAttached_DoesNothing() {
            EditorSceneLifecycleService service = CreateService();

            service.BindSceneToPhysicsRuntime(null, Array.Empty<Entity>());
        }

        /// <summary>
        /// Ensures an attached runtime that cannot bind editor scenes fails loudly rather than
        /// leaving the editor with an unbound scene that silently never simulates.
        /// </summary>
        [Fact]
        public void BindSceneToPhysicsRuntime_WhenRuntimeIsNotSceneBindable_Throws() {
            EditorSceneLifecycleService service = CreateService();
            NonBindablePhysicsRuntime runtime = new NonBindablePhysicsRuntime();

            Assert.Throws<InvalidCastException>(() => service.BindSceneToPhysicsRuntime(runtime, Array.Empty<Entity>()));
        }

        /// <summary>
        /// Ensures scene binding refuses a null hierarchy, because an unbound scene must never be
        /// mistaken for an empty one.
        /// </summary>
        [Fact]
        public void BindSceneToPhysicsRuntime_WhenRootEntitiesAreMissing_Throws() {
            EditorSceneLifecycleService service = CreateService();

            Assert.Throws<ArgumentNullException>(() => service.BindSceneToPhysicsRuntime(new RecordingSceneBindablePhysicsRuntime(), null));
        }
    }
}
