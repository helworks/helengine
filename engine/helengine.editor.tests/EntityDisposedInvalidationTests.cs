using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies disposed runtime entities and components fail fast instead of behaving like live scene objects.
    /// </summary>
    public sealed class EntityDisposedInvalidationTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the runtime harness.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes one deterministic runtime core for ownership-lifetime tests.
        /// </summary>
        public EntityDisposedInvalidationTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-entity-disposed-invalidation-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            TestClockDrivenCore core = new TestClockDrivenCore(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });

            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Deletes temporary test content after each run.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures disposed entities reject later scene-graph mutation.
        /// </summary>
        [Fact]
        public void AddChild_WhenReceiverEntityWasDisposed_ThrowsObjectDisposedException() {
            Entity parent = CreateInitializedEntity();
            Entity child = CreateInitializedEntity();

            parent.Dispose();

            ObjectDisposedException exception = Assert.Throws<ObjectDisposedException>(() => parent.AddChild(child));
            Assert.Contains(nameof(Entity), exception.ObjectName, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures disposed entities reject externally visible state reads.
        /// </summary>
        [Fact]
        public void ParentProperty_WhenEntityWasDisposed_ThrowsObjectDisposedException() {
            Entity parent = CreateInitializedEntity();
            Entity child = CreateInitializedEntity();
            parent.AddChild(child);

            child.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _ = child.Parent);
        }

        /// <summary>
        /// Ensures disposed components reject later parent access.
        /// </summary>
        [Fact]
        public void ParentProperty_WhenComponentWasDisposed_ThrowsObjectDisposedException() {
            Entity entity = CreateInitializedEntity();
            ProbeComponent component = new ProbeComponent();
            entity.AddComponent(component);

            entity.RemoveComponent(component);
            component.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _ = component.Parent);
        }

        /// <summary>
        /// Ensures disposed components cannot be reattached.
        /// </summary>
        [Fact]
        public void AddComponent_WhenSuppliedComponentWasDisposed_ThrowsObjectDisposedException() {
            Entity firstEntity = CreateInitializedEntity();
            Entity secondEntity = CreateInitializedEntity();
            ProbeComponent component = new ProbeComponent();
            firstEntity.AddComponent(component);

            firstEntity.RemoveComponent(component);
            component.Dispose();

            Assert.Throws<ObjectDisposedException>(() => secondEntity.AddComponent(component));
        }

        /// <summary>
        /// Creates one initialized entity ready for runtime lifecycle participation.
        /// </summary>
        /// <returns>Initialized entity with component and child collections.</returns>
        static Entity CreateInitializedEntity() {
            Entity entity = new Entity();
            entity.InitChildren();
            entity.InitComponents();
            entity.InitializeHierarchy();
            return entity;
        }

        /// <summary>
        /// Minimal lifecycle probe used by disposed-component tests.
        /// </summary>
        sealed class ProbeComponent : Component {
        }
    }
}
