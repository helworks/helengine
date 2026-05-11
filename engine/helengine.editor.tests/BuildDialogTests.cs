using System.Reflection;
using helengine;
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
        /// Configurable input backend used by pointer-routing build dialog tests.
        /// </summary>
        readonly TestInputBackend Input;

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
            Input = new TestInputBackend();
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), Input);
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

            PlatformTabStripView platformTabStrip = GetPrivateField<PlatformTabStripView>(dialog, "PlatformTabStrip");
            List<TabComponent> platformTabs = GetPrivateField<List<TabComponent>>(platformTabStrip, "Tabs");
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
        /// Ensures scaled metrics resize the build dialog scene list and footer buttons.
        /// </summary>
        [Fact]
        public void Show_WithScaledMetrics_UsesScaledSceneListAndFooterButtons() {
            EditorUiMetrics metrics = new EditorUiMetrics(1.5d);
            BuildDialog dialog = new BuildDialog(CreateFont(), metrics);

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

            RoundedRectComponent sceneListBackground = GetPrivateField<RoundedRectComponent>(dialog, "SceneListBackground");
            ButtonComponent copySettingsButton = GetPrivateField<ButtonComponent>(dialog, "CopySettingsButton");
            ButtonComponent browseOutputFolderButton = GetPrivateField<ButtonComponent>(dialog, "BrowseOutputFolderButton");
            ButtonComponent buildQueueButton = GetPrivateField<ButtonComponent>(dialog, "BuildQueueButton");

            Assert.Equal(metrics.ScalePixels(BuildDialog.PanelWidth - BuildDialog.QueueColumnWidth - (BuildDialog.PanelPadding * 3)), sceneListBackground.Size.X);
            Assert.Equal(metrics.ScalePixels(BuildDialog.FooterButtonHeight), copySettingsButton.Size.Y);
            Assert.Equal(metrics.ScalePixels(BuildDialog.FooterButtonHeight), browseOutputFolderButton.Size.Y);
            Assert.Equal(metrics.ScalePixels(BuildDialog.FooterButtonHeight), buildQueueButton.Size.Y);
        }

        /// <summary>
        /// Ensures inactive platform tabs use the shared tab component defaults and the active tab stays selected.
        /// </summary>
        [Fact]
        public void Show_WhenPs2TabIsInactive_UsesTabComponentDefaults() {
            BuildDialog dialog = new BuildDialog(CreateFont());

            dialog.Show(
                ["windows", "ps2"],
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
                        },
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "ps2",
                            SelectedSceneIds = [
                                "Scenes/City.helen"
                            ]
                        }
                    ]
                });

            PlatformTabStripView platformTabStrip = GetPrivateField<PlatformTabStripView>(dialog, "PlatformTabStrip");
            List<TabComponent> platformTabs = GetPrivateField<List<TabComponent>>(platformTabStrip, "Tabs");
            List<EditorEntity> platformTabHosts = GetPrivateField<List<EditorEntity>>(platformTabStrip, "TabHosts");
            RoundedRectComponent sceneListBackground = GetPrivateField<RoundedRectComponent>(dialog, "SceneListBackground");
            EditorEntity sceneListRoot = GetPrivateField<EditorEntity>(dialog, "SceneListRoot");

            Assert.Equal(BuildDialog.PlatformTabWidth, (int)platformTabHosts[1].LocalPosition.X);
            Assert.All(platformTabs, tab => Assert.Equal(RoundedRectCorners.TopLeft | RoundedRectCorners.TopRight, tab.Corners));
            Assert.True(platformTabs[0].IsSelected);
            Assert.False(platformTabs[1].IsSelected);
            Assert.Equal(RoundedRectCorners.BottomLeft | RoundedRectCorners.BottomRight, sceneListBackground.Corners);
            Assert.Equal(BuildDialog.PlatformTabHeight - 1, (int)sceneListRoot.LocalPosition.Y);
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
        /// Ensures Add to Build preserves the selected displayed scene when the visible row order differs from the raw scene list.
        /// </summary>
        [Fact]
        public void HandleAddToBuildClicked_WhenVisibleRowsAreReordered_PreservesDisplayedSceneSelection() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            BuildDialogAddRequest raisedRequest = null;
            dialog.AddRequested += request => raisedRequest = request;
            dialog.Show(
                ["windows"],
                [
                    "Scenes/Physics/TestSceneTriggerVolume.helen",
                    "Scenes/DemoDiscMenu.helen"
                ],
                "windows",
                new EditorBuildConfigDocument {
                    Platforms = [
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "windows",
                            SelectedSceneIds = [
                                "Scenes/DemoDiscMenu.helen"
                            ],
                            SceneOrders = [
                                new EditorBuildSceneOrderDocument {
                                    SceneId = "Scenes/Physics/TestSceneTriggerVolume.helen",
                                    OrderNumber = 2
                                },
                                new EditorBuildSceneOrderDocument {
                                    SceneId = "Scenes/DemoDiscMenu.helen",
                                    OrderNumber = 1
                                }
                            ],
                            OutputDirectoryPath = @"C:\builds\windows"
                        }
                    ]
                });

            List<TextComponent> mapLabelTexts = GetPrivateField<List<TextComponent>>(dialog, "MapLabelTexts");
            List<CheckBoxComponent> mapCheckBoxes = GetPrivateField<List<CheckBoxComponent>>(dialog, "MapCheckBoxes");

            Assert.Equal("Scenes/DemoDiscMenu.helen", mapLabelTexts[0].Text);
            Assert.True(mapCheckBoxes[0].IsChecked);
            Assert.False(mapCheckBoxes[1].IsChecked);

            InvokePrivate(dialog, "HandleAddToBuildClicked");

            Assert.NotNull(raisedRequest);
            Assert.Equal(
                [
                    "Scenes/DemoDiscMenu.helen"
                ],
                raisedRequest.SelectedSceneIds);
        }

        /// <summary>
        /// Ensures Add to Build preserves a selected scene that is outside the current visible scene-list viewport.
        /// </summary>
        [Fact]
        public void HandleAddToBuildClicked_WhenSelectedSceneIsOutsideVisibleViewport_PreservesHiddenSelection() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            BuildDialogAddRequest raisedRequest = null;
            IReadOnlyList<string> sceneIds = CreateSceneIds(18);
            dialog.AddRequested += request => raisedRequest = request;

            dialog.Show(
                ["windows"],
                sceneIds,
                "windows",
                new EditorBuildConfigDocument {
                    Platforms = [
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "windows",
                            SelectedSceneIds = [
                                "Scenes/Map14.helen"
                            ],
                            OutputDirectoryPath = @"C:\builds\windows"
                        }
                    ]
                });

            List<TextComponent> mapLabelTexts = GetPrivateField<List<TextComponent>>(dialog, "MapLabelTexts");

            Assert.DoesNotContain("Scenes/Map14.helen", mapLabelTexts.Select(label => label.Text));

            InvokePrivate(dialog, "HandleAddToBuildClicked");

            Assert.NotNull(raisedRequest);
            Assert.Equal(
                [
                    "Scenes/Map14.helen"
                ],
                raisedRequest.SelectedSceneIds);
        }

        /// <summary>
        /// Ensures switching to another platform restores that platform's saved debug-build value.
        /// </summary>
        [Fact]
        public void HandlePlatformTabClicked_WhenPlatformsStoreDifferentDebugBuildValues_RestoresTheActiveValue() {
            BuildDialog dialog = new BuildDialog(CreateFont());
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
                            OutputDirectoryPath = @"C:\builds\windows",
                            DebugBuild = true
                        },
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "linux",
                            SelectedSceneIds = [
                                "Scenes/Menu.helen"
                            ],
                            OutputDirectoryPath = "/tmp/linux-build",
                            DebugBuild = false
                        }
                    ]
                });

            CheckBoxComponent debugBuildCheckBox = GetPrivateField<CheckBoxComponent>(dialog, "DebugBuildCheckBox");

            Assert.True(debugBuildCheckBox.IsChecked);

            InvokePrivate(dialog, "HandlePlatformTabClicked", "linux");

            Assert.False(debugBuildCheckBox.IsChecked);
        }

        /// <summary>
        /// Ensures Add to Build snapshots the active platform's debug-build flag into the queued build request.
        /// </summary>
        [Fact]
        public void HandleAddToBuildClicked_WhenDebugBuildIsEnabled_SnapshotsTheDebugBuildFlag() {
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
                            OutputDirectoryPath = @"C:\builds\windows",
                            DebugBuild = true
                        }
                    ]
                });

            InvokePrivate(dialog, "HandleAddToBuildClicked");

            Assert.NotNull(raisedRequest);
            Assert.True(raisedRequest.DebugBuild);
        }

        /// <summary>
        /// Ensures Add to Build clears persisted runtime-module selections because the dialog no longer exposes module picking.
        /// </summary>
        [Fact]
        public void HandleAddToBuildClicked_WhenPlatformContainsPersistedRuntimeModules_ClearsTheSelection() {
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
                            OutputDirectoryPath = @"C:\builds\windows",
                            SelectedCodeModuleIds = [
                                "gameplay",
                                "ui"
                            ]
                        }
                    ]
                });

            InvokePrivate(dialog, "HandleAddToBuildClicked");

            Assert.NotNull(raisedRequest);
            Assert.Empty(raisedRequest.SelectedCodeModuleIds);
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
            mapCheckBoxes[0].IsChecked = true;
            mapCheckBoxes[1].IsChecked = false;
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
        public void CopyMapListFrom_WhenSourcePlatformSelected_CopiesSceneSelectionIntoActivePlatform() {
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

            dialog.CopyMapListFrom("windows");

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
            Assert.Equal("1", mapOrderFields[0].Text);
            Assert.Equal("2", mapOrderFields[1].Text);
            Assert.Equal(2, linuxConfig.SceneOrders.Count);
            Assert.Equal(1, linuxConfig.SceneOrders[0].OrderNumber);
            Assert.Equal(2, linuxConfig.SceneOrders[1].OrderNumber);
            Assert.Equal("/tmp/linux-build", linuxConfig.OutputDirectoryPath);
        }

        /// <summary>
        /// Ensures the copy-settings button raises the request that opens the chooser modal.
        /// </summary>
        [Fact]
        public void HandleCopySettingsButtonClicked_WhenInvoked_RaisesCopySettingsRequested() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            bool raised = false;
            dialog.CopySettingsRequested += () => raised = true;
            dialog.Show(
                ["windows", "linux"],
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
                        },
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "linux",
                            SelectedSceneIds = [
                                "Scenes/Menu.helen"
                            ]
                        }
                    ]
                });

            InvokePrivate(dialog, "HandleCopySettingsButtonClicked");

            Assert.True(raised);
        }

        /// <summary>
        /// Ensures one queue row is rendered for each persisted queued build item and cards omit verbose status-message text.
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
                            Status = EditorBuildQueueItemStatus.Pending,
                            DebugBuild = true,
                            SelectedBuildProfileId = "b",
                            SelectedGraphicsProfileId = "g",
                            SelectedCodeModuleIds = [
                                "gameplay",
                                "ai"
                            ]
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
            string[] firstLines = queueItemTexts[0].Text.Split('\n');
            string[] secondLines = queueItemTexts[1].Text.Split('\n');

            Assert.Equal(2, queueItemTexts.Count);
            Assert.Equal(3, firstLines.Length);
            Assert.Equal("windows | Pending", firstLines[0]);
            Assert.Equal("1 scene(s) | Debug", firstLines[1]);
            Assert.Equal("build b | gfx g | runtime modules 2", firstLines[2]);
            Assert.Equal("windows | Failed", secondLines[0]);
            Assert.Equal("1 scene(s) | Release", secondLines[1]);
            Assert.DoesNotContain("Unsupported scene format.", queueItemTexts[1].Text);
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
                BuildDialog.DialogContentHeight - BuildDialog.HeaderHeight - BuildDialog.PanelPadding - BuildDialog.FooterButtonHeight - 8,
                (int)buildQueueButtonHost.LocalPosition.Y);
            Assert.Equal(BuildDialog.DialogContentHeight, (int)buildLogsRoot.LocalPosition.Y);
            Assert.True(buildLogsRoot.LocalPosition.Y > addToBuildButtonHost.LocalPosition.Y + BuildDialog.FooterButtonHeight);
            Assert.Equal(0f, buildLogsBackground.Radius);
            Assert.Equal(BuildDialog.BuildLogsSectionHeight, buildLogsBackground.Size.Y);
            Assert.True(buildLogsProgressTrack.Size.X > 0);
            Assert.True(buildLogsProgressFill.Size.X > 0);
            Assert.True(buildLogsProgressFill.Size.X < buildLogsProgressTrack.Size.X);
            Assert.Contains("Progress:", buildLogsText.Text);
            Assert.Contains("Windows build completed.", buildLogsText.Text);
            Assert.Contains("linux | Pending", buildLogsText.Text);
        }

        /// <summary>
        /// Ensures the build-log text component opts into shared text wrapping.
        /// </summary>
        [Fact]
        public void Show_WhenQueueItemsProvided_EnablesWrappingForBuildLogsText() {
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
                            StatusMessage = "This status message is intentionally long so wrapping matters."
                        }
                    ]
                });

            TextComponent buildLogsText = GetPrivateField<TextComponent>(dialog, "BuildLogsText");
            PropertyInfo wrapTextProperty = buildLogsText.GetType().GetProperty("WrapText");

            Assert.NotNull(wrapTextProperty);
            Assert.True((bool)wrapTextProperty.GetValue(buildLogsText));
        }

        /// <summary>
        /// Ensures only the build-log body opts into text selection so labels remain passive.
        /// </summary>
        [Fact]
        public void Show_WhenQueueItemsProvided_EnablesSelectionOnlyForBuildLogsText() {
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
                            StatusMessage = "This status message is intentionally long so selection matters."
                        }
                    ]
                });

            TextComponent buildLogsText = GetPrivateField<TextComponent>(dialog, "BuildLogsText");
            TextComponent buildLogsTitleText = GetPrivateField<TextComponent>(dialog, "BuildLogsTitleText");

            Assert.True(buildLogsText.SelectionEnabled);
            Assert.False(buildLogsTitleText.SelectionEnabled);
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

            Assert.Equal(0f, queueListBackground.Radius);
            Assert.Equal(ThemeManager.Colors.AccentTertiary, queueListBackground.BorderColor);
            Assert.Equal(2f, queueListBackground.BorderThickness);
            Assert.Equal(6f, queueHeaderBackground.Radius);
            Assert.Equal(ThemeManager.Colors.AccentSecondary, queueHeaderBackground.FillColor);
            Assert.Equal("Queue", queueHeaderText.Text);
            Assert.Equal(ThemeManager.Colors.InputForegroundPrimary, queueHeaderText.Color);
        }

        /// <summary>
        /// Ensures each queued build spans the queue column and uses a bottom separator instead of a full card border.
        /// </summary>
        [Fact]
        public void Show_WhenQueueItemsProvided_RendersFullWidthRowPerQueueItem() {
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

            List<EditorEntity> queueItemHosts = GetPrivateField<List<EditorEntity>>(dialog, "QueueItemHosts");
            List<RoundedRectComponent> queueItemCardBackgrounds = GetPrivateField<List<RoundedRectComponent>>(dialog, "QueueItemCardBackgrounds");

            Assert.Equal(2, queueItemHosts.Count);
            Assert.Equal(2, queueItemCardBackgrounds.Count);
            Assert.All(queueItemHosts, queueItemHost => {
                RoundedRectComponent background = FindComponent<RoundedRectComponent>(queueItemHost);
                SpriteComponent separator = FindComponent<SpriteComponent>(queueItemHost);

                Assert.Equal(2f, queueItemHost.LocalPosition.X);
                Assert.Equal(ThemeManager.Colors.SurfacePrimary, background.FillColor);
                Assert.Equal(ThemeManager.Colors.SurfacePrimary, background.BorderColor);
                Assert.Equal(0f, background.BorderThickness);
                Assert.Equal(BuildDialog.QueueColumnWidth - 4, background.Size.X);
                Assert.Equal(BuildDialog.QueueRowHeight, background.Size.Y);
                Assert.Equal(TextureUtils.PixelTexture, separator.Texture);
                Assert.Equal(ThemeManager.Colors.AccentTertiary, separator.Color);
                Assert.Equal(BuildDialog.QueueColumnWidth - 4, separator.Size.X);
                Assert.Equal(1, separator.Size.Y);
                Assert.Equal(BuildDialog.QueueRowHeight - 1f, separator.Parent.LocalPosition.Y);
            });
        }

        /// <summary>
        /// Ensures a long optional capability summary is clipped on the third line before it reaches the remove button lane.
        /// </summary>
        [Fact]
        public void Show_WhenQueueItemsProvided_ClipsCapabilitySummaryOnThirdLine() {
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
                            Status = EditorBuildQueueItemStatus.Failed,
                            DebugBuild = false,
                            SelectedBuildProfileId = "build-profile-with-a-very-long-name",
                            SelectedGraphicsProfileId = "graphics-profile-with-a-very-long-name",
                            SelectedCodegenProfileId = "codegen-profile-with-a-very-long-name",
                            SelectedCodeModuleIds = [
                                "gameplay",
                                "ai",
                                "editor"
                            ],
                            StatusMessage = "This failure belongs in the build log, not in the card."
                        }
                    ]
                });

            TextComponent queueText = Assert.Single(GetPrivateField<List<TextComponent>>(dialog, "QueueItemTexts"));
            string[] lines = queueText.Text.Split('\n');

            Assert.Equal(3, lines.Length);
            Assert.Equal("windows | Failed", lines[0]);
            Assert.Equal("1 scene(s) | Release", lines[1]);
            Assert.DoesNotContain("This failure belongs in the build log, not in the card.", queueText.Text);
            Assert.EndsWith("...", lines[2]);
        }

        /// <summary>
        /// Ensures the queue-card text width is reduced before layout so the remove button keeps a dedicated right-edge lane.
        /// </summary>
        [Fact]
        public void Show_WhenQueueItemsProvided_ReservesTextWidthForRemoveButton() {
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
                            Status = EditorBuildQueueItemStatus.Pending,
                            DebugBuild = true,
                            SelectedBuildProfileId = "b1",
                            SelectedGraphicsProfileId = "g1",
                            SelectedCodegenProfileId = "c1",
                            SelectedCodeModuleIds = [
                                "gameplay",
                                "ai"
                            ]
                        }
                    ]
                });

            TextComponent queueText = Assert.Single(GetPrivateField<List<TextComponent>>(dialog, "QueueItemTexts"));
            RoundedRectComponent queueCardBackground = Assert.Single(GetPrivateField<List<RoundedRectComponent>>(dialog, "QueueItemCardBackgrounds"));
            EditorEntity removeButtonHost = Assert.Single(GetPrivateField<List<EditorEntity>>(dialog, "QueueItemRemoveButtonHosts"));

            Assert.Equal(
                queueCardBackground.Size.X - (BuildDialog.QueueCardTextPadding * 2) - BuildDialog.QueueCardRemoveButtonWidth - BuildDialog.QueueCardTextButtonGap,
                queueText.Size.X);
            Assert.True(removeButtonHost.LocalPosition.X >= queueText.Parent.LocalPosition.X + queueText.Size.X + BuildDialog.QueueCardTextButtonGap);
        }

        /// <summary>
        /// Ensures each queued build row exposes one remove button aligned on the right edge.
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
        /// Ensures the queued-build list uses a scroll viewport when the queue exceeds the visible row count.
        /// </summary>
        [Fact]
        public void Show_WhenQueueItemsExceedViewport_VirtualizesRowsAndRespondsToScrollOffset() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            List<EditorBuildQueueItemDocument> queueItems = [];

            for (int index = 0; index < 9; index++) {
                queueItems.Add(new EditorBuildQueueItemDocument {
                    QueueItemId = "queue-" + (index + 1).ToString(),
                    PlatformId = "platform-" + (index + 1).ToString(),
                    SelectedSceneIds = [
                        "Scenes/City.helen"
                    ],
                    OutputDirectoryPath = @"C:\builds\windows",
                    Status = EditorBuildQueueItemStatus.Pending,
                    DebugBuild = index % 2 == 0
                });
            }

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
                    QueueItems = queueItems
                });

            ScrollComponent queueScrollComponent = GetPrivateField<ScrollComponent>(dialog, "QueueScrollComponent");
            List<TextComponent> queueItemTexts = GetPrivateField<List<TextComponent>>(dialog, "QueueItemTexts");

            Assert.True(queueScrollComponent.MaximumScrollOffset > 0);
            Assert.Equal(queueScrollComponent.VisibleItemCount, queueItemTexts.Count);
            Assert.Contains("platform-1 | Pending", queueItemTexts[0].Text);

            Assert.True(queueScrollComponent.ScrollTo(1));

            Assert.Contains("platform-2 | Pending", queueItemTexts[0].Text);
            Assert.DoesNotContain("platform-1 | Pending", queueItemTexts[0].Text);

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
                    QueueItems = queueItems
                });

            Assert.Equal(0, queueScrollComponent.ScrollOffset);
            Assert.Contains("platform-1 | Pending", queueItemTexts[0].Text);
        }

        /// <summary>
        /// Ensures the scene list uses a scroll viewport when the active platform exposes more scenes than fit inside the bordered list area.
        /// </summary>
        [Fact]
        public void Show_WhenSceneRowsExceedViewport_VirtualizesRowsAndRespondsToScrollOffset() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            IReadOnlyList<string> sceneIds = CreateSceneIds(18);

            dialog.Show(
                ["windows"],
                sceneIds,
                "windows",
                new EditorBuildConfigDocument {
                    Platforms = [
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "windows",
                            SelectedSceneIds = [
                                "Scenes/Map00.helen"
                            ],
                            OutputDirectoryPath = @"C:\builds\windows"
                        }
                    ]
                });

            ScrollComponent sceneListScrollComponent = GetPrivateField<ScrollComponent>(dialog, "SceneListScrollComponent");
            List<TextComponent> mapLabelTexts = GetPrivateField<List<TextComponent>>(dialog, "MapLabelTexts");

            Assert.True(sceneListScrollComponent.MaximumScrollOffset > 0);
            Assert.Equal(sceneListScrollComponent.VisibleItemCount, mapLabelTexts.Count);
            Assert.Equal("Scenes/Map00.helen", mapLabelTexts[0].Text);

            Assert.True(sceneListScrollComponent.ScrollTo(1));

            Assert.Equal("Scenes/Map01.helen", mapLabelTexts[0].Text);
            Assert.DoesNotContain("Scenes/Map00.helen", mapLabelTexts[0].Text);

            dialog.Show(
                ["windows"],
                sceneIds,
                "windows",
                new EditorBuildConfigDocument {
                    Platforms = [
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "windows",
                            SelectedSceneIds = [
                                "Scenes/Map00.helen"
                            ],
                            OutputDirectoryPath = @"C:\builds\windows"
                        }
                    ]
                });

            Assert.Equal(0, sceneListScrollComponent.ScrollOffset);
            Assert.Equal("Scenes/Map00.helen", mapLabelTexts[0].Text);
        }

        /// <summary>
        /// Ensures the scene list allocates one trailing pooled row when the viewport clips into the next row.
        /// </summary>
        [Fact]
        public void UpdateSceneListRowsLayout_WhenViewportClipsNextRow_RendersPartiallyVisibleTrailingRow() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            IReadOnlyList<string> sceneIds = CreateSceneIds(18);

            dialog.Show(
                ["windows"],
                sceneIds,
                "windows",
                new EditorBuildConfigDocument {
                    Platforms = [
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "windows",
                            SelectedSceneIds = [
                                "Scenes/Map00.helen"
                            ],
                            OutputDirectoryPath = @"C:\builds\windows"
                        }
                    ]
                });

            RoundedRectComponent sceneListBackground = GetPrivateField<RoundedRectComponent>(dialog, "SceneListBackground");
            ScrollComponent sceneListScrollComponent = GetPrivateField<ScrollComponent>(dialog, "SceneListScrollComponent");

            sceneListBackground.Size = new int2(
                sceneListBackground.Size.X,
                (BuildDialog.SceneListPadding * 2) + (BuildDialog.SceneRowHeight * 3) + (BuildDialog.SceneRowHeight / 2));
            sceneListScrollComponent.VisibleItemCount = 0;

            InvokePrivate(dialog, "UpdateSceneListRowsLayout");

            List<TextComponent> mapLabelTexts = GetPrivateField<List<TextComponent>>(dialog, "MapLabelTexts");

            Assert.Equal(4, sceneListScrollComponent.VisibleItemCount);
            Assert.Equal(4, mapLabelTexts.Count);
            Assert.Equal("Scenes/Map03.helen", mapLabelTexts[^1].Text);

            Assert.True(sceneListScrollComponent.ScrollTo(1));

            Assert.Equal("Scenes/Map01.helen", mapLabelTexts[0].Text);
            Assert.Equal("Scenes/Map04.helen", mapLabelTexts[^1].Text);
        }

        /// <summary>
        /// Ensures the build-log section also pages through content with its own scroll viewport.
        /// </summary>
        [Fact]
        public void Show_WhenBuildLogLinesExceedViewport_VirtualizesLogsAndRespondsToScrollOffset() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            List<EditorBuildQueueItemDocument> queueItems = [];

            for (int index = 0; index < 8; index++) {
                queueItems.Add(new EditorBuildQueueItemDocument {
                    QueueItemId = "queue-" + (index + 1).ToString(),
                    PlatformId = "platform-" + (index + 1).ToString(),
                    SelectedSceneIds = [
                        "Scenes/City.helen"
                    ],
                    OutputDirectoryPath = @"C:\builds\windows",
                    Status = EditorBuildQueueItemStatus.Pending,
                    StatusMessage = "log line " + (index + 1).ToString()
                });
            }

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
                    QueueItems = queueItems
                });

            ScrollComponent buildLogsScrollComponent = GetPrivateField<ScrollComponent>(dialog, "BuildLogsScrollComponent");
            TextComponent buildLogsText = GetPrivateField<TextComponent>(dialog, "BuildLogsText");

            Assert.True(buildLogsScrollComponent.MaximumScrollOffset > 0);
            Assert.StartsWith("Progress:", buildLogsText.Text);
            Assert.Contains("platform-1 | Pending | log line 1", buildLogsText.Text);

            Assert.True(buildLogsScrollComponent.ScrollTo(1));

            Assert.DoesNotContain("Progress:", buildLogsText.Text);
            Assert.StartsWith("platform-1 | Pending | log line 1", buildLogsText.Text);
            Assert.DoesNotContain("platform-6 | Pending | log line 6", buildLogsText.Text);

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
                    QueueItems = queueItems
                });

            Assert.Equal(0, buildLogsScrollComponent.ScrollOffset);
            Assert.StartsWith("Progress:", buildLogsText.Text);
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

            TextComponent outputLabelText = GetPrivateField<TextComponent>(dialog, "OutputLabelText");
            List<TextComponent> mapLabelTexts = GetPrivateField<List<TextComponent>>(dialog, "MapLabelTexts");
            List<TextComponent> queueItemTexts = GetPrivateField<List<TextComponent>>(dialog, "QueueItemTexts");
            TextComponent buildLogsText = GetPrivateField<TextComponent>(dialog, "BuildLogsText");

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
        /// Ensures clicking the Build dialog close button works before the panel has been dragged.
        /// </summary>
        [Fact]
        public void Update_WhenPointerClicksTitleBarCloseButtonBeforeMoving_HidesDialog() {
            CreateModalCamera(1280, 960);

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
            dialog.UpdateLayout(1280, 960);

            int2 panelPosition = GetPrivateField<int2>(dialog, "PanelPosition");
            EditorEntity closeButtonHost = GetPrivateField<EditorEntity>(dialog, "CloseButtonHost");
            ButtonComponent closeButton = GetPrivateField<ButtonComponent>(dialog, "CloseButton");
            int pointerX = panelPosition.X + (int)Math.Round(closeButtonHost.LocalPosition.X) + (closeButton.Size.X / 2);
            int pointerY = panelPosition.Y + (int)Math.Round(closeButtonHost.LocalPosition.Y) + (closeButton.Size.Y / 2);

            AdvanceInput(new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceInput(new MouseState(pointerX, pointerY, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceInput(new MouseState(pointerX, pointerY, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceInput(new MouseState(pointerX, pointerY, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));

            Assert.False(dialog.Enabled);
        }

        /// <summary>
        /// Ensures clicking one queue-item remove button works before the panel has been dragged.
        /// </summary>
        [Fact]
        public void Update_WhenPointerClicksQueueItemRemoveButtonBeforeMoving_RaisesRemoveRequest() {
            CreateModalCamera(1280, 960);

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
            dialog.UpdateLayout(1280, 960);

            int2 panelPosition = GetPrivateField<int2>(dialog, "PanelPosition");
            EditorEntity queueColumnRoot = GetPrivateField<EditorEntity>(dialog, "QueueColumnRoot");
            EditorEntity queueItemsRoot = GetPrivateField<EditorEntity>(dialog, "QueueItemsRoot");
            EditorEntity queueItemHost = Assert.Single(GetPrivateField<List<EditorEntity>>(dialog, "QueueItemHosts"));
            EditorEntity removeButtonHost = Assert.Single(GetPrivateField<List<EditorEntity>>(dialog, "QueueItemRemoveButtonHosts"));
            ButtonComponent removeButton = Assert.Single(GetPrivateField<List<ButtonComponent>>(dialog, "QueueItemRemoveButtons"));
            int pointerX = panelPosition.X +
                           (int)Math.Round(queueColumnRoot.LocalPosition.X) +
                           (int)Math.Round(queueItemsRoot.LocalPosition.X) +
                           (int)Math.Round(queueItemHost.LocalPosition.X) +
                           (int)Math.Round(removeButtonHost.LocalPosition.X) +
                           (removeButton.Size.X / 2);
            int pointerY = panelPosition.Y +
                           (int)Math.Round(queueColumnRoot.LocalPosition.Y) +
                           (int)Math.Round(queueItemsRoot.LocalPosition.Y) +
                           (int)Math.Round(queueItemHost.LocalPosition.Y) +
                           (int)Math.Round(removeButtonHost.LocalPosition.Y) +
                           (removeButton.Size.Y / 2);

            AdvanceInput(new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceInput(new MouseState(pointerX, pointerY, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceInput(new MouseState(pointerX, pointerY, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceInput(new MouseState(pointerX, pointerY, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));

            Assert.Equal("queue-1", removedQueueItemId);
        }

        /// <summary>
        /// Ensures clicking one visible scene checkbox through the live pointer system hovers and toggles it.
        /// </summary>
        [Fact]
        public void Update_WhenPointerClicksSceneCheckboxBeforeMoving_TogglesSelection() {
            CreateModalCamera(1280, 960);

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
                            SelectedSceneIds = [],
                            OutputDirectoryPath = @"C:\builds\windows"
                        }
                    ]
                });
            dialog.UpdateLayout(1280, 960);

            int2 panelPosition = GetPrivateField<int2>(dialog, "PanelPosition");
            EditorEntity buildColumnRoot = GetPrivateField<EditorEntity>(dialog, "BuildColumnRoot");
            EditorEntity sceneListRoot = GetPrivateField<EditorEntity>(dialog, "SceneListRoot");
            CameraComponent contentCamera = GetPrivateField<CameraComponent>(dialog, "SceneListContentCameraComponent");
            BuildDialogSceneRow firstRow = GetPrivateField<List<BuildDialogSceneRow>>(dialog, "SceneRows")
                .Where(row => row.Root.Enabled)
                .OrderBy(row => row.Root.LocalPosition.Y)
                .First();
            InteractableComponent checkboxInteractable = FindComponent<InteractableComponent>(firstRow.CheckBoxHost);
            int pointerX = panelPosition.X +
                           (int)Math.Round(buildColumnRoot.LocalPosition.X) +
                           (int)Math.Round(sceneListRoot.LocalPosition.X) +
                           (int)Math.Round(firstRow.Root.LocalPosition.X) +
                           (int)Math.Round(firstRow.CheckBoxHost.LocalPosition.X) +
                           (firstRow.CheckBox.Size.X / 2);
            int pointerY = panelPosition.Y +
                           (int)Math.Round(buildColumnRoot.LocalPosition.Y) +
                           (int)Math.Round(sceneListRoot.LocalPosition.Y) +
                           (int)Math.Round(firstRow.Root.LocalPosition.Y) +
                           (int)Math.Round(firstRow.CheckBoxHost.LocalPosition.Y) +
                           (firstRow.CheckBox.Size.Y / 2);

            Assert.True(contentCamera.Viewport.Contains(pointerX, pointerY));
            Assert.Contains(checkboxInteractable, Core.Instance.ObjectManager.Interactables);
            Assert.Same(
                checkboxInteractable,
                PointerInteractableHitResolver.ResolveTopInteractableAt(
                    Core.Instance.ObjectManager.Interactables,
                    Core.Instance.ObjectManager.Drawables2D,
                    contentCamera,
                    pointerX,
                    pointerY));

            AdvanceInput(new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceInput(new MouseState(pointerX, pointerY, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));

            Assert.Equal(PointerCursorKind.Hand, Core.Instance.PointerInteractionSystem.HoverCursor);

            AdvanceInput(new MouseState(pointerX, pointerY, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceInput(new MouseState(pointerX, pointerY, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));

            Assert.True(firstRow.CheckBox.IsChecked);
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
            Assert.Equal(BuildDialog.PlatformTabHeight - 1, (int)sceneListRoot.LocalPosition.Y);
            Assert.Equal(ThemeManager.Colors.AccentTertiary, sceneListBackground.BorderColor);
            Assert.Equal(2f, sceneListBackground.BorderThickness);
            Assert.Equal(BuildDialog.PanelWidth - BuildDialog.QueueColumnWidth - (BuildDialog.PanelPadding * 3), sceneListBackground.Size.X);
            Assert.True(sceneListBackground.Size.Y > 0);
            Assert.All(mapLabelHosts, host => Assert.Equal(BuildDialog.SceneListPadding + BuildDialog.SceneOrderFieldWidth + 8, host.LocalPosition.X));
        }

        /// <summary>
        /// Ensures the build scene list renders through a dedicated clipped viewport that stays inside the bordered list surface.
        /// </summary>
        [Fact]
        public void Show_CreatesClippedSceneListContentViewportInsideTheBorder() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            dialog.Show(
                ["windows"],
                CreateSceneIds(24),
                "windows",
                new EditorBuildConfigDocument {
                    Platforms = [
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "windows",
                            SelectedSceneIds = [
                                "Scenes/Map00.helen"
                            ]
                        }
                    ]
                });
            dialog.UpdateLayout(1280, 900);

            CameraComponent contentCamera = GetPrivateField<CameraComponent>(dialog, "SceneListContentCameraComponent");
            RoundedRectComponent sceneListBackground = GetPrivateField<RoundedRectComponent>(dialog, "SceneListBackground");
            EditorEntity buildColumnRoot = GetPrivateField<EditorEntity>(dialog, "BuildColumnRoot");
            EditorEntity sceneListRoot = GetPrivateField<EditorEntity>(dialog, "SceneListRoot");
            EditorEntity sceneListItemsRoot = GetPrivateField<EditorEntity>(dialog, "SceneListItemsRoot");
            List<BuildDialogSceneRow> sceneRows = GetPrivateField<List<BuildDialogSceneRow>>(dialog, "SceneRows");
            int2 panelPosition = GetPrivateField<int2>(dialog, "PanelPosition");
            int borderInset = (int)Math.Ceiling(sceneListBackground.BorderThickness);

            Assert.True(sceneRows.Count > 0);
            Assert.Equal(0b0000000100000000, sceneListItemsRoot.LayerMask);
            Assert.Equal(0b0000000100000000, sceneRows[0].Root.LayerMask);
            Assert.Equal(0b0000000100000000, sceneRows[0].OrderHost.LayerMask);
            Assert.Equal(0b0000000100000000, sceneRows[0].LabelHost.LayerMask);
            Assert.Equal(0b0000000100000000, sceneRows[0].CheckBoxHost.LayerMask);
            Assert.Equal(panelPosition.X + buildColumnRoot.LocalPosition.X + sceneListRoot.LocalPosition.X + borderInset, contentCamera.Viewport.X);
            Assert.Equal(panelPosition.Y + buildColumnRoot.LocalPosition.Y + sceneListRoot.LocalPosition.Y + borderInset, contentCamera.Viewport.Y);
            Assert.Equal(sceneListBackground.Size.X - (borderInset * 2), contentCamera.Viewport.Z);
            Assert.Equal(sceneListBackground.Size.Y - (borderInset * 2), contentCamera.Viewport.W);
        }

        /// <summary>
        /// Ensures the clipped build scene-list viewport tracks the dialog when the modal is repositioned.
        /// </summary>
        [Fact]
        public void ApplyVisibleDialogState_WhenDialogMoves_RepositionsTheClippedSceneListViewport() {
            BuildDialog dialog = new BuildDialog(CreateFont());
            dialog.Show(
                ["windows"],
                CreateSceneIds(12),
                "windows",
                new EditorBuildConfigDocument {
                    Platforms = [
                        new EditorBuildPlatformConfigDocument {
                            PlatformId = "windows",
                            SelectedSceneIds = [
                                "Scenes/Map00.helen"
                            ]
                        }
                    ]
                });
            dialog.UpdateLayout(1280, 900);

            CameraComponent contentCamera = GetPrivateField<CameraComponent>(dialog, "SceneListContentCameraComponent");
            RoundedRectComponent sceneListBackground = GetPrivateField<RoundedRectComponent>(dialog, "SceneListBackground");
            EditorEntity buildColumnRoot = GetPrivateField<EditorEntity>(dialog, "BuildColumnRoot");
            EditorEntity sceneListRoot = GetPrivateField<EditorEntity>(dialog, "SceneListRoot");
            int borderInset = (int)Math.Ceiling(sceneListBackground.BorderThickness);
            float initialViewportX = contentCamera.Viewport.X;
            float initialViewportY = contentCamera.Viewport.Y;

            InvokePrivate(dialog, "HandleHeaderCursor", new int2(12, 12), new int2(0, 0), PointerInteraction.Press);
            InvokePrivate(dialog, "HandleHeaderCursor", new int2(12, 12), new int2(32, 24), PointerInteraction.Hover);
            InvokePrivate(dialog, "HandleHeaderCursor", new int2(12, 12), new int2(0, 0), PointerInteraction.Release);

            Assert.Equal(initialViewportX + 32f, contentCamera.Viewport.X);
            Assert.Equal(initialViewportY + 24f, contentCamera.Viewport.Y);
            Assert.Equal(contentCamera.Viewport.X, sceneListRoot.Position.X + borderInset);
            Assert.Equal(contentCamera.Viewport.Y, sceneListRoot.Position.Y + borderInset);
        }

        /// <summary>
        /// Ensures the lower-left controls stay within the dialog bounds even when many scenes are available.
        /// </summary>
        [Fact]
        public void Show_WhenManyScenesAreAvailable_KeepsCopySettingsButtonInsideDialogBounds() {
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

            EditorEntity copySettingsButtonHost = GetPrivateField<EditorEntity>(dialog, "CopySettingsButtonHost");
            ButtonComponent copySettingsButton = GetPrivateField<ButtonComponent>(dialog, "CopySettingsButton");
            EditorEntity outputFieldHost = GetPrivateField<EditorEntity>(dialog, "OutputFieldHost");
            TextBoxComponent outputDirectoryField = GetPrivateField<TextBoxComponent>(dialog, "OutputDirectoryField");
            EditorEntity addToBuildButtonHost = GetPrivateField<EditorEntity>(dialog, "AddToBuildButtonHost");

            Assert.InRange(copySettingsButtonHost.LocalPosition.Y, 0f, BuildDialog.PanelHeight - BuildDialog.HeaderHeight - BuildDialog.PanelPadding);
            Assert.True(copySettingsButtonHost.LocalPosition.Y + copySettingsButton.Size.Y <= BuildDialog.PanelHeight - BuildDialog.HeaderHeight);
            Assert.Equal("Copy settings from...", GetPrivateField<string>(copySettingsButton, "text"));
            Assert.Equal(BuildDialog.FooterButtonHeight, copySettingsButton.Size.Y);
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
            Assert.True(addToBuildButtonHost.LocalPosition.X < buildQueueButtonHost.LocalPosition.X);
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
        /// Creates the modal camera used to evaluate pointer input against dialog-owned controls.
        /// </summary>
        /// <param name="width">Viewport width in pixels.</param>
        /// <param name="height">Viewport height in pixels.</param>
        void CreateModalCamera(int width, int height) {
            EditorEntity cameraEntity = new EditorEntity {
                InternalEntity = true,
                LayerMask = 0b1000000000000000
            };

            CameraComponent camera = new CameraComponent {
                LayerMask = 0b1000000000000000,
                CameraDrawOrder = 255,
                Viewport = new float4(0f, 0f, width, height)
            };
            cameraEntity.AddComponent(camera);
        }

        /// <summary>
        /// Advances the input system by one frame using the supplied mouse state.
        /// </summary>
        /// <param name="mouseState">Mouse state to expose for the next frame.</param>
        void AdvanceInput(MouseState mouseState) {
            Input.SetMouseState(mouseState);
            Input.EarlyUpdate();
            Input.Update();
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
        /// Finds the first component of the requested type anywhere in the supplied entity hierarchy.
        /// </summary>
        /// <typeparam name="T">Component type to locate.</typeparam>
        /// <param name="entity">Root entity to inspect.</param>
        /// <returns>Matching component instance.</returns>
        T FindComponent<T>(Entity entity) where T : Component {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            List<Entity> pendingEntities = new List<Entity> {
                entity
            };

            for (int entityIndex = 0; entityIndex < pendingEntities.Count; entityIndex++) {
                Entity currentEntity = pendingEntities[entityIndex];
                if (currentEntity.Components != null) {
                    for (int componentIndex = 0; componentIndex < currentEntity.Components.Count; componentIndex++) {
                        if (currentEntity.Components[componentIndex] is T component) {
                            return component;
                        }
                    }
                }

                if (currentEntity.Children != null) {
                    for (int childIndex = 0; childIndex < currentEntity.Children.Count; childIndex++) {
                        pendingEntities.Add(currentEntity.Children[childIndex]);
                    }
                }
            }

            throw new InvalidOperationException("Expected to find the requested component in the entity hierarchy.");
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
        /// Invokes one non-public instance method using the supplied argument list.
        /// </summary>
        /// <param name="target">Object that owns the method.</param>
        /// <param name="methodName">Exact private method name.</param>
        /// <param name="arguments">Arguments passed to the method.</param>
        void InvokePrivate(object target, string methodName, params object[] arguments) {
            MethodInfo method = FindPrivateMethod(target.GetType(), methodName);
            method.Invoke(target, arguments);
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
