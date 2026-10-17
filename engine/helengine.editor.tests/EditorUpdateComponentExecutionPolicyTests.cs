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

        /// <summary>Owns the test entities and resources independently of the global current core.</summary>
        readonly Core CoreValue;

        /// <summary>
        /// Initializes a core instance that can evaluate component registration and updates.
        /// </summary>
        public EditorUpdateComponentExecutionPolicyTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "editor-execution-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            CoreValue = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath)
            });
            CoreValue.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Deletes the temporary content root after each test.
        /// </summary>
        public void Dispose() {
            while (CoreValue.ObjectManager.Entities.Count > 0) {
                CoreValue.ObjectManager.Entities[CoreValue.ObjectManager.Entities.Count - 1].Dispose();
            }
            CoreValue.Dispose();
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
        /// Ensures suppressed user scene update components also suppress hierarchy initialization in editor mode.
        /// </summary>
        [Fact]
        public void InitializeHierarchy_WhenEditorModeAndUserSceneEntityAndComponentLacksRunInEditor_SuppressesInitializedLifecycle() {
            EditorEntity entity = CreateUserSceneEntity();
            EditorUpdateLifecycleProbeComponent component = new EditorUpdateLifecycleProbeComponent();

            EnterEditorAndRun(() => entity.AddComponent(component));
            EnterEditorAndRun(() => entity.InitializeHierarchy());

            Assert.Equal(0, component.ComponentInitializedCallCount);
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
            EnterEditorAndRun(() => entity.InitializeHierarchy());

            Assert.Equal(1, component.ComponentAddedCallCount);
            Assert.Equal(1, component.ComponentInitializedCallCount);
            Assert.Single(Core.Instance.ObjectManager.Updateables);

            EnterEditorAndRun(() => Core.Instance.Update());

            Assert.Equal(1, component.UpdateCallCount);

            EnterEditorAndRun(() => entity.RemoveComponent(component));

            Assert.Equal(1, component.ComponentRemovedCallCount);
            Assert.Null(component.Parent);
        }

        /// <summary>
        /// Ensures missing legacy markers cannot enable gameplay behavior in editor mode.
        /// </summary>
        [Fact]
        public void AddComponent_WhenEditorModeAndEntityLacksSuppressionMarker_SuppressesLifecycle() {
            EditorEntity entity = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) {
                LayerMask = EditorLayerMasks.SceneObjects
            };
            EditorUpdateLifecycleProbeComponent component = new EditorUpdateLifecycleProbeComponent();

            EnterEditorAndRun(() => entity.AddComponent(component));
            EnterEditorAndRun(() => entity.InitializeHierarchy());

            Assert.Equal(0, component.ComponentAddedCallCount);
            Assert.Empty(Core.Instance.ObjectManager.Updateables);
        }

        /// <summary>
        /// Ensures hierarchy initialization cannot bypass the default gameplay suppression.
        /// </summary>
        [Fact]
        public void InitializeHierarchy_WhenEditorModeAndEntityLacksSuppressionMarker_SuppressesInitializedLifecycle() {
            EditorEntity entity = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) {
                LayerMask = EditorLayerMasks.SceneObjects
            };
            EditorUpdateLifecycleProbeComponent component = new EditorUpdateLifecycleProbeComponent();

            EnterEditorAndRun(() => entity.AddComponent(component));
            EnterEditorAndRun(() => entity.InitializeHierarchy());

            Assert.Equal(0, component.ComponentInitializedCallCount);
            Assert.True(entity.IsInitialized);
        }

        /// <summary>Ensures ordinary components cannot execute gameplay through lifecycle callbacks.</summary>
        [Fact]
        public void PlainComponent_InEditor_SuppressesAllLifecycleCallbacks() {
            Entity entity = new Entity(Core.Instance);
            entity.InitComponents();
            EditorPlainLifecycleProbeComponent component = new();
            EnterEditorAndRun(() => {
                entity.AddComponent(component);
                entity.InitializeHierarchy();
                entity.Static = true;
                entity.Enabled = false;
                entity.Enabled = true;
                entity.RemoveComponent(component);
            });
            Assert.Equal(0, component.CallbackCount);
            Assert.Null(component.Parent);
            entity.Dispose();
        }

        /// <summary>Ensures a previously registered gameplay update cannot bypass editor mode.</summary>
        [Fact]
        public void PreviouslyRegisteredUpdate_InEditor_DoesNotExecute() {
            using Entity entity = new Entity(Core.Instance);
            entity.InitComponents();
            EditorUpdateLifecycleProbeComponent component = new();
            entity.AddComponent(component);
            entity.InitializeHierarchy();
            Assert.Contains(component, Core.Instance.ObjectManager.Updateables);
            EnterEditorAndRun(() => Core.Instance.ObjectManager.Update());
            Assert.Equal(0, component.UpdateCallCount);
            Core.Instance.ObjectManager.Update();
            Assert.Equal(1, component.UpdateCallCount);
        }

        /// <summary>Ensures a game subclass cannot inherit the renderer's editor execution permission.</summary>
        [Fact]
        public void UnmarkedRenderingSubclass_InEditor_DoesNotExecute() {
            using Entity entity = new Entity(Core.Instance);
            entity.InitComponents();
            EditorUnmarkedSpriteProbeComponent component = new();
            EnterEditorAndRun(() => entity.AddComponent(component));
            Assert.Equal(0, component.AddedCount);
        }

        /// <summary>Ensures editor ownership suppresses scripts even outside a frame's execution scope.</summary>
        [Fact]
        public void EditorOwnedEntity_OutsideUpdateScope_DoesNotExecuteGameplay() {
            using EditorCore editor = new EditorCore(null);
            editor.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
            using Entity entity = new Entity(editor);
            entity.InitComponents();
            EditorPlainLifecycleProbeComponent component = new();
            entity.AddComponent(component);
            entity.InitializeHierarchy();
            entity.Static = true;
            entity.RemoveComponent(component);
            Assert.Equal(0, component.CallbackCount);
        }

        /// <summary>Ensures the opt-in applies to plain components as well as update components.</summary>
        [Fact]
        public void OptedInPlainComponent_InEditor_RunsFullLifecycle() {
            using Entity entity = new Entity(CoreValue);
            entity.InitComponents();
            EditorOptedInPlainLifecycleProbeComponent component = new();
            EnterEditorAndRun(() => {
                entity.AddComponent(component);
                entity.InitializeHierarchy();
                entity.Static = true;
                entity.Enabled = false;
                entity.Enabled = true;
                entity.RemoveComponent(component);
            });
            Assert.Equal(7, component.CallbackCount);
        }

        /// <summary>Ensures gameplay components retain normal lifecycle behavior in a runtime host.</summary>
        [Fact]
        public void PlainComponent_InRuntime_RunsFullLifecycle() {
            using Entity entity = new Entity(CoreValue);
            entity.InitComponents();
            EditorPlainLifecycleProbeComponent component = new();
            entity.AddComponent(component);
            entity.InitializeHierarchy();
            entity.Static = true;
            entity.RemoveComponent(component);
            Assert.Equal(5, component.CallbackCount);
        }

        /// <summary>Ensures implementing IUpdateable directly cannot bypass the editor's component policy.</summary>
        [Fact]
        public void ManuallyRegisteredPlainComponent_InEditor_DoesNotUpdate() {
            using EditorCore editor = new EditorCore(null);
            editor.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
            using Entity entity = new Entity(editor);
            entity.InitComponents();
            EditorPlainLifecycleProbeComponent component = new();
            entity.AddComponent(component);
            editor.ObjectManager.RegisterForUpdate(component);
            editor.ObjectManager.Update();
            Assert.Equal(0, component.UpdateCount);
            editor.ObjectManager.RemoveFromUpdate(component, component.UpdateOrder);
        }

        /// <summary>Ensures reparenting a suppressed renderer subclass cannot register it for rendering.</summary>
        [Fact]
        public void UnmarkedRenderingSubclass_ReparentedInEditor_RemainsUnregistered() {
            using EditorCore editor = new EditorCore(null);
            editor.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
            using Entity parent = new Entity(editor);
            parent.InitChildren();
            using Entity child = new Entity(editor);
            child.InitComponents();
            EditorUnmarkedSpriteProbeComponent component = new();
            child.AddComponent(component);
            parent.AddChild(child);
            Assert.DoesNotContain(component, editor.ObjectManager.Drawables2D);
        }

        /// <summary>Ensures visual components keep their registration while gameplay animation stays inactive.</summary>
        [Fact]
        public void EditorOwnedScene_PreservesRenderingAndSuppressesAnimation() {
            using EditorCore editor = new EditorCore(null);
            editor.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
            using Entity entity = new Entity(editor);
            entity.InitComponents();
            PointLightComponent light = new();
            SpriteComponent sprite = new();
            AnimationPlayerComponent animation = new();
            entity.AddComponent(light);
            entity.AddComponent(sprite);
            entity.AddComponent(animation);
            entity.InitializeHierarchy();
            Assert.Contains(light, editor.ObjectManager.PointLights);
            Assert.Contains(sprite, editor.ObjectManager.Drawables2D);
            Assert.DoesNotContain(animation, editor.ObjectManager.Updateables);
            entity.Enabled = false;
            Assert.DoesNotContain(light, editor.ObjectManager.PointLights);
            Assert.DoesNotContain(sprite, editor.ObjectManager.Drawables2D);
            entity.Enabled = true;
            Assert.Contains(light, editor.ObjectManager.PointLights);
            Assert.Contains(sprite, editor.ObjectManager.Drawables2D);
        }

        /// <summary>
        /// Ensures nested authoring scopes restore their predecessor's policy and eventually return the thread to runtime execution.
        /// </summary>
        [Fact]
        public void NestedEditorScopes_RestorePreviousPolicyAndRuntimeMode() {
            using Entity entity = new Entity(CoreValue);
            entity.InitComponents();
            EditorPlainLifecycleProbeComponent component = new EditorPlainLifecycleProbeComponent();
            FixedComponentExecutionPolicy innerPolicy = new FixedComponentExecutionPolicy(true);
            Assert.Equal(ComponentExecutionMode.Runtime, ComponentExecutionContext.CurrentMode);
            Assert.Null(ComponentExecutionContext.CurrentPolicy);

            ComponentExecutionContext.EnterEditor(EditorComponentExecutionPolicy.Instance);
            try {
                Assert.Same(EditorComponentExecutionPolicy.Instance, ComponentExecutionContext.CurrentPolicy);
                Assert.False(ComponentExecutionPolicy.ShouldRunComponentLifecycle(component, entity));
                ComponentExecutionContext.EnterEditor(innerPolicy);
                try {
                    Assert.Equal(ComponentExecutionMode.Editor, ComponentExecutionContext.CurrentMode);
                    Assert.Same(innerPolicy, ComponentExecutionContext.CurrentPolicy);
                    Assert.True(ComponentExecutionPolicy.ShouldRunComponentLifecycle(component, entity));
                } finally {
                    ComponentExecutionContext.ExitEditor();
                }

                Assert.Same(EditorComponentExecutionPolicy.Instance, ComponentExecutionContext.CurrentPolicy);
                Assert.False(ComponentExecutionPolicy.ShouldRunComponentLifecycle(component, entity));
            } finally {
                ComponentExecutionContext.ExitEditor();
            }

            Assert.Equal(ComponentExecutionMode.Runtime, ComponentExecutionContext.CurrentMode);
            Assert.Null(ComponentExecutionContext.CurrentPolicy);
            Assert.True(ComponentExecutionPolicy.ShouldRunComponentLifecycle(component, entity));
            Assert.Equal(1, innerPolicy.EvaluationCount);
        }

        /// <summary>
        /// Ensures a scope without an explicit policy cannot silently enter editor mode or alter an enclosing scope.
        /// </summary>
        [Fact]
        public void EnterEditor_WithNullPolicy_RejectsEntryAndPreservesEnclosingScope() {
            ComponentExecutionContext.EnterEditor(EditorComponentExecutionPolicy.Instance);
            try {
                Assert.Throws<ArgumentNullException>(() => ComponentExecutionContext.EnterEditor(null));
                Assert.Same(EditorComponentExecutionPolicy.Instance, ComponentExecutionContext.CurrentPolicy);
                Assert.Equal(ComponentExecutionMode.Editor, ComponentExecutionContext.CurrentMode);
            } finally {
                ComponentExecutionContext.ExitEditor();
            }

            Assert.Null(ComponentExecutionContext.CurrentPolicy);
            Assert.Equal(ComponentExecutionMode.Runtime, ComponentExecutionContext.CurrentMode);
        }

        /// <summary>
        /// Ensures an unmatched scope exit fails without changing normal runtime state.
        /// </summary>
        [Fact]
        public void ExitEditor_WithoutMatchingEntry_Throws() {
            Assert.Throws<InvalidOperationException>(() => ComponentExecutionContext.ExitEditor());
            Assert.Null(ComponentExecutionContext.CurrentPolicy);
            Assert.Equal(ComponentExecutionMode.Runtime, ComponentExecutionContext.CurrentMode);
        }

        /// <summary>
        /// Ensures a worker thread cannot inherit or replace an editor scope owned by the calling thread.
        /// </summary>
        [Fact]
        public void EditorScope_DoesNotFlowToAnotherThread() {
            ComponentExecutionMode workerMode = ComponentExecutionMode.Editor;
            IComponentExecutionPolicy workerPolicy = EditorComponentExecutionPolicy.Instance;
            FixedComponentExecutionPolicy threadPolicy = new FixedComponentExecutionPolicy(true);
            IComponentExecutionPolicy workerActivePolicy = null;
            IComponentExecutionPolicy workerFinalPolicy = EditorComponentExecutionPolicy.Instance;
            ComponentExecutionContext.EnterEditor(EditorComponentExecutionPolicy.Instance);
            try {
                Thread worker = new Thread(() => {
                    workerMode = ComponentExecutionContext.CurrentMode;
                    workerPolicy = ComponentExecutionContext.CurrentPolicy;
                    ComponentExecutionContext.EnterEditor(threadPolicy);
                    try {
                        workerActivePolicy = ComponentExecutionContext.CurrentPolicy;
                    } finally {
                        ComponentExecutionContext.ExitEditor();
                    }
                    workerFinalPolicy = ComponentExecutionContext.CurrentPolicy;
                });
                worker.Start();
                worker.Join();

                Assert.Equal(ComponentExecutionMode.Runtime, workerMode);
                Assert.Null(workerPolicy);
                Assert.Same(threadPolicy, workerActivePolicy);
                Assert.Null(workerFinalPolicy);
                Assert.Same(EditorComponentExecutionPolicy.Instance, ComponentExecutionContext.CurrentPolicy);
            } finally {
                ComponentExecutionContext.ExitEditor();
            }
        }

        /// <summary>
        /// Ensures a runtime host takes the direct lifecycle path even when its options contain an editor policy.
        /// </summary>
        [Fact]
        public void RuntimeHost_DoesNotEvaluateConfiguredEditorPolicy() {
            FixedComponentExecutionPolicy policy = new FixedComponentExecutionPolicy(false);
            CoreValue.InitializationOptions.EditorComponentExecutionPolicy = policy;
            using Entity entity = new Entity(CoreValue);
            entity.InitComponents();

            Assert.True(ComponentExecutionPolicy.ShouldRunComponentLifecycle(new EditorPlainLifecycleProbeComponent(), entity));
            Assert.Equal(0, policy.EvaluationCount);
        }

        /// <summary>
        /// Ensures the innermost explicit scope overrides the editor core's policy until the scope is exited.
        /// </summary>
        [Fact]
        public void EditorScope_TakesPrecedenceOverOwningCorePolicy() {
            using EditorCore editor = new EditorCore(null);
            editor.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
            FixedComponentExecutionPolicy ownerPolicy = new FixedComponentExecutionPolicy(false);
            FixedComponentExecutionPolicy scopePolicy = new FixedComponentExecutionPolicy(true);
            editor.InitializationOptions.EditorComponentExecutionPolicy = ownerPolicy;
            using Entity entity = new Entity(editor);
            entity.InitComponents();
            EditorPlainLifecycleProbeComponent component = new EditorPlainLifecycleProbeComponent();
            ComponentExecutionContext.EnterEditor(scopePolicy);
            try {
                Assert.True(ComponentExecutionPolicy.ShouldRunComponentLifecycle(component, entity));
                Assert.Equal(0, ownerPolicy.EvaluationCount);
            } finally {
                ComponentExecutionContext.ExitEditor();
            }

            Assert.False(ComponentExecutionPolicy.ShouldRunComponentLifecycle(component, entity));
            Assert.Equal(1, ownerPolicy.EvaluationCount);
            Assert.Equal(1, scopePolicy.EvaluationCount);
        }

        /// <summary>
        /// Ensures editor mode cannot silently suppress or permit lifecycle execution when neither the scope nor its owner supplies a policy.
        /// </summary>
        [Fact]
        public void EditorHost_WithoutConfiguredPolicy_FailsExplicitly() {
            using EditorCore editor = new EditorCore(null);
            editor.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
            using Entity entity = new Entity(editor);
            entity.InitComponents();
            editor.InitializationOptions.EditorComponentExecutionPolicy = null;
            try {
                Assert.Throws<InvalidOperationException>(() => ComponentExecutionPolicy.ShouldRunComponentLifecycle(new EditorPlainLifecycleProbeComponent(), entity));
            } finally {
                editor.InitializationOptions.EditorComponentExecutionPolicy = EditorComponentExecutionPolicy.Instance;
            }
        }

        /// <summary>
        /// Ensures editor defaults exist at construction and explicitly supplied host hooks survive initialization unchanged.
        /// </summary>
        [Fact]
        public void EditorCore_ConfiguresDefaultsAndPreservesExplicitHostHooks() {
            using EditorCore editor = new EditorCore(null);
            Assert.Same(EditorComponentExecutionPolicy.Instance, editor.InitializationOptions.EditorComponentExecutionPolicy);
            Assert.IsType<AutomaticRuntimeComponentDeserializerFactory>(editor.InitializationOptions.RuntimeComponentDeserializerFactory);
            FixedComponentExecutionPolicy policy = new FixedComponentExecutionPolicy(false);
            AutomaticRuntimeComponentDeserializerFactory factory = new AutomaticRuntimeComponentDeserializerFactory();
            CoreInitializationOptions options = new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath),
                EditorComponentExecutionPolicy = policy,
                RuntimeComponentDeserializerFactory = factory
            };

            editor.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"), options);

            Assert.Same(options, editor.InitializationOptions);
            Assert.Same(policy, editor.InitializationOptions.EditorComponentExecutionPolicy);
            Assert.Same(factory, editor.InitializationOptions.RuntimeComponentDeserializerFactory);
        }

        /// <summary>
        /// Ensures caller-owned initialization options receive the editor hooks when the caller leaves them unspecified.
        /// </summary>
        [Fact]
        public void EditorCore_InitializeWithFreshOptions_ConfiguresAbsentHostHooks() {
            using EditorCore editor = new EditorCore(null);
            CoreInitializationOptions options = new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath)
            };

            editor.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"), options);

            Assert.Same(options, editor.InitializationOptions);
            Assert.Same(EditorComponentExecutionPolicy.Instance, options.EditorComponentExecutionPolicy);
            Assert.IsType<AutomaticRuntimeComponentDeserializerFactory>(options.RuntimeComponentDeserializerFactory);
        }

        /// <summary>
        /// Creates one editor scene entity whose update-driven behavior should stay inactive during authoring.
        /// </summary>
        /// <returns>Configured user scene entity.</returns>
        EditorEntity CreateUserSceneEntity() {
            EditorEntity entity = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) {
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

            ComponentExecutionContext.EnterEditor(EditorComponentExecutionPolicy.Instance);
            try {
                action();
            } finally {
                ComponentExecutionContext.ExitEditor();
            }
        }
    }
}

