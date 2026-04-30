using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies queued build execution ordering, failure handling, and persisted status updates.
    /// </summary>
    public sealed class EditorBuildQueueServiceTests : IDisposable {
        /// <summary>
        /// Gets the isolated temporary project root used by the current test instance.
        /// </summary>
        string TempProjectRootPath { get; }

        /// <summary>
        /// Initializes one isolated temporary project root used to persist build queue state during the current test.
        /// </summary>
        public EditorBuildQueueServiceTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-build-queue-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempProjectRootPath);
        }

        /// <summary>
        /// Deletes the temporary project root used by the current test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures pending queued builds execute sequentially and persist the final done state.
        /// </summary>
        [Fact]
        public void RunPending_WhenAllQueuedBuildsSucceed_MarksEachItemDoneAndPersistsIt() {
            EditorBuildConfigService buildConfigService = new EditorBuildConfigService(TempProjectRootPath);
            EditorBuildConfigDocument buildConfig = new EditorBuildConfigDocument {
                Platforms = [
                    new EditorBuildPlatformConfigDocument {
                        PlatformId = "windows"
                    }
                ],
                QueueItems = [
                    new EditorBuildQueueItemDocument {
                        QueueItemId = "queue-1",
                        PlatformId = "windows",
                        SelectedSceneIds = [
                            "Scenes/City.helen"
                        ],
                        OutputDirectoryPath = @"C:\builds\windows",
                        Status = EditorBuildQueueItemStatus.Pending
                    },
                    new EditorBuildQueueItemDocument {
                        QueueItemId = "queue-2",
                        PlatformId = "windows",
                        SelectedSceneIds = [
                            "Scenes/Menu.helen"
                        ],
                        OutputDirectoryPath = @"C:\builds\windows",
                        Status = EditorBuildQueueItemStatus.Pending
                    }
                ]
            };
            buildConfigService.Save(buildConfig);
            TestEditorBuildExecutor buildExecutor = new TestEditorBuildExecutor([
                EditorBuildExecutionResult.Success("Built queue-1."),
                EditorBuildExecutionResult.Success("Built queue-2.")
            ]);
            EditorBuildQueueService queueService = new EditorBuildQueueService(buildConfigService, buildExecutor);

            queueService.RunPending(buildConfig, [
                "windows"
            ]);

            Assert.Equal(
                new[] {
                    "queue-1",
                    "queue-2"
                },
                buildExecutor.ExecutedQueueItemIds);
            Assert.Equal(
                new[] {
                    EditorBuildQueueItemStatus.Running,
                    EditorBuildQueueItemStatus.Running
                },
                buildExecutor.ObservedStatuses);
            Assert.All(
                buildConfig.QueueItems,
                queueItem => Assert.Equal(EditorBuildQueueItemStatus.Done, queueItem.Status));

            EditorBuildConfigDocument persistedDocument = buildConfigService.Load([
                "windows"
            ], string.Empty);
            Assert.All(
                persistedDocument.QueueItems,
                queueItem => Assert.Equal(EditorBuildQueueItemStatus.Done, queueItem.Status));
            Assert.Equal("Built queue-2.", persistedDocument.QueueItems[1].StatusMessage);
        }

        /// <summary>
        /// Ensures the queue stops after the first failed build and preserves the remaining pending items.
        /// </summary>
        [Fact]
        public void RunPending_WhenOneQueuedBuildFails_StopsQueueAndLeavesLaterItemsPending() {
            EditorBuildConfigService buildConfigService = new EditorBuildConfigService(TempProjectRootPath);
            EditorBuildConfigDocument buildConfig = new EditorBuildConfigDocument {
                Platforms = [
                    new EditorBuildPlatformConfigDocument {
                        PlatformId = "windows"
                    }
                ],
                QueueItems = [
                    new EditorBuildQueueItemDocument {
                        QueueItemId = "queue-1",
                        PlatformId = "windows",
                        SelectedSceneIds = [
                            "Scenes/City.helen"
                        ],
                        OutputDirectoryPath = @"C:\builds\windows",
                        Status = EditorBuildQueueItemStatus.Pending
                    },
                    new EditorBuildQueueItemDocument {
                        QueueItemId = "queue-2",
                        PlatformId = "windows",
                        SelectedSceneIds = [
                            "Scenes/Menu.helen"
                        ],
                        OutputDirectoryPath = @"C:\builds\windows",
                        Status = EditorBuildQueueItemStatus.Pending
                    }
                ]
            };
            buildConfigService.Save(buildConfig);
            TestEditorBuildExecutor buildExecutor = new TestEditorBuildExecutor([
                EditorBuildExecutionResult.Failure("Unsupported scene format.")
            ]);
            EditorBuildQueueService queueService = new EditorBuildQueueService(buildConfigService, buildExecutor);

            queueService.RunPending(buildConfig, [
                "windows"
            ]);

            Assert.Single(buildExecutor.ExecutedQueueItemIds);
            Assert.Equal(EditorBuildQueueItemStatus.Failed, buildConfig.QueueItems[0].Status);
            Assert.Equal("Unsupported scene format.", buildConfig.QueueItems[0].StatusMessage);
            Assert.Equal(EditorBuildQueueItemStatus.Pending, buildConfig.QueueItems[1].Status);

            EditorBuildConfigDocument persistedDocument = buildConfigService.Load([
                "windows"
            ], string.Empty);
            Assert.Equal(EditorBuildQueueItemStatus.Failed, persistedDocument.QueueItems[0].Status);
            Assert.Equal(EditorBuildQueueItemStatus.Pending, persistedDocument.QueueItems[1].Status);
        }

        /// <summary>
        /// Ensures queued builds targeting a disabled platform fail immediately without invoking the executor.
        /// </summary>
        [Fact]
        public void RunPending_WhenQueuedBuildTargetsDisabledPlatform_FailsItemWithoutExecutingIt() {
            EditorBuildConfigService buildConfigService = new EditorBuildConfigService(TempProjectRootPath);
            EditorBuildConfigDocument buildConfig = new EditorBuildConfigDocument {
                Platforms = [
                    new EditorBuildPlatformConfigDocument {
                        PlatformId = "windows"
                    }
                ],
                QueueItems = [
                    new EditorBuildQueueItemDocument {
                        QueueItemId = "queue-1",
                        PlatformId = "linux",
                        SelectedSceneIds = [
                            "Scenes/City.helen"
                        ],
                        OutputDirectoryPath = "/tmp/linux-build",
                        Status = EditorBuildQueueItemStatus.Pending
                    }
                ]
            };
            buildConfigService.Save(buildConfig);
            TestEditorBuildExecutor buildExecutor = new TestEditorBuildExecutor([]);
            EditorBuildQueueService queueService = new EditorBuildQueueService(buildConfigService, buildExecutor);

            queueService.RunPending(buildConfig, [
                "windows"
            ]);

            Assert.Empty(buildExecutor.ExecutedQueueItemIds);
            Assert.Equal(EditorBuildQueueItemStatus.Failed, buildConfig.QueueItems[0].Status);
            Assert.Equal("Platform 'linux' is no longer enabled for this project.", buildConfig.QueueItems[0].StatusMessage);
        }
    }
}
