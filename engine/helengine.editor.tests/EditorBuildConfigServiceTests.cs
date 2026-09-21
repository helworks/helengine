using System.Text.Json;
using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies persistence and default seeding behavior for `user_settings/build_config.json`.
/// </summary>
public sealed class EditorBuildConfigServiceTests : IDisposable {
    /// <summary>
    /// Gets the isolated temporary project root used by the current test instance.
    /// </summary>
    string TempProjectRootPath { get; }

    /// <summary>
    /// Initializes one isolated temporary project root for the current test instance.
    /// </summary>
    public EditorBuildConfigServiceTests() {
        TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-build-config-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempProjectRootPath);
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
    /// Ensures missing local build settings are seeded for each supported platform and persisted to disk.
    /// </summary>
    [Fact]
    public void Load_WhenBuildConfigFileIsMissing_SeedsSupportedPlatformsWithCurrentSceneAndCreatesFile() {
        EditorBuildConfigService service = CreateService();

        EditorBuildConfigDocument document = service.Load(["windows", "linux"], "Scenes/City.helen");

        Assert.Equal(2, document.Platforms.Count);
        AssertPlatform(document.Platforms[0], "windows", ["Scenes/City.helen"], string.Empty);
        AssertPlatform(document.Platforms[1], "linux", ["Scenes/City.helen"], string.Empty);
        Assert.Empty(document.QueueItems);
        Assert.True(File.Exists(Path.Combine(TempProjectRootPath, "user_settings", "build_config.json")));
        Assert.True(File.Exists(Path.Combine(TempProjectRootPath, "settings", "build_config.json")));
    }

