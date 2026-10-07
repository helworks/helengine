using helengine;

namespace helengine.core.tests {
    /// <summary>
    /// Verifies mesh preparation supports the optional entity collections used by generated UI hierarchies.
    /// </summary>
    public sealed class RuntimeMeshPreparationServiceTests {
        /// <summary>
        /// Ensures visiting an empty entity succeeds without allocating collections or tracking nonexistent models.
        /// </summary>
        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void Prepare_EmptyEntity_SupportsOptionalCollections(bool initializeComponents, bool initializeChildren) {
            Entity entity = new Entity(CreateInitializedCore());
            if (initializeComponents) {
                entity.InitComponents();
            }
            if (initializeChildren) {
                entity.InitChildren();
            }
            RuntimeMeshPreparationService service = new RuntimeMeshPreparationService();

            service.Prepare(entity, _ => Assert.Fail("An empty entity cannot create a prepared model."));

            Assert.Equal(initializeComponents, entity.Components != null);
            Assert.Equal(initializeChildren, entity.Children != null);
        }

        /// <summary>
        /// Ensures an entity without components still visits child meshes and preserves their validation failures.
        /// </summary>
        [Fact]
        public void Prepare_ParentWithoutComponents_StillPreparesChildMeshes() {
            Core core = CreateInitializedCore();
            Entity parent = new Entity(core);
            parent.InitChildren();
            Entity child = new Entity(core);
            child.InitComponents();
            MeshComponent mesh = new MeshComponent();
            mesh.SetSyntheticBooleanMember("MeshBakeScale", true);
            mesh.SetSyntheticBooleanMember("MeshBakeScaleAtCookTime", false);
            child.AddComponent(mesh);
            parent.AddChild(child);

            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
                new RuntimeMeshPreparationService().Prepare(parent, _ => { }));

            Assert.Equal("Load-time mesh preparation requires a runtime model with retained raw geometry.", failure.Message);
        }

        /// <summary>
        /// Creates a headless core with an object manager so test entities register through normal ownership.
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
