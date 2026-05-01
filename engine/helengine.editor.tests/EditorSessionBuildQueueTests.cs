using System.Reflection;
using System.Runtime.CompilerServices;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies editor-session wiring for the Build dialog and persisted build queue workflow.
    /// </summary>
    public sealed class EditorSessionBuildQueueTests : IDisposable {
        /// <summary>
        /// Gets the isolated temporary project root used by the current test instance.
        /// </summary>
        string TempProjectRootPath { get; }

        /// <summary>
        /// Gets the project-relative current scene identifier used by the tests.
        /// </summary>
        string CurrentSceneId => "Scenes/City.helen";

        /// <summary>
        /// Gets the absolute path to the current scene file used by the tests.
        /// </summary>
        string CurrentScenePath => Path.Combine(TempProjectRootPath, "assets", "Scenes", "City.helen");

        /// <summary>
        /// Initializes one isolated temporary project root and scene catalog for the current test instance.
        /// </summary>
        public EditorSessionBuildQueueTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-build-queue-session-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "Scenes"));
            File.WriteAllText(CurrentScenePath, "{}");
            File.WriteAllText(Path.Combine(TempProjectRootPath, "assets", "Scenes", "Menu.helen"), "{}");

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempProjectRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Deletes the temporary project root created for the current test instance.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures opening the Build dialog seeds the current scene on first use and renders the enabled project platforms.
        /// </summary>
        [Fact]
        public void HandleBuildRequested_WhenInvoked_ShowsDialogWithCurrentSceneSeededForActivePlatform() {
            EditorBuildConfigService buildConfigService = new EditorBuildConfigService(TempProjectRootPath);
            EditorBuildQueueService buildQueueService = new EditorBuildQueueService(buildConfigService, new TestEditorBuildExecutor([]));
            EditorSession session = CreateSession(buildConfigService, buildQueueService, "windows");
            BuildDialog dialog = GetPrivateField<BuildDialog>(session, "buildDialog");

            InvokePrivate(session, "HandleBuildRequested");

            Assert.True(dialog.IsVisible);
            Assert.Equal("windows", GetPrivateField<string>(dialog, "ActivePlatformId"));
            List<CheckBoxComponent> mapCheckBoxes = GetPrivateField<List<CheckBoxComponent>>(dialog, "MapCheckBoxes");
            List<TextComponent> mapLabelTexts = GetPrivateField<List<TextComponent>>(dialog, "MapLabelTexts");
            Assert.Collection(
                mapLabelTexts,
                label => Assert.Equal("Scenes/City.helen", label.Text),
                label => Assert.Equal("Scenes/Menu.helen", label.Text));
            Assert.True(mapCheckBoxes[0].IsChecked);
            Assert.False(mapCheckBoxes[1].IsChecked);
        }

        /// <summary>
        /// Ensures adding a build appends one pending queue item and persists it to the local build config.
        /// </summary>
        [Fact]
        public void HandleBuildDialogAddRequested_WhenInvoked_PersistsOnePendingQueueItem() {
            EditorBuildConfigService buildConfigService = new EditorBuildConfigService(TempProjectRootPath);
            EditorBuildQueueService buildQueueService = new EditorBuildQueueService(buildConfigService, new TestEditorBuildExecutor([]));
            EditorSession session = CreateSession(buildConfigService, buildQueueService, "windows");

            InvokePrivate(session, "HandleBuildDialogAddRequested", new BuildDialogAddRequest("windows", [
                CurrentSceneId
            ], @"C:\builds\windows"));

            EditorBuildConfigDocument persistedDocument = buildConfigService.Load([
                "windows"
            ], CurrentSceneId);
            EditorBuildQueueItemDocument queueItem = Assert.Single(persistedDocument.QueueItems);
            Assert.Equal("windows", queueItem.PlatformId);
            Assert.Equal(EditorBuildQueueItemStatus.Pending, queueItem.Status);
            Assert.Equal(
                new[] {
                    CurrentSceneId
                },
                queueItem.SelectedSceneIds);
            Assert.Equal(@"C:\builds\windows", queueItem.OutputDirectoryPath);
        }

        /// <summary>
        /// Ensures the session snapshots the active platform's debug-build flag into the persisted queue item.
        /// </summary>
        [Fact]
        public void HandleBuildDialogAddRequested_WhenDebugBuildIsEnabled_PersistsTheDebugBuildFlag() {
            EditorBuildConfigService buildConfigService = new EditorBuildConfigService(TempProjectRootPath);
            EditorBuildQueueService buildQueueService = new EditorBuildQueueService(buildConfigService, new TestEditorBuildExecutor([]));
            EditorSession session = CreateSession(buildConfigService, buildQueueService, "windows");

            InvokePrivate(session, "HandleBuildDialogAddRequested", new BuildDialogAddRequest("windows", [
                CurrentSceneId
            ], @"C:\builds\windows", true));

            EditorBuildConfigDocument persistedDocument = buildConfigService.Load([
                "windows"
            ], CurrentSceneId);
            EditorBuildQueueItemDocument queueItem = Assert.Single(persistedDocument.QueueItems);

            Assert.True(queueItem.DebugBuild);
        }

        /// <summary>
        /// Ensures the session does not persist a queued build when the requested output folder is blank.
        /// </summary>
        [Fact]
        public void HandleBuildDialogAddRequested_WhenOutputFolderIsBlank_DoesNotPersistQueueItem() {
            EditorBuildConfigService buildConfigService = new EditorBuildConfigService(TempProjectRootPath);
            EditorBuildQueueService buildQueueService = new EditorBuildQueueService(buildConfigService, new TestEditorBuildExecutor([]));
            EditorSession session = CreateSession(buildConfigService, buildQueueService, "windows");

            InvokePrivate(session, "HandleBuildDialogAddRequested", new BuildDialogAddRequest("windows", [
                CurrentSceneId
            ], ""));

            EditorBuildConfigDocument persistedDocument = buildConfigService.Load([
                "windows"
            ], CurrentSceneId);
            Assert.Empty(persistedDocument.QueueItems);
        }

        /// <summary>
        /// Ensures adding one queued build registers only one visible queue summary text drawable.
        /// </summary>
        [Fact]
        public void HandleBuildDialogAddRequested_WhenInvoked_RegistersOneQueueSummaryTextDrawable() {
            EditorBuildConfigService buildConfigService = new EditorBuildConfigService(TempProjectRootPath);
            EditorBuildQueueService buildQueueService = new EditorBuildQueueService(buildConfigService, new TestEditorBuildExecutor([]));
            EditorSession session = CreateSession(buildConfigService, buildQueueService, "windows");
            BuildDialog dialog = GetPrivateField<BuildDialog>(session, "buildDialog");
            dialog.Show([
                    "windows"
                ],
                [
                    CurrentSceneId
                ],
                "windows",
                buildConfigService.Load([
                    "windows"
                ], CurrentSceneId));

            InvokePrivate(session, "HandleBuildDialogAddRequested", new BuildDialogAddRequest("windows", [
                CurrentSceneId
            ], @"C:\builds\windows"));

            int matchingQueueTextCount = 0;
            for (int index = 0; index < Core.Instance.ObjectManager.Drawables2D.Count; index++) {
                IDrawable2D drawable = Core.Instance.ObjectManager.Drawables2D[index];
                if (drawable is TextComponent textComponent && textComponent.Text == "windows | Pending | 1 scene(s)") {
                    matchingQueueTextCount++;
                }
            }

            Assert.Equal(1, matchingQueueTextCount);
        }

        /// <summary>
        /// Ensures adding one queued build positions the visible queue summary inside the dialog's right column instead of near the host origin.
        /// </summary>
        [Fact]
        public void HandleBuildDialogAddRequested_WhenInvoked_PositionsQueueSummaryInsideRightColumn() {
            EditorBuildConfigService buildConfigService = new EditorBuildConfigService(TempProjectRootPath);
            EditorBuildQueueService buildQueueService = new EditorBuildQueueService(buildConfigService, new TestEditorBuildExecutor([]));
            EditorSession session = CreateSession(buildConfigService, buildQueueService, "windows");
            BuildDialog dialog = GetPrivateField<BuildDialog>(session, "buildDialog");
            dialog.Show([
                    "windows"
                ],
                [
                    CurrentSceneId
                ],
                "windows",
                buildConfigService.Load([
                    "windows"
                ], CurrentSceneId));

            InvokePrivate(session, "HandleBuildDialogAddRequested", new BuildDialogAddRequest("windows", [
                CurrentSceneId
            ], @"C:\builds\windows"));

            List<EditorEntity> queueItemHosts = GetPrivateField<List<EditorEntity>>(dialog, "QueueItemHosts");
            EditorEntity queueItemHost = Assert.Single(queueItemHosts);

            Assert.True(queueItemHost.Position.X > BuildDialog.PanelWidth / 2f);
            Assert.True(queueItemHost.Position.Y > BuildDialog.HeaderHeight);
        }

        /// <summary>
        /// Ensures rebuilding the dialog with an existing queued item does not leave a stale queue-summary drawable behind.
        /// </summary>
        [Fact]
        public void HandleBuildDialogAddRequested_WhenQueueAlreadyContainsItem_DoesNotLeakOldQueueSummaryDrawable() {
            EditorBuildConfigService buildConfigService = new EditorBuildConfigService(TempProjectRootPath);
            EditorBuildConfigDocument existingBuildConfig = buildConfigService.Load([
                "windows"
            ], CurrentSceneId);
            existingBuildConfig.QueueItems.Add(new EditorBuildQueueItemDocument {
                QueueItemId = "queue-0",
                PlatformId = "windows",
                SelectedSceneIds = [
                    CurrentSceneId
                ],
                OutputDirectoryPath = @"C:\builds\existing",
                Status = EditorBuildQueueItemStatus.Pending
            });
            buildConfigService.Save(existingBuildConfig);

            EditorBuildQueueService buildQueueService = new EditorBuildQueueService(buildConfigService, new TestEditorBuildExecutor([]));
            EditorSession session = CreateSession(buildConfigService, buildQueueService, "windows");
            BuildDialog dialog = GetPrivateField<BuildDialog>(session, "buildDialog");
            dialog.Show([
                    "windows"
                ],
                [
                    CurrentSceneId
                ],
                "windows",
                buildConfigService.Load([
                    "windows"
                ], CurrentSceneId));

            InvokePrivate(session, "HandleBuildDialogAddRequested", new BuildDialogAddRequest("windows", [
                CurrentSceneId
            ], @"C:\builds\windows"));

            int matchingQueueTextCount = 0;
            for (int index = 0; index < Core.Instance.ObjectManager.Drawables2D.Count; index++) {
                IDrawable2D drawable = Core.Instance.ObjectManager.Drawables2D[index];
                if (drawable is TextComponent textComponent && textComponent.Text == "windows | Pending | 1 scene(s)") {
                    matchingQueueTextCount++;
                }
            }

            List<TextComponent> queueItemTexts = GetPrivateField<List<TextComponent>>(dialog, "QueueItemTexts");

            Assert.Equal(2, queueItemTexts.Count);
            Assert.Equal(queueItemTexts.Count, matchingQueueTextCount);
        }

        /// <summary>
        /// Ensures running the queue executes persisted pending items and rewrites their resulting status.
        /// </summary>
        [Fact]
        public void HandleBuildDialogBuildQueueRequested_WhenPendingItemsExist_RunsQueueAndPersistsStatuses() {
            EditorBuildConfigService buildConfigService = new EditorBuildConfigService(TempProjectRootPath);
            EditorBuildConfigDocument buildConfig = buildConfigService.Load([
                "windows"
            ], CurrentSceneId);
            buildConfig.QueueItems.Add(new EditorBuildQueueItemDocument {
                QueueItemId = "queue-1",
                PlatformId = "windows",
                SelectedSceneIds = [
                    CurrentSceneId
                ],
                OutputDirectoryPath = @"C:\builds\windows",
                Status = EditorBuildQueueItemStatus.Pending
            });
            buildConfigService.Save(buildConfig);
            TestEditorBuildExecutor buildExecutor = new TestEditorBuildExecutor([
                EditorBuildExecutionResult.Success("Build completed.")
            ]);
            EditorBuildQueueService buildQueueService = new EditorBuildQueueService(buildConfigService, buildExecutor);
            EditorSession session = CreateSession(buildConfigService, buildQueueService, "windows");

            InvokePrivate(session, "HandleBuildDialogBuildQueueRequested");

            EditorBuildConfigDocument persistedDocument = buildConfigService.Load([
                "windows"
            ], CurrentSceneId);
            EditorBuildQueueItemDocument queueItem = Assert.Single(persistedDocument.QueueItems);
            Assert.Single(buildExecutor.ExecutedQueueItemIds);
            Assert.Equal(EditorBuildQueueItemStatus.Done, queueItem.Status);
            Assert.Equal("Build completed.", queueItem.StatusMessage);
        }

        /// <summary>
        /// Ensures removing one queued build deletes it from persisted local build state regardless of its status.
        /// </summary>
        [Fact]
        public void HandleBuildDialogRemoveQueueItemRequested_WhenQueueItemExists_RemovesPersistedQueueItem() {
            EditorBuildConfigService buildConfigService = new EditorBuildConfigService(TempProjectRootPath);
            EditorBuildConfigDocument buildConfig = buildConfigService.Load([
                "windows"
            ], CurrentSceneId);
            buildConfig.QueueItems.Add(new EditorBuildQueueItemDocument {
                QueueItemId = "queue-1",
                PlatformId = "windows",
                SelectedSceneIds = [
                    CurrentSceneId
                ],
                OutputDirectoryPath = @"C:\builds\windows",
                Status = EditorBuildQueueItemStatus.Done
            });
            buildConfig.QueueItems.Add(new EditorBuildQueueItemDocument {
                QueueItemId = "queue-2",
                PlatformId = "windows",
                SelectedSceneIds = [
                    CurrentSceneId
                ],
                OutputDirectoryPath = @"C:\builds\windows-two",
                Status = EditorBuildQueueItemStatus.Failed
            });
            buildConfigService.Save(buildConfig);
            EditorBuildQueueService buildQueueService = new EditorBuildQueueService(buildConfigService, new TestEditorBuildExecutor([]));
            EditorSession session = CreateSession(buildConfigService, buildQueueService, "windows");

            InvokePrivate(session, "HandleBuildDialogRemoveQueueItemRequested", "queue-1");

            EditorBuildConfigDocument persistedDocument = buildConfigService.Load([
                "windows"
            ], CurrentSceneId);
            EditorBuildQueueItemDocument remainingQueueItem = Assert.Single(persistedDocument.QueueItems);
            Assert.Equal("queue-2", remainingQueueItem.QueueItemId);
        }

        /// <summary>
        /// Ensures the build-dialog browse action uses the host resolver and writes the chosen folder back into the visible output field.
        /// </summary>
        [Fact]
        public void HandleBuildDialogBrowseOutputFolderRequested_WhenResolverReturnsPath_UpdatesDialogOutputField() {
            EditorBuildConfigService buildConfigService = new EditorBuildConfigService(TempProjectRootPath);
            EditorBuildQueueService buildQueueService = new EditorBuildQueueService(buildConfigService, new TestEditorBuildExecutor([]));
            EditorSession session = CreateSession(buildConfigService, buildQueueService, "windows");
            BuildDialog dialog = GetPrivateField<BuildDialog>(session, "buildDialog");
            dialog.Show([
                    "windows"
                ],
                [
                    CurrentSceneId
                ],
                "windows",
                buildConfigService.Load([
                    "windows"
                ], CurrentSceneId));
            SetPrivateField(session, "BrowseOutputFolderResolver", new Func<string>(() => @"D:\exports\windows"));

            InvokePrivate(session, "HandleBuildDialogBrowseOutputFolderRequested");

            TextBoxComponent outputDirectoryField = GetPrivateField<TextBoxComponent>(dialog, "OutputDirectoryField");
            Assert.Equal(@"D:\exports\windows", outputDirectoryField.Text);
        }

        /// <summary>
        /// Creates one partially initialized editor session containing only the collaborators required by the Build dialog workflow.
        /// </summary>
        /// <param name="buildConfigService">Service used to persist local build dialog state.</param>
        /// <param name="buildQueueService">Service used to execute persisted pending build queue items.</param>
        /// <param name="activePlatform">Current active project platform.</param>
        /// <returns>Partially initialized editor session configured for Build dialog tests.</returns>
        EditorSession CreateSession(EditorBuildConfigService buildConfigService, EditorBuildQueueService buildQueueService, string activePlatform) {
            EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));

            SetPrivateField(session, "projectPath", TempProjectRootPath);
            SetPrivateField(session, "ProjectSupportedPlatforms", new List<string> {
                "windows"
            });
            SetPrivateField(session, "ActiveProjectPlatform", activePlatform);
            SetPrivateField(session, "CurrentScenePath", CurrentScenePath);
            SetPrivateField(session, "buildDialog", new BuildDialog(CreateFont()));
            SetPrivateField(session, "buildConfigService", buildConfigService);
            SetPrivateField(session, "buildQueueService", buildQueueService);
            SetPrivateField(session, "sceneCatalogService", new EditorProjectSceneCatalogService(TempProjectRootPath));

            return session;
        }

        /// <summary>
        /// Invokes one non-public instance method with the supplied arguments.
        /// </summary>
        /// <param name="target">Object that owns the method.</param>
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
        /// Creates one deterministic font asset for Build dialog layout tests.
        /// </summary>
        /// <returns>Font asset with stable glyph metrics.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>();
            string glyphs = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789/.:\\-_ []()";

            for (int i = 0; i < glyphs.Length; i++) {
                characters[glyphs[i]] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f);
            }

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 128,
                    Height = 128
                },
                characters,
                16f,
                128,
                128);
        }
    }
}
