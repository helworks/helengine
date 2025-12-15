using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using helengine.editor.launcher.Models;
using helengine.editor.launcher.Services;
using helengine.editor.launcher.Views.Pages;
using helengine.editor.launcher.Theme;

namespace helengine.editor.launcher.Views;

public sealed class LauncherShell : UserControl {
    readonly EngineInstallManager engineManager;
    readonly ProjectScaffolder projectScaffolder;
    readonly RecentProjectsService recentProjectsService;
    readonly HomeView homeView;
    readonly NewProjectView newProjectView;
    readonly EnginesView enginesView;
    readonly TextBlock statusText;
    readonly ContentControl pageHost;
    List<RecentProject> recentProjects = new();

    public LauncherShell() {
        engineManager = new EngineInstallManager();
        projectScaffolder = new ProjectScaffolder();
        recentProjectsService = new RecentProjectsService();
        homeView = new HomeView();
        newProjectView = new NewProjectView();
        enginesView = new EnginesView();

        statusText = new TextBlock {
            FontSize = 12,
            Foreground = LauncherTheme.TextSecondary
        };

        pageHost = new ContentControl();

        var root = BuildLayout();
        Content = root;

        WireEvents();
        newProjectView.SetProjectLocation(GetDefaultProjectsFolder());
        RefreshEngineBindings();
        _ = LoadRecentProjectsAsync();
        ShowHome();
    }

    Control BuildLayout() {
        var topGrid = new Grid {
            Background = LauncherTheme.TopBarBackground
        };

        var titleStack = new StackPanel {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center
        };
        titleStack.Children.Add(new TextBlock {
            Text = "helengine",
            FontSize = 22,
            FontWeight = FontWeight.SemiBold,
            Foreground = LauncherTheme.AccentLilac
        });
        titleStack.Children.Add(new TextBlock {
            Text = "launcher",
            FontSize = 14,
            Foreground = LauncherTheme.TextSecondary,
            VerticalAlignment = VerticalAlignment.Center
        });
        topGrid.Children.Add(titleStack);

        var topBar = new Border {
            Padding = new Thickness(16),
            Background = LauncherTheme.TopBarBackground,
            Child = topGrid
        };

        var bottomBar = new Border {
            Background = LauncherTheme.PanelBackground,
            Padding = new Thickness(10),
            Child = statusText
        };

        var root = new DockPanel { Background = LauncherTheme.AppBackground };
        DockPanel.SetDock(topBar, Dock.Top);
        root.Children.Add(topBar);

        DockPanel.SetDock(bottomBar, Dock.Bottom);
        root.Children.Add(bottomBar);

        var contentBorder = new Border {
            Padding = new Thickness(20),
            Background = LauncherTheme.PanelBackground,
            Child = pageHost
        };
        root.Children.Add(contentBorder);

        return root;
    }

    void WireEvents() {
        homeView.CreateProjectRequested += (_, _) => ShowNewProject();
        homeView.BrowseProjectRequested += async (_, _) => await BrowseExistingProjectAsync();
        homeView.ManageEnginesRequested += (_, _) => ShowEngines();

        newProjectView.BackRequested += (_, _) => ShowHome();
        newProjectView.BrowseLocationRequested += async (_, _) => await BrowseProjectLocationAsync();
        newProjectView.CreateRequested += async (_, req) => await OnCreateProjectRequestedAsync(req);

        enginesView.BackRequested += (_, _) => ShowHome();
        enginesView.InstallFromLocalRequested += async (_, _) => await InstallEngineFromLocalAsync();
    }

    void ShowPage(Control control) {
        pageHost.Content = control;
        if (control == enginesView) {
            enginesView.ClearStatus();
        } else if (control == newProjectView) {
            newProjectView.ClearStatus();
        }
    }

    void ShowHome() {
        ShowPage(homeView);
        SetStatus("ready");
    }

    void ShowNewProject() {
        ShowPage(newProjectView);
    }

    void ShowEngines() {
        RefreshEngineBindings();
        ShowPage(enginesView);
    }

    void RefreshEngineBindings() {
        var installs = engineManager.InstalledEngines;
        newProjectView.SetEngines(installs);
        enginesView.SetEngines(installs);
    }

    async Task BrowseExistingProjectAsync() {
        var folder = await PickFolderAsync("Select a project folder");
        if (string.IsNullOrWhiteSpace(folder)) {
            return;
        }

        var project = await BuildProjectFromFolderAsync(folder);
        if (project == null) {
            SetStatus("Selected folder is not a helengine project.", true);
            return;
        }

        await AddRecentProjectAsync(project);
        SetStatus($"Added {project.Name} to recent projects");
    }

    async Task BrowseProjectLocationAsync() {
        var folder = await PickFolderAsync("Choose where to place your project");
        if (string.IsNullOrWhiteSpace(folder)) {
            return;
        }

        newProjectView.SetProjectLocation(folder);
        SetStatus($"Project location set to {folder}");
    }

    async Task InstallEngineFromLocalAsync() {
        enginesView.ClearStatus();

        var folder = await PickFolderAsync("Select a helengine build folder");
        if (string.IsNullOrWhiteSpace(folder)) {
            return;
        }

        if (!EngineVersionDetector.TryDetect(folder, out var versionInfo, out var error)) {
            enginesView.SetStatus(error, true);
            SetStatus(error, true);
            return;
        }

        var install = engineManager.AddLocalInstall(folder, versionInfo!);
        RefreshEngineBindings();

        enginesView.SetStatus($"Installed {install.DisplayName} (v{install.Version}) from {install.InstallPath}");
        SetStatus($"Installed engine {install.DisplayName} (v{install.Version})");
    }

