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
        /// <summary>
        /// Temporary project root used by editor-session scene save tests.
        /// </summary>
        readonly string TempProjectRootPath;

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
                ContentRootPath = TempProjectRootPath
            });
            EditorSceneMutationService.Reset();
        }

        /// <summary>
        /// Deletes temporary project state after each test.
        /// </summary>
        public void Dispose() {
            EditorSceneMutationService.Reset();
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
            Assert.Equal("Saved - helengine - project.heproj", titleBar.Title);
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
            Assert.Equal("DirtyScene* - helengine - project.heproj", titleBar.Title);
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
            Assert.Equal("Saved - helengine - project.heproj", titleBar.Title);
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

            EditorEntity entity = Assert.IsType<EditorEntity>(Core.Instance.EntityFactory.Create("FPS"));
            entity.LayerMask = EditorLayerMasks.SceneObjects;
            FPSComponent fps = new FPSComponent();
            entity.AddComponent(fps);
            Core.Instance.ObjectManager.RegisterEntity(entity);

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
                EditorSceneMutationService.SceneMutated += handleSceneMutated;
                InvokePrivate(session, "HandleSceneSettingsDialogConfirmed", updatedSettings);
            } finally {
                EditorSceneMutationService.SceneMutated -= handleSceneMutated;
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
        /// Creates a partially initialized editor session containing only the collaborators used by scene save handlers.
        /// </summary>
        /// <returns>Editor session instance configured for scene save tests.</returns>
        EditorSession CreateSessionForSceneSave() {
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            registry.Register(new MeshComponentPersistenceDescriptor());
            registry.Register(new FPSComponentPersistenceDescriptor());

            EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));
            AssetBrowserPanel assetBrowserPanel = new AssetBrowserPanel(CreateFont(), TempProjectRootPath);
            SaveFileDialog saveFileDialog = new SaveFileDialog(CreateFont(), TempProjectRootPath);
            SceneSavePathResolver pathResolver = new SceneSavePathResolver(TempProjectRootPath);
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            SceneSettingsAsset currentSceneSettings = new SceneSettingsAsset();
            EditorSceneCanvasProfileState sceneCanvasProfileState = new EditorSceneCanvasProfileState();
            sceneCanvasProfileState.ApplySceneSettings(currentSceneSettings);
            SceneSettingsDialog sceneSettingsDialog = new SceneSettingsDialog(CreateFont(), EditorUiMetrics.Default);
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "helengine - project.heproj");

            SetPrivateField(session, "assetBrowserPanel", assetBrowserPanel);
            SetPrivateField(session, "saveFileDialog", saveFileDialog);
            SetPrivateField(session, "SceneSavePathResolver", pathResolver);
            SetPrivateField(session, "SceneSaveService", saveService);
            SetPrivateField(session, "CurrentScenePath", string.Empty);
            SetPrivateField(session, "CurrentSceneSettings", currentSceneSettings);
            SetPrivateField(session, "sceneCanvasProfileState", sceneCanvasProfileState);
            SetPrivateField(session, "sceneSettingsDialog", sceneSettingsDialog);
            SetPrivateField(session, "titleBar", titleBar);
            SetPrivateField(session, "ProjectDisplayName", "project.heproj");

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
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return Assert.IsType<T>(field.GetValue(target));
        }

        /// <summary>
        /// Assigns one non-public instance field.
        /// </summary>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to assign.</param>
        /// <param name="value">Value assigned to the field.</param>
        void SetPrivateField(object target, string fieldName, object value) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
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
