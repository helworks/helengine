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

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Deletes the temporary content root after each test.
        /// </summary>
        public void Dispose() {
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
            List<TextComponent> mapLabelTexts = GetPrivateField<List<TextComponent>>(dialog, "MapLabelTexts");
            List<CheckBoxComponent> mapCheckBoxes = GetPrivateField<List<CheckBoxComponent>>(dialog, "MapCheckBoxes");
            string activePlatformId = GetPrivateField<string>(dialog, "ActivePlatformId");

            Assert.Equal(2, platformTabs.Count);
            Assert.Equal("linux", activePlatformId);
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
            TextBoxComponent outputDirectoryField = GetPrivateField<TextBoxComponent>(dialog, "OutputDirectoryField");
            mapCheckBoxes[1].IsChecked = true;
            outputDirectoryField.Text = @"D:\exports\windows";

            InvokePrivate(dialog, "HandleAddToBuildClicked");

            Assert.NotNull(raisedRequest);
            Assert.Equal("windows", raisedRequest.PlatformId);
            Assert.Equal(
                new[] {
                    "Scenes/City.helen",
                    "Scenes/Menu.helen"
                },
                raisedRequest.SelectedSceneIds);
            Assert.Equal(@"D:\exports\windows", raisedRequest.OutputDirectoryPath);
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
            EditorBuildPlatformConfigDocument linuxConfig = Assert.Single(buildConfig.Platforms.Where(platform => platform.PlatformId == "linux"));
            Assert.True(mapCheckBoxes[0].IsChecked);
            Assert.False(mapCheckBoxes[1].IsChecked);
            Assert.Equal(
                new[] {
                    "Scenes/City.helen"
                },
                linuxConfig.SelectedSceneIds);
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
        /// Reads one non-public instance field from the supplied object.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Exact private field name.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            object value = field.GetValue(target);
            return Assert.IsType<T>(value);
        }

        /// <summary>
        /// Invokes one non-public instance method on the supplied target object.
        /// </summary>
        /// <param name="target">Object that owns the method.</param>
        /// <param name="methodName">Exact private method name.</param>
        void InvokePrivate(object target, string methodName) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, []);
        }

        /// <summary>
        /// Invokes one non-public instance method that accepts a single string argument.
        /// </summary>
        /// <param name="target">Object that owns the method.</param>
        /// <param name="methodName">Exact private method name.</param>
        /// <param name="value">String argument passed to the method.</param>
        void InvokePrivate(object target, string methodName, string value) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, [
                value
            ]);
        }
    }
}
