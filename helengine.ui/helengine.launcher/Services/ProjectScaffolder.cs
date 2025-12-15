using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using helengine.editor.launcher.Models;

namespace helengine.editor.launcher.Services;

public sealed record ProjectCreateResult(bool Success, string Message, string ProjectPath);

public sealed class ProjectScaffolder {
    readonly string _launcherSettingsFilePath;

    public ProjectScaffolder() {
        _launcherSettingsFilePath = ResolveLauncherSettingsPath();
    }

    public async Task<ProjectCreateResult> CreateAsync(string baseLocation, string projectName, EngineInstall engine) {
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

            Directory.CreateDirectory(assetsFolder);
            Directory.CreateDirectory(cacheFolder);
            Directory.CreateDirectory(settingsFolder);

            var utcNow = DateTime.UtcNow;

            var projectFile = new ProjectTemplate {
                Name = projectName,
                Created = utcNow,
                LastOpened = utcNow,
                Description = "created via helengine launcher",
                Version = "1.0.0"
            };

            var settingsFile = new ProjectSettingsTemplate {
                Name = projectName,
                Version = "1.0.0",
                Created = utcNow,
                EngineVersion = engine.Version
            };

            string projectFilePath = Path.Combine(projectPath, "project.heproj");
            string settingsFilePath = Path.Combine(settingsFolder, "project.json");

            await File.WriteAllTextAsync(projectFilePath, SerializeProject(projectFile));
            await File.WriteAllTextAsync(settingsFilePath, SerializeSettings(settingsFile));
            LogProjectPath(projectPath);

            return new ProjectCreateResult(true, $"Created project at {projectPath}", projectPath);
        } catch (Exception ex) {
            return Failure($"Failed to create project: {ex.Message}");
        }
    }

    static readonly JsonSerializerOptions ProjectFileOptions = new() {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    static readonly JsonSerializerOptions SettingsFileOptions = new() {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    static string SerializeProject(ProjectTemplate value) => JsonFormatting.SerializeWithIndent(value, ProjectFileOptions);

    static string SerializeSettings(ProjectSettingsTemplate value) => JsonFormatting.SerializeWithIndent(value, SettingsFileOptions);

    void LogProjectPath(string projectPath) {
        try {
            var settings = LoadLauncherSettings();
            settings.LastProjectPath = projectPath;
            var json = JsonFormatting.SerializeWithIndent(settings, ProjectFileOptions);
            File.WriteAllText(_launcherSettingsFilePath, json);
        } catch {
        }
    }

    LauncherSettingsTemplate LoadLauncherSettings() {
        try {
            if (File.Exists(_launcherSettingsFilePath)) {
                var json = File.ReadAllText(_launcherSettingsFilePath);
                var existing = JsonSerializer.Deserialize<LauncherSettingsTemplate>(json, ProjectFileOptions);
                if (existing != null) {
                    return existing;
                }
            }
        } catch {
        }

        return new LauncherSettingsTemplate();
    }

    static string ResolveLauncherSettingsPath() {
        try {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string root = string.IsNullOrWhiteSpace(appData) ? Environment.CurrentDirectory : Path.Combine(appData, "helengine");
            string settingsFolder = Path.Combine(root, "settings");
            Directory.CreateDirectory(settingsFolder);
            return Path.Combine(settingsFolder, "launcher.json");
        } catch {
            return Path.Combine(Environment.CurrentDirectory, "launcher.json");
        }
    }

    static string SanitizeName(string name) {
        var invalid = Path.GetInvalidFileNameChars();
        var safeChars = name.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray();
        return new string(safeChars).Trim();
    }

    static ProjectCreateResult Failure(string message) => new(false, message, string.Empty);

    sealed class ProjectTemplate {
        public string Name { get; set; } = string.Empty;
        public DateTime LastOpened { get; set; }
        public DateTime Created { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
    }

    sealed class ProjectSettingsTemplate {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public DateTime Created { get; set; }
        public string EngineVersion { get; set; } = string.Empty;
    }

    sealed class LauncherSettingsTemplate {
        public string? LastProjectPath { get; set; }
    }
}
