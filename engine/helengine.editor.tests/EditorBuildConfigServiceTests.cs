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
        Assert.Equal(2, loadedDocument.QueueItems.Count);
        Assert.Equal(EditorBuildQueueItemStatus.Pending, loadedDocument.QueueItems[0].Status);
        Assert.Equal(EditorBuildQueueItemStatus.Failed, loadedDocument.QueueItems[1].Status);
        Assert.Equal("Unsupported scene format.", loadedDocument.QueueItems[1].StatusMessage);
    }

    /// <summary>
    /// Ensures newly enabled platforms are added with one current-scene default without overwriting existing selections.
    /// </summary>
    [Fact]
    public void Load_WhenSupportedPlatformIsMissingFromBuildConfig_AddsItWithCurrentSceneAndKeepsExistingPlatforms() {
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
        AssertPlatform(document.Platforms[0], "windows", ["Scenes/City.helen"], @"C:\builds\windows");
        AssertPlatform(document.Platforms[1], "linux", ["Scenes/Menu.helen"], string.Empty);
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
