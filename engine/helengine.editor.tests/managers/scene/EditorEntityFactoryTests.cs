using helengine.editor.tests.testing;
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

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
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
        /// Ensures editor factory creation returns an editor entity with the hidden authored-scene metadata.
        /// </summary>
        [Fact]
        public void Create_ReturnsEditorEntityWithHiddenAuthoringComponents() {
            IEntityFactory factory = new EditorEntityFactory();

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
