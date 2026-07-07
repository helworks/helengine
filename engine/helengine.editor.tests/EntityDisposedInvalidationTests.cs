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
                ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath)
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
        public void AddChild_WhenReceiverEntityWasDisposed_ThrowsInvalidOperationException() {
            Entity parent = CreateInitializedEntity();
            Entity child = CreateInitializedEntity();

            parent.Dispose();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => parent.AddChild(child));
            Assert.Contains("Disposed entities cannot be used.", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures disposed entities reject externally visible transform reads.
        /// </summary>
        [Fact]
        public void PositionProperty_WhenEntityWasDisposed_ThrowsInvalidOperationException() {
            Entity parent = CreateInitializedEntity();
            Entity child = CreateInitializedEntity();
            parent.AddChild(child);

            child.Dispose();

            Assert.Throws<InvalidOperationException>(() => _ = child.Position);
        }

        /// <summary>
        /// Ensures disposed components reject later public property access.
        /// </summary>
        [Fact]
        public void IsEditorUpdateExecutionSuppressionMarker_WhenComponentWasDisposed_ThrowsInvalidOperationException() {
            Entity entity = CreateInitializedEntity();
            ProbeComponent component = new ProbeComponent();
            entity.AddComponent(component);

            entity.RemoveComponent(component);
            component.Dispose();

            Assert.Throws<InvalidOperationException>(() => _ = component.IsEditorUpdateExecutionSuppressionMarker);
        }

        /// <summary>
        /// Ensures disposed components cannot be reattached.
        /// </summary>
        [Fact]
        public void AddComponent_WhenSuppliedComponentWasDisposed_ThrowsInvalidOperationException() {
            Entity firstEntity = CreateInitializedEntity();
            Entity secondEntity = CreateInitializedEntity();
            ProbeComponent component = new ProbeComponent();
            firstEntity.AddComponent(component);

            firstEntity.RemoveComponent(component);
            component.Dispose();

            Assert.Throws<InvalidOperationException>(() => secondEntity.AddComponent(component));
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
            /// <summary>
            /// Exposes one public property path that should reject access after disposal.
            /// </summary>
            public override bool IsEditorUpdateExecutionSuppressionMarker {
                get {
                    ThrowIfDisposed();
                    return false;
                }
            }
        }
    }
}

