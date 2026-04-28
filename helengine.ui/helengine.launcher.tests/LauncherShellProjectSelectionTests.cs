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
    /// Raises one click event on the first button that matches the supplied label.
    /// </summary>
    /// <param name="root">Control tree that contains the button.</param>
    /// <param name="label">Displayed button label to click.</param>
    static void ClickButton(Control root, string label) {
        Button button = root.GetLogicalDescendants().OfType<Button>().First(currentButton => Equals(currentButton.Content, label));
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
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
    /// Finds one named control inside the supplied logical tree.
    /// </summary>
    /// <typeparam name="T">Expected control type.</typeparam>
    /// <param name="root">Control tree root to inspect.</param>
    /// <param name="name">Control name to match.</param>
    /// <returns>Matching control.</returns>
    static T FindNamedControl<T>(Control root, string name) where T : Control {
        T control = root.GetLogicalDescendants().OfType<T>().FirstOrDefault(currentControl => currentControl.Name == name);
        return control ?? throw new InvalidOperationException($"Could not find control named {name}.");
    }

    /// <summary>
    /// Waits until the supplied condition becomes true or fails the test after a short timeout.
    /// </summary>
    /// <param name="condition">Condition that must become true.</param>
    static async Task WaitForConditionAsync(Func<bool> condition) {
        for (int attempt = 0; attempt < 20; attempt++) {
            if (condition()) {
                return;
            }

            await Task.Delay(10);
        }

        Assert.True(condition(), "Timed out waiting for the launcher UI state to update.");
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

    /// <summary>
    /// Supplies deterministic picker results to launcher shell tests.
    /// </summary>
    sealed class FakeLauncherStoragePicker : ILauncherStoragePicker {
        readonly string ProjectFilePath;

        /// <summary>
        /// Creates the fake picker with a predefined project-file result.
        /// </summary>
        /// <param name="projectFilePath">Project-file path to return for browse-project actions.</param>
        public FakeLauncherStoragePicker(string projectFilePath) {
            ProjectFilePath = projectFilePath;
        }

        /// <summary>
        /// Returns the predefined project-file path for launcher project selection.
        /// </summary>
        /// <param name="owner">Owning control that requested the picker.</param>
        /// <param name="title">Picker title.</param>
        /// <returns>Predefined project-file path or an empty string when simulating cancellation.</returns>
        public Task<string> PickProjectFileAsync(Control owner, string title) {
            return Task.FromResult(ProjectFilePath);
        }

        /// <summary>
        /// Returns an empty folder result because these tests only exercise project-file browsing.
        /// </summary>
        /// <param name="owner">Owning control that requested the picker.</param>
        /// <param name="title">Picker title.</param>
        /// <returns>Empty folder path.</returns>
        public Task<string> PickFolderAsync(Control owner, string title) {
            return Task.FromResult(string.Empty);
        }
    }
}