    /// <summary>
    /// Ensures a platform that follows the project writes its scenes to the project file and none to the local file,
    /// while output folder and queue stay local.
    /// </summary>
    [Fact]
    public void Save_WhenPlatformFollowsProject_WritesScenesToProjectFileAndKeepsLocalStateLocal() {
        EditorBuildConfigService service = CreateService();
        service.Save(new EditorBuildConfigDocument {
            Platforms = [
                new EditorBuildPlatformConfigDocument {
                    PlatformId = "windows",
                    SelectedSceneIds = ["Scenes/City.helen", "Scenes/Menu.helen"],
                    SceneOrders = [
                        new EditorBuildSceneOrderDocument { SceneId = "Scenes/Menu.helen", OrderNumber = 1 },
                        new EditorBuildSceneOrderDocument { SceneId = "Scenes/City.helen", OrderNumber = 2 }
                    ],
                    OutputDirectoryPath = @"C:\builds\windows"
                }
            ],
            QueueItems = [
                new EditorBuildQueueItemDocument { QueueItemId = "queue-1", PlatformId = "windows", SelectedSceneIds = ["Scenes/City.helen"], OutputDirectoryPath = @"C:\builds\windows" }
            ]
        });

        using JsonDocument projectJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(TempProjectRootPath, "settings", "build_config.json")));
        using JsonDocument localJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(TempProjectRootPath, "user_settings", "build_config.json")));
        JsonElement projectPlatform = Assert.Single(projectJson.RootElement.GetProperty("platforms").EnumerateArray());
        JsonElement localPlatform = Assert.Single(localJson.RootElement.GetProperty("platforms").EnumerateArray());

        Assert.Equal("windows", projectPlatform.GetProperty("platformId").GetString());
        Assert.Equal(2, projectPlatform.GetProperty("selectedSceneReferences").GetArrayLength());
        Assert.Equal(2, projectPlatform.GetProperty("sceneOrders").GetArrayLength());
        Assert.False(projectPlatform.TryGetProperty("outputDirectoryPath", out _));
        Assert.False(projectJson.RootElement.TryGetProperty("queueItems", out _));
        Assert.Equal(0, localPlatform.GetProperty("selectedSceneReferences").GetArrayLength());
        Assert.Equal(0, localPlatform.GetProperty("sceneOrders").GetArrayLength());
        Assert.False(localPlatform.GetProperty("overridesProjectScenes").GetBoolean());
        Assert.Equal(@"C:\builds\windows", localPlatform.GetProperty("outputDirectoryPath").GetString());
        Assert.Single(localJson.RootElement.GetProperty("queueItems").EnumerateArray());
    }

    /// <summary>
    /// Ensures a platform with a local scene override keeps its scenes in the local file and leaves the project
    /// package untouched, and that both views survive a reload.
    /// </summary>
    [Fact]
    public void Save_WhenPlatformOverridesProjectScenes_KeepsLocalScenesAndProjectPackageApart() {
        EditorBuildConfigService service = CreateService();
        service.Save(new EditorBuildConfigDocument {
            Platforms = [
                new EditorBuildPlatformConfigDocument { PlatformId = "windows", SelectedSceneIds = ["Scenes/City.helen", "Scenes/Menu.helen"] }
            ]
        });

        EditorBuildConfigDocument document = service.TryLoadExisting();
        document.Platforms[0].OverridesProjectScenes = true;
        document.Platforms[0].SelectedSceneIds = ["Scenes/Menu.helen"];
        service.Save(document);

        EditorBuildConfigDocument reloaded = service.TryLoadExisting();
        EditorProjectBuildConfigDocument project = service.TryLoadProjectBuildConfig();
        EditorBuildPlatformConfigDocument platform = Assert.Single(reloaded.Platforms);
        Assert.True(platform.OverridesProjectScenes);
        Assert.Equal(["Scenes/Menu.helen"], platform.SelectedSceneIds);
        Assert.Equal(["Scenes/City.helen", "Scenes/Menu.helen"], Assert.Single(project.Platforms).SelectedSceneIds);
    }

    /// <summary>
    /// Ensures a machine that has only the project file composes every platform with the project scenes and an
    /// empty output folder instead of reporting missing build settings.
    /// </summary>
    [Fact]
    public void TryLoadExisting_WhenOnlyProjectFileExists_ComposesPlatformsFromTheProjectPackage() {
        EditorBuildConfigService service = CreateService();
        service.Save(new EditorBuildConfigDocument {
            Platforms = [
                new EditorBuildPlatformConfigDocument { PlatformId = "ps2", SelectedSceneIds = ["Scenes/City.helen"], OutputDirectoryPath = @"C:\builds\ps2" }
            ]
        });
        File.Delete(Path.Combine(TempProjectRootPath, "user_settings", "build_config.json"));

        EditorBuildConfigDocument document = service.TryLoadExisting();

        Assert.NotNull(document);
        AssertPlatform(Assert.Single(document.Platforms), "ps2", ["Scenes/City.helen"], string.Empty);
        Assert.Empty(document.QueueItems);
    }

    /// <summary>
    /// Ensures a following platform whose local file still lists scenes takes them from the project package only.
    /// </summary>
    [Fact]
    public void TryLoadExisting_WhenFollowingPlatformHasLocalScenes_UsesTheProjectPackage() {
        EditorBuildConfigService service = CreateService();
        service.Save(new EditorBuildConfigDocument {
            Platforms = [
                new EditorBuildPlatformConfigDocument { PlatformId = "windows", SelectedSceneIds = ["Scenes/City.helen"] }
            ]
        });
        EditorBuildConfigDocument overriding = service.TryLoadExisting();
        overriding.Platforms[0].OverridesProjectScenes = true;
        overriding.Platforms[0].SelectedSceneIds = ["Scenes/Menu.helen"];
        service.Save(overriding);
        string localPath = Path.Combine(TempProjectRootPath, "user_settings", "build_config.json");
        File.WriteAllText(localPath, File.ReadAllText(localPath).Replace("\"overridesProjectScenes\": true", "\"overridesProjectScenes\": false"));

        EditorBuildConfigDocument document = service.TryLoadExisting();

        Assert.Equal(["Scenes/City.helen"], Assert.Single(document.Platforms).SelectedSceneIds);
    }

    /// <summary>
    /// Ensures dropping a local override reloads the project scenes into the composed platform entry.
    /// </summary>
    [Fact]
    public void ResetPlatformScenesToProject_ReplacesTheLocalOverrideWithTheProjectPackage() {
        EditorBuildConfigService service = CreateService();
        service.Save(new EditorBuildConfigDocument {
            Platforms = [
                new EditorBuildPlatformConfigDocument {
                    PlatformId = "windows",
                    SelectedSceneIds = ["Scenes/City.helen", "Scenes/Menu.helen"],
                    SceneOrders = [new EditorBuildSceneOrderDocument { SceneId = "Scenes/Menu.helen", OrderNumber = 1 }]
                }
            ]
        });
        EditorBuildConfigDocument document = service.TryLoadExisting();
        document.Platforms[0].OverridesProjectScenes = true;
        document.Platforms[0].SelectedSceneIds = ["Scenes/Menu.helen"];
        document.Platforms[0].SceneOrders = [];

        service.ResetPlatformScenesToProject(document, "windows");

        Assert.False(document.Platforms[0].OverridesProjectScenes);
        Assert.Equal(["Scenes/City.helen", "Scenes/Menu.helen"], document.Platforms[0].SelectedSceneIds);
        EditorBuildSceneOrderDocument order = Assert.Single(document.Platforms[0].SceneOrders);
        Assert.Equal("Scenes/Menu.helen", order.SceneId);
        Assert.Equal(1, order.OrderNumber);
    }

    /// <summary>
    /// Ensures a project file that cannot be parsed is left alone by save instead of being replaced.
    /// </summary>
    [Fact]
    public void Save_WhenProjectFileIsMalformed_LeavesItUntouched() {
        EditorBuildConfigService service = CreateService();
        string projectPath = Path.Combine(TempProjectRootPath, "settings", "build_config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath));
        File.WriteAllText(projectPath, "{ not json");

        service.Save(new EditorBuildConfigDocument {
            Platforms = [new EditorBuildPlatformConfigDocument { PlatformId = "windows", SelectedSceneIds = ["Scenes/City.helen"] }]
        });

        Assert.Equal("{ not json", File.ReadAllText(projectPath));
        Assert.True(File.Exists(Path.Combine(TempProjectRootPath, "user_settings", "build_config.json")));
    }

    /// <summary>
    /// Ensures saved platform selections, output folders, and queued build statuses survive reload.
    /// </summary>
    [Fact]
    public void Load_WhenBuildConfigFileExists_PreservesPlatformsQueueAndStatuses() {
        EditorBuildConfigService service = CreateService();
        EditorBuildConfigDocument savedDocument = new EditorBuildConfigDocument {
            Platforms = [
                new EditorBuildPlatformConfigDocument {
                    PlatformId = "windows",
                    SelectedSceneIds = ["Scenes/City.helen", "Scenes/Menu.helen"],
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
                }
            ],
            QueueItems = [
                new EditorBuildQueueItemDocument {
                    QueueItemId = "queue-1",
                    PlatformId = "windows",
                    SelectedSceneIds = ["Scenes/City.helen"],
                    OutputDirectoryPath = @"C:\builds\windows",
                    Status = EditorBuildQueueItemStatus.Pending,
                    StatusMessage = string.Empty
                },
                new EditorBuildQueueItemDocument {
                    QueueItemId = "queue-2",
                    PlatformId = "linux",
                    SelectedSceneIds = ["Scenes/Menu.helen"],
                    OutputDirectoryPath = "/tmp/linux-build",
                    Status = EditorBuildQueueItemStatus.Failed,
                    StatusMessage = "Unsupported scene format."
                }
            ]
        };
        service.Save(savedDocument);

        EditorBuildConfigDocument loadedDocument = service.Load(["windows"], "Scenes/Other.helen");

        Assert.Single(loadedDocument.Platforms);
        AssertPlatform(loadedDocument.Platforms[0], "windows", ["Scenes/City.helen", "Scenes/Menu.helen"], @"C:\builds\windows");
        Assert.Equal(2, loadedDocument.Platforms[0].SceneOrders.Count);
        Assert.Equal("Scenes/City.helen", loadedDocument.Platforms[0].SceneOrders[0].SceneId);
        Assert.Equal(2, loadedDocument.Platforms[0].SceneOrders[0].OrderNumber);
        Assert.Equal("Scenes/Menu.helen", loadedDocument.Platforms[0].SceneOrders[1].SceneId);
        Assert.Equal(1, loadedDocument.Platforms[0].SceneOrders[1].OrderNumber);
        Assert.Equal(2, loadedDocument.QueueItems.Count);
        Assert.Equal(EditorBuildQueueItemStatus.Pending, loadedDocument.QueueItems[0].Status);
        Assert.Equal(EditorBuildQueueItemStatus.Failed, loadedDocument.QueueItems[1].Status);
        Assert.Equal("Unsupported scene format.", loadedDocument.QueueItems[1].StatusMessage);
    }

    /// <summary>
    /// Ensures the per-platform debug-build flag survives save and reload.
    /// </summary>
    [Fact]
    public void Load_WhenBuildConfigContainsDebugBuild_PreservesItAfterReload() {
        EditorBuildConfigService service = CreateService();
        EditorBuildConfigDocument savedDocument = new EditorBuildConfigDocument {
            Platforms = [
                new EditorBuildPlatformConfigDocument {
                    PlatformId = "windows",
                    SelectedSceneIds = ["Scenes/City.helen"],
                    SceneOrders = [],
                    OutputDirectoryPath = @"C:\builds\windows",
                    DebugBuild = true
                }
            ]
        };
        service.Save(savedDocument);

        EditorBuildConfigDocument loadedDocument = service.Load(["windows"], "Scenes/Other.helen");

        Assert.True(loadedDocument.Platforms[0].DebugBuild);
    }

    /// <summary>
    /// Ensures older build-config files without a debug-build field load as release builds.
    /// </summary>
    [Fact]
    public void Load_WhenBuildConfigOmitsDebugBuild_DefaultsItToFalse() {
        EditorBuildConfigService service = CreateService();
        WriteBuildConfigFile(
            """
            {
              "platforms": [
                {
                  "platformId": "windows",
                  "selectedSceneIds": [
                    "Scenes/City.helen"
                  ],
                  "sceneOrders": [],
                  "outputDirectoryPath": "C:\\builds\\windows"
                }
              ],
              "queueItems": []
            }
            """);

        EditorBuildConfigDocument loadedDocument = service.Load(["windows"], "Scenes/Other.helen");

        Assert.False(loadedDocument.Platforms[0].DebugBuild);
    }

    /// <summary>
    /// Ensures invalid path-only selections are discarded while newly enabled platforms receive current-format defaults.
    /// </summary>
    [Fact]
    public void Load_WhenPathOnlySelectionExists_DiscardsItAndSeedsNewPlatform() {
        EditorBuildConfigService service = CreateService();
        WriteBuildConfigFile(
            """
            {
              "platforms": [
                {
                  "platformId": "windows",
                  "selectedSceneIds": [
                    "Scenes/City.helen"
                  ],
                  "outputDirectoryPath": "C:\\builds\\windows"
                }
              ],
              "queueItems": []
            }
            """);

        EditorBuildConfigDocument document = service.Load(["windows", "linux"], "Scenes/Menu.helen");

        Assert.Equal(2, document.Platforms.Count);
        AssertPlatform(document.Platforms[0], "windows", [], @"C:\builds\windows");
        AssertPlatform(document.Platforms[1], "linux", ["Scenes/Menu.helen"], string.Empty);
    }

    /// <summary>
    /// Ensures legacy code-module selections are ignored on load and removed when the document is saved again.
    /// </summary>
    [Fact]
    public void Save_WithLegacySelectedCodeModuleIds_RewritesConfigWithoutSelectedCodeModuleIds() {
        EditorBuildConfigService service = CreateService();
        WriteBuildConfigFile(
            """
            {
              "platforms": [
                {
                  "platformId": "windows",
                  "selectedSceneIds": [
                    "Scenes/City.helen"
                  ],
                  "selectedCodeModuleIds": [
                    "gameplay"
                  ]
                }
              ],
              "queueItems": [
                {
                  "queueItemId": "queue1",
                  "platformId": "windows",
                  "selectedSceneIds": [
                    "Scenes/City.helen"
                  ],
                  "selectedCodeModuleIds": [
                    "gameplay"
                  ]
                }
              ]
            }
            """);

        EditorBuildConfigDocument document = service.Load(["windows"], "Scenes/City.helen");
        service.Save(document);

        string rewrittenJson = File.ReadAllText(Path.Combine(TempProjectRootPath, "user_settings", "build_config.json"));
        Assert.DoesNotContain("selectedCodeModuleIds", rewrittenJson, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures current Nintendo DS local build settings preserve the authored build-profile selection when reloaded.
    /// </summary>
    [Fact]
    public void TryLoadExisting_WhenNintendoDsPlatformUsesAuthoredBuildProfileId_PreservesTheSelection() {
        EditorBuildConfigService service = CreateService();
        WriteBuildConfigFile(
            """
            {
              "platforms": [
                {
                  "platformId": "ds",
                  "selectedSceneIds": [],
                  "sceneOrders": [],
                  "outputDirectoryPath": "",
                  "debugBuild": true,
                  "selectedBuildProfileId": "ds-default",
                  "selectedGraphicsProfileId": "",
                  "selectedBuildOptionValues": {},
                  "selectedGraphicsOptionValues": {},
                  "selectedCodegenProfileId": "",
                  "selectedStorageProfileId": "",
                  "selectedMediaProfileId": "",
                  "selectedCodegenOptionValues": {}
                }
              ],
              "queueItems": [
                {
                  "queueItemId": "queuedsdebug",
                  "platformId": "ds",
                  "selectedSceneIds": [],
                  "outputDirectoryPath": "",
                  "status": 0,
                  "statusMessage": "",
                  "debugBuild": true,
                  "executionMode": 0,
                  "selectedBuildProfileId": "ds-default",
                  "selectedGraphicsProfileId": "",
                  "selectedBuildOptionValues": {},
                  "selectedGraphicsOptionValues": {},
                  "selectedCodegenProfileId": "",
                  "selectedStorageProfileId": "",
                  "selectedMediaProfileId": "",
                  "selectedCodegenOptionValues": {}
                }
              ]
            }
            """);

        EditorBuildConfigDocument document = service.TryLoadExisting();

        EditorBuildPlatformConfigDocument platform = Assert.Single(document.Platforms);
        EditorBuildQueueItemDocument queueItem = Assert.Single(document.QueueItems);
        Assert.Equal("ds-default", platform.SelectedBuildProfileId);
        Assert.Equal("ds-default", queueItem.SelectedBuildProfileId);
        string json = File.ReadAllText(Path.Combine(TempProjectRootPath, "user_settings", "build_config.json"));
        Assert.Contains("\"selectedBuildProfileId\": \"ds-default\"", json);
    }

    /// <summary>
    /// Ensures saving Nintendo DS local build settings preserves the authored profile identifier.
    /// </summary>
    [Fact]
    public void Save_WhenNintendoDsLocalBuildSettingsUseAuthoredBuildProfileId_PreservesTheSelection() {
        EditorBuildConfigService service = CreateService();
        EditorBuildConfigDocument document = new EditorBuildConfigDocument {
            Platforms = [
                new EditorBuildPlatformConfigDocument {
                    PlatformId = "ds",
                    DebugBuild = false,
                    SelectedBuildProfileId = "ds-default"
                }
            ],
            QueueItems = [
                new EditorBuildQueueItemDocument {
                    QueueItemId = "queuedsrelease",
                    PlatformId = "ds",
                    DebugBuild = false,
                    SelectedBuildProfileId = "ds-default"
                }
            ]
        };

        service.Save(document);

        string json = File.ReadAllText(Path.Combine(TempProjectRootPath, "user_settings", "build_config.json"));
        Assert.Contains("\"selectedBuildProfileId\": \"ds-default\"", json);
    }

    /// <summary>
    /// Creates the service under test for the current temporary project root.
    /// </summary>
    /// <returns>Build-config service configured for the current test project.</returns>
    EditorBuildConfigService CreateService() {
        return new EditorBuildConfigService(TempProjectRootPath);
    }

    /// <summary>
    /// Writes raw JSON to the expected local build-config path.
    /// </summary>
    /// <param name="json">Serialized build-config payload to persist.</param>
    void WriteBuildConfigFile(string json) {
        string settingsDirectoryPath = Path.Combine(TempProjectRootPath, "user_settings");
        Directory.CreateDirectory(settingsDirectoryPath);
        File.WriteAllText(Path.Combine(settingsDirectoryPath, "build_config.json"), json);
    }

    /// <summary>
    /// Asserts one platform configuration matches the expected values.
    /// </summary>
    /// <param name="platform">Platform configuration to verify.</param>
    /// <param name="platformId">Expected platform identifier.</param>
    /// <param name="sceneIds">Expected scene identifiers.</param>
    /// <param name="outputDirectoryPath">Expected output directory path.</param>
    void AssertPlatform(EditorBuildPlatformConfigDocument platform, string platformId, IReadOnlyList<string> sceneIds, string outputDirectoryPath) {
        Assert.Equal(platformId, platform.PlatformId);
        Assert.Equal(sceneIds, platform.SelectedSceneIds);
        Assert.Equal(outputDirectoryPath, platform.OutputDirectoryPath);
    }
}
