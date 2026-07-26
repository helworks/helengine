using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies that update-driven components do not become active until their entity hierarchy is fully initialized.
    /// </summary>
    public class UpdateComponentInitializationTests {
        /// <summary>
        /// Ensures an update component attached during incremental scene materialization cannot update before its entity hierarchy is initialized.
        /// </summary>
        [Fact]
        public void AddComponent_BeforeInitializeHierarchy_DoesNotRegisterForUpdatesUntilInitialized() {
            InitializeCore();
            Entity entity = CreateUninitializedEntity();
            UpdateComponent component = new UpdateComponent();

            entity.AddComponent(component);

            Assert.DoesNotContain(component, Core.Instance.ObjectManager.Updateables);

            entity.InitializeHierarchy();

            Assert.Contains(component, Core.Instance.ObjectManager.Updateables);
        }

        /// <summary>
        /// Initializes the core services required for update registration tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(
                null,
                new TestRenderManager2D(),
                null,
                new PlatformInfo("test", "test-version"),
                new CoreInitializationOptions {
                    ContentStreamSource = new HostFileSystemContentStreamSource(Environment.CurrentDirectory)
                });
        }

        /// <summary>
        /// Creates an entity whose component and child collections are ready while lifecycle initialization remains deferred.
        /// </summary>
        /// <returns>Entity prepared for incremental scene materialization.</returns>
        Entity CreateUninitializedEntity() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();
            return entity;
        }
    }
}
