using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.scene {
    /// <summary>
    /// Verifies authored-entity creation in non-editor hosts.
    /// </summary>
    public class RuntimeEntityFactoryTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the lightweight core harness.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required to construct runtime entities.
        /// </summary>
        public RuntimeEntityFactoryTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-runtime-entity-factory-tests", Guid.NewGuid().ToString("N"));
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
        /// Ensures runtime factory creation returns a plain entity with authored defaults.
        /// </summary>
        [Fact]
        public void Create_ReturnsPlainEntityWithAuthoredDefaults() {
            IEntityFactory factory = new RuntimeEntityFactory();

            Entity entity = factory.Create("Player");

            Assert.IsNotType<EditorEntity>(entity);
            Assert.Equal(float3.Zero, entity.LocalPosition);
            Assert.Equal(float3.One, entity.LocalScale);
            Assert.Equal(float4.Identity, entity.LocalOrientation);
        }

        /// <summary>
        /// Ensures child creation parents the entity before returning it.
        /// </summary>
        [Fact]
        public void CreateChild_ParentsChildBeforeReturning() {
            IEntityFactory factory = new RuntimeEntityFactory();
            Entity parent = factory.Create("Parent");
            parent.InitChildren();

            Entity child = factory.CreateChild(parent, "Child");

            Assert.Same(parent, child.Parent);
            Assert.Contains(child, parent.Children);
        }
    }
}
