using System.Reflection;
using System.Runtime.CompilerServices;
using helengine.editor.tests.testing;
using helengine.platforms;
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
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
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
        /// Ensures Profiles receives only the alphabetical intersection of enabled and available platforms.
        /// </summary>
        [Fact]
        public void HandleProfilesRequested_WhenSomePlatformsAreUnavailable_ShowsOnlyEnabledAndAvailablePlatforms() {
            EditorBuildConfigService buildConfigService = new EditorBuildConfigService(TempProjectRootPath);
            EditorBuildQueueService buildQueueService = new EditorBuildQueueService(buildConfigService, new TestEditorBuildExecutor([]));
            EditorSession session = CreateSession(buildConfigService, buildQueueService, "windows");
            SetPrivateField(session, "ProjectSupportedPlatforms", new List<string> {
                "ps2",
                "windows"
            });
            ConfigureAvailablePlatforms("windows");

            InvokePrivate(session, "HandleProfilesRequested");

            ProfilesDialog dialog = GetPrivateField<ProfilesDialog>(session, "profilesDialog");
            List<string> supportedPlatformIds = GetPrivateField<List<string>>(dialog, "SupportedPlatformIds");
            Assert.Equal(["windows"], supportedPlatformIds);
        }

        /// <summary>
        /// Ensures Build shows only platforms that are both enabled and currently available.
        /// </summary>
        [Fact]
        public void HandleBuildRequested_WhenProjectContainsUnavailablePlatforms_HidesUnavailableTabs() {
            EditorBuildConfigService buildConfigService = new EditorBuildConfigService(TempProjectRootPath);
            EditorBuildQueueService buildQueueService = new EditorBuildQueueService(buildConfigService, new TestEditorBuildExecutor([]));
            EditorSession session = CreateSession(buildConfigService, buildQueueService, "windows");
            SetPrivateField(session, "ProjectSupportedPlatforms", new List<string> {
                "ps2",
                "windows"
            });
            ConfigureAvailablePlatforms("windows");

            InvokePrivate(session, "HandleBuildRequested");

            BuildDialog dialog = GetPrivateField<BuildDialog>(session, "buildDialog");
            PlatformTabStripView platformTabStrip = GetPrivateField<PlatformTabStripView>(dialog, "PlatformTabStrip");
            Assert.Equal("windows", GetPrivateField<string>(dialog, "ActivePlatformId"));
            Assert.Equal(1, platformTabStrip.TabCount);
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
                label => Assert.Equal("City", label.Text),
                label => Assert.Equal("Menu", label.Text));
            Assert.True(mapCheckBoxes[0].IsChecked);
            Assert.False(mapCheckBoxes[1].IsChecked);
        }

        /// <summary>
        /// Ensures the Build dialog copy-settings request opens the chooser modal and applies the selected source platform.
        /// </summary>
        [Fact]
        public void HandleBuildDialogCopySettingsRequested_WhenInvoked_ShowsChooserAndCopiesSelectedPlatform() {
            EditorBuildConfigService buildConfigService = new EditorBuildConfigService(TempProjectRootPath);
            EditorBuildQueueService buildQueueService = new EditorBuildQueueService(buildConfigService, new TestEditorBuildExecutor([]));
            EditorSession session = CreateSession(buildConfigService, buildQueueService, "ps2");
            SetPrivateField(session, "ProjectSupportedPlatforms", new List<string> {
                "windows",
                "ps2"
            });
            ConfigureAvailablePlatforms("windows", "ps2");

            EditorBuildConfigDocument buildConfig = new EditorBuildConfigDocument {
                Platforms = [
                    new EditorBuildPlatformConfigDocument {
                        PlatformId = "windows",
                        SelectedSceneIds = [
                            "Scenes/City.helen"
                        ]
                    },
                    new EditorBuildPlatformConfigDocument {
                        PlatformId = "ps2",
                        SelectedSceneIds = [
                            "Scenes/Menu.helen"
                        ]
                    }
                ]
            };
            buildConfigService.Save(buildConfig);

            BuildDialog dialog = GetPrivateField<BuildDialog>(session, "buildDialog");
            dialog.Show([
                    "windows",
                    "ps2"
                ],
                [
                    "Scenes/City.helen",
                    "Scenes/Menu.helen"
                ],
                "ps2",
                buildConfig);

            InvokePrivate(session, "HandleBuildDialogCopySettingsRequested");

            BuildDialogCopySettingsDialog copySettingsDialog = GetPrivateField<BuildDialogCopySettingsDialog>(session, "buildDialogCopySettingsDialog");
            ComboBoxComponent sourcePlatformComboBox = GetPrivateField<ComboBoxComponent>(copySettingsDialog, "SourceComboBox");

            Assert.True(copySettingsDialog.IsVisible);
            Assert.Equal("windows", sourcePlatformComboBox.SelectedItem);

            InvokePrivate(session, "HandleBuildDialogCopySettingsConfirmed", "windows");

            List<CheckBoxComponent> mapCheckBoxes = GetPrivateField<List<CheckBoxComponent>>(dialog, "MapCheckBoxes");
            EditorBuildPlatformConfigDocument ps2Config = Assert.Single(buildConfig.Platforms.Where(platform => platform.PlatformId == "ps2"));

            Assert.True(mapCheckBoxes[0].IsChecked);
            Assert.False(mapCheckBoxes[1].IsChecked);
            Assert.Equal([
                "Scenes/City.helen"
            ], ps2Config.SelectedSceneIds);
        }

        /// <summary>
        /// Ensures adding a build appends one pending queue item and persists it to the local build config.
        /// </summary>
        [Fact]
        public void HandleBuildDialogAddRequested_WhenInvoked_PersistsOnePendingQueueItem() {
            EditorBuildConfigService buildConfigService = new EditorBuildConfigService(TempProjectRootPath);
            EditorBuildQueueService buildQueueService = new EditorBuildQueueService(buildConfigService, new TestEditorBuildExecutor([]));
            EditorSession session = CreateSession(buildConfigService, buildQueueService, "windows");

            InvokePrivate(session, "HandleBuildDialogAddRequested", new BuildDialogAddRequest(
                "windows",
                [
                    CurrentSceneId
                ],
                @"C:\builds\windows",
                false,
                "release",
                "default",
                "default",
                "loose-files",
                "windows-install-tree",
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                new Dictionary<string, string>()));

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
            Assert.Equal("loose-files", queueItem.SelectedStorageProfileId);
            Assert.Equal("windows-install-tree", queueItem.SelectedMediaProfileId);
        }

        /// <summary>
        /// Ensures adding one queued build refreshes the visible dialog without discarding a manual panel position.
        /// </summary>
        [Fact]
        public void HandleBuildDialogAddRequested_WhenDialogWasMoved_PreservesDialogPosition() {
            EditorBuildConfigService buildConfigService = new EditorBuildConfigService(TempProjectRootPath);
            EditorBuildQueueService buildQueueService = new EditorBuildQueueService(buildConfigService, new TestEditorBuildExecutor([]));
            EditorSession session = CreateSession(buildConfigService, buildQueueService, "windows");
            BuildDialog dialog = GetPrivateField<BuildDialog>(session, "buildDialog");
            EditorBuildConfigDocument buildConfig = buildConfigService.Load([
                "windows"
            ], CurrentSceneId);

            dialog.Show([
                    "windows"
                ],
                [
                    CurrentSceneId
                ],
                "windows",
                buildConfig);
            dialog.UpdateLayout(1280, 960);
            SetPrivateField(dialog, "PanelPosition", new int2(164, 118));
            SetPrivateField(dialog, "IsUserPositioned", true);
            InvokePrivate(dialog, "ApplyDialogPosition");

            InvokePrivate(session, "HandleBuildDialogAddRequested", new BuildDialogAddRequest(
                "windows",
                [
                    CurrentSceneId
                ],
                @"C:\builds\windows"));

            Assert.Equal(new int2(164, 118), GetPrivateField<int2>(dialog, "PanelPosition"));
            Assert.True(GetPrivateField<bool>(dialog, "IsUserPositioned"));
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
                if (drawable is TextComponent textComponent && textComponent.Text.StartsWith("windows | Pending\n1 scene(s) | Release", StringComparison.Ordinal)) {
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
                if (drawable is TextComponent textComponent && textComponent.Text.StartsWith("windows | Pending\n1 scene(s) | Release", StringComparison.Ordinal)) {
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
        /// Ensures removing one queued build refreshes the visible dialog without discarding a manual panel position.
        /// </summary>
        [Fact]
        public void HandleBuildDialogRemoveQueueItemRequested_WhenDialogWasMoved_PreservesDialogPosition() {
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
            dialog.UpdateLayout(1280, 960);
            SetPrivateField(dialog, "PanelPosition", new int2(212, 146));
            SetPrivateField(dialog, "IsUserPositioned", true);
            InvokePrivate(dialog, "ApplyDialogPosition");

            InvokePrivate(session, "HandleBuildDialogRemoveQueueItemRequested", "queue-1");

            Assert.Equal(new int2(212, 146), GetPrivateField<int2>(dialog, "PanelPosition"));
            Assert.True(GetPrivateField<bool>(dialog, "IsUserPositioned"));
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
            SetPrivateField(session, "ProjectLocalSettingsService", new EditorProjectLocalSettingsService(TempProjectRootPath, [
                "windows"
            ]));
            SetPrivateField(session, "buildDialog", new BuildDialog(CreateFont()));
            SetPrivateField(session, "buildDialogCopySettingsDialog", new BuildDialogCopySettingsDialog(CreateFont()));
            SetPrivateField(session, "profilesDialog", new ProfilesDialog(CreateFont()));
            SetPrivateField(session, "profileSettingsService", new EditorProfileSettingsService(TempProjectRootPath));
            SetPrivateField(session, "buildConfigService", buildConfigService);
            SetPrivateField(session, "buildQueueService", buildQueueService);
            SetPrivateField(session, "sceneCatalogService", new EditorProjectSceneCatalogService(TempProjectRootPath));
            SetPrivateField(session, "RequiredEngineVersion", "1.0.0-custom");
            SetPrivateField(session, "availablePlatformProviderResolver", new AvailablePlatformProviderResolver(new PlatformDiscoveryOptions(TempProjectRootPath), new WindowsLauncherInstallRootLocator()));

            return session;
        }

        /// <summary>
        /// Writes one temporary platform manifest that marks only the supplied platform identifiers as installed.
        /// </summary>
        /// <param name="installedPlatformIds">Platform identifiers that should be treated as available.</param>
        void ConfigureAvailablePlatforms(params string[] installedPlatformIds) {
            List<AvailablePlatformDescriptor> platforms = new List<AvailablePlatformDescriptor> {
                new AvailablePlatformDescriptor("ps2", "PlayStation 2", string.Empty, "platforms/ps2", false),
                new AvailablePlatformDescriptor("windows", "Windows DirectX", string.Empty, "platforms/windows", false)
            };
            string manifestPath = Path.Combine(TempProjectRootPath, "platforms.json");
            List<string> manifestEntries = new List<string>(platforms.Count);

            for (int index = 0; index < platforms.Count; index++) {
                AvailablePlatformDescriptor platform = platforms[index];
                manifestEntries.Add($$"""
                {
                  "engineVersion": "1.0.0-custom",
                  "platformId": "{{platform.Id}}",
                  "displayName": "{{platform.DisplayName}}",
                  "builderAssemblyPath": "",
                  "playerSourceRootPath": "{{platform.PlayerSourceRootPath}}"
                }
                """);

                if (installedPlatformIds.Contains(platform.Id, StringComparer.OrdinalIgnoreCase)) {
                    Directory.CreateDirectory(Path.Combine(TempProjectRootPath, platform.PlayerSourceRootPath));
                }
            }

            File.WriteAllText(
                manifestPath,
                """
                {
                  "platforms": [
                """ + string.Join(",\n", manifestEntries) + """
                  ]
                }
                """);
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
            FieldInfo field = FindPrivateField(target.GetType(), fieldName);
            return Assert.IsType<T>(field.GetValue(target));
        }

        /// <summary>
        /// Assigns one non-public instance field.
        /// </summary>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to assign.</param>
        /// <param name="value">Value assigned to the field.</param>
        void SetPrivateField(object target, string fieldName, object value) {
            FieldInfo field = FindPrivateField(target.GetType(), fieldName);
            field.SetValue(target, value);
        }

        /// <summary>
        /// Finds one non-public instance field declared on the supplied type or one of its base types.
        /// </summary>
        /// <param name="type">Type whose field hierarchy should be searched.</param>
        /// <param name="fieldName">Name of the field to resolve.</param>
        /// <returns>Resolved field metadata.</returns>
        FieldInfo FindPrivateField(Type type, string fieldName) {
            FieldInfo field = null;
            Type currentType = type;
            while (currentType != null && field == null) {
                field = currentType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                currentType = currentType.BaseType;
            }

            return field ?? throw new InvalidOperationException("Could not find field '" + fieldName + "'.");
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
