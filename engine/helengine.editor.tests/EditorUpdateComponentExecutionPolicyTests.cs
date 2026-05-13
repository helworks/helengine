using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies that update-driven gameplay behavior stays inactive on user scene entities while the editor is authoring them.
    /// </summary>
    public class EditorUpdateComponentExecutionPolicyTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the lightweight core harness.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes a core instance that can evaluate component registration and updates.
        /// </summary>
        public EditorUpdateComponentExecutionPolicyTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-update-component-policy-tests", Guid.NewGuid().ToString("N"));
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
        /// Ensures user scene update components attach as data in editor mode without running gameplay lifecycle or update registration.
        /// </summary>
        [Fact]
        public void AddComponent_WhenEditorModeAndUserSceneEntityAndComponentLacksRunInEditor_SuppressesLifecycleAndUpdateRegistration() {
            EditorEntity entity = CreateUserSceneEntity();
            EditorUpdateLifecycleProbeComponent component = new EditorUpdateLifecycleProbeComponent();

            EnterEditorAndRun(() => entity.AddComponent(component));

            Assert.Same(entity, component.Parent);
            Assert.Equal(0, component.ComponentAddedCallCount);
            Assert.Empty(Core.Instance.ObjectManager.Updateables);
        }

        /// <summary>
        /// Ensures user scene update components can be removed in editor mode even when their gameplay lifecycle never ran.
        /// </summary>
        [Fact]
        public void RemoveComponent_WhenEditorModeAndUserSceneEntityAndComponentLacksRunInEditor_DetachesWithoutRunningGameplayTeardown() {
            EditorEntity entity = CreateUserSceneEntity();
            EditorUpdateLifecycleProbeComponent component = new EditorUpdateLifecycleProbeComponent();
            EnterEditorAndRun(() => entity.AddComponent(component));

            EnterEditorAndRun(() => entity.RemoveComponent(component));

            Assert.Equal(0, component.ComponentRemovedCallCount);
            Assert.Null(component.Parent);
            Assert.DoesNotContain(component, entity.Components);
        }

        /// <summary>
        /// Ensures suppressed user scene update components do not tick while the editor update loop is active.
        /// </summary>
        [Fact]
        public void CoreUpdate_WhenEditorModeAndUserSceneEntityAndComponentLacksRunInEditor_DoesNotCallUpdate() {
            EditorEntity entity = CreateUserSceneEntity();
            EditorUpdateLifecycleProbeComponent component = new EditorUpdateLifecycleProbeComponent();
            EnterEditorAndRun(() => entity.AddComponent(component));

            EnterEditorAndRun(() => Core.Instance.Update());

            Assert.Equal(0, component.UpdateCallCount);
        }

        /// <summary>
        /// Ensures explicitly opted-in update components run their full lifecycle in editor mode.
        /// </summary>
        [Fact]
        public void AddUpdateAndRemove_WhenEditorModeAndUserSceneEntityAndComponentHasRunInEditor_RunsFullLifecycle() {
            EditorEntity entity = CreateUserSceneEntity();
            EditorRunInEditorUpdateLifecycleProbeComponent component = new EditorRunInEditorUpdateLifecycleProbeComponent();

            EnterEditorAndRun(() => entity.AddComponent(component));

            Assert.Equal(1, component.ComponentAddedCallCount);
            Assert.Single(Core.Instance.ObjectManager.Updateables);

            EnterEditorAndRun(() => Core.Instance.Update());

            Assert.Equal(1, component.UpdateCallCount);

            EnterEditorAndRun(() => entity.RemoveComponent(component));

            Assert.Equal(1, component.ComponentRemovedCallCount);
            Assert.Null(component.Parent);
        }

        /// <summary>
        /// Ensures plain editor entities without the explicit suppression marker continue to run update-driven component lifecycle in editor mode.
        /// </summary>
        [Fact]
        public void AddComponent_WhenEditorModeAndEntityLacksSuppressionMarker_RunsLifecycleNormally() {
            EditorEntity entity = new EditorEntity {
                LayerMask = EditorLayerMasks.SceneObjects
            };
            EditorUpdateLifecycleProbeComponent component = new EditorUpdateLifecycleProbeComponent();

            EnterEditorAndRun(() => entity.AddComponent(component));

            Assert.Equal(1, component.ComponentAddedCallCount);
            Assert.Single(Core.Instance.ObjectManager.Updateables);
        }

        /// <summary>
        /// Creates one editor scene entity whose update-driven behavior should stay inactive during authoring.
        /// </summary>
        /// <returns>Configured user scene entity.</returns>
        EditorEntity CreateUserSceneEntity() {
            EditorEntity entity = new EditorEntity {
                LayerMask = EditorLayerMasks.SceneObjects
            };
            entity.AddComponent(new EditorUpdateExecutionSuppressionComponent());
            return entity;
        }

        /// <summary>
        /// Executes one action while the current thread is marked as editor component execution.
        /// </summary>
        /// <param name="action">Action to run inside the editor execution scope.</param>
        void EnterEditorAndRun(Action action) {
            if (action == null) {
                throw new ArgumentNullException(nameof(action));
            }

            ComponentExecutionContext.EnterEditor();
            try {
                action();
            } finally {
                ComponentExecutionContext.ExitEditor();
            }
        }
    }
}
