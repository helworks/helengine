using System.Reflection;
using System.Runtime.CompilerServices;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies editor-session undo and redo handlers can replay authored scene mutations through the real session history pipeline.
    /// </summary>
    public sealed class EditorSessionUndoRedoIntegrationTests : IDisposable {
        /// <summary>
        /// Temporary project root used by the current editor-session history integration test.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Deterministic input backend used when keyboard shortcuts are exercised through the real keyboard-focus update component.
        /// </summary>
        readonly TestInputBackend InputBackend;

        /// <summary>
        /// Initializes an isolated editor project root for the current session history integration test.
        /// </summary>
        public EditorSessionUndoRedoIntegrationTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-session-undo-redo-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets"));
            InputBackend = new TestInputBackend();
            EnsureEditorCoreHost();
            EditorSelectionService.ClearSelection();
            EditorSceneMutationService.Reset();
            EditorKeyboardFocusService.Reset();
        }

        /// <summary>
        /// Clears shared editor state and removes temporary project files after each test.
        /// </summary>
        public void Dispose() {
            EditorSelectionService.ClearSelection();
            EditorSceneMutationService.Reset();
            EditorKeyboardFocusService.Reset();
            EditorEntityHistoryMutationService.Reset();
            EditorComponentHistoryMutationService.Reset();
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures creating one scene entity through the editor-session add command can be undone and redone through the real global shortcut handlers.
        /// </summary>
        [Fact]
        public void Handle_global_undo_and_redo_shortcuts_restore_one_created_scene_entity() {
            EditorSession session = CreateSessionForUndoRedo();

            InvokePrivate(session, "HandleAddEmptyRequested");

            Assert.Equal(1, CountUserSceneEntities());

            InvokePrivate(session, "HandleGlobalUndoShortcut");

            Assert.Equal(0, CountUserSceneEntities());

            InvokePrivate(session, "HandleGlobalRedoShortcut");

            Assert.Equal(1, CountUserSceneEntities());
        }

        /// <summary>
        /// Ensures undoing one created entity restores the previously selected scene entity and redo selects the recreated entity again.
        /// </summary>
        [Fact]
        public void Handle_global_undo_and_redo_shortcuts_restore_the_previous_selection_for_created_entities() {
            EditorSession session = CreateSessionForUndoRedo();
            EditorEntity previousSelection = CreateUserSceneEntity(100u, "Existing");
            EditorSelectionService.SetSelectedEntity(previousSelection);

            InvokePrivate(session, "HandleAddEmptyRequested");

            EditorEntity createdEntity = Assert.IsType<EditorEntity>(EditorSelectionService.SelectedEntity);
            Assert.NotSame(previousSelection, createdEntity);
            Assert.Equal(2, CountUserSceneEntities());

            InvokePrivate(session, "HandleGlobalUndoShortcut");

            Assert.Same(previousSelection, EditorSelectionService.SelectedEntity);
            Assert.Equal(1, CountUserSceneEntities());

            InvokePrivate(session, "HandleGlobalRedoShortcut");

            EditorEntity redoneEntity = Assert.IsType<EditorEntity>(EditorSelectionService.SelectedEntity);
            Assert.NotSame(previousSelection, redoneEntity);
            Assert.Equal(2, CountUserSceneEntities());
        }

        /// <summary>
        /// Ensures modal editor workflows still block the global undo shortcut so scene history is not replayed behind open dialogs.
        /// </summary>
        [Fact]
        public void Handle_global_undo_shortcut_when_open_map_dialog_is_visible_does_not_replay_history() {
            EditorSession session = CreateSessionForUndoRedo();
            OpenFileDialog openFileDialog = GetPrivateField<OpenFileDialog>(session, "openFileDialog");

            InvokePrivate(session, "HandleAddEmptyRequested");

            Assert.Equal(1, CountUserSceneEntities());

            openFileDialog.Show();
            InvokePrivate(session, "HandleGlobalUndoShortcut");

            Assert.Equal(1, CountUserSceneEntities());
        }

        /// <summary>
        /// Ensures one tracked creation mutation marks the scene dirty, undo returns to the clean baseline, and redo marks the scene dirty again.
        /// </summary>
        [Fact]
        public void Handle_global_undo_and_redo_shortcuts_update_dirty_state_for_the_unsaved_baseline() {
            EditorSession session = CreateSessionForUndoRedo();

            Assert.False(GetPrivateField<bool>(session, "IsSceneDirty"));

            InvokePrivate(session, "HandleAddEmptyRequested");
            Assert.True(GetPrivateField<bool>(session, "IsSceneDirty"));

            InvokePrivate(session, "HandleGlobalUndoShortcut");
            Assert.False(GetPrivateField<bool>(session, "IsSceneDirty"));

            InvokePrivate(session, "HandleGlobalRedoShortcut");
            Assert.True(GetPrivateField<bool>(session, "IsSceneDirty"));
        }

        /// <summary>
        /// Ensures marking the current history cursor as saved makes undo dirty the scene and redo return to the saved revision.
        /// </summary>
        [Fact]
        public void Mark_scene_clean_then_undo_and_redo_tracks_the_saved_revision_cursor() {
            EditorSession session = CreateSessionForUndoRedo();

            InvokePrivate(session, "HandleAddEmptyRequested");
            InvokePrivate(session, "MarkSceneClean");

            Assert.False(GetPrivateField<bool>(session, "IsSceneDirty"));

            InvokePrivate(session, "HandleGlobalUndoShortcut");
            Assert.True(GetPrivateField<bool>(session, "IsSceneDirty"));

            InvokePrivate(session, "HandleGlobalRedoShortcut");
            Assert.False(GetPrivateField<bool>(session, "IsSceneDirty"));
        }

        /// <summary>
        /// Ensures one legacy untracked mutation remains dirty independently of the undo stack and is not cleared by empty undo or redo requests.
        /// </summary>
        [Fact]
        public void Legacy_untracked_scene_mutation_remains_dirty_when_undo_or_redo_have_no_history_to_apply() {
            EditorSession session = CreateSessionForUndoRedo();

            InvokePrivate(session, "MarkSceneClean");
            Assert.False(GetPrivateField<bool>(session, "IsSceneDirty"));

            EditorSceneMutationService.MarkSceneMutated();
            Assert.True(GetPrivateField<bool>(session, "IsSceneDirty"));

            InvokePrivate(session, "HandleGlobalUndoShortcut");
            Assert.True(GetPrivateField<bool>(session, "IsSceneDirty"));

            InvokePrivate(session, "HandleGlobalRedoShortcut");
            Assert.True(GetPrivateField<bool>(session, "IsSceneDirty"));
        }

        /// <summary>
        /// Ensures Ctrl+Z, Ctrl+Y, and Ctrl+Shift+Z drive the editor session through the real keyboard-focus update component instead of relying on direct handler invocation.
        /// </summary>
        [Fact]
        public void Keyboard_focus_update_component_routes_keyboard_shortcuts_into_session_history() {
            EditorSession session = CreateSessionForUndoRedo();
            EditorKeyboardFocusUpdateComponent component = new EditorKeyboardFocusUpdateComponent {
                UndoShortcutRequested = CreatePrivateActionDelegate(session, "HandleGlobalUndoShortcut"),
                RedoShortcutRequested = CreatePrivateActionDelegate(session, "HandleGlobalRedoShortcut")
            };

            InvokePrivate(session, "HandleAddEmptyRequested");
            Assert.Equal(1, CountUserSceneEntities());

            AdvanceToNeutralFrame();
            InputBackend.SetKeyboardState(new KeyboardState(Keys.LeftControl, Keys.Z));
            InputBackend.EarlyUpdate();
            component.Update();

            Assert.Equal(0, CountUserSceneEntities());

            AdvanceToNeutralFrame();
            InputBackend.SetKeyboardState(new KeyboardState(Keys.LeftControl, Keys.Y));
            InputBackend.EarlyUpdate();
            component.Update();

            Assert.Equal(1, CountUserSceneEntities());

            AdvanceToNeutralFrame();
            InputBackend.SetKeyboardState(new KeyboardState(Keys.LeftControl, Keys.Z));
            InputBackend.EarlyUpdate();
            component.Update();

            Assert.Equal(0, CountUserSceneEntities());

            AdvanceToNeutralFrame();
            InputBackend.SetKeyboardState(new KeyboardState(Keys.LeftControl, Keys.LeftShift, Keys.Z));
            InputBackend.EarlyUpdate();
            component.Update();

            Assert.Equal(1, CountUserSceneEntities());
        }

        /// <summary>
        /// Ensures external editor tooling can register one custom component history adapter through the session-facing registry and record one mutation through the static component-history bridge.
        /// </summary>
        [Fact]
        public void Session_component_history_registry_supports_custom_adapter_registration_through_the_static_bridge() {
            EditorSession session = CreateSessionForUndoRedo();
            RecordingComponentHistoryAdapter adapter = new RecordingComponentHistoryAdapter();
            EditorEntity entity = CreateUserSceneEntity(200u, "Camera Entity");
            CameraComponent camera = new CameraComponent {
                NearPlaneDistance = 0.1f
            };
            entity.AddComponent(camera);

            session.ComponentHistoryAdapters.Register<CameraComponent>(adapter);

            bool captured = EditorComponentHistoryMutationService.TryCaptureEntityState(camera, out SerializedEditorEntityState previousEntityState);
            Assert.True(captured);

            camera.NearPlaneDistance = 3.5f;
            bool recorded = EditorComponentHistoryMutationService.TryRecordComponentMutation(camera, previousEntityState);

            Assert.True(recorded);
            Assert.Equal(1, adapter.InvocationCount);
            Assert.Same(camera, adapter.RecordedComponent);
            Assert.Same(previousEntityState, adapter.PreviousEntityState);
            Assert.Equal("Camera Entity", adapter.CurrentEntityState.EntityAsset.Name);
            Assert.True(GetPrivateField<EditorUndoRedoService>(session, "UndoRedoService").CanUndo);
            Assert.True(GetPrivateField<bool>(session, "IsSceneDirty"));
        }

        /// <summary>
        /// Ensures one custom component adapter can return a real undoable operation that participates in the session undo and redo flow.
        /// </summary>
        [Fact]
        public void Session_component_history_registry_allows_custom_operations_to_participate_in_undo_and_redo() {
            EditorSession session = CreateSessionForUndoRedo();
            CameraNearPlaneHistoryAdapter adapter = new CameraNearPlaneHistoryAdapter(0.1f);
            EditorEntity entity = CreateUserSceneEntity(201u, "Custom Camera Entity");
            CameraComponent camera = new CameraComponent {
                NearPlaneDistance = 0.1f
            };
            entity.AddComponent(camera);
            session.ComponentHistoryAdapters.Register<CameraComponent>(adapter);

            bool captured = EditorComponentHistoryMutationService.TryCaptureEntityState(camera, out SerializedEditorEntityState previousEntityState);
            Assert.True(captured);

            camera.NearPlaneDistance = 3.5f;
            bool recorded = EditorComponentHistoryMutationService.TryRecordComponentMutation(camera, previousEntityState);
            Assert.True(recorded);
            Assert.Equal(1, adapter.InvocationCount);
            Assert.Equal(3.5f, camera.NearPlaneDistance, 3);

            InvokePrivate(session, "HandleGlobalUndoShortcut");
            Assert.Equal(0.1f, camera.NearPlaneDistance, 3);

            InvokePrivate(session, "HandleGlobalRedoShortcut");
            Assert.Equal(3.5f, camera.NearPlaneDistance, 3);
        }

        /// <summary>
        /// Creates a partially initialized editor session containing the collaborators required by scene-entity creation and undo/redo replay.
        /// </summary>
        /// <returns>Editor session instance configured for undo and redo integration tests.</returns>
        EditorSession CreateSessionForUndoRedo() {
            ComponentPersistenceRegistry persistenceRegistry = new ComponentPersistenceRegistry();
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, persistenceRegistry);
            SceneFileLoadService loadService = new SceneFileLoadService(TempProjectRootPath, persistenceRegistry, new TestSceneAssetReferenceResolver());
            EditorHistoryCaptureService historyCaptureService = new EditorHistoryCaptureService(saveService);
            ComponentHistoryAdapterRegistry historyAdapterRegistry = new ComponentHistoryAdapterRegistry();
            SceneSettingsAsset currentSceneSettings = new SceneSettingsAsset();
            EditorSceneCanvasProfileState sceneCanvasProfileState = new EditorSceneCanvasProfileState();
            sceneCanvasProfileState.ApplySceneSettings(currentSceneSettings);
            EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));

            SetPrivateField(session, "core", Assert.IsType<EditorCore>(Core.Instance));
            SetPrivateField(session, "projectPath", TempProjectRootPath);
            SetPrivateField(session, "openFileDialog", new OpenFileDialog(CreateFont(), TempProjectRootPath));
            SetPrivateField(session, "saveFileDialog", new SaveFileDialog(CreateFont(), TempProjectRootPath));
            SetPrivateField(session, "unsavedChangesDialog", new UnsavedChangesDialog(CreateFont()));
            SetPrivateField(session, "reparentEntityDialog", new ReparentEntityDialog(CreateFont(), EditorUiMetrics.Default));
            SetPrivateField(session, "sceneSettingsDialog", new SceneSettingsDialog(CreateFont(), EditorUiMetrics.Default));
            SetPrivateField(session, "sceneCatalogService", new EditorProjectSceneCatalogService(TempProjectRootPath));
            SetPrivateField(session, "SceneSaveService", saveService);
            SetPrivateField(session, "SceneFileLoadService", loadService);
            SetPrivateField(session, "SceneCreationService", new EditorSceneCreationService());
            SetPrivateField(session, "ReparentService", new EditorEntityReparentService());
            SetPrivateField(session, "HistoryCaptureService", historyCaptureService);
            SetPrivateField(session, "ComponentHistoryAdapterRegistry", historyAdapterRegistry);
            SetPrivateField(session, "CurrentScenePath", string.Empty);
            SetPrivateField(session, "CurrentSceneSettings", currentSceneSettings);
            SetPrivateField(session, "CurrentSceneOwnedAssets", CreateOwnedAssetSet());
            SetPrivateField(session, "sceneCanvasProfileState", sceneCanvasProfileState);
            SetPrivateField(session, "HasUntrackedSceneChangesSinceSave", false);
            SetPrivateField(session, "IsSceneDirty", false);

            EditorHistoryContext historyContext = InvokePrivate<EditorHistoryContext>(session, "CreateHistoryContext");
            EditorUndoRedoService undoRedoService = new EditorUndoRedoService(historyContext);
            EditorMutationService historyMutationService = new EditorMutationService(
                undoRedoService,
                historyCaptureService,
                historyAdapterRegistry,
                () => InvokePrivate(session, "RaiseTrackedSceneMutated"));

            SetPrivateField(session, "UndoRedoService", undoRedoService);
            SetPrivateField(session, "HistoryMutationService", historyMutationService);
            EditorEntityHistoryMutationService.CaptureEntityState = historyMutationService.CaptureEntityState;
            EditorEntityHistoryMutationService.RecordEntityStateChange = historyMutationService.RecordEntityStateChange;
            EditorComponentHistoryMutationService.CaptureEntityState = historyMutationService.CaptureEntityState;
            EditorComponentHistoryMutationService.RecordComponentMutation = historyMutationService.RecordComponentMutation;

            MethodInfo handleSceneMutatedMethod = typeof(EditorSession).GetMethod("HandleSceneMutated", BindingFlags.Instance | BindingFlags.NonPublic);
            if (handleSceneMutatedMethod == null) {
                throw new InvalidOperationException("Expected private HandleSceneMutated method was not found.");
            }

            Action sceneMutatedHandler = (Action)Delegate.CreateDelegate(typeof(Action), session, handleSceneMutatedMethod);
            EditorSceneMutationService.SceneMutated += sceneMutatedHandler;

            return session;
        }

        /// <summary>
        /// Counts the current user-authored scene entities owned by the active core.
        /// </summary>
        /// <returns>Number of non-internal editor entities currently present in the scene.</returns>
        int CountUserSceneEntities() {
            int count = 0;
            List<Entity> entities = Core.Instance.ObjectManager.Entities;
            for (int index = 0; index < entities.Count; index++) {
                if (entities[index] is not EditorEntity editorEntity
                    || editorEntity.InternalEntity
                    || editorEntity.LayerMask != EditorLayerMasks.SceneObjects) {
                    continue;
                }

                count++;
            }

            return count;
        }

        /// <summary>
        /// Creates one user-authored scene entity with a stable scene id so selection restoration can target it through undo history.
        /// </summary>
        /// <param name="entityId">Stable scene entity id assigned to the created entity.</param>
        /// <param name="name">Display name assigned to the entity.</param>
        /// <returns>Created user-authored scene entity.</returns>
        EditorEntity CreateUserSceneEntity(uint entityId, string name) {
            EditorEntity entity = new EditorEntity {
                Name = name,
                LayerMask = EditorLayerMasks.SceneObjects
            };
            EntitySaveComponent saveComponent = Assert.IsType<EntitySaveComponent>(Assert.Single(entity.Components, component => component is EntitySaveComponent));
            saveComponent.EntityId = entityId;
            return entity;
        }

        /// <summary>
        /// Creates one empty scene-owned asset set for synthetic editor-session fixtures.
        /// </summary>
        /// <returns>Empty runtime owned-asset set.</returns>
        RuntimeSceneOwnedAssetSet CreateOwnedAssetSet() {
            return new RuntimeSceneOwnedAssetSet(
                Array.Empty<RuntimeTexture>(),
                Array.Empty<FontAsset>(),
                Array.Empty<AudioAsset>(),
                Array.Empty<RuntimeModel>(),
                Array.Empty<RuntimeMaterial>());
        }

        /// <summary>
        /// Ensures the active core host is an initialized editor core for the temporary project root used by the current test.
        /// </summary>
        void EnsureEditorCoreHost() {
            if (Core.Instance is EditorCore editorCore
                && editorCore.Project != null
                && string.Equals(editorCore.Project.Path, TempProjectRootPath, StringComparison.Ordinal)) {
                return;
            }

            EditorCore core = new EditorCore(new Project {
                Name = "Editor Session Undo Redo",
                Path = TempProjectRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), InputBackend, new PlatformInfo("test", "test-version"), new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempProjectRootPath),
                ScenePathResolver = new EditorProjectSceneCatalogService(TempProjectRootPath)
            });
            core.InputSystem.SetKeyboardActive(true);
        }

        /// <summary>
        /// Invokes one non-public instance method.
        /// </summary>
        /// <param name="target">Object that owns the target method.</param>
        /// <param name="methodName">Name of the method that should be invoked.</param>
        /// <param name="arguments">Arguments passed to the invoked method.</param>
        void InvokePrivate(object target, string methodName, params object[] arguments) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) {
                throw new InvalidOperationException("Expected private method was not found.");
            }

            method.Invoke(target, arguments);
        }

        /// <summary>
        /// Invokes one non-public instance method and returns its typed result.
        /// </summary>
        /// <typeparam name="T">Expected return type.</typeparam>
        /// <param name="target">Object that owns the target method.</param>
        /// <param name="methodName">Name of the method that should be invoked.</param>
        /// <param name="arguments">Arguments passed to the invoked method.</param>
        /// <returns>Typed method result.</returns>
        T InvokePrivate<T>(object target, string methodName, params object[] arguments) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) {
                throw new InvalidOperationException("Expected private method was not found.");
            }

            return Assert.IsType<T>(method.Invoke(target, arguments));
        }

        /// <summary>
        /// Assigns one non-public instance field.
        /// </summary>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to assign.</param>
        /// <param name="value">Value assigned to the field.</param>
        void SetPrivateField(object target, string fieldName, object value) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) {
                throw new InvalidOperationException("Expected private field was not found.");
            }

            field.SetValue(target, value);
        }

        /// <summary>
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) {
                throw new InvalidOperationException("Expected private field was not found.");
            }

            return Assert.IsType<T>(field.GetValue(target));
        }

        /// <summary>
        /// Creates one strongly typed action delegate for the supplied private parameterless editor-session method.
        /// </summary>
        /// <param name="target">Object that owns the private method.</param>
        /// <param name="methodName">Name of the private parameterless method.</param>
        /// <returns>Delegate that invokes the requested method.</returns>
        Action CreatePrivateActionDelegate(object target, string methodName) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) {
                throw new InvalidOperationException("Expected private method was not found.");
            }

            return (Action)Delegate.CreateDelegate(typeof(Action), target, method);
        }

        /// <summary>
        /// Advances the shared input backend through one neutral frame so the next keyboard state is observed as a press transition.
        /// </summary>
        void AdvanceToNeutralFrame() {
            InputBackend.SetKeyboardState(new KeyboardState());
            InputBackend.EarlyUpdate();
            InputBackend.Update();
        }

        /// <summary>
        /// Creates one deterministic font asset used by modal dialog collaborators in the synthetic editor session.
        /// </summary>
        /// <returns>Font asset with basic glyph coverage and stable metrics.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>();
            string glyphs = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .:-_";
            for (int index = 0; index < glyphs.Length; index++) {
                char glyph = glyphs[index];
                if (characters.ContainsKey(glyph)) {
                    continue;
                }

                float width = glyph == ' ' ? 4f : 8f;
                characters[glyph] = new FontChar(new float4(0f, 0f, width, 12f), 0f, width, 0f, 0f);
            }

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                16f,
                64,
                64);
        }
    }
}
