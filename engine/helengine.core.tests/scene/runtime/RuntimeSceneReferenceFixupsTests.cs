using helengine;

namespace helengine.core.tests.scene.runtime {
    /// <summary>
    /// Verifies reference binding traverses generated UI leaves while preserving missing and ambiguous id errors.
    /// </summary>
    public sealed class RuntimeSceneReferenceFixupsTests {
        /// <summary>
        /// Ensures a component-free parent and a child without a child collection still bind an authored reference.
        /// </summary>
        [Fact]
        public void Bind_LeafWithoutChildren_ResolvesReference() {
            Core core = CreateInitializedCore();
            Entity root = new Entity(core);
            root.InitChildren();
            Entity target = CreateLeaf(core, 42u);
            root.AddChild(target);
            SceneEntityReference reference = new SceneEntityReference { EntityId = 42u };
            using RuntimeSceneReferenceFixups fixups = new RuntimeSceneReferenceFixups();
            fixups.Track(reference, "test.component");

            fixups.Bind(new List<Entity> { root });

            Assert.Same(target, reference.ResolvedEntity);
            Assert.Null(target.Children);
        }

        /// <summary>
        /// Ensures skipping nonexistent children does not hide a reference to a missing scene entity.
        /// </summary>
        [Fact]
        public void Bind_MissingId_StillThrows() {
            using RuntimeSceneReferenceFixups fixups = new RuntimeSceneReferenceFixups();
            fixups.Track(new SceneEntityReference { EntityId = 42u }, "test.component");

            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
                fixups.Bind(new List<Entity> { new Entity(CreateInitializedCore()) }));

            Assert.Contains("missing scene entity id 42", failure.Message);
        }

        /// <summary>
        /// Ensures references remain invalid when multiple leaf entities share an authored id.
        /// </summary>
        [Fact]
        public void Bind_DuplicateLeafId_StillThrows() {
            Core core = CreateInitializedCore();
            using RuntimeSceneReferenceFixups fixups = new RuntimeSceneReferenceFixups();
            fixups.Track(new SceneEntityReference { EntityId = 42u }, "test.component");

            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
                fixups.Bind(new List<Entity> { CreateLeaf(core, 42u), CreateLeaf(core, 42u) }));

            Assert.Contains("ambiguous scene entity id 42", failure.Message);
        }

        /// <summary>
        /// Creates a leaf with identity metadata and deliberately leaves its unused child collection uninitialized.
        /// </summary>
        static Entity CreateLeaf(Core core, uint entityId) {
            Entity entity = new Entity(core);
            entity.InitComponents();
            entity.AddComponent(new SceneEntityRuntimeIdComponent { SceneEntityId = entityId });
            return entity;
        }

        /// <summary>
        /// Creates the headless object manager required for normal entity registration.
        /// </summary>
        static Core CreateInitializedCore() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            return core;
        }
    }
}
