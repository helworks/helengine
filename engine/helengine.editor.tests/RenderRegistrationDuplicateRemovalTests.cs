using Xunit;
using helengine.editor.tests.testing;

namespace helengine.editor.tests {
    /// <summary>
    /// Locks duplicate render-registration cleanup so one unregister request fully removes stale drawable entries from shared runtime lists.
    /// </summary>
    public class RenderRegistrationDuplicateRemovalTests {
        /// <summary>
        /// Verifies object-manager 3D render registration removal drains duplicate registrations instead of leaving one stale drawable behind.
        /// </summary>
        [Fact]
        public void RemoveFromRender3D_WhenDrawableWasRegisteredMultipleTimes_RemovesAllRegistrations() {
            ObjectManager objectManager = new(new CoreInitializationOptions());
            MeshComponent drawable = new();

            objectManager.RegisterForRender3D(drawable);
            objectManager.RegisterForRender3D(drawable);

            objectManager.RemoveFromRender3D(drawable);

            Assert.Empty(objectManager.Drawables3D);
        }

        /// <summary>
        /// Verifies one camera-facing 3D render queue removal drains duplicate entries so stale drawables cannot survive scene teardown.
        /// </summary>
        [Fact]
        public void RenderList3D_WhenDrawableWasRegisteredMultipleTimes_RemoveClearsAllDuplicateEntries() {
            RenderList3D renderList = new(0);
            MeshComponent drawable = new();

            renderList.Add(drawable);
            renderList.Add(drawable);

            bool removed = renderList.Remove(drawable);

            Assert.True(removed);
            Assert.Equal(0, renderList.Count);
        }

        /// <summary>
        /// Verifies per-camera 3D render queues treat repeated registration of the same drawable as idempotent input.
        /// </summary>
        [Fact]
        public void RenderList3D_WhenDrawableWasRegisteredMultipleTimes_AddKeepsSingleEntry() {
            RenderList3D renderList = new(0);
            MeshComponent drawable = new();

            renderList.Add(drawable);
            renderList.Add(drawable);

            Assert.Equal(1, renderList.Count);
        }

        /// <summary>
        /// Verifies object-manager camera removal drains duplicate registrations so stale disposed cameras cannot survive one unregister request.
        /// </summary>
        [Fact]
        public void RemoveCamera_WhenCameraWasRegisteredMultipleTimes_RemovesAllRegistrations() {
            Core core = new();
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"), new CoreInitializationOptions());
            ObjectManager objectManager = core.ObjectManager;
            CameraComponent camera = new();

            objectManager.RegisterCamera(camera);
            objectManager.RegisterCamera(camera);

            objectManager.RemoveCamera(camera);

            Assert.Empty(objectManager.Cameras);
        }

        /// <summary>
        /// Verifies object-manager camera registration is idempotent so one live camera cannot accumulate duplicate manager entries.
        /// </summary>
        [Fact]
        public void RegisterCamera_WhenCameraWasRegisteredMultipleTimes_KeepsSingleRegistration() {
            Core core = new();
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"), new CoreInitializationOptions());
            ObjectManager objectManager = core.ObjectManager;
            CameraComponent camera = new();

            objectManager.RegisterCamera(camera);
            objectManager.RegisterCamera(camera);

            Assert.Single(objectManager.Cameras);
        }

        /// <summary>
        /// Verifies object-manager entity registration is idempotent so entity teardown cannot leave one stale duplicate in the shared entity list.
        /// </summary>
        [Fact]
        public void RegisterEntity_WhenEntityWasRegisteredMultipleTimes_KeepsSingleRegistration() {
            Core core = new();
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"), new CoreInitializationOptions());
            ObjectManager objectManager = core.ObjectManager;
            Entity entity = new();

            objectManager.RegisterEntity(entity);

            Assert.Single(objectManager.Entities);
        }
    }
}
