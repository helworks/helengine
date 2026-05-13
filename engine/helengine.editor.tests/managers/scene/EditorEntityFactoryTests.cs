using helengine.editor.tests.testing;
using helengine.ui;
using Xunit;

namespace helengine.editor.tests.managers.scene {
    /// <summary>
    /// Verifies authored-entity creation in editor hosts.
    /// </summary>
    public class EditorEntityFactoryTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the lightweight core harness.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required to construct authored editor entities.
        /// </summary>
        public EditorEntityFactoryTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-entity-factory-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            EditorCore core = new EditorCore(new Project {
                Name = "Editor Entity Factory",
                Path = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"), new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
        }

        /// <summary>
        /// Deletes the temporary content root after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures initialized editor cores expose the editor-authored entity factory through the core surface.
        /// </summary>
        [Fact]
        public void Initialize_PopulatesEntityFactoryWithEditorFactory() {
            Assert.NotNull(Core.Instance);
            Assert.NotNull(Core.Instance.EntityFactory);
            Assert.IsType<EditorEntityFactory>(Core.Instance.EntityFactory);
        }

        /// <summary>
        /// Ensures editor factory creation returns an editor entity with the hidden authored-scene metadata.
        /// </summary>
        [Fact]
        public void Create_ReturnsEditorEntityWithHiddenAuthoringComponents() {
            IEntityFactory factory = Core.Instance.EntityFactory;

            Entity entity = factory.Create("Authored");

            Assert.IsType<EditorEntity>(entity);
            Assert.Contains(entity.Components, component => component is EntitySaveComponent);
            Assert.Contains(entity.Components, component => component is EditorUpdateExecutionSuppressionComponent);
            Assert.Equal(float3.Zero, entity.LocalPosition);
            Assert.Equal(float3.One, entity.LocalScale);
            Assert.Equal(float4.Identity, entity.LocalOrientation);
        }

        /// <summary>
        /// Ensures child creation parents the editor entity before returning it.
        /// </summary>
        [Fact]
        public void CreateChild_ParentsChildBeforeReturning() {
            IEntityFactory factory = new EditorEntityFactory();
            EditorEntity parent = Assert.IsType<EditorEntity>(factory.Create("Parent"));
            parent.InitChildren();

            Entity child = factory.CreateChild(parent, "Child");

            Assert.IsType<EditorEntity>(child);
            Assert.Same(parent, child.Parent);
            Assert.Contains(child, parent.Children);
        }
    }
}
