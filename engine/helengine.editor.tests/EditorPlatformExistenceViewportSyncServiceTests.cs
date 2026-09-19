using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies viewport suppression follows group-scoped existence overrides.
    /// </summary>
    public sealed class EditorPlatformExistenceViewportSyncServiceTests : IDisposable {
        readonly string ProjectRootPath;
        readonly Core CoreValue;

        public EditorPlatformExistenceViewportSyncServiceTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-existence-sync-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ProjectRootPath);
            CoreValue = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            CoreValue.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }

        public void Dispose() {
            CoreValue.Dispose();
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        [Fact]
        public void Apply_WhenEntityExistsOnlyInAGroup_SuppressesPlatformsOutsideIt() {
            new EditorProjectPlatformsService(ProjectRootPath).Save(new EditorProjectPlatformsDocument { SupportedPlatforms = ["windows", "ds"] });
            EditorProjectPlatformGroupsService groupsService = new EditorProjectPlatformGroupsService(ProjectRootPath);
            EditorProjectPlatformGroupsDocument groups = groupsService.Load();
            groupsService.AddGroup(groups, null, "handheld");
            groupsService.AssignPlatform(groups, "handheld", "ds");
            groups.DefaultLevelOrder = [SceneOverrideScopeStepKind.Group, SceneOverrideScopeStepKind.Platform];
            groupsService.Save(groups);

            EditorEntity entity = new EditorEntity(CoreValue, new helengine.editor.EditorSessionInteractionServices()) { IsSceneOwned = true };
            EntitySaveComponent saveComponent = entity.Components.OfType<EntitySaveComponent>().Single();
            EntityPlatformExistenceEditingService existence = new EntityPlatformExistenceEditingService();
            existence.SetExists(saveComponent, EditorOverrideScope.Common, false);
            existence.SetExists(saveComponent, EditorOverrideScope.FromSteps(SceneOverrideScopePath.Group("handheld")), true);
            EditorPlatformExistenceViewportSyncService sync = new EditorPlatformExistenceViewportSyncService(CoreValue.ObjectManager, ProjectRootPath);

            sync.Apply("windows");
            Assert.True(entity.RuntimeSuppressed);

            sync.Apply("ds");
            Assert.False(entity.RuntimeSuppressed);
        }

        /// <summary>
        /// Ensures editor-internal and non-scene entities are never suppressed by platform existence.
        /// </summary>
        [Fact]
        public void Apply_LeavesEditorInternalEntitiesUntouched() {
            EditorPlatformExistenceViewportSyncService service = new EditorPlatformExistenceViewportSyncService(CoreValue.ObjectManager, ProjectRootPath);
            EditorEntity internalEntity = new EditorEntity(CoreValue, new helengine.editor.EditorSessionInteractionServices()) { InternalEntity = true };

            service.Apply("windows");

            Assert.False(internalEntity.RuntimeSuppressed);
        }

        /// <summary>
        /// Ensures existence-override edits raise the changed event so viewport suppression can re-resolve event-driven.
        /// </summary>
        [Fact]
        public void SetExists_WhenOverrideChanges_RaisesExistenceChanged() {
            EditorEntity sceneEntity = new EditorEntity(CoreValue, new helengine.editor.EditorSessionInteractionServices()) { IsSceneOwned = true };
            EntitySaveComponent saveComponent = sceneEntity.Components.OfType<EntitySaveComponent>().Single();
            int raisedCount = 0;
            EntityPlatformExistenceEditingService existenceService = new EntityPlatformExistenceEditingService();
            existenceService.ExistenceChanged += () => raisedCount++;

            existenceService.SetExists(saveComponent, "windows", false);
            existenceService.SetExists(saveComponent, "windows", true);

            Assert.Equal(2, raisedCount);
        }
    }
}
