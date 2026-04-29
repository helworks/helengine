using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using helengine.editor.launcher.Models;
using helengine.editor.launcher.Services;
using helengine.editor.launcher.Views;
using helengine.editor.launcher.Views.Pages;
using Xunit;

namespace helengine.editor.launcher.tests;

/// <summary>
/// Verifies the launcher shell treats `.heproj` files as the canonical browse target.
/// </summary>
public sealed class LauncherShellProjectSelectionTests : IDisposable {
    readonly string TempDirectoryPath;
    readonly string ProjectsFilePath;

    /// <summary>
    /// Creates one isolated temporary directory and recents file path for the current test instance.
    /// </summary>
    public LauncherShellProjectSelectionTests() {
        TempDirectoryPath = Path.Combine(Path.GetTempPath(), "helengine-launcher-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempDirectoryPath);
        ProjectsFilePath = Path.Combine(TempDirectoryPath, "projects.json");
    }

    /// <summary>
    /// Deletes the isolated temporary directory when the test completes.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(TempDirectoryPath)) {
            Directory.Delete(TempDirectoryPath, true);
        }
    }

    /// <summary>
    /// Ensures selecting an invalid project file surfaces the expected launcher status message.
    /// </summary>
    [AvaloniaFact]
    public async Task BrowseExistingProjectAsync_WhenPickerReturnsInvalidFile_ShowsProjectError() {
        RecentProjectsService recentProjectsService = new RecentProjectsService(ProjectsFilePath);
        LauncherShell shell = new LauncherShell(
            recentProjectsService,
            new FakeLauncherStoragePicker(Path.Combine(TempDirectoryPath, "invalid.txt")),
            new ProjectFileLoader());

        ClickButton(shell, "browse project");
        await WaitForConditionAsync(() => FindTextBlocks(shell).Any(text => text.Text == "Selected file is not a helengine project."));

        Assert.Contains(FindTextBlocks(shell), text => text.Text == "Selected file is not a helengine project.");
    }

    /// <summary>
    /// Ensures selecting a valid `.heproj` file adds the resulting project to the recent-project list.
    /// </summary>
    [AvaloniaFact]
    public async Task BrowseExistingProjectAsync_WhenPickerReturnsHeproj_AddsRecentProjectWithFilePath() {
        string projectFilePath = Path.Combine(TempDirectoryPath, "sample-project.heproj");
        await File.WriteAllTextAsync(
            projectFilePath,
            """
            {
              "projectFormatVersion": 1,
              "name": "Project From File",
              "requiredEngineVersion": "0.4.0",
              "supportedPlatforms": [ "windows", "future-console" ],
              "created": "2026-04-01T00:00:00Z",
              "lastOpened": "2026-04-20T00:00:00Z",
              "description": "project metadata description",
              "version": "2.0.0"
            }
            """);

        RecentProjectsService recentProjectsService = new RecentProjectsService(ProjectsFilePath);
        LauncherShell shell = new LauncherShell(
            recentProjectsService,
            new FakeLauncherStoragePicker(projectFilePath),
            new ProjectFileLoader());

        ClickButton(shell, "browse project");
        await WaitForConditionAsync(() => FindTextBlocks(shell).Any(text => text.Text == projectFilePath));

        Assert.Contains(FindTextBlocks(shell), text => text.Text == "Project From File");
        Assert.Contains(FindTextBlocks(shell), text => text.Text == projectFilePath);
        Assert.Contains(FindTextBlocks(shell), text => text.Text == "requires engine 0.4.0");
        Assert.Contains(FindTextBlocks(shell), text => text.Text == "platforms windows, future-console");
    }

    /// <summary>
    /// Ensures shared project-file validation failures surface the canonical error message in launcher status.
    /// </summary>
    [AvaloniaFact]
    public async Task BrowseExistingProjectAsync_WhenProjectFormatIsUnsupported_ShowsSharedErrorMessage() {
        string projectFilePath = Path.Combine(TempDirectoryPath, "sample-project.heproj");
        await File.WriteAllTextAsync(
            projectFilePath,
            """
            {
              "projectFormatVersion": 2,
              "name": "Future Project",
              "requiredEngineVersion": "0.4.0",
              "supportedPlatforms": [ "windows" ],
              "created": "2026-04-01T00:00:00Z",
              "lastOpened": "2026-04-20T00:00:00Z",
              "version": "2.0.0"
            }
            """);

        RecentProjectsService recentProjectsService = new RecentProjectsService(ProjectsFilePath);
        LauncherShell shell = new LauncherShell(
            recentProjectsService,
            new FakeLauncherStoragePicker(projectFilePath),
            new ProjectFileLoader());

        ClickButton(shell, "browse project");
        await WaitForConditionAsync(() => FindTextBlocks(shell).Any(text => text.Text == "Project format version '2' is not supported."));

        Assert.Contains(FindTextBlocks(shell), text => text.Text == "Project format version '2' is not supported.");
    }

    /// <summary>
    /// Ensures cancelling the project-file picker leaves the empty recent-project state unchanged.
    /// </summary>
    [AvaloniaFact]
    public async Task BrowseExistingProjectAsync_WhenPickerReturnsEmpty_DoesNotMutateRecentProjects() {
        RecentProjectsService recentProjectsService = new RecentProjectsService(ProjectsFilePath);
        LauncherShell shell = new LauncherShell(
            recentProjectsService,
            new FakeLauncherStoragePicker(string.Empty),
            new ProjectFileLoader());

        ClickButton(shell, "browse project");
        await Task.Delay(25);

        Assert.Contains(FindTextBlocks(shell), text => text.Text == "No projects yet. Create or browse to add one.");
        Assert.DoesNotContain(FindTextBlocks(shell), text => text.Text == "Selected file is not a helengine project.");
    }

    /// <summary>
    /// Ensures creating a project stores and renders the canonical `.heproj` path in recents.
    /// </summary>
    [AvaloniaFact]
    public async Task CreateProjectAsync_WhenCreationSucceeds_StoresAndShowsProjectFilePath() {
        string projectsRootPath = Path.Combine(TempDirectoryPath, "projects-root");
        string expectedProjectFilePath = Path.Combine(projectsRootPath, "sample-project", "project.heproj");
        RecentProjectsService recentProjectsService = new RecentProjectsService(ProjectsFilePath);
        HomeView homeView = new HomeView();
        NewProjectView newProjectView = new NewProjectView();
        LauncherShell shell = new LauncherShell(
            new EngineInstallManager(),
            new ProjectScaffolder(),
            recentProjectsService,
            new FakeLauncherStoragePicker(string.Empty),
            new ProjectFileLoader(),
            homeView,
            newProjectView,
            new EnginesView());

        newProjectView.SetEngines(
            new[] {
                new EngineInstall {
                    Name = "Test Engine",
                    Version = "9.9.9",
                    InstallPath = Path.Combine(TempDirectoryPath, "engine")
                }
            });

        ClickButton(shell, "create project");
        FindNamedControl<TextBox>(shell, "ProjectNameTextBox").Text = "sample-project";
        FindNamedControl<TextBox>(shell, "ProjectLocationTextBox").Text = projectsRootPath;
        FindNamedControl<ComboBox>(shell, "EngineSelector").SelectedIndex = 0;

        ClickButton(shell, "create project");
        await WaitForConditionAsync(() => FindTextBlocks(shell).Any(text => text.Text == expectedProjectFilePath));

        IReadOnlyList<RecentProject> projects = await recentProjectsService.LoadAsync();
        RecentProject project = Assert.Single(projects);
        Assert.Equal(expectedProjectFilePath, project.Path);
        Assert.Contains(FindTextBlocks(shell), text => text.Text == expectedProjectFilePath);
        Assert.Contains(FindTextBlocks(shell), text => text.Text == "requires engine 9.9.9");
        Assert.Contains(FindTextBlocks(shell), text => text.Text == $"platforms {ResolveCurrentPlatformId()}");
    }

    /// <summary>
    /// Ensures clicking one rendered recent-project card launches the matching installed editor host with the canonical project-file path.
    /// </summary>
    [AvaloniaFact]
    public async Task RecentProjectCard_WhenClicked_LaunchesInstalledEditorForTheProject() {
        string projectFilePath = Path.Combine(TempDirectoryPath, "sample-project.heproj");
        string launchedProjectFilePath = Path.Combine(TempDirectoryPath, "launched-project.txt");
        await File.WriteAllTextAsync(projectFilePath, "{}");
        string engineInstallPath = CreateFakeEditorInstall(launchedProjectFilePath);

        RecentProjectsService recentProjectsService = new RecentProjectsService(ProjectsFilePath);
        await recentProjectsService.AddOrUpdateAsync(
            new RecentProject {
                Name = "Sample Project",
                Path = projectFilePath,
                RequiredEngineVersion = "1.2.3",
                SupportedPlatforms = new[] { "windows" },
                Created = DateTime.UtcNow.AddDays(-2),
                LastOpened = DateTime.UtcNow.AddDays(-1),
                Version = "1.0.0",
                Description = "Launcher project"
            });

        EngineInstallManager engineManager = new EngineInstallManager();
        engineManager.ReplaceInstalls(
            new[] {
                new EngineInstall {
                    Name = "Installed Test Engine",
                    Version = "1.2.3",
                    InstallPath = engineInstallPath
                }
            });

        LauncherShell shell = new LauncherShell(
            engineManager,
            new ProjectScaffolder(),
            recentProjectsService,
            new FakeLauncherStoragePicker(string.Empty),
            new ProjectFileLoader(),
            new HomeView(),
            new NewProjectView(),
            new EnginesView());

        await WaitForConditionAsync(() => FindProjectCardButton(shell, projectFilePath) != null);
        Button button = FindProjectCardButton(shell, projectFilePath) ?? throw new InvalidOperationException("Could not find the recent-project button.");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        await WaitForConditionAsync(() => File.Exists(launchedProjectFilePath));

        Assert.Equal(projectFilePath, (await File.ReadAllTextAsync(launchedProjectFilePath)).Trim());
    }

    /// <summary>
    /// Ensures clicking a recent-project card shows a clear error when the required engine version is not installed.
    /// </summary>
    [AvaloniaFact]
    public async Task RecentProjectCard_WhenRequiredEngineIsMissing_ShowsStatusError() {
        string projectFilePath = Path.Combine(TempDirectoryPath, "missing-engine-project.heproj");
        await File.WriteAllTextAsync(projectFilePath, "{}");

        RecentProjectsService recentProjectsService = new RecentProjectsService(ProjectsFilePath);
        await recentProjectsService.AddOrUpdateAsync(
            new RecentProject {
                Name = "Missing Engine Project",
                Path = projectFilePath,
                RequiredEngineVersion = "7.7.7",
                SupportedPlatforms = new[] { "linux" },
                Created = DateTime.UtcNow.AddDays(-2),
                LastOpened = DateTime.UtcNow.AddDays(-1),
                Version = "1.0.0",
                Description = "Launcher project"
            });

        EngineInstallManager engineManager = new EngineInstallManager();
        engineManager.ReplaceInstalls(Array.Empty<EngineInstall>());

        LauncherShell shell = new LauncherShell(
            engineManager,
            new ProjectScaffolder(),
            recentProjectsService,
            new FakeLauncherStoragePicker(string.Empty),
            new ProjectFileLoader(),
            new HomeView(),
            new NewProjectView(),
            new EnginesView());

        await WaitForConditionAsync(() => FindProjectCardButton(shell, projectFilePath) != null);
        Button button = FindProjectCardButton(shell, projectFilePath) ?? throw new InvalidOperationException("Could not find the recent-project button.");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        await WaitForConditionAsync(() => FindTextBlocks(shell).Any(text => text.Text == "Engine version 7.7.7 is not installed."));

        Assert.Contains(FindTextBlocks(shell), text => text.Text == "Engine version 7.7.7 is not installed.");
    }

    /// <summary>
    /// Finds one recent-project button by the canonical project-file path rendered inside its card.
    /// </summary>
    /// <param name="root">Control tree root to inspect.</param>
    /// <param name="projectFilePath">Canonical project-file path expected inside the card.</param>
    /// <returns>Matching project-card button when present; otherwise <c>null</c>.</returns>
    static Button FindProjectCardButton(Control root, string projectFilePath) {
        foreach (Button button in root.GetLogicalDescendants().OfType<Button>()) {
            if (button.Content is Border border
                && border.Child is StackPanel stack
                && stack.Children.OfType<TextBlock>().Any(text => string.Equals(text.Text, projectFilePath, StringComparison.Ordinal))) {
                return button;
            }
        }

        return null;
    }

    /// <summary>
    /// Raises one click event on the first button that matches the supplied label.
    /// </summary>
    /// <param name="root">Control tree that contains the button.</param>
    /// <param name="label">Displayed button label to click.</param>
    static void ClickButton(Control root, string label) {
        Button button = root.GetLogicalDescendants().OfType<Button>().First(currentButton => Equals(currentButton.Content, label));
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    /// <summary>
    /// Finds one named control inside the supplied logical tree.
    /// </summary>
    /// <typeparam name="T">Expected control type.</typeparam>
    /// <param name="root">Control tree root to inspect.</param>
    /// <param name="name">Control name to match.</param>
    /// <returns>Matching control when present; otherwise <c>null</c>.</returns>
    static T FindNamedControl<T>(Control root, string name) where T : Control {
        return root.GetLogicalDescendants().OfType<T>().FirstOrDefault(currentControl => currentControl.Name == name);
    }

    /// <summary>
    /// Collects the text blocks currently rendered under the supplied control.
    /// </summary>
    /// <param name="root">Control tree root to inspect.</param>
    /// <returns>Rendered text blocks.</returns>
    static IReadOnlyList<TextBlock> FindTextBlocks(Control root) {
        return root.GetLogicalDescendants().OfType<TextBlock>().ToList();
    }

    /// <summary>
    /// Waits until the supplied condition succeeds or times out.
    /// </summary>
    /// <param name="condition">Condition that must become true.</param>
    static async Task WaitForConditionAsync(Func<bool> condition) {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline) {
            if (condition()) {
                return;
            }

            await Task.Delay(25);
        }

        Assert.True(condition(), "Timed out waiting for the expected launcher condition.");
    }

    /// <summary>
    /// Resolves the current launcher platform identifier for project-create expectations.
    /// </summary>
    /// <returns>Canonical launcher platform identifier.</returns>
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
    /// Creates one fake installed editor host that writes the launched project path into a marker file.
    /// </summary>
    /// <param name="launchedProjectFilePath">Marker file that captures the launched project path.</param>
    /// <returns>Engine install folder containing the fake editor entry point.</returns>
    string CreateFakeEditorInstall(string launchedProjectFilePath) {
        string engineInstallPath = Path.Combine(TempDirectoryPath, "engine-install");
        Directory.CreateDirectory(engineInstallPath);

        if (OperatingSystem.IsWindows()) {
            string scriptPath = Path.Combine(engineInstallPath, "helengine.editor.app.cmd");
            File.WriteAllText(
                scriptPath,
                $"@echo off{Environment.NewLine}echo %~1>{launchedProjectFilePath}{Environment.NewLine}");
            return engineInstallPath;
        }

        string executablePath = Path.Combine(engineInstallPath, "helengine.editor.app");
        File.WriteAllText(
            executablePath,
            $"#!/bin/sh{Environment.NewLine}printf '%s\\n' \"$1\" > \"{launchedProjectFilePath}\"{Environment.NewLine}");
        File.SetUnixFileMode(
            executablePath,
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite |
            UnixFileMode.UserExecute |
            UnixFileMode.GroupRead |
            UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead |
            UnixFileMode.OtherExecute);
        return engineInstallPath;
    }
}
