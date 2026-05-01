using System.Reflection;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the local build dialog platform tabs, map selection, and queue rendering behavior.
    /// </summary>
    public sealed class BuildDialogTests : IDisposable {
        /// <summary>
        /// Gets the temporary content root used by the dialog tests.
        /// </summary>
        string TempRootPath { get; }

        /// <summary>
        /// Initializes the runtime services required by the build dialog tests.
        /// </summary>
        public BuildDialogTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-build-dialog-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);
            EditorInputCaptureService.Reset();

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Deletes the temporary content root after each test.
        /// </summary>
        public void Dispose() {
            EditorInputCaptureService.Reset();
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures one platform tab is created per enabled platform and the active platform's saved scenes are checked.
        /// </summary>
        [Fact]
        public void Show_WhenPlatformsAndBuildConfigProvided_CreatesTabsAndChecksSavedScenesForActivePlatform() {
            BuildDialog dialog = new BuildDialog(CreateFont());

            dialog.Show(
                ["windows", "linux"],
                [
                    "Scenes/City.helen",
                    "Scenes/Menu.helen"
                ],
                "linux",
                new EditorBuildConfigDocument {
                    Platforms = [
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "windows",
                            SelectedSceneIds = [
                                "Scenes/City.helen"
                            ]
                        },
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "linux",
                            SelectedSceneIds = [
                                "Scenes/Menu.helen"
                            ]
                        }
                    ]
                });

            List<ButtonComponent> platformTabs = GetPrivateField<List<ButtonComponent>>(dialog, "PlatformTabs");
            List<TextBoxComponent> mapOrderFields = GetPrivateField<List<TextBoxComponent>>(dialog, "MapOrderFields");
            List<TextComponent> mapLabelTexts = GetPrivateField<List<TextComponent>>(dialog, "MapLabelTexts");
            List<CheckBoxComponent> mapCheckBoxes = GetPrivateField<List<CheckBoxComponent>>(dialog, "MapCheckBoxes");
            string activePlatformId = GetPrivateField<string>(dialog, "ActivePlatformId");

            Assert.Equal(2, platformTabs.Count);
            Assert.Equal("linux", activePlatformId);
            Assert.Collection(
                mapOrderFields,
                field => Assert.Equal("1", field.Text),
                field => Assert.Equal("2", field.Text));
            Assert.Collection(
                mapLabelTexts,
                label => Assert.Equal("Scenes/City.helen", label.Text),
                label => Assert.Equal("Scenes/Menu.helen", label.Text));
            Assert.False(mapCheckBoxes[0].IsChecked);
            Assert.True(mapCheckBoxes[1].IsChecked);
        }

        /// <summary>
        /// Ensures Add to Build raises one request for the active platform with the selected maps and output folder.
        /// </summary>
        [Fact]
        public void HandleAddToBuildClicked_WhenCurrentPlatformHasSelection_RaisesAddRequestedForActivePlatform() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            BuildDialogAddRequest raisedRequest = null;
            dialog.AddRequested += request => raisedRequest = request;
            dialog.Show(
                ["windows", "linux"],
                [
                    "Scenes/City.helen",
                    "Scenes/Menu.helen"
                ],
                "windows",
                new EditorBuildConfigDocument {
                    Platforms = [
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "windows",
                            SelectedSceneIds = [
                                "Scenes/City.helen"
                            ],
                            OutputDirectoryPath = @"C:\builds\windows"
                        },
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "linux",
                            SelectedSceneIds = [
                                "Scenes/Menu.helen"
                            ],
                            OutputDirectoryPath = "/tmp/linux-build"
                        }
                    ]
                });

            List<CheckBoxComponent> mapCheckBoxes = GetPrivateField<List<CheckBoxComponent>>(dialog, "MapCheckBoxes");
            List<TextBoxComponent> mapOrderFields = GetPrivateField<List<TextBoxComponent>>(dialog, "MapOrderFields");
            TextBoxComponent outputDirectoryField = GetPrivateField<TextBoxComponent>(dialog, "OutputDirectoryField");
            mapOrderFields[0].Text = "2";
            mapOrderFields[1].Text = "1";
            mapCheckBoxes[1].IsChecked = true;
            outputDirectoryField.Text = @"D:\exports\windows";

            InvokePrivate(dialog, "HandleAddToBuildClicked");

            Assert.NotNull(raisedRequest);
            Assert.Equal("windows", raisedRequest.PlatformId);
            Assert.Equal(
                new[] {
                    "Scenes/Menu.helen",
                    "Scenes/City.helen"
                },
                raisedRequest.SelectedSceneIds);
            Assert.Equal(@"D:\exports\windows", raisedRequest.OutputDirectoryPath);
        }

        /// <summary>
        /// Ensures pressing Enter in a scene-order field reflows the visible scene rows using the committed order numbers.
        /// </summary>
        [Fact]
        public void HandleSceneOrderFieldSubmitted_WhenPressedEnter_ReflowsSceneRowsByOrderNumber() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            dialog.Show(
                ["windows"],
                [
                    "Scenes/City.helen",
                    "Scenes/Menu.helen"
                ],
                "windows",
                new EditorBuildConfigDocument {
                    Platforms = [
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "windows",
                            SelectedSceneIds = [
                                "Scenes/City.helen",
                                "Scenes/Menu.helen"
                            ],
                            SceneOrders = [
                                new EditorBuildSceneOrderDocument {
                                    SceneId = "Scenes/City.helen",
                                    OrderNumber = 1
                                },
                                new EditorBuildSceneOrderDocument {
                                    SceneId = "Scenes/Menu.helen",
                                    OrderNumber = 2
                                }
                            ],
                            OutputDirectoryPath = @"C:\builds\windows"
                        }
                    ]
                });

            List<TextBoxComponent> mapOrderFields = GetPrivateField<List<TextBoxComponent>>(dialog, "MapOrderFields");
            mapOrderFields[0].Text = "2";
            mapOrderFields[1].Text = "1";
            mapOrderFields[1].SetTargetFocused(true);
            mapOrderFields[1].ActivateFromKey(Keys.Enter);

            List<TextComponent> mapLabelTexts = GetPrivateField<List<TextComponent>>(dialog, "MapLabelTexts");
            List<TextBoxComponent> reflowedOrderFields = GetPrivateField<List<TextBoxComponent>>(dialog, "MapOrderFields");

            Assert.Collection(
                mapLabelTexts,
                label => Assert.Equal("Scenes/Menu.helen", label.Text),
                label => Assert.Equal("Scenes/City.helen", label.Text));
            Assert.Collection(
                reflowedOrderFields,
                field => Assert.Equal("1", field.Text),
                field => Assert.Equal("2", field.Text));
        }

        /// <summary>
        /// Ensures Add to Build does not raise a request when the active platform output folder is blank.
        /// </summary>
        [Fact]
        public void HandleAddToBuildClicked_WhenOutputFolderIsBlank_DoesNotRaiseAddRequested() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            BuildDialogAddRequest raisedRequest = null;
            dialog.AddRequested += request => raisedRequest = request;
            dialog.Show(
                ["windows"],
                [
                    "Scenes/City.helen"
                ],
                "windows",
                new EditorBuildConfigDocument {
                    Platforms = [
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "windows",
                            SelectedSceneIds = [
                                "Scenes/City.helen"
                            ],
                            OutputDirectoryPath = @"C:\builds\windows"
                        }
                    ]
                });

            TextBoxComponent outputDirectoryField = GetPrivateField<TextBoxComponent>(dialog, "OutputDirectoryField");
            outputDirectoryField.Text = "";

            InvokePrivate(dialog, "HandleAddToBuildClicked");

            Assert.Null(raisedRequest);
        }

        /// <summary>
        /// Ensures the output-folder textbox turns red after an invalid add click and clears immediately when a valid path is entered.
        /// </summary>
        [Fact]
        public void HandleAddToBuildClicked_WhenOutputFolderValidationChanges_UpdatesOutputFieldBorderImmediately() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            dialog.Show(
                ["windows"],
                [
                    "Scenes/City.helen"
                ],
                "windows",
                new EditorBuildConfigDocument {
                    Platforms = [
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "windows",
                            SelectedSceneIds = [
                                "Scenes/City.helen"
                            ],
                            OutputDirectoryPath = @"C:\builds\windows"
                        }
                    ]
                });

            TextBoxComponent outputDirectoryField = GetPrivateField<TextBoxComponent>(dialog, "OutputDirectoryField");
            RoundedRectComponent backgroundSprite = GetPrivateField<RoundedRectComponent>(outputDirectoryField, "backgroundSprite");
            EditorEntity outputFieldHost = GetPrivateField<EditorEntity>(dialog, "OutputFieldHost");
            float3 originalPosition = outputFieldHost.LocalPosition;
            outputDirectoryField.Text = "";

            InvokePrivate(dialog, "HandleAddToBuildClicked");

            Assert.Equal(ThemeManager.Colors.StateDanger, backgroundSprite.BorderColor);

            for (int frame = 0; frame < 3; frame++) {
                outputDirectoryField.Update();
                dialog.UpdateLayout(1280, 720);
            }

            Assert.NotEqual(originalPosition, outputFieldHost.LocalPosition);

            for (int frame = 0; frame < 24; frame++) {
                outputDirectoryField.Update();
                dialog.UpdateLayout(1280, 720);
            }

            Assert.Equal(originalPosition, outputFieldHost.LocalPosition);

            outputDirectoryField.Text = @"D:\exports\windows";

            Assert.Equal(ThemeManager.Colors.AccentTertiary, backgroundSprite.BorderColor);

            outputDirectoryField.Text = "";
            InvokePrivate(dialog, "HandleAddToBuildClicked");

            Assert.Equal(ThemeManager.Colors.StateDanger, backgroundSprite.BorderColor);
        }

        /// <summary>
        /// Ensures one Add to Build click validates both the output folder and the scene list, so empty scenes still show feedback even when the output folder is also blank.
        /// </summary>
        [Fact]
        public void HandleAddToBuildClicked_WhenOutputFolderAndScenesAreBothInvalid_ShowsBothValidationFeedbackStates() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            dialog.Show(
                ["windows"],
                [
                    "Scenes/City.helen"
                ],
                "windows",
                new EditorBuildConfigDocument {
                    Platforms = [
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "windows",
                            SelectedSceneIds = [
                                "Scenes/City.helen"
                            ],
                            OutputDirectoryPath = @"C:\builds\windows"
                        }
                    ]
                });

            TextBoxComponent outputDirectoryField = GetPrivateField<TextBoxComponent>(dialog, "OutputDirectoryField");
            RoundedRectComponent outputBackgroundSprite = GetPrivateField<RoundedRectComponent>(outputDirectoryField, "backgroundSprite");
            RoundedRectComponent sceneListBackground = GetPrivateField<RoundedRectComponent>(dialog, "SceneListBackground");
            List<CheckBoxComponent> mapCheckBoxes = GetPrivateField<List<CheckBoxComponent>>(dialog, "MapCheckBoxes");
            EditorEntity outputFieldHost = GetPrivateField<EditorEntity>(dialog, "OutputFieldHost");
            EditorEntity sceneListRoot = GetPrivateField<EditorEntity>(dialog, "SceneListRoot");
            float3 originalOutputPosition = outputFieldHost.LocalPosition;
            float3 originalPosition = sceneListRoot.LocalPosition;

            outputDirectoryField.Text = "";
            mapCheckBoxes[0].IsChecked = false;

            InvokePrivate(dialog, "HandleAddToBuildClicked");

            Assert.Equal(ThemeManager.Colors.StateDanger, outputBackgroundSprite.BorderColor);
            Assert.Equal(ThemeManager.Colors.StateDanger, sceneListBackground.BorderColor);

            for (int frame = 0; frame < 3; frame++) {
                outputDirectoryField.Update();
                InvokePrivate(dialog, "UpdateFeedbackAnimation");
                dialog.UpdateLayout(1280, 720);
            }

            Assert.NotEqual(originalOutputPosition, outputFieldHost.LocalPosition);
            Assert.NotEqual(originalPosition, sceneListRoot.LocalPosition);
        }

        /// <summary>
        /// Ensures Add to Build does not raise a request when no scenes are selected, and the scene list shows invalid feedback only after the Add to Build click until a scene is selected again.
        /// The shake must survive the dialog layout pass used by the live editor frame loop.
        /// </summary>
        [Fact]
        public void HandleAddToBuildClicked_WhenNoScenesSelected_ShakesAndMarksSceneListInvalidUntilSelectionReturns() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            BuildDialogAddRequest raisedRequest = null;
            dialog.AddRequested += request => raisedRequest = request;
            dialog.Show(
                ["windows"],
                [
                    "Scenes/City.helen",
                    "Scenes/Menu.helen"
                ],
                "windows",
                new EditorBuildConfigDocument {
                    Platforms = [
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "windows",
                            SelectedSceneIds = [
                                "Scenes/City.helen"
                            ],
                            OutputDirectoryPath = @"C:\builds\windows"
                        }
                    ]
                });

            List<CheckBoxComponent> mapCheckBoxes = GetPrivateField<List<CheckBoxComponent>>(dialog, "MapCheckBoxes");
            RoundedRectComponent sceneListBackground = GetPrivateField<RoundedRectComponent>(dialog, "SceneListBackground");
            EditorEntity sceneListRoot = GetPrivateField<EditorEntity>(dialog, "SceneListRoot");
            float3 originalPosition = sceneListRoot.LocalPosition;
            mapCheckBoxes[0].IsChecked = false;

            Assert.Equal(ThemeManager.Colors.AccentTertiary, sceneListBackground.BorderColor);
            Assert.Equal(originalPosition, sceneListRoot.LocalPosition);

            InvokePrivate(dialog, "HandleAddToBuildClicked");

            Assert.Null(raisedRequest);
            Assert.Equal(ThemeManager.Colors.StateDanger, sceneListBackground.BorderColor);

            for (int frame = 0; frame < 3; frame++) {
                InvokePrivate(dialog, "UpdateFeedbackAnimation");
                dialog.UpdateLayout(1280, 720);
            }

            float shakeOffsetX = GetPrivateField<float>(dialog, "SceneListShakeOffsetX");
            Assert.NotEqual(0f, shakeOffsetX);
            Assert.NotEqual(originalPosition, sceneListRoot.LocalPosition);

            for (int frame = 0; frame < 24; frame++) {
                InvokePrivate(dialog, "UpdateFeedbackAnimation");
                dialog.UpdateLayout(1280, 720);
            }

            Assert.Equal(originalPosition, sceneListRoot.LocalPosition);

            InvokePrivate(dialog, "HandleSceneSelectionChanged", mapCheckBoxes[1], true);

            Assert.Equal(ThemeManager.Colors.AccentTertiary, sceneListBackground.BorderColor);
        }

        /// <summary>
        /// Ensures switching to another platform tab writes the current tab's pending map and folder edits back into the local config document.
        /// </summary>
        [Fact]
        public void HandlePlatformTabClicked_WhenActivePlatformWasEdited_SyncsEditsBeforeSwitchingTabs() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            EditorBuildConfigDocument buildConfig = new EditorBuildConfigDocument {
                Platforms = [
                    new EditorBuildPlatformConfigDocument {
                        PlatformId = "windows",
                        SelectedSceneIds = [
                            "Scenes/City.helen"
                        ],
                        SceneOrders = [
                            new EditorBuildSceneOrderDocument {
                                SceneId = "Scenes/City.helen",
                                OrderNumber = 2
                            },
                            new EditorBuildSceneOrderDocument {
                                SceneId = "Scenes/Menu.helen",
                                OrderNumber = 1
                            }
                        ],
                        OutputDirectoryPath = @"C:\builds\windows"
                    },
                    new EditorBuildPlatformConfigDocument {
                        PlatformId = "linux",
                        SelectedSceneIds = [
                            "Scenes/Menu.helen"
                        ],
                        OutputDirectoryPath = "/tmp/linux-build"
                    }
                ]
            };
            dialog.Show(
                ["windows", "linux"],
                [
                    "Scenes/City.helen",
                    "Scenes/Menu.helen"
                ],
                "windows",
                buildConfig);

            List<CheckBoxComponent> mapCheckBoxes = GetPrivateField<List<CheckBoxComponent>>(dialog, "MapCheckBoxes");
            TextBoxComponent outputDirectoryField = GetPrivateField<TextBoxComponent>(dialog, "OutputDirectoryField");
            mapCheckBoxes[0].IsChecked = false;
            mapCheckBoxes[1].IsChecked = true;
            outputDirectoryField.Text = @"D:\exports\windows";

            InvokePrivate(dialog, "HandlePlatformTabClicked", "linux");

            EditorBuildPlatformConfigDocument windowsConfig = Assert.Single(buildConfig.Platforms.Where(platform => platform.PlatformId == "windows"));
            Assert.Equal(
                new[] {
                    "Scenes/Menu.helen"
                },
                windowsConfig.SelectedSceneIds);
            Assert.Equal(@"D:\exports\windows", windowsConfig.OutputDirectoryPath);
        }

        /// <summary>
        /// Ensures the Build Queue action writes the visible tab state into the build config before raising the event.
        /// </summary>
        [Fact]
        public void HandleBuildQueueRequested_WhenActivePlatformWasEdited_SyncsEditsBeforeRaisingEvent() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            bool raised = false;
            dialog.BuildQueueRequested += () => raised = true;
            EditorBuildConfigDocument buildConfig = new EditorBuildConfigDocument {
                Platforms = [
                    new EditorBuildPlatformConfigDocument {
                        PlatformId = "windows",
                        SelectedSceneIds = [
                            "Scenes/City.helen"
                        ],
                        OutputDirectoryPath = @"C:\builds\windows"
                    }
                ]
            };
            dialog.Show(
                ["windows"],
                [
                    "Scenes/City.helen",
                    "Scenes/Menu.helen"
                ],
                "windows",
                buildConfig);

            List<CheckBoxComponent> mapCheckBoxes = GetPrivateField<List<CheckBoxComponent>>(dialog, "MapCheckBoxes");
            TextBoxComponent outputDirectoryField = GetPrivateField<TextBoxComponent>(dialog, "OutputDirectoryField");
            mapCheckBoxes[1].IsChecked = true;
            outputDirectoryField.Text = @"D:\exports\windows";

            InvokePrivate(dialog, "HandleBuildQueueRequested");

            EditorBuildPlatformConfigDocument windowsConfig = Assert.Single(buildConfig.Platforms);
            Assert.True(raised);
            Assert.Equal(
                new[] {
                    "Scenes/City.helen",
                    "Scenes/Menu.helen"
                },
                windowsConfig.SelectedSceneIds);
            Assert.Equal(@"D:\exports\windows", windowsConfig.OutputDirectoryPath);
        }

        /// <summary>
        /// Ensures copying a map list from another platform replaces only the current platform's selected scenes.
        /// </summary>
        [Fact]
        public void HandleCopyMapListClicked_WhenSourcePlatformSelected_CopiesSceneSelectionIntoActivePlatform() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            EditorBuildConfigDocument buildConfig = new EditorBuildConfigDocument {
                Platforms = [
                    new EditorBuildPlatformConfigDocument {
                        PlatformId = "windows",
                        SelectedSceneIds = [
                            "Scenes/City.helen"
                        ],
                        OutputDirectoryPath = @"C:\builds\windows"
                    },
                    new EditorBuildPlatformConfigDocument {
                        PlatformId = "linux",
                        SelectedSceneIds = [
                            "Scenes/Menu.helen"
                        ],
                        OutputDirectoryPath = "/tmp/linux-build"
                    }
                ]
            };
            dialog.Show(
                ["windows", "linux"],
                [
                    "Scenes/City.helen",
                    "Scenes/Menu.helen"
                ],
                "linux",
                buildConfig);

            ComboBoxComponent copySourcePlatformComboBox = GetPrivateField<ComboBoxComponent>(dialog, "CopySourcePlatformComboBox");
            copySourcePlatformComboBox.SelectedIndex = 0;

            InvokePrivate(dialog, "HandleCopyMapListClicked");

            List<CheckBoxComponent> mapCheckBoxes = GetPrivateField<List<CheckBoxComponent>>(dialog, "MapCheckBoxes");
            List<TextBoxComponent> mapOrderFields = GetPrivateField<List<TextBoxComponent>>(dialog, "MapOrderFields");
            EditorBuildPlatformConfigDocument linuxConfig = Assert.Single(buildConfig.Platforms.Where(platform => platform.PlatformId == "linux"));
            Assert.True(mapCheckBoxes[0].IsChecked);
            Assert.False(mapCheckBoxes[1].IsChecked);
            Assert.Equal(
                new[] {
                    "Scenes/City.helen"
                },
                linuxConfig.SelectedSceneIds);
            Assert.Equal("2", mapOrderFields[0].Text);
            Assert.Equal("1", mapOrderFields[1].Text);
            Assert.Equal(2, linuxConfig.SceneOrders.Count);
            Assert.Equal(2, linuxConfig.SceneOrders[0].OrderNumber);
            Assert.Equal(1, linuxConfig.SceneOrders[1].OrderNumber);
            Assert.Equal("/tmp/linux-build", linuxConfig.OutputDirectoryPath);
        }

        /// <summary>
        /// Ensures one queue row is rendered for each persisted queued build item.
        /// </summary>
        [Fact]
        public void Show_WhenQueueItemsProvided_RendersOneQueueRowPerItem() {
            BuildDialog dialog = new BuildDialog(CreateFont());

            dialog.Show(
                ["windows"],
                [
                    "Scenes/City.helen"
                ],
                "windows",
                new EditorBuildConfigDocument {
                    Platforms = [
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "windows",
                            SelectedSceneIds = [
                                "Scenes/City.helen"
                            ]
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
                            OutputDirectoryPath = @"C:\builds\windows-two",
                            Status = EditorBuildQueueItemStatus.Failed,
                            StatusMessage = "Unsupported scene format."
                        }
                    ]
                });

            List<TextComponent> queueItemTexts = GetPrivateField<List<TextComponent>>(dialog, "QueueItemTexts");

            Assert.Equal(2, queueItemTexts.Count);
            Assert.Contains("Pending", queueItemTexts[0].Text);
            Assert.Contains("Failed", queueItemTexts[1].Text);
            Assert.Contains("Unsupported scene format.", queueItemTexts[1].Text);
        }

        /// <summary>
        /// Ensures the build dialog adds a bottom build-log section without shifting the existing footer controls.
        /// </summary>
        [Fact]
        public void Show_WhenQueueItemsProvided_RendersBuildLogsBelowExistingControls() {
            BuildDialog dialog = new BuildDialog(CreateFont());

            dialog.Show(
                ["windows"],
                [
                    "Scenes/City.helen"
                ],
                "windows",
                new EditorBuildConfigDocument {
                    Platforms = [
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "windows",
                            SelectedSceneIds = [
                                "Scenes/City.helen"
                            ]
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
                            Status = EditorBuildQueueItemStatus.Done,
                            StatusMessage = "Windows build completed."
                        },
                        new EditorBuildQueueItemDocument {
                            QueueItemId = "queue-2",
                            PlatformId = "linux",
                            SelectedSceneIds = [
                                "Scenes/Menu.helen"
                            ],
                            OutputDirectoryPath = "/tmp/linux-build",
                            Status = EditorBuildQueueItemStatus.Pending
                        }
                    ]
                });

            EditorEntity addToBuildButtonHost = GetPrivateField<EditorEntity>(dialog, "AddToBuildButtonHost");
            EditorEntity buildQueueButtonHost = GetPrivateField<EditorEntity>(dialog, "BuildQueueButtonHost");
            EditorEntity buildLogsRoot = GetPrivateField<EditorEntity>(dialog, "BuildLogsRoot");
            RoundedRectComponent buildLogsBackground = GetPrivateField<RoundedRectComponent>(dialog, "BuildLogsBackground");
            RoundedRectComponent buildLogsProgressTrack = GetPrivateField<RoundedRectComponent>(dialog, "BuildLogsProgressTrack");
            RoundedRectComponent buildLogsProgressFill = GetPrivateField<RoundedRectComponent>(dialog, "BuildLogsProgressFill");
            TextComponent buildLogsText = GetPrivateField<TextComponent>(dialog, "BuildLogsText");

            Assert.Equal(
                BuildDialog.LegacyContentHeight - BuildDialog.HeaderHeight - BuildDialog.PanelPadding - BuildDialog.FooterButtonHeight - 8,
                (int)buildQueueButtonHost.LocalPosition.Y);
            Assert.Equal(BuildDialog.LegacyContentHeight, (int)buildLogsRoot.LocalPosition.Y);
            Assert.True(buildLogsRoot.LocalPosition.Y > addToBuildButtonHost.LocalPosition.Y + BuildDialog.FooterButtonHeight);
            Assert.Equal(BuildDialog.BuildLogsSectionHeight, buildLogsBackground.Size.Y);
            Assert.True(buildLogsProgressTrack.Size.X > 0);
            Assert.True(buildLogsProgressFill.Size.X > 0);
            Assert.True(buildLogsProgressFill.Size.X < buildLogsProgressTrack.Size.X);
            Assert.Contains("Progress:", buildLogsText.Text);
            Assert.Contains("Windows build completed.", buildLogsText.Text);
            Assert.Contains("linux | Pending", buildLogsText.Text);
        }

        /// <summary>
        /// Ensures the queue area is enclosed by a bordered section with its own header label.
        /// </summary>
        [Fact]
        public void Show_CreatesBorderedQueueSectionWithHeader() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            dialog.Show(
                ["windows"],
                [
                    "Scenes/City.helen"
                ],
                "windows",
                new EditorBuildConfigDocument {
                    Platforms = [
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "windows",
                            SelectedSceneIds = [
                                "Scenes/City.helen"
                            ]
                        }
                    ]
                });

            RoundedRectComponent queueListBackground = GetPrivateField<RoundedRectComponent>(dialog, "QueueListBackground");
            RoundedRectComponent queueHeaderBackground = GetPrivateField<RoundedRectComponent>(dialog, "QueueHeaderBackground");
            TextComponent queueHeaderText = GetPrivateField<TextComponent>(dialog, "QueueHeaderText");

            Assert.Equal(ThemeManager.Colors.AccentTertiary, queueListBackground.BorderColor);
            Assert.Equal(2f, queueListBackground.BorderThickness);
            Assert.Equal(ThemeManager.Colors.AccentSecondary, queueHeaderBackground.FillColor);
            Assert.Equal("Queue", queueHeaderText.Text);
            Assert.Equal(ThemeManager.Colors.InputForegroundPrimary, queueHeaderText.Color);
        }

        /// <summary>
        /// Ensures each queued build is rendered inside its own bordered card instead of floating as loose text.
        /// </summary>
        [Fact]
        public void Show_WhenQueueItemsProvided_RendersBorderedCardPerQueueItem() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            dialog.Show(
                ["windows"],
                [
                    "Scenes/City.helen"
                ],
                "windows",
                new EditorBuildConfigDocument {
                    Platforms = [
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "windows",
                            SelectedSceneIds = [
                                "Scenes/City.helen"
                            ]
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
                            OutputDirectoryPath = @"C:\builds\windows-two",
                            Status = EditorBuildQueueItemStatus.Failed
                        }
                    ]
                });

            List<RoundedRectComponent> queueItemCardBackgrounds = GetPrivateField<List<RoundedRectComponent>>(dialog, "QueueItemCardBackgrounds");

            Assert.Equal(2, queueItemCardBackgrounds.Count);
            Assert.All(queueItemCardBackgrounds, background => {
                Assert.Equal(ThemeManager.Colors.AccentTertiary, background.BorderColor);
                Assert.Equal(2f, background.BorderThickness);
                Assert.True(background.Size.X > 0);
                Assert.True(background.Size.Y > 0);
            });
        }

        /// <summary>
        /// Ensures each queued build card exposes one remove button aligned on the right edge.
        /// </summary>
        [Fact]
        public void Show_WhenQueueItemsProvided_CreatesRemoveButtonPerQueueItem() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            string removedQueueItemId = string.Empty;
            dialog.RemoveQueueItemRequested += queueItemId => removedQueueItemId = queueItemId;
            dialog.Show(
                ["windows"],
                [
                    "Scenes/City.helen"
                ],
                "windows",
                new EditorBuildConfigDocument {
                    Platforms = [
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "windows",
                            SelectedSceneIds = [
                                "Scenes/City.helen"
                            ]
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
                        }
                    ]
                });

            List<ButtonComponent> queueItemRemoveButtons = GetPrivateField<List<ButtonComponent>>(dialog, "QueueItemRemoveButtons");
            List<EditorEntity> queueItemRemoveButtonHosts = GetPrivateField<List<EditorEntity>>(dialog, "QueueItemRemoveButtonHosts");
            RoundedRectComponent queueCardBackground = Assert.Single(GetPrivateField<List<RoundedRectComponent>>(dialog, "QueueItemCardBackgrounds"));
            ButtonComponent removeButton = Assert.Single(queueItemRemoveButtons);
            EditorEntity removeButtonHost = Assert.Single(queueItemRemoveButtonHosts);

            Assert.Equal("X", GetPrivateField<string>(removeButton, "text"));
            Assert.True(removeButtonHost.LocalPosition.X > queueCardBackground.Size.X - 48);

            InvokePrivate(dialog, "HandleQueueItemRemoveClicked", "queue-1");

            Assert.Equal("queue-1", removedQueueItemId);
        }

        /// <summary>
        /// Ensures the build dialog close button uses the lighter modal chrome text color.
        /// </summary>
        [Fact]
        public void Constructor_UsesLighterCloseButtonTextColor() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            ButtonComponent closeButton = GetPrivateField<ButtonComponent>(dialog, "CloseButton");
            byte4 buttonTextColor = GetPrivateField<byte4>(closeButton, "ButtonTextColor");

            Assert.Equal(ThemeManager.Colors.AccentQuaternary, buttonTextColor);
        }

        /// <summary>
        /// Ensures the build dialog close button owns a left separator line like the editor window chrome.
        /// </summary>
        [Fact]
        public void Constructor_CreatesCloseButtonLeftSeparator() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            SpriteComponent closeButtonSeparator = GetPrivateField<SpriteComponent>(dialog, "CloseButtonSeparator");

            Assert.Equal(TextureUtils.PixelTexture, closeButtonSeparator.Texture);
            Assert.Equal(ThemeManager.Colors.AccentQuaternary, closeButtonSeparator.Color);
        }

        /// <summary>
        /// Ensures the build dialog uses the lighter modal foreground color for its labels and queue text.
        /// </summary>
        [Fact]
        public void Show_UsesModalForegroundColorForBuildDialogText() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            dialog.Show(
                ["windows"],
                [
                    "Scenes/City.helen"
                ],
                "windows",
                new EditorBuildConfigDocument {
                    Platforms = [
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "windows",
                            SelectedSceneIds = [
                                "Scenes/City.helen"
                            ]
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
                        }
                    ]
                });

            TextComponent copySourceLabelText = GetPrivateField<TextComponent>(dialog, "CopySourceLabelText");
            TextComponent outputLabelText = GetPrivateField<TextComponent>(dialog, "OutputLabelText");
            List<TextComponent> mapLabelTexts = GetPrivateField<List<TextComponent>>(dialog, "MapLabelTexts");
            List<TextComponent> queueItemTexts = GetPrivateField<List<TextComponent>>(dialog, "QueueItemTexts");
            TextComponent buildLogsText = GetPrivateField<TextComponent>(dialog, "BuildLogsText");

            Assert.Equal(ThemeManager.Colors.InputForegroundPrimary, copySourceLabelText.Color);
            Assert.Equal(ThemeManager.Colors.InputForegroundPrimary, outputLabelText.Color);
            Assert.All(mapLabelTexts, label => Assert.Equal(ThemeManager.Colors.InputForegroundPrimary, label.Color));
            Assert.All(queueItemTexts, label => Assert.Equal(ThemeManager.Colors.InputForegroundPrimary, label.Color));
            Assert.Equal(ThemeManager.Colors.InputForegroundPrimary, buildLogsText.Color);
        }

        /// <summary>
        /// Ensures the shared modal shell blocks pointer capture across the full host backdrop, not only inside the panel bounds.
        /// </summary>
        [Fact]
        public void UpdateLayout_WhenDialogIsVisible_BlocksPointerOutsidePanelAcrossHost() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            dialog.Show(
                ["windows"],
                [
                    "Scenes/City.helen"
                ],
                "windows",
                new EditorBuildConfigDocument {
                    Platforms = [
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "windows",
                            SelectedSceneIds = [
                                "Scenes/City.helen"
                            ]
                        }
                    ]
                });
            dialog.UpdateLayout(1280, 720);

            Assert.True(EditorInputCaptureService.IsPointerBlocked(new int2(8, 8)));

            dialog.Hide();

            Assert.False(EditorInputCaptureService.IsPointerBlocked(new int2(8, 8)));
        }

        /// <summary>
        /// Ensures the scene list is enclosed by a bordered surface instead of leaving the rows visually floating.
        /// </summary>
        [Fact]
        public void Show_CreatesBorderedSceneListContainer() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            dialog.Show(
                ["windows"],
                [
                    "Scenes/City.helen",
                    "Scenes/Menu.helen"
                ],
                "windows",
                new EditorBuildConfigDocument {
                    Platforms = [
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "windows",
                            SelectedSceneIds = [
                                "Scenes/City.helen"
                            ]
                        }
                    ]
                });

            EditorEntity sceneListRoot = GetPrivateField<EditorEntity>(dialog, "SceneListRoot");
            RoundedRectComponent sceneListBackground = GetPrivateField<RoundedRectComponent>(dialog, "SceneListBackground");
            List<EditorEntity> mapLabelHosts = GetPrivateField<List<EditorEntity>>(dialog, "MapLabelHosts");

            Assert.Equal(0f, sceneListRoot.LocalPosition.X);
            Assert.True(sceneListRoot.LocalPosition.Y >= BuildDialog.PlatformTabHeight);
            Assert.Equal(ThemeManager.Colors.AccentTertiary, sceneListBackground.BorderColor);
            Assert.Equal(2f, sceneListBackground.BorderThickness);
            Assert.Equal(BuildDialog.PanelWidth - BuildDialog.QueueColumnWidth - (BuildDialog.PanelPadding * 3), sceneListBackground.Size.X);
            Assert.True(sceneListBackground.Size.Y > 0);
            Assert.All(mapLabelHosts, host => Assert.Equal(BuildDialog.SceneListPadding, host.LocalPosition.X));
        }

        /// <summary>
        /// Ensures the lower-left controls stay within the dialog bounds even when many scenes are available.
        /// </summary>
        [Fact]
        public void Show_WhenManyScenesAreAvailable_KeepsCopyControlsInsideDialogBounds() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            dialog.Show(
                ["windows", "linux"],
                CreateSceneIds(18),
                "windows",
                new EditorBuildConfigDocument {
                    Platforms = [
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "windows",
                            SelectedSceneIds = [
                                "Scenes/Map00.helen"
                            ]
                        },
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "linux",
                            SelectedSceneIds = [
                                "Scenes/Map00.helen"
                            ]
                        }
                    ]
                });

            EditorEntity copySourcePlatformComboBoxHost = GetPrivateField<EditorEntity>(dialog, "CopySourcePlatformComboBoxHost");
            ComboBoxComponent copySourcePlatformComboBox = GetPrivateField<ComboBoxComponent>(dialog, "CopySourcePlatformComboBox");
            EditorEntity outputFieldHost = GetPrivateField<EditorEntity>(dialog, "OutputFieldHost");
            TextBoxComponent outputDirectoryField = GetPrivateField<TextBoxComponent>(dialog, "OutputDirectoryField");
            EditorEntity addToBuildButtonHost = GetPrivateField<EditorEntity>(dialog, "AddToBuildButtonHost");

            Assert.InRange(copySourcePlatformComboBoxHost.LocalPosition.Y, 0f, BuildDialog.PanelHeight - BuildDialog.HeaderHeight - BuildDialog.PanelPadding);
            Assert.True(copySourcePlatformComboBoxHost.LocalPosition.Y + copySourcePlatformComboBox.Size.Y <= BuildDialog.PanelHeight - BuildDialog.HeaderHeight);
            Assert.True(outputFieldHost.LocalPosition.Y + outputDirectoryField.Size.Y <= BuildDialog.PanelHeight - BuildDialog.HeaderHeight);
            Assert.True(addToBuildButtonHost.LocalPosition.Y + BuildDialog.FooterButtonHeight <= BuildDialog.PanelHeight - BuildDialog.HeaderHeight);
        }

        /// <summary>
        /// Ensures the output-folder row exposes a browse button and keeps the Add to Build footer height aligned with Build Queue.
        /// </summary>
        [Fact]
        public void Show_CreatesOutputFolderBrowseRowAndMatchesFooterButtonHeights() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            dialog.Show(
                ["windows"],
                [
                    "Scenes/City.helen"
                ],
                "windows",
                new EditorBuildConfigDocument {
                    Platforms = [
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "windows",
                            SelectedSceneIds = [
                                "Scenes/City.helen"
                            ]
                        }
                    ]
                });

            EditorEntity outputFieldHost = GetPrivateField<EditorEntity>(dialog, "OutputFieldHost");
            TextBoxComponent outputDirectoryField = GetPrivateField<TextBoxComponent>(dialog, "OutputDirectoryField");
            EditorEntity browseOutputFolderButtonHost = GetPrivateField<EditorEntity>(dialog, "BrowseOutputFolderButtonHost");
            ButtonComponent browseOutputFolderButton = GetPrivateField<ButtonComponent>(dialog, "BrowseOutputFolderButton");
            ButtonComponent addToBuildButton = GetPrivateField<ButtonComponent>(dialog, "AddToBuildButton");
            ButtonComponent buildQueueButton = GetPrivateField<ButtonComponent>(dialog, "BuildQueueButton");
            EditorEntity addToBuildButtonHost = GetPrivateField<EditorEntity>(dialog, "AddToBuildButtonHost");
            EditorEntity buildQueueButtonHost = GetPrivateField<EditorEntity>(dialog, "BuildQueueButtonHost");
            int2 browseButtonSize = GetPrivateField<int2>(browseOutputFolderButton, "size");
            int2 addToBuildButtonSize = GetPrivateField<int2>(addToBuildButton, "size");
            int2 buildQueueButtonSize = GetPrivateField<int2>(buildQueueButton, "size");

            Assert.True(outputDirectoryField.Size.X < BuildDialog.PanelWidth - BuildDialog.QueueColumnWidth - (BuildDialog.PanelPadding * 3));
            Assert.True(browseOutputFolderButtonHost.LocalPosition.X > outputFieldHost.LocalPosition.X);
            Assert.Equal("Browse", GetPrivateField<string>(browseOutputFolderButton, "text"));
            Assert.Equal(BuildDialog.FooterButtonHeight, browseButtonSize.Y);
            Assert.Equal(buildQueueButtonSize.Y, addToBuildButtonSize.Y);
            Assert.Equal(buildQueueButtonHost.LocalPosition.Y, addToBuildButtonHost.LocalPosition.Y);
        }

        /// <summary>
        /// Ensures the output-folder textbox uses modal render orders so it remains visible above the dialog surface.
        /// </summary>
        [Fact]
        public void Show_RendersOutputFolderTextBoxInModalForeground() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            dialog.Show(
                ["windows"],
                [
                    "Scenes/City.helen"
                ],
                "windows",
                new EditorBuildConfigDocument {
                    Platforms = [
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "windows",
                            SelectedSceneIds = [
                                "Scenes/City.helen"
                            ]
                        }
                    ]
                });

            TextBoxComponent outputDirectoryField = GetPrivateField<TextBoxComponent>(dialog, "OutputDirectoryField");
            RoundedRectComponent backgroundSprite = GetPrivateField<RoundedRectComponent>(outputDirectoryField, "backgroundSprite");
            TextComponent textComponent = GetPrivateField<TextComponent>(outputDirectoryField, "textComponent");

            Assert.Equal(RenderOrder2D.ModalBackground, backgroundSprite.RenderOrder2D);
            Assert.Equal(RenderOrder2D.ModalForeground, textComponent.RenderOrder2D);
        }

        /// <summary>
        /// Ensures the browse button raises the output-folder browse request event.
        /// </summary>
        [Fact]
        public void HandleBrowseOutputFolderClicked_WhenInvoked_RaisesBrowseOutputFolderRequested() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            bool raised = false;
            dialog.BrowseOutputFolderRequested += () => raised = true;
            dialog.Show(
                ["windows"],
                [
                    "Scenes/City.helen"
                ],
                "windows",
                new EditorBuildConfigDocument {
                    Platforms = [
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "windows",
                            SelectedSceneIds = [
                                "Scenes/City.helen"
                            ]
                        }
                    ]
                });

            InvokePrivate(dialog, "HandleBrowseOutputFolderClicked");

            Assert.True(raised);
        }

        /// <summary>
        /// Creates one deterministic font asset for modal layout and control tests.
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

        /// <summary>
        /// Creates one deterministic scene list for layout-stress coverage.
        /// </summary>
        /// <param name="count">Number of scene ids to create.</param>
        /// <returns>Scene ids with stable ordering.</returns>
        IReadOnlyList<string> CreateSceneIds(int count) {
            List<string> sceneIds = new List<string>(count);

            for (int index = 0; index < count; index++) {
                sceneIds.Add("Scenes/Map" + index.ToString("00") + ".helen");
            }

            return sceneIds;
        }

        /// <summary>
        /// Reads one non-public instance field from the supplied object.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Exact private field name.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            FieldInfo field = FindPrivateField(target.GetType(), fieldName);
            object value = field.GetValue(target);
            return Assert.IsType<T>(value);
        }

        /// <summary>
        /// Invokes one non-public instance method on the supplied target object.
        /// </summary>
        /// <param name="target">Object that owns the method.</param>
        /// <param name="methodName">Exact private method name.</param>
        void InvokePrivate(object target, string methodName) {
            MethodInfo method = FindPrivateMethod(target.GetType(), methodName);
            method.Invoke(target, []);
        }

        /// <summary>
        /// Invokes one non-public instance method that accepts a single string argument.
        /// </summary>
        /// <param name="target">Object that owns the method.</param>
        /// <param name="methodName">Exact private method name.</param>
        /// <param name="value">String argument passed to the method.</param>
        void InvokePrivate(object target, string methodName, string value) {
            MethodInfo method = FindPrivateMethod(target.GetType(), methodName);
            method.Invoke(target, [
                value
            ]);
        }

        /// <summary>
        /// Invokes one non-public instance method that accepts a checkbox and checked-state payload.
        /// </summary>
        /// <param name="target">Object that owns the method.</param>
        /// <param name="methodName">Exact private method name.</param>
        /// <param name="checkBox">Checkbox argument passed to the method.</param>
        /// <param name="isChecked">Checked-state argument passed to the method.</param>
        void InvokePrivate(object target, string methodName, CheckBoxComponent checkBox, bool isChecked) {
            MethodInfo method = FindPrivateMethod(target.GetType(), methodName);
            method.Invoke(target, [
                checkBox,
                isChecked
            ]);
        }

        /// <summary>
        /// Finds one inherited non-public instance field by walking the type hierarchy.
        /// </summary>
        /// <param name="type">Type that starts the field lookup.</param>
        /// <param name="fieldName">Exact field name to locate.</param>
        /// <returns>Matching field metadata.</returns>
        FieldInfo FindPrivateField(Type type, string fieldName) {
            Type currentType = type;

            while (currentType != null) {
                FieldInfo field = currentType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

                if (field != null) {
                    return field;
                }

                currentType = currentType.BaseType;
            }

            throw new InvalidOperationException($"Field '{fieldName}' was not found on type '{type.FullName}'.");
        }

        /// <summary>
        /// Finds one inherited non-public instance method by walking the type hierarchy.
        /// </summary>
        /// <param name="type">Type that starts the method lookup.</param>
        /// <param name="methodName">Exact method name to locate.</param>
        /// <returns>Matching method metadata.</returns>
        MethodInfo FindPrivateMethod(Type type, string methodName) {
            Type currentType = type;

            while (currentType != null) {
                MethodInfo method = currentType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);

                if (method != null) {
                    return method;
                }

                currentType = currentType.BaseType;
            }

            throw new InvalidOperationException($"Method '{methodName}' was not found on type '{type.FullName}'.");
        }
    }
}
