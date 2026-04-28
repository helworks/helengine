using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using helengine.editor.launcher.Models;
using helengine.projectfile;

namespace helengine.editor.launcher.Services;

/// <summary>
/// Creates new launcher projects by writing the canonical shared `.heproj` file and local project settings.
/// </summary>
public sealed class ProjectScaffolder {
    /// <summary>
    /// Gets the JSON formatting used for launcher-owned local settings documents.
    /// </summary>
    static JsonSerializerOptions JsonSerializerOptions { get; } = new() {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Gets the shared writer used to persist canonical `.heproj` documents.
    /// </summary>
    ProjectFileWriter ProjectFileWriter { get; }

    /// <summary>
    /// Gets the launcher settings file path used to remember the last created project file.
    /// </summary>
    string LauncherSettingsFilePath { get; }

    /// <summary>
    /// Creates the scaffolder with the default shared project writer and launcher settings location.
    /// </summary>
    public ProjectScaffolder() {
        ProjectFileWriter = new ProjectFileWriter();
        LauncherSettingsFilePath = ResolveLauncherSettingsPath();
    }

    /// <summary>
    /// Creates one new project under the supplied base location using the selected engine version.
    /// </summary>
    /// <param name="baseLocation">Parent directory that will receive the new project folder.</param>
    /// <param name="projectName">Requested display and folder name for the new project.</param>
    /// <param name="engine">Selected engine installation that defines the required engine version.</param>
    /// <returns>Result describing whether project creation succeeded and where the project root was created.</returns>
    public async Task<ProjectCreateResult> CreateAsync(string baseLocation, string projectName, EngineInstall engine) {
        ArgumentNullException.ThrowIfNull(engine);

        if (string.IsNullOrWhiteSpace(baseLocation)) {
            return Failure("Choose a project location first.");
        }

        if (string.IsNullOrWhiteSpace(projectName)) {
            return Failure("Enter a project name first.");
        }

        string sanitizedName = SanitizeName(projectName);
        if (string.IsNullOrWhiteSpace(sanitizedName)) {
            return Failure("Project name does not contain any valid characters.");
        }

        try {
            string rootLocation = Path.GetFullPath(baseLocation);
            Directory.CreateDirectory(rootLocation);

            string projectPath = Path.Combine(rootLocation, sanitizedName);
            if (Directory.Exists(projectPath) && Directory.GetFileSystemEntries(projectPath).Length > 0) {
                return Failure("Project directory already exists and is not empty.");
            }

            Directory.CreateDirectory(projectPath);

            string assetsFolder = Path.Combine(projectPath, "assets");
            string cacheFolder = Path.Combine(projectPath, "cache");
            string settingsFolder = Path.Combine(projectPath, "settings");
            string projectFilePath = Path.Combine(projectPath, "project.heproj");
            string settingsFilePath = Path.Combine(settingsFolder, "project.json");

            Directory.CreateDirectory(assetsFolder);
            Directory.CreateDirectory(cacheFolder);
            Directory.CreateDirectory(settingsFolder);

            DateTime utcNow = DateTime.UtcNow;
            string activePlatform = ResolveCurrentPlatformId();

            ProjectFileDocument projectFileDocument = new ProjectFileDocument {
                Name = projectName,
                Version = "1.0.0",
                RequiredEngineVersion = engine.Version,
                SupportedPlatforms = [activePlatform],
                Created = utcNow,
                LastOpened = utcNow,
                Description = "created via helengine launcher"
            };

            ProjectLocalSettingsDocument projectLocalSettingsDocument = new ProjectLocalSettingsDocument {
                ActivePlatform = activePlatform
            };

            await ProjectFileWriter.WriteAsync(projectFilePath, projectFileDocument);
            await File.WriteAllTextAsync(settingsFilePath, SerializeProjectLocalSettings(projectLocalSettingsDocument));
            LogProjectFilePath(projectFilePath);

            return new ProjectCreateResult(true, $"Created project at {projectPath}", projectPath);
        } catch (Exception exception) {
            return Failure($"Failed to create project: {exception.Message}");
        }
    }

    /// <summary>
    /// Serializes one local project-settings document using the launcher JSON formatting rules.
    /// </summary>
    /// <param name="value">Local settings document to serialize.</param>
    /// <returns>Formatted JSON payload.</returns>
    static string SerializeProjectLocalSettings(ProjectLocalSettingsDocument value) {
        return JsonFormatting.SerializeWithIndent(value, JsonSerializerOptions);
    }

    /// <summary>
    /// Serializes one launcher-settings document using the launcher JSON formatting rules.
    /// </summary>
    /// <param name="value">Launcher settings document to serialize.</param>
    /// <returns>Formatted JSON payload.</returns>
    static string SerializeLauncherSettings(LauncherSettingsDocument value) {
        return JsonFormatting.SerializeWithIndent(value, JsonSerializerOptions);
    }

    /// <summary>
    /// Stores the last created project file path inside launcher-local settings.
    /// </summary>
    /// <param name="projectFilePath">Canonical project file path to remember.</param>
    void LogProjectFilePath(string projectFilePath) {
        try {
            LauncherSettingsDocument launcherSettings = LoadLauncherSettings();
            launcherSettings.LastProjectPath = projectFilePath;
            string json = SerializeLauncherSettings(launcherSettings);
            File.WriteAllText(LauncherSettingsFilePath, json);
        } catch {
        }
    }

    /// <summary>
    /// Loads launcher-local settings from disk when available.
    /// </summary>
    /// <returns>Launcher settings document or a new empty document when no saved state exists.</returns>
    LauncherSettingsDocument LoadLauncherSettings() {
        try {
            if (File.Exists(LauncherSettingsFilePath)) {
                string json = File.ReadAllText(LauncherSettingsFilePath);
                LauncherSettingsDocument? existingSettings = JsonSerializer.Deserialize<LauncherSettingsDocument>(json, JsonSerializerOptions);
                if (existingSettings != null) {
                    return existingSettings;
                }
            }
        } catch {
        }

        return new LauncherSettingsDocument();
    }

    /// <summary>
    /// Resolves the launcher-local settings file path under the user application-data folder.
    /// </summary>
    /// <returns>Absolute launcher settings file path.</returns>
    static string ResolveLauncherSettingsPath() {
        try {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string root = string.IsNullOrWhiteSpace(appData) ? Environment.CurrentDirectory : Path.Combine(appData, "helengine");
            string settingsFolder = Path.Combine(root, "settings");
            Directory.CreateDirectory(settingsFolder);
            return Path.Combine(settingsFolder, "launcher.json");
        } catch {
            return Path.Combine(Environment.CurrentDirectory, "launcher.json");
        }
    }

    /// <summary>
    /// Replaces invalid file-name characters so the requested project name can be used as a folder name.
    /// </summary>
    /// <param name="name">Requested project name.</param>
    /// <returns>Sanitized project folder name.</returns>
    static string SanitizeName(string name) {
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        char[] safeCharacters = name.Select(character => invalidCharacters.Contains(character) ? '-' : character).ToArray();
        return new string(safeCharacters).Trim();
    }

    /// <summary>
    /// Resolves the canonical launcher platform identifier for the current operating system.
    /// </summary>
    /// <returns>Canonical supported-platform identifier.</returns>
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

    /// <summary>
    /// Creates a failure result with no project path.
    /// </summary>
    /// <param name="message">Failure message to expose to the launcher UI.</param>
    /// <returns>Failure result.</returns>
    static ProjectCreateResult Failure(string message) {
        return new ProjectCreateResult(false, message, string.Empty);
    }
}
