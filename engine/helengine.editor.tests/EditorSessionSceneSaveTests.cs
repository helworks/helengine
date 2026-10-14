using System.Reflection;
using System.Runtime.CompilerServices;
using helengine.editor;
using helengine.editor.tests.testing;
using helengine.ui;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies scene save routing from the editor session.
    /// </summary>
    public class EditorSessionSceneSaveTests : IDisposable {
        EditorSessionInteractionServices InteractionServices => GeneratedAssetGraph.InteractionServices;
        /// <summary>
        /// Temporary project root used by editor-session scene save tests.
        /// </summary>
        readonly string TempProjectRootPath;
        readonly Core CoreValue;
        readonly TestGeneratedAssetGraph GeneratedAssetGraph;

        /// <summary>
        /// Initializes an isolated project root and core services for scene save routing tests.
        /// </summary>
        public EditorSessionSceneSaveTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-scene-save-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "Scenes"));

            EditorCore core = new EditorCore(new Project {
                Name = "Editor Session Scene Save",
                Path = TempProjectRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"), new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempProjectRootPath)
            });
            CoreValue = core;
            GeneratedAssetGraph = new TestGeneratedAssetGraph(core);
        }

        /// <summary>
        /// Deletes temporary project state after each test.
        /// </summary>
        public void Dispose() {
            GeneratedAssetGraph.Dispose();
            CoreValue.Dispose();
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures `Save Map` falls back to the save-file dialog when no current scene path exists.
        /// </summary>
        [Fact]
        public void HandleSaveMapRequested_WhenCurrentScenePathIsEmpty_ShowsSaveFileDialog() {
            EditorSession session = CreateSessionForSceneSave();

            InvokePrivate(session, "HandleSaveMapRequested");

            SaveFileDialog saveFileDialog = GetPrivateField<SaveFileDialog>(session, "saveFileDialog");
            Assert.True(saveFileDialog.IsVisible);
        }

        /// <summary>
        /// Ensures successful scene saves update the current path and write a `.helen` file.
        /// </summary>
        [Fact]
        public void HandleSceneSaveRequested_WhenSaveSucceeds_UpdatesCurrentScenePathAndWritesFile() {
            EditorSession session = CreateSessionForSceneSave();
            string expectedPath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "Saved.helen");
            Directory.CreateDirectory(Path.GetDirectoryName(expectedPath));

            InvokePrivate(session, "HandleSceneSaveRequested", expectedPath);

            string currentScenePath = GetPrivateField<string>(session, "CurrentScenePath");
            Assert.Equal(expectedPath, currentScenePath);
            Assert.True(File.Exists(expectedPath));
        }

        /// <summary>
        /// Ensures successful scene saves persist the tracked scene canvas profile.
        /// </summary>
        [Fact]
        public void HandleSceneSaveRequested_WhenSceneSettingsAreCustomized_PersistsCanvasProfile() {
            EditorSession session = CreateSessionForSceneSave();
            string expectedPath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "CanvasProfile.helen");
            Directory.CreateDirectory(Path.GetDirectoryName(expectedPath));
            SetPrivateField(session, "CurrentSceneSettings", new SceneSettingsAsset {
                CanvasProfile = new SceneCanvasProfile {
                    Width = 1600,
                    Height = 900
                }
            });

            InvokePrivate(session, "HandleSceneSaveRequested", expectedPath);

            SceneAsset sceneAsset;
            using (FileStream stream = File.OpenRead(expectedPath)) {
                sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Assert.Equal(1600, sceneAsset.SceneSettings.CanvasProfile.Width);
            Assert.Equal(900, sceneAsset.SceneSettings.CanvasProfile.Height);
        }

        /// <summary>
        /// Ensures successful scene saves recompute the editor title from the saved scene name and project file name.
        /// </summary>
        [Fact]
        public void HandleSceneSaveRequested_WhenSaveSucceeds_UpdatesTitleToSceneNameAndProjectFileName() {
            EditorSession session = CreateSessionForSceneSave();
            string expectedPath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "Saved.helen");
            Directory.CreateDirectory(Path.GetDirectoryName(expectedPath));

            InvokePrivate(session, "HandleSceneSaveRequested", expectedPath);

            EditorTitleBar titleBar = GetPrivateField<EditorTitleBar>(session, "titleBar");
            Assert.Equal("Saved - helengine - city", titleBar.Title);
        }

        /// <summary>
        /// Ensures mutating the current scene marks the visible map title as dirty.
        /// </summary>
        [Fact]
        public void HandleSceneMutated_WhenCurrentSceneHasPath_AppendsDirtyMarkerToWindowTitle() {
            EditorSession session = CreateSessionForSceneSave();
            string currentScenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "DirtyScene.helen");

            SetPrivateField(session, "CurrentScenePath", currentScenePath);
            InvokePrivate(session, "RefreshWindowTitle");
            InvokePrivate(session, "HandleSceneMutated");
            InvokePrivate(session, "RefreshWindowTitle");

            EditorTitleBar titleBar = GetPrivateField<EditorTitleBar>(session, "titleBar");
            Assert.Equal("DirtyScene* - helengine - city", titleBar.Title);
        }

        /// <summary>
        /// Ensures saving a dirty current scene clears the visible dirty marker from the title.
        /// </summary>
        [Fact]
        public void HandleSceneSaveRequested_WhenDirtySceneIsSaved_RemovesDirtyMarkerFromWindowTitle() {
            EditorSession session = CreateSessionForSceneSave();
            string savePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "Saved.helen");
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));

            SetPrivateField(session, "CurrentScenePath", savePath);
            InvokePrivate(session, "HandleSceneMutated");
            InvokePrivate(session, "RefreshWindowTitle");
            InvokePrivate(session, "HandleSceneSaveRequested", savePath);

            EditorTitleBar titleBar = GetPrivateField<EditorTitleBar>(session, "titleBar");
            Assert.Equal("Saved - helengine - city", titleBar.Title);
        }

        /// <summary>
        /// Ensures Ctrl+S saves the current scene through the existing Save Map flow when a scene path already exists.
        /// </summary>
        [Fact]
        public void HandleGlobalSaveShortcut_WhenCurrentScenePathExists_SavesTheScene() {
            EditorSession session = CreateSessionForSceneSave();
            string savePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "ShortcutSave.helen");
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));

            SetPrivateField(session, "CurrentScenePath", savePath);

            InvokePrivate(session, "HandleGlobalSaveShortcut");

            Assert.True(File.Exists(savePath));
            Assert.Equal(savePath, GetPrivateField<string>(session, "CurrentScenePath"));
        }

        /// <summary>
        /// Ensures Ctrl+S shows the save dialog when the current scene has not been saved yet.
        /// </summary>
        [Fact]
        public void HandleGlobalSaveShortcut_WhenCurrentScenePathIsEmpty_ShowsSaveFileDialog() {
            EditorSession session = CreateSessionForSceneSave();

            SetPrivateField(session, "CurrentScenePath", string.Empty);
            InvokePrivate(session, "HandleGlobalSaveShortcut");

            SaveFileDialog saveFileDialog = GetPrivateField<SaveFileDialog>(session, "saveFileDialog");
            Assert.True(saveFileDialog.IsVisible);
        }

        /// <summary>
        /// Ensures the editor session can save a scene that contains an FPS overlay component.
        /// </summary>
        [Fact]
        public void HandleSceneSaveRequested_WhenSceneContainsFpsComponent_WritesSceneFile() {
            EditorSession session = CreateSessionForSceneSave();

            EditorEntity entity = Assert.IsType<EditorEntity>(CoreValue.EntityFactory.Create("FPS"));
            entity.LayerMask = EditorLayerMasks.SceneObjects;
            FPSComponent fps = new FPSComponent();
            entity.AddComponent(fps);
            CoreValue.ObjectManager.RegisterEntity(entity);

            string expectedPath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "Fps.helen");
            Directory.CreateDirectory(Path.GetDirectoryName(expectedPath));

            InvokePrivate(session, "HandleSceneSaveRequested", expectedPath);

            Assert.True(File.Exists(expectedPath));
        }

        /// <summary>
        /// Ensures requesting Scene Settings shows the scene settings dialog.
        /// </summary>
        [Fact]
        public void HandleSceneSettingsRequested_WhenInvoked_ShowsSceneSettingsDialog() {
            EditorSession session = CreateSessionForSceneSave();

            InvokePrivate(session, "HandleSceneSettingsRequested");

            SceneSettingsDialog sceneSettingsDialog = GetPrivateField<SceneSettingsDialog>(session, "sceneSettingsDialog");
            Assert.True(sceneSettingsDialog.IsVisible);
        }

        /// <summary>
        /// Ensures confirmed scene settings update the tracked canvas profile and mark the scene dirty when changed.
        /// </summary>
        [Fact]
        public void HandleSceneSettingsDialogConfirmed_WhenCanvasProfileChanges_UpdatesStateAndMarksSceneDirty() {
            EditorSession session = CreateSessionForSceneSave();
            SceneSettingsAsset updatedSettings = new SceneSettingsAsset {
                CanvasProfile = new SceneCanvasProfile {
                    Width = 1600,
                    Height = 900
                }
            };
            Action handleSceneMutated = () => InvokePrivate(session, "HandleSceneMutated");

            try {
                InteractionServices.SceneMutation.SceneMutated += handleSceneMutated;
                InvokePrivate(session, "HandleSceneSettingsDialogConfirmed", updatedSettings);
            } finally {
                InteractionServices.SceneMutation.SceneMutated -= handleSceneMutated;
            }

            SceneSettingsAsset currentSceneSettings = GetPrivateField<SceneSettingsAsset>(session, "CurrentSceneSettings");
            EditorSceneCanvasProfileState sceneCanvasProfileState = GetPrivateField<EditorSceneCanvasProfileState>(session, "sceneCanvasProfileState");
            bool isSceneDirty = GetPrivateField<bool>(session, "IsSceneDirty");
            Assert.Equal(1600, currentSceneSettings.CanvasProfile.Width);
            Assert.Equal(900, currentSceneSettings.CanvasProfile.Height);
            Assert.Equal(1600, sceneCanvasProfileState.CanvasWidth);
            Assert.Equal(900, sceneCanvasProfileState.CanvasHeight);
            Assert.True(isSceneDirty);
        }

        /// <summary>
        /// Ensures confirmed scene settings mark the scene dirty when the dont-unload flag changes.
        /// </summary>
        [Fact]
        public void HandleSceneSettingsDialogConfirmed_WhenDontUnloadChanges_UpdatesStateAndMarksSceneDirty() {
            EditorSession session = CreateSessionForSceneSave();
            SceneSettingsAsset updatedSettings = new SceneSettingsAsset {
                CanvasProfile = new SceneCanvasProfile {
                    Width = SceneCanvasProfile.DefaultWidth,
                    Height = SceneCanvasProfile.DefaultHeight
                },
                DontUnload = true
            };
            Action handleSceneMutated = () => InvokePrivate(session, "HandleSceneMutated");

            try {
                InteractionServices.SceneMutation.SceneMutated += handleSceneMutated;
                InvokePrivate(session, "HandleSceneSettingsDialogConfirmed", updatedSettings);
            } finally {
                InteractionServices.SceneMutation.SceneMutated -= handleSceneMutated;
            }

            SceneSettingsAsset currentSceneSettings = GetPrivateField<SceneSettingsAsset>(session, "CurrentSceneSettings");
            bool isSceneDirty = GetPrivateField<bool>(session, "IsSceneDirty");
            Assert.True(currentSceneSettings.DontUnload);
            Assert.True(isSceneDirty);
        }

        /// <summary>
        /// Ensures successful scene saves persist the dont-unload scene setting.
        /// </summary>
        [Fact]
        public void HandleSceneSaveRequested_WhenDontUnloadIsEnabled_PersistsSceneSettingsFlag() {
            EditorSession session = CreateSessionForSceneSave();
            string expectedPath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "Persistent.helen");
            Directory.CreateDirectory(Path.GetDirectoryName(expectedPath));
            SetPrivateField(session, "CurrentSceneSettings", new SceneSettingsAsset {
                CanvasProfile = new SceneCanvasProfile {
                    Width = 1600,
                    Height = 900
                },
                DontUnload = true
            });

            InvokePrivate(session, "HandleSceneSaveRequested", expectedPath);

            using FileStream stream = File.OpenRead(expectedPath);
            SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            Assert.True(sceneAsset.SceneSettings.DontUnload);
        }

        /// <summary>
        /// Ensures Play does not save a scene without unsaved changes: in-memory settings that were never marked dirty
        /// must not reach the file.
        /// </summary>
        [Fact]
        public void SaveCurrentSceneForPlay_WhenSceneIsClean_DoesNotSaveTheScene() {
            EditorSession session = CreateSessionForSceneSave();
            string scenePath = SaveSceneThenChangeSettings(session, "PlayClean.helen");

            bool ready = InvokePrivate<bool>(session, "SaveCurrentSceneForPlay");

            Assert.True(ready);
            Assert.False(ReadSavedDontUnload(scenePath));
        }

        /// <summary>
        /// Ensures Play saves a scene with unsaved changes before cooking it and records it as clean afterwards.
        /// </summary>
        [Fact]
        public void SaveCurrentSceneForPlay_WhenSceneIsDirty_SavesTheScene() {
            EditorSession session = CreateSessionForSceneSave();
            string scenePath = SaveSceneThenChangeSettings(session, "PlayDirty.helen");
            EditorSceneLifecycleService lifecycle = GetPrivateField<EditorSceneLifecycleService>(session, "SceneLifecycleService");
            lifecycle.IsSceneDirty = true;

            bool ready = InvokePrivate<bool>(session, "SaveCurrentSceneForPlay");

            Assert.True(ready);
            Assert.True(ReadSavedDontUnload(scenePath));
            Assert.False(lifecycle.IsSceneDirty);
        }

        /// <summary>
        /// Ensures the concurrent-edit policy for a Play preparing in the background: a save requested while Play is
        /// preparing is refused, so the file the background cook reads stays the scene as it was saved when Play was
        /// pressed, and the edit stays unsaved in the editor; once the preparation completed, the same save goes through
        /// and reaches the next Play.
        /// </summary>
        [Fact]
        public void HandleSceneSaveRequested_WhilePlayIsPreparing_KeepsThePressTimeSceneOnDisk() {
            EditorSession session = CreateSessionForSceneSave();
            string scenePath = SaveSceneThenChangeSettings(session, "PlayPreparing.helen");
            EditorSceneLifecycleService lifecycle = GetPrivateField<EditorSceneLifecycleService>(session, "SceneLifecycleService");
            lifecycle.IsSceneDirty = true;
            EditorMainThreadActionQueue queue = new EditorMainThreadActionQueue();
            EditorPlaySessionService playSession = StartPreparingPlay(session, scenePath, queue);
            Assert.True(playSession.IsPreparationActive);

            InvokePrivate(session, "HandleSceneSaveRequested", scenePath);

            Assert.False(ReadSavedDontUnload(scenePath));
            Assert.True(lifecycle.IsSceneDirty);

            queue.ProcessPending();
            Assert.False(playSession.IsPreparationActive);
            InvokePrivate(session, "HandleSceneSaveRequested", scenePath);

            Assert.True(ReadSavedDontUnload(scenePath));
            Assert.False(lifecycle.IsSceneDirty);
        }

        /// <summary>
        /// Ensures Save in the unsaved-changes dialog, refused because Play is preparing, says so in the dialog, which
        /// stays open so the user can choose again, and that the dialog asks its normal question the next time it opens.
        /// </summary>
        [Fact]
        public void UnsavedChangesSave_WhilePlayIsPreparing_SaysWhyInTheOpenDialog() {
            EditorSession session = CreateSessionForSceneSave();
            string scenePath = SaveSceneThenChangeSettings(session, "PlayPreparingPrompt.helen");
            GetPrivateField<EditorSceneLifecycleService>(session, "SceneLifecycleService").IsSceneDirty = true;
            UnsavedChangesDialog dialog = new UnsavedChangesDialog(CoreValue, InteractionServices, CreateFont());
            SetPrivateField(session, "unsavedChangesDialog", dialog);
            dialog.Show();
            StartPreparingPlay(session, scenePath, new EditorMainThreadActionQueue());

            InvokePrivate(session, "HandleUnsavedChangesSaveRequested");

            Assert.True(dialog.Enabled);
            Assert.Equal(UnsavedChangesDialog.PlayPreparingSaveRefusedMessage, dialog.Message);
            Assert.False(ReadSavedDontUnload(scenePath));

            dialog.Hide();
            dialog.Show();
            Assert.Equal(UnsavedChangesDialog.DefaultMessage, dialog.Message);
        }

        /// <summary>
        /// Ensures building and reloading scripts is refused while Play is preparing, because a reload unloads the script
        /// assemblies the background cook may be resolving types from. The fixture owns no hot-reload service, so a
        /// build that was not refused would fail on it.
        /// </summary>
        [Fact]
        public void HandleBuildScriptsRequested_WhilePlayIsPreparing_IsRefused() {
            EditorSession session = CreateSessionForSceneSave();
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "PlayScripts.helen");
            InvokePrivate(session, "HandleSceneSaveRequested", scenePath);
            StartPreparingPlay(session, scenePath, new EditorMainThreadActionQueue());
            List<string> warnings = new List<string>();
            Action<LogEntry> captureWarning = entry => warnings.Add(entry.Message);

            Logger.WarningLogged += captureWarning;
            try {
                InvokePrivate(session, "HandleBuildScriptsRequested");
            } finally {
                Logger.WarningLogged -= captureWarning;
            }

            Assert.Contains("Play is preparing; build the scripts again once the player has started or Play was stopped.", warnings);
        }

        /// <summary>
        /// Gives the lightweight session a Play session whose preparation already ran inline but whose completion was not
        /// yet delivered to the frame thread, so the session sees a Play that is still preparing.
        /// </summary>
        /// <param name="session">Session under test.</param>
        /// <param name="scenePath">Saved scene path Play prepares.</param>
        /// <param name="queue">Main-thread queue the preparation reports through; left undrained.</param>
        /// <returns>The Play session, preparing.</returns>
        EditorPlaySessionService StartPreparingPlay(EditorSession session, string scenePath, EditorMainThreadActionQueue queue) {
            EditorPlaySessionService playSession = new EditorPlaySessionService(
                new managers.play.FakePlayCookRunner(new EditorPlayCookResult(true, "Cooked.", @"C:\project\cache\play\ps2-staging", "scene", "scenes/scene.helen")),
                new managers.play.FakePlayScriptBuilder(EditorBuildExecutionResult.Success("Scripts built."), @"C:\project\cache\play\modules"),
                new managers.play.FakePlayProcessLauncher(),
                @"C:\player\helengine.player.exe",
                queue,
                new managers.play.InlinePlayPreparationScheduler());
            SetPrivateField(session, "PlaySession", playSession);
            EditorBuildExecutionResult result = playSession.Play(
                "ps2", "PlayStation 2", scenePath, () => true,
                @"C:\platforms\ps2\helengine.ps2.player.dll", "helengine.ps2.player.Ps2PlayerAdapter",
                60d, helengine.baseplatform.Definitions.PlayerInputDevices.Gamepad, false, launchResult => { });
            Assert.True(result.Succeeded, result.Message);
            return playSession;
        }

        /// <summary>
        /// Saves the session's scene through the regular save path, which leaves it clean, then changes the in-memory
        /// scene settings so a later save is visible in the file.
        /// </summary>
        /// <param name="session">Session whose scene is saved.</param>
        /// <param name="fileName">Scene file name beneath `assets/Scenes`.</param>
        /// <returns>Absolute path of the saved scene.</returns>
        string SaveSceneThenChangeSettings(EditorSession session, string fileName) {
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", fileName);
            InvokePrivate(session, "HandleSceneSaveRequested", scenePath);
            Assert.False(GetPrivateField<EditorSceneLifecycleService>(session, "SceneLifecycleService").IsSceneDirty);
            Assert.False(ReadSavedDontUnload(scenePath));
            GetPrivateField<SceneSettingsAsset>(session, "CurrentSceneSettings").DontUnload = true;
            return scenePath;
        }

        /// <summary>
        /// Reads the persisted keep-loaded flag of one saved scene.
        /// </summary>
        /// <param name="scenePath">Absolute path of the saved scene.</param>
        /// <returns>The saved scene's <see cref="SceneSettingsAsset.DontUnload"/> value.</returns>
        bool ReadSavedDontUnload(string scenePath) {
            using FileStream stream = File.OpenRead(scenePath);
            SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            return sceneAsset.SceneSettings.DontUnload;
        }

        /// <summary>
        /// Invokes one non-public instance method and returns its result.
        /// </summary>
        /// <typeparam name="T">Expected return type.</typeparam>
        /// <param name="target">Target object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        /// <param name="arguments">Arguments passed to the method.</param>
        /// <returns>Value returned by the method.</returns>
        T InvokePrivate<T>(object target, string methodName, params object[] arguments) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            return Assert.IsType<T>(method.Invoke(target, arguments));
        }

        /// <summary>
        /// Creates a partially initialized editor session containing only the collaborators used by scene save handlers.
        /// </summary>
        /// <returns>Editor session instance configured for scene save tests.</returns>
        EditorSession CreateSessionForSceneSave() {
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();

            EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));
            AssetBrowserPanel assetBrowserPanel = new AssetBrowserPanel(CoreValue, InteractionServices, CreateFont(), TempProjectRootPath, new GeneratedAssetProviderRegistry());
            SaveFileDialog saveFileDialog = new SaveFileDialog(CoreValue, InteractionServices, CreateFont(), TempProjectRootPath, new GeneratedAssetProviderRegistry());
            SceneSavePathResolver pathResolver = new SceneSavePathResolver(TempProjectRootPath);
            SceneSaveService saveService = new SceneSaveService(
                GeneratedAssetGraph.CreateAuthoringSession(TempProjectRootPath),
                registry);
            SceneSettingsAsset currentSceneSettings = new SceneSettingsAsset();
            EditorSceneCanvasProfileState sceneCanvasProfileState = new EditorSceneCanvasProfileState();
            sceneCanvasProfileState.ApplySceneSettings(currentSceneSettings);
            SceneSettingsDialog sceneSettingsDialog = new SceneSettingsDialog(CoreValue, InteractionServices, CreateFont(), EditorUiMetrics.Default);
            EditorTitleBar titleBar = new EditorTitleBar(CoreValue, InteractionServices, CreateFont(), 1280, 720, "helengine - city");
            EditorHistoryCaptureService historyCaptureService = new EditorHistoryCaptureService(saveService);
            EditorUndoRedoService undoRedoService = new EditorUndoRedoService(new EditorHistoryContext());
            EditorMutationService historyMutationService = new EditorMutationService(
                undoRedoService,
                historyCaptureService,
                new ComponentHistoryAdapterRegistry(),
                () => InteractionServices.SceneMutation.MarkSceneMutated());

            SetPrivateField(session, "SceneLifecycleService", new EditorSceneLifecycleService(new EditorProjectSceneCatalogService(TempProjectRootPath)));
            SetPrivateField(session, "interactionServices", InteractionServices);
            SetPrivateField(session, "assetBrowserPanel", assetBrowserPanel);
            SetPrivateField(session, "saveFileDialog", saveFileDialog);
            SetPrivateField(session, "SceneSavePathResolver", pathResolver);
            SetPrivateField(session, "SceneSaveService", saveService);
            SetPrivateField(session, "CurrentScenePath", string.Empty);
            SetPrivateField(session, "CurrentSceneSettings", currentSceneSettings);
            SetPrivateField(session, "sceneCanvasProfileState", sceneCanvasProfileState);
            SetPrivateField(session, "sceneSettingsDialog", sceneSettingsDialog);
            SetPrivateField(session, "titleBar", titleBar);
            SetPrivateField(session, "ProjectName", "city");
            SetPrivateField(session, "HistoryCaptureService", historyCaptureService);
            SetPrivateField(session, "UndoRedoService", undoRedoService);
            SetPrivateField(session, "HistoryMutationService", historyMutationService);

            return session;
        }

        /// <summary>
        /// Invokes one non-public instance method.
        /// </summary>
        /// <param name="target">Target object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        /// <param name="arguments">Arguments passed to the method.</param>
        void InvokePrivate(object target, string methodName, params object[] arguments) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, arguments);
        }

        /// <summary>
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            return Assert.IsType<T>(EditorSessionPrivateMemberAccessor.GetValue(target, fieldName));
        }

        /// <summary>
        /// Assigns one non-public instance field.
        /// </summary>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to assign.</param>
        /// <param name="value">Value assigned to the field.</param>
        void SetPrivateField(object target, string fieldName, object value) {
            EditorSessionPrivateMemberAccessor.SetValue(target, fieldName, value);
        }

        /// <summary>
        /// Creates a small font asset that can satisfy the layout requirements of the save UI.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['v'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['F'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['D'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['A'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f)
            };

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
