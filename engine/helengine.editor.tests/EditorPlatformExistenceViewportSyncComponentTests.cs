using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the viewport sync that suppresses scene entities not existing on the active platform.
    /// </summary>
    public sealed class EditorPlatformExistenceViewportSyncComponentTests {
        /// <summary>
        /// Ensures entities excluded from the active platform are suppressed and re-shown when the platform changes.
        /// </summary>
        [Fact]
        public void Update_WhenEntityDoesNotExistOnActivePlatform_SuppressesItUntilThePlatformChanges() {
            Core core = CreateCore();
            string activePlatform = "windows";
            EditorPlatformExistenceViewportSyncComponent component = new EditorPlatformExistenceViewportSyncComponent(() => activePlatform);
            EditorEntity sceneEntity = new EditorEntity { IsSceneOwned = true };
            EntitySaveComponent saveComponent = FindSaveComponent(sceneEntity);
            new EntityPlatformExistenceEditingService().SetExists(saveComponent, "windows", false);

            component.Update();

            Assert.True(sceneEntity.RuntimeSuppressed);
            Assert.True(sceneEntity.Enabled);

            activePlatform = "ps2";
            component.Update();

            Assert.False(sceneEntity.RuntimeSuppressed);
        }

        /// <summary>
        /// Ensures editor-internal and non-scene entities are never suppressed by platform existence.
        /// </summary>
        [Fact]
        public void Update_LeavesEditorInternalEntitiesUntouched() {
            Core core = CreateCore();
            EditorPlatformExistenceViewportSyncComponent component = new EditorPlatformExistenceViewportSyncComponent(() => "windows");
            EditorEntity internalEntity = new EditorEntity { InternalEntity = true };

            component.Update();

            Assert.False(internalEntity.RuntimeSuppressed);
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
        /// <returns>Initialized core.</returns>
        static Core CreateCore() {
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
            return core;
        }
    }
}
