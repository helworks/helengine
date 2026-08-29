using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the event-driven sync that suppresses scene entities not existing on the active platform.
    /// </summary>
    public sealed class EditorPlatformExistenceViewportSyncServiceTests {

        /// <summary>
        /// Ensures entities excluded from the applied platform are suppressed and re-shown when another platform applies.
        /// </summary>
        [Fact]
        public void Apply_WhenEntityDoesNotExistOnPlatform_SuppressesItUntilAnotherPlatformApplies() {
            CreateCore();
            EditorPlatformExistenceViewportSyncService service = new EditorPlatformExistenceViewportSyncService(Core.Instance.ObjectManager);
            EditorEntity sceneEntity = new EditorEntity { IsSceneOwned = true };
            EntitySaveComponent saveComponent = FindSaveComponent(sceneEntity);
            EntityPlatformExistenceEditingService existenceService = new EntityPlatformExistenceEditingService();
            existenceService.SetExists(saveComponent, "windows", false);

            service.Apply("windows");

            Assert.True(sceneEntity.RuntimeSuppressed);
            Assert.True(sceneEntity.Enabled);

            service.Apply("ps2");

            Assert.False(sceneEntity.RuntimeSuppressed);
        }

        /// <summary>
        /// Ensures editor-internal and non-scene entities are never suppressed by platform existence.
        /// </summary>
        [Fact]
        public void Apply_LeavesEditorInternalEntitiesUntouched() {
            CreateCore();
            EditorPlatformExistenceViewportSyncService service = new EditorPlatformExistenceViewportSyncService(Core.Instance.ObjectManager);
            EditorEntity internalEntity = new EditorEntity { InternalEntity = true };

            service.Apply("windows");

            Assert.False(internalEntity.RuntimeSuppressed);
        }

        /// <summary>
        /// Ensures existence-override edits raise the changed event so viewport suppression can re-resolve event-driven.
        /// </summary>
        [Fact]
        public void SetExists_WhenOverrideChanges_RaisesExistenceChanged() {
            CreateCore();
            EditorEntity sceneEntity = new EditorEntity { IsSceneOwned = true };
            EntitySaveComponent saveComponent = FindSaveComponent(sceneEntity);
            int raisedCount = 0;
            EntityPlatformExistenceEditingService existenceService = new EntityPlatformExistenceEditingService();
            existenceService.ExistenceChanged += () => raisedCount++;

            existenceService.SetExists(saveComponent, "windows", false);
            existenceService.SetExists(saveComponent, "windows", true);

            Assert.Equal(2, raisedCount);
        }

        /// <summary>
        /// Resolves the hidden save component attached to one editor entity.
        /// </summary>
        /// <param name="entity">Entity whose save component should be returned.</param>
        /// <returns>Attached save component.</returns>
        static EntitySaveComponent FindSaveComponent(EditorEntity entity) {
            for (int index = 0; index < entity.Components.Count; index++) {
                if (entity.Components[index] is EntitySaveComponent saveComponent) {
                    return saveComponent;
                }
            }

            throw new Xunit.Sdk.XunitException("Editor entity must include a save component.");
        }

        /// <summary>
        /// Creates one initialized core instance for sync tests.
        /// </summary>
        static void CreateCore() {
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }
    }
}
