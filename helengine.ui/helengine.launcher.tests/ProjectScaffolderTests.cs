using System.Text.Json;
using helengine.editor.launcher.Models;
using helengine.editor.launcher.Services;
using helengine.projectfile;
using Xunit;

namespace helengine.editor.launcher.tests;

/// <summary>
/// Verifies launcher project creation writes the canonical shared `.heproj` contract and keeps local settings separate.
/// </summary>
public sealed class ProjectScaffolderTests : IDisposable {
    readonly string TempDirectoryPath;

    /// <summary>
    /// Creates one isolated temporary directory used by the current test instance.
    /// </summary>
    public ProjectScaffolderTests() {
        TempDirectoryPath = Path.Combine(Path.GetTempPath(), "helengine-launcher-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempDirectoryPath);
    }

    /// <summary>
    /// Deletes the isolated temporary directory after the current test completes.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(TempDirectoryPath)) {
            Directory.Delete(TempDirectoryPath, true);
        }
    }

    /// <summary>
    /// Ensures new projects write required engine and supported-platform metadata into the canonical `.heproj` file.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WritesCanonicalProjectFileMetadata() {
        ProjectScaffolder scaffolder = new ProjectScaffolder();
        EngineInstall engine = new EngineInstall {
            Name = "Test Engine",
            Version = "9.9.9",
            InstallPath = Path.Combine(TempDirectoryPath, "engine")
        };

        ProjectCreateResult result = await scaffolder.CreateAsync(TempDirectoryPath, "sample-project", engine);

        Assert.True(result.Success);

        string projectFilePath = Path.Combine(result.ProjectPath, "project.heproj");
        ProjectFileReadResult readResult = await new ProjectFileReader().ReadAsync(projectFilePath);

        Assert.True(readResult.Succeeded);
        Assert.Equal("sample-project", readResult.Document.Name);
        Assert.Equal("9.9.9", readResult.Document.RequiredEngineVersion);
        Assert.Equal(new[] { ResolveCurrentPlatformId() }, readResult.Document.SupportedPlatforms);
    }

    /// <summary>
    /// Ensures local project settings keep only local state and stop duplicating canonical engine-version metadata.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WritesLocalSettingsWithoutCanonicalEngineMetadata() {
        ProjectScaffolder scaffolder = new ProjectScaffolder();
        EngineInstall engine = new EngineInstall {
            Name = "Test Engine",
            Version = "9.9.9",
            InstallPath = Path.Combine(TempDirectoryPath, "engine")
        };

        ProjectCreateResult result = await scaffolder.CreateAsync(TempDirectoryPath, "sample-project", engine);

        Assert.True(result.Success);

        string settingsFilePath = Path.Combine(result.ProjectPath, "settings", "project.json");
        using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(settingsFilePath));
        JsonElement root = document.RootElement;

        Assert.True(root.TryGetProperty("activePlatform", out JsonElement activePlatformValue));
        Assert.Equal(ResolveCurrentPlatformId(), activePlatformValue.GetString());
        Assert.False(root.TryGetProperty("engineVersion", out _));
    }

    /// <summary>
    /// Resolves the canonical platform identifier used for the current launcher runtime.
    /// </summary>
    /// <returns>Canonical platform identifier for the current operating system.</returns>
    static string ResolveCurrentPlatformId() {
        if (OperatingSystem.IsWindows()) {
            return "windows";
        }

        if (OperatingSystem.IsLinux()) {
            return "linux";
        }

        if (OperatingSystem.IsMacOS()) {
            return "macos";
        }

        return "unknown";
    }
}
