using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies editor mutation recording emits reversible operations and dispatches component-scoped edits through the registered adapter pipeline.
    /// </summary>
    public sealed class EditorMutationServiceTests : IDisposable {
        /// <summary>
        /// Temporary project root used to back scene snapshot serialization.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes one isolated editor-core host and temporary project root for mutation-service tests.
        /// </summary>
        public EditorMutationServiceTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-mutation-service-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempRootPath, "assets"));

            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath)
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Removes temporary project state after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures entity-scoped mutations record one reversible entity-state operation and raise one tracked scene-mutation notification.
        /// </summary>
        [Fact]
        public void Record_entity_state_change_records_one_entity_operation_and_marks_the_scene_mutated() {
            RecordingUndoRedoService undoRedoService = new RecordingUndoRedoService();
            int mutationNotificationCount = 0;
            EditorMutationService service = CreateMutationService(undoRedoService, new ComponentHistoryAdapterRegistry(), () => mutationNotificationCount++);
            EditorEntity entity = CreateSceneEntity(1u, "Before");
            SerializedEditorEntityState previousEntityState = service.CaptureEntityState(entity);

            entity.Name = "After";
            service.RecordEntityStateChange(entity, previousEntityState);

            Assert.Single(undoRedoService.RecordedOperations);
            Assert.IsType<EntityStateChangeHistoryOperation>(undoRedoService.RecordedOperations[0]);
            Assert.Equal(1, mutationNotificationCount);
        }

        /// <summary>
        /// Ensures component-scoped mutations dispatch through the registered component adapter and record the returned operation.
        /// </summary>
        [Fact]
        public void Record_component_mutation_uses_the_registered_component_history_adapter() {
            RecordingUndoRedoService undoRedoService = new RecordingUndoRedoService();
            RecordingComponentHistoryAdapter adapter = new RecordingComponentHistoryAdapter();
            ComponentHistoryAdapterRegistry registry = new ComponentHistoryAdapterRegistry();
            registry.Register<CameraComponent>(adapter);
            int mutationNotificationCount = 0;
            EditorMutationService service = CreateMutationService(undoRedoService, registry, () => mutationNotificationCount++);
            EditorEntity entity = CreateSceneEntity(2u, "Camera");
            CameraComponent component = new CameraComponent {
                NearPlaneDistance = 0.1f
            };
            entity.AddComponent(component);
            SerializedEditorEntityState previousEntityState = service.CaptureEntityState(entity);

            component.NearPlaneDistance = 3.5f;
            service.RecordComponentMutation(entity, component, previousEntityState);

            Assert.Equal(1, adapter.InvocationCount);
            Assert.Same(component, adapter.RecordedComponent);
            Assert.Same(previousEntityState, adapter.PreviousEntityState);
            Assert.Equal("Camera", adapter.CurrentEntityState.EntityAsset.Name);
            Assert.Single(undoRedoService.RecordedOperations);
            Assert.Same(adapter.ReturnedOperation, undoRedoService.RecordedOperations[0]);
            Assert.Equal(1, mutationNotificationCount);
        }

        /// <summary>
        /// Creates one mutation service backed by the current temporary project root.
        /// </summary>
        /// <param name="undoRedoService">Undo/redo service that should capture recorded operations.</param>
        /// <param name="registry">Component history adapter registry used by the mutation service.</param>
        /// <param name="markSceneMutated">Callback invoked when the mutation service records one tracked scene mutation.</param>
        /// <returns>Configured editor mutation service.</returns>
        EditorMutationService CreateMutationService(RecordingUndoRedoService undoRedoService, ComponentHistoryAdapterRegistry registry, Action markSceneMutated) {
            SceneSaveService saveService = new SceneSaveService(TempRootPath, new ComponentPersistenceRegistry());
            EditorHistoryCaptureService captureService = new EditorHistoryCaptureService(saveService);
            return new EditorMutationService(undoRedoService, captureService, registry, markSceneMutated);
        }

        /// <summary>
        /// Creates one user-authored editor entity with a stable scene id.
        /// </summary>
        /// <param name="entityId">Stable scene entity id assigned to the entity save component.</param>
        /// <param name="name">Entity display name.</param>
        /// <returns>Configured editor entity.</returns>
        EditorEntity CreateSceneEntity(uint entityId, string name) {
            EditorEntity entity = new EditorEntity {
                Name = name
            };
            EntitySaveComponent saveComponent = Assert.IsType<EntitySaveComponent>(Assert.Single(entity.Components, component => component is EntitySaveComponent));
            saveComponent.EntityId = entityId;
            return entity;
        }
    }
}