    async Task OnCreateProjectRequestedAsync(NewProjectRequest request) {
        newProjectView.ClearStatus();
        var result = await projectScaffolder.CreateAsync(request.ProjectLocation, request.ProjectName, request.Engine);

        if (!result.Success) {
            newProjectView.ShowStatus(result.Message, true);
            SetStatus(result.Message, true);
            return;
        }

        newProjectView.ResetForm();
        await AddRecentProjectAsync(BuildRecentProjectFromCreate(request, result.ProjectPath));
        SetStatus(result.Message);
        ShowHome();
    }

    async Task<string?> PickFolderAsync(string title) {
        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is { CanPickFolder: true } provider) {
            var results = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions {
                Title = title,
                AllowMultiple = false
            });

            var folder = results?.FirstOrDefault();
            string? localPath = folder?.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(localPath)) {
                return localPath;
            }
        }

        SetStatus("Folder picking is not available on this platform.", true);
        return null;
    }

    async Task LoadRecentProjectsAsync() {
        recentProjects = (await recentProjectsService.LoadAsync()).ToList();
        homeView.SetProjects(recentProjects);
    }

    async Task AddRecentProjectAsync(RecentProject project) {
        recentProjects = (await recentProjectsService.AddOrUpdateAsync(project)).ToList();
        homeView.SetProjects(recentProjects);
    }

    RecentProject BuildRecentProjectFromCreate(NewProjectRequest request, string projectPath) {
        var now = DateTime.UtcNow;
        return new RecentProject {
            Name = request.ProjectName,
            Path = projectPath,
            Created = now,
            LastOpened = now,
            TimesOpened = 1,
            Description = "created via helengine launcher",
            Version = "1.0.0"
        };
    }

    async Task<RecentProject?> BuildProjectFromFolderAsync(string folder) {
        if (!Directory.Exists(folder)) {
            return null;
        }

        string projectFile = Path.Combine(folder, "project.heproj");
        string settingsFile = Path.Combine(folder, "settings", "project.json");

        if (!File.Exists(projectFile) && !File.Exists(settingsFile)) {
            return null;
        }

        var project = new RecentProject {
            Name = Path.GetFileName(folder),
            Path = folder,
            Created = Directory.GetCreationTimeUtc(folder),
            LastOpened = DateTime.UtcNow,
            TimesOpened = 1,
            Description = "added from launcher",
            Version = "1.0.0"
        };

        if (File.Exists(projectFile)) {
            await TryApplyMetadataAsync(projectFile, project);
        }

        if (File.Exists(settingsFile)) {
            await TryApplySettingsAsync(settingsFile, project);
        }

        return project;
    }

    static async Task TryApplyMetadataAsync(string path, RecentProject project) {
        try {
            await using var stream = File.OpenRead(path);
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;

            if (TryReadProperty(root, "name", out var nameValue)) {
                project.Name = nameValue.GetString() ?? project.Name;
            }

            if (TryReadProperty(root, "created", out var createdValue)) {
                project.Created = ParseUtc(createdValue.GetString(), project.Created);
            }

            if (TryReadProperty(root, "lastOpened", out var lastOpenedValue)) {
                project.LastOpened = ParseUtc(lastOpenedValue.GetString(), project.LastOpened);
            }

            if (TryReadProperty(root, "description", out var descValue)) {
                project.Description = descValue.GetString() ?? project.Description;
            }

            if (TryReadProperty(root, "version", out var versionValue)) {
                project.Version = versionValue.GetString() ?? project.Version;
            }
        } catch {
        }
    }

    static async Task TryApplySettingsAsync(string path, RecentProject project) {
        try {
            await using var stream = File.OpenRead(path);
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;

            if (TryReadProperty(root, "name", out var nameValue)) {
                project.Name = nameValue.GetString() ?? project.Name;
            }

            if (TryReadProperty(root, "created", out var createdValue)) {
                project.Created = ParseUtc(createdValue.GetString(), project.Created);
            }
        } catch {
        }
    }

    static bool TryReadProperty(JsonElement root, string propertyName, out JsonElement element) {
        if (root.TryGetProperty(propertyName, out element)) {
            return true;
        }

        // fall back to PascalCase for any legacy files
        var pascal = char.ToUpperInvariant(propertyName[0]) + propertyName[1..];
        return root.TryGetProperty(pascal, out element);
    }

    static DateTime ParseUtc(string? value, DateTime fallback) {
        if (string.IsNullOrWhiteSpace(value)) {
            return fallback;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)) {
            return parsed.ToUniversalTime();
        }

        return fallback;
    }

    string GetDefaultProjectsFolder() {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(docs)) {
            return Path.Combine(docs, "helengine projects");
        }

        return Path.Combine(Environment.CurrentDirectory, "helengine projects");
    }

    void SetStatus(string message, bool isError = false) {
        statusText.Text = message;
        statusText.Foreground = isError ? LauncherTheme.Danger : LauncherTheme.TextSecondary;
    }
}
