using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using helengine.editor.launcher.Models;
using helengine.editor.launcher.Services;
using helengine.editor.launcher.Views.Pages;
using helengine.editor.launcher.Theme;

namespace helengine.editor.launcher.Views;

/// <summary>
/// Composes the launcher shell, shared header, footer status, and page orchestration.
/// </summary>
public sealed class LauncherShell : UserControl {
    readonly EngineInstallManager EngineManager;
    readonly ProjectScaffolder ProjectScaffolder;
    readonly RecentProjectsService RecentProjectsService;
    readonly ILauncherStoragePicker LauncherStoragePicker;
    readonly ProjectFileLoader ProjectFileLoader;
    readonly EditorProjectLauncher EditorProjectLauncher;
    readonly HomeView HomeView;
    readonly NewProjectView NewProjectView;
    readonly EnginesView EnginesView;
    readonly TextBlock StatusText;
    readonly TextBlock HeaderTitleText;
    readonly TextBlock HeaderSubtitleText;
    readonly ContentControl HeaderActionsHost;
    readonly ContentControl PageHost;
    List<RecentProject> RecentProjects = new();

    /// <summary>
    /// Creates the launcher shell with the default launcher services and storage picker.
    /// </summary>
    public LauncherShell() {
        EngineManager = new EngineInstallManager();
        ProjectScaffolder = new ProjectScaffolder();
        RecentProjectsService = new RecentProjectsService();
        LauncherStoragePicker = new LauncherStoragePicker();
        ProjectFileLoader = new ProjectFileLoader();
        EditorProjectLauncher = new EditorProjectLauncher();
        HomeView = new HomeView();
        NewProjectView = new NewProjectView();
        EnginesView = new EnginesView();
        StatusText = CreateStatusText();
        HeaderTitleText = CreateHeaderTitleText();
        HeaderSubtitleText = CreateHeaderSubtitleText();
        HeaderActionsHost = CreateHeaderActionsHost();
        PageHost = new ContentControl();
        InitializeShell();
    }

    /// <summary>
    /// Creates the launcher shell with injectable services for focused tests.
    /// </summary>
    /// <param name="recentProjectsService">Recent-project persistence service.</param>
    /// <param name="launcherStoragePicker">Storage picker abstraction used for browse flows.</param>
    /// <param name="projectFileLoader">Loader that interprets selected `.heproj` files.</param>
    public LauncherShell(RecentProjectsService recentProjectsService, ILauncherStoragePicker launcherStoragePicker, ProjectFileLoader projectFileLoader) {
        EngineManager = new EngineInstallManager();
        ProjectScaffolder = new ProjectScaffolder();
        RecentProjectsService = recentProjectsService ?? throw new ArgumentNullException(nameof(recentProjectsService));
        LauncherStoragePicker = launcherStoragePicker ?? throw new ArgumentNullException(nameof(launcherStoragePicker));
        ProjectFileLoader = projectFileLoader ?? throw new ArgumentNullException(nameof(projectFileLoader));
        EditorProjectLauncher = new EditorProjectLauncher();
        HomeView = new HomeView();
        NewProjectView = new NewProjectView();
        EnginesView = new EnginesView();
        StatusText = CreateStatusText();
        HeaderTitleText = CreateHeaderTitleText();
        HeaderSubtitleText = CreateHeaderSubtitleText();
        HeaderActionsHost = CreateHeaderActionsHost();
        PageHost = new ContentControl();
        InitializeShell();
    }

    /// <summary>
    /// Creates the launcher shell with injectable services and views for focused workflow tests.
    /// </summary>
    /// <param name="engineManager">Engine-install service that supplies available engines.</param>
    /// <param name="projectScaffolder">Project scaffolder used by the create flow.</param>
    /// <param name="recentProjectsService">Recent-project persistence service.</param>
    /// <param name="launcherStoragePicker">Storage picker abstraction used for browse flows.</param>
    /// <param name="projectFileLoader">Loader that interprets selected `.heproj` files.</param>
    /// <param name="homeView">Home page view instance.</param>
    /// <param name="newProjectView">New-project page view instance.</param>
    /// <param name="enginesView">Engines page view instance.</param>
    public LauncherShell(
        EngineInstallManager engineManager,
        ProjectScaffolder projectScaffolder,
        RecentProjectsService recentProjectsService,
        ILauncherStoragePicker launcherStoragePicker,
        ProjectFileLoader projectFileLoader,
        HomeView homeView,
        NewProjectView newProjectView,
        EnginesView enginesView) {
        EngineManager = engineManager ?? throw new ArgumentNullException(nameof(engineManager));
        ProjectScaffolder = projectScaffolder ?? throw new ArgumentNullException(nameof(projectScaffolder));
        RecentProjectsService = recentProjectsService ?? throw new ArgumentNullException(nameof(recentProjectsService));
        LauncherStoragePicker = launcherStoragePicker ?? throw new ArgumentNullException(nameof(launcherStoragePicker));
        ProjectFileLoader = projectFileLoader ?? throw new ArgumentNullException(nameof(projectFileLoader));
        EditorProjectLauncher = new EditorProjectLauncher();
        HomeView = homeView ?? throw new ArgumentNullException(nameof(homeView));
        NewProjectView = newProjectView ?? throw new ArgumentNullException(nameof(newProjectView));
        EnginesView = enginesView ?? throw new ArgumentNullException(nameof(enginesView));
        StatusText = CreateStatusText();
        HeaderTitleText = CreateHeaderTitleText();
        HeaderSubtitleText = CreateHeaderSubtitleText();
        HeaderActionsHost = CreateHeaderActionsHost();
        PageHost = new ContentControl();
        InitializeShell();
    }

    /// <summary>
    /// Creates the footer status text block used by the shared shell frame.
    /// </summary>
    /// <returns>Configured status text block.</returns>
    static TextBlock CreateStatusText() {
        return new TextBlock {
            FontSize = 12,
            Foreground = LauncherTheme.TextSecondary
        };
    }

    /// <summary>
    /// Creates the shared header title text block.
    /// </summary>
    /// <returns>Configured header title text block.</returns>
    static TextBlock CreateHeaderTitleText() {
        return new TextBlock {
            Name = "HeaderTitleText",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = LauncherTheme.TextPrimary
        };
    }

    /// <summary>
    /// Creates the shared header subtitle text block.
    /// </summary>
    /// <returns>Configured header subtitle text block.</returns>
    static TextBlock CreateHeaderSubtitleText() {
        return new TextBlock {
            Name = "HeaderSubtitleText",
            FontSize = 12,
            Foreground = LauncherTheme.TextSecondary
        };
    }

    /// <summary>
    /// Creates the shared host that renders page-provided header actions.
    /// </summary>
    /// <returns>Configured header action host.</returns>
    static ContentControl CreateHeaderActionsHost() {
        return new ContentControl {
            Name = "HeaderActionsHost",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    /// <summary>
    /// Builds the shell frame, wires events, and initializes the first visible page.
    /// </summary>
    void InitializeShell() {
        var root = BuildLayout();
        Content = root;

        WireEvents();
        NewProjectView.SetProjectLocation(GetDefaultProjectsFolder());
        RefreshEngineBindings();
        _ = LoadRecentProjectsAsync();
        ShowHome();
    }

    /// <summary>
    /// Builds the outer launcher frame, shared header, page host, and footer status area.
    /// </summary>
    /// <returns>Composed shell layout.</returns>
    Control BuildLayout() {
        var topGrid = new Grid {
            Background = LauncherTheme.TopBarBackground,
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            RowDefinitions = new RowDefinitions("Auto")
        };

        var brandStack = new StackPanel {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center
        };
        brandStack.Children.Add(new TextBlock {
            Text = "helengine",
            FontSize = 22,
            FontWeight = FontWeight.SemiBold,
            Foreground = LauncherTheme.AccentLilac
        });
        brandStack.Children.Add(new TextBlock {
            Text = "launcher",
            FontSize = 14,
            Foreground = LauncherTheme.TextSecondary,
            VerticalAlignment = VerticalAlignment.Center
        });
        topGrid.Children.Add(brandStack);

        var headerStateStack = new StackPanel {
            Spacing = 2,
            Margin = new Thickness(18, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        headerStateStack.Children.Add(HeaderTitleText);
        headerStateStack.Children.Add(HeaderSubtitleText);
        Grid.SetColumn(headerStateStack, 1);
        topGrid.Children.Add(headerStateStack);

        Grid.SetColumn(HeaderActionsHost, 2);
        topGrid.Children.Add(HeaderActionsHost);

        var topBar = new Border {
            Name = "TopBarBorder",
            Padding = new Thickness(16, 12),
            Background = LauncherTheme.TopBarBackground,
            Child = topGrid
        };

        var bottomBar = new Border {
            Background = LauncherTheme.PanelBackground,
            Padding = new Thickness(10),
            Child = StatusText
        };

        var root = new DockPanel { Background = LauncherTheme.AppBackground };
        DockPanel.SetDock(topBar, Dock.Top);
        root.Children.Add(topBar);

        DockPanel.SetDock(bottomBar, Dock.Bottom);
        root.Children.Add(bottomBar);

        var contentBorder = new Border {
            Name = "ContentSurfaceBorder",
            Padding = new Thickness(14),
            Background = LauncherTheme.PanelBackground,
            Child = PageHost
        };
        root.Children.Add(contentBorder);

        return root;
    }

    /// <summary>
    /// Wires page events into shell-owned navigation and launcher workflows.
    /// </summary>
    void WireEvents() {
        HomeView.CreateProjectRequested += (_, _) => ShowNewProject();
        HomeView.BrowseProjectRequested += async (_, _) => await BrowseExistingProjectAsync();
        HomeView.ManageEnginesRequested += (_, _) => ShowEngines();
        HomeView.OpenProjectRequested += OnOpenProjectRequested;

        NewProjectView.BackRequested += (_, _) => ShowHome();
        NewProjectView.BrowseLocationRequested += async (_, _) => await BrowseProjectLocationAsync();
        NewProjectView.CreateRequested += async (_, req) => await OnCreateProjectRequestedAsync(req);

        EnginesView.BackRequested += (_, _) => ShowHome();
        EnginesView.InstallFromLocalRequested += async (_, _) => await InstallEngineFromLocalAsync();
    }

    /// <summary>
    /// Starts the open-project workflow for one clicked recent-project card.
    /// </summary>
    /// <param name="project">Recent project selected on the home page.</param>
    void OnOpenProjectRequested(RecentProject project) {
        _ = OpenRecentProjectAsync(project);
    }

    void ShowPage(Control control) {
        PageHost.Content = control;
        if (control == EnginesView) {
            EnginesView.ClearStatus();
        } else if (control == NewProjectView) {
            NewProjectView.ClearStatus();
        }

        if (control is ILauncherPage launcherPage) {
            ApplyHeaderState(launcherPage.BuildHeaderState());
        }
    }

    /// <summary>
    /// Shows the home page and resets the footer status to the ready state.
    /// </summary>
    void ShowHome() {
        ShowPage(HomeView);
        SetStatus("ready");
    }

    /// <summary>
    /// Shows the new-project page.
    /// </summary>
    void ShowNewProject() {
        ShowPage(NewProjectView);
    }

    /// <summary>
    /// Shows the engines page after refreshing the current engine bindings.
    /// </summary>
    void ShowEngines() {
        RefreshEngineBindings();
        ShowPage(EnginesView);
    }

    /// <summary>
    /// Refreshes the engine data exposed by the new-project and engines pages.
    /// </summary>
    void RefreshEngineBindings() {
        var installs = EngineManager.InstalledEngines;
        NewProjectView.SetEngines(installs);
        EnginesView.SetEngines(installs);
    }

    /// <summary>
    /// Opens a `.heproj` picker and adds the selected project file to recents when valid.
    /// </summary>
    async Task BrowseExistingProjectAsync() {
        try {
            string projectFilePath = await LauncherStoragePicker.PickProjectFileAsync(this, "Select a helengine project");
            if (string.IsNullOrWhiteSpace(projectFilePath)) {
                return;
            }

            RecentProject project = await ProjectFileLoader.LoadAsync(projectFilePath);
            await AddRecentProjectAsync(project);
            SetStatus($"Added {project.Name} to recent projects");
        } catch (InvalidOperationException exception) {
            if (exception.Message.Contains("not available on this platform", StringComparison.OrdinalIgnoreCase)) {
                SetStatus(exception.Message, true);
                return;
            }

            SetStatus(exception.Message, true);
            return;
        }
    }

    /// <summary>
    /// Opens a folder picker for the project-location field on the new-project page.
    /// </summary>
    async Task BrowseProjectLocationAsync() {
        try {
            string folder = await LauncherStoragePicker.PickFolderAsync(this, "Choose where to place your project");
            if (string.IsNullOrWhiteSpace(folder)) {
                return;
            }

            NewProjectView.SetProjectLocation(folder);
            SetStatus($"Project location set to {folder}");
        } catch (InvalidOperationException exception) {
            SetStatus(exception.Message, true);
        }
    }

    /// <summary>
    /// Installs an engine build selected from a local folder.
    /// </summary>
    async Task InstallEngineFromLocalAsync() {
        EnginesView.ClearStatus();

        try {
            string folder = await LauncherStoragePicker.PickFolderAsync(this, "Select a helengine build folder");
            if (string.IsNullOrWhiteSpace(folder)) {
                return;
            }

            if (!EngineVersionDetector.TryDetect(folder, out var versionInfo, out var error)) {
                EnginesView.SetStatus(error, true);
                SetStatus(error, true);
                return;
            }

            var install = EngineManager.AddLocalInstall(folder, versionInfo!);
            RefreshEngineBindings();

            EnginesView.SetStatus($"Installed {install.DisplayName} (v{install.Version}) from {install.InstallPath}");
            SetStatus($"Installed engine {install.DisplayName} (v{install.Version})");
        } catch (InvalidOperationException exception) {
            EnginesView.SetStatus(exception.Message, true);
            SetStatus(exception.Message, true);
        }
    }

    /// <summary>
    /// Creates a new project and adds the result to launcher recents.
    /// </summary>
    async Task OnCreateProjectRequestedAsync(NewProjectRequest request) {
        NewProjectView.ClearStatus();
        ProjectCreateResult result = await ProjectScaffolder.CreateAsync(request.ProjectLocation, request.ProjectName, request.Engine);

        if (!result.Success) {
            NewProjectView.ShowStatus(result.Message, true);
            SetStatus(result.Message, true);
            return;
        }

        string projectFilePath = Path.Combine(result.ProjectPath, "project.heproj");
        RecentProject project = await ProjectFileLoader.LoadAsync(projectFilePath);
        NewProjectView.ResetForm();
        await AddRecentProjectAsync(project);
        SetStatus(result.Message);
        ShowHome();
    }

    /// <summary>
    /// Loads persisted recent projects into the home page.
    /// </summary>
    async Task LoadRecentProjectsAsync() {
        RecentProjects = (await RecentProjectsService.LoadAsync()).ToList();
        HomeView.SetProjects(RecentProjects);
    }

    /// <summary>
    /// Adds one project to recents and refreshes the home page list.
    /// </summary>
    /// <param name="project">Recent-project entry to persist.</param>
    async Task AddRecentProjectAsync(RecentProject project) {
        RecentProjects = (await RecentProjectsService.AddOrUpdateAsync(project)).ToList();
        HomeView.SetProjects(RecentProjects);
    }

    /// <summary>
    /// Opens one recent project in the matching installed editor and updates launcher recents when the launch succeeds.
    /// </summary>
    /// <param name="project">Recent project selected on the home page.</param>
    async Task OpenRecentProjectAsync(RecentProject project) {
        try {
            EditorProjectLauncher.Launch(project, EngineManager.InstalledEngines);
            await AddRecentProjectAsync(CreateOpenedProject(project));
            SetStatus($"Opening {ResolveProjectDisplayName(project)}");
        } catch (InvalidOperationException exception) {
            SetStatus(exception.Message, true);
        }
    }

    /// <summary>
    /// Creates the recent-project snapshot that should be persisted after one successful editor launch.
    /// </summary>
    /// <param name="project">Recent project selected on the home page.</param>
    /// <returns>Updated recent-project snapshot with a fresh last-opened time.</returns>
    static RecentProject CreateOpenedProject(RecentProject project) {
        return new RecentProject {
            Name = project.Name,
            Path = project.Path,
            LastOpened = DateTime.UtcNow,
            Created = project.Created,
            TimesOpened = Math.Max(project.TimesOpened, 1),
            Description = project.Description,
            Version = project.Version,
            RequiredEngineVersion = project.RequiredEngineVersion,
            SupportedPlatforms = project.SupportedPlatforms.ToArray()
        };
    }

    /// <summary>
    /// Resolves the human-visible project name used in launcher status messages.
    /// </summary>
    /// <param name="project">Recent project selected on the home page.</param>
    /// <returns>Display name chosen for status output.</returns>
    static string ResolveProjectDisplayName(RecentProject project) {
        if (!string.IsNullOrWhiteSpace(project.Name)) {
            return project.Name;
        }

        return Path.GetFileName(project.Path);
    }

    /// <summary>
    /// Resolves the default parent folder suggested for new launcher projects.
    /// </summary>
    /// <returns>Default projects root folder.</returns>
    string GetDefaultProjectsFolder() {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(docs)) {
            return Path.Combine(docs, "helengine projects");
        }

        return Path.Combine(Environment.CurrentDirectory, "helengine projects");
    }

    /// <summary>
    /// Updates the footer status message and its error styling.
    /// </summary>
    /// <param name="message">Status text to display.</param>
    /// <param name="isError">Whether the status should be rendered as an error.</param>
    void SetStatus(string message, bool isError = false) {
        StatusText.Text = message;
        StatusText.Foreground = isError ? LauncherTheme.Danger : LauncherTheme.TextSecondary;
    }

    /// <summary>
    /// Applies page-provided header state to the shared shell header.
    /// </summary>
    /// <param name="headerState">Header title, subtitle, and actions for the active page.</param>
    void ApplyHeaderState(LauncherHeaderState headerState) {
        HeaderTitleText.Text = headerState.Title;
        HeaderSubtitleText.Text = headerState.Subtitle;
        HeaderSubtitleText.IsVisible = !string.IsNullOrWhiteSpace(headerState.Subtitle);
        HeaderActionsHost.Content = BuildHeaderActions(headerState.Actions);
    }

    /// <summary>
    /// Builds the right-aligned header action strip for the active page.
    /// </summary>
    /// <param name="actions">Header actions to render.</param>
    /// <returns>Rendered header action control tree.</returns>
    Control BuildHeaderActions(IReadOnlyList<LauncherHeaderAction> actions) {
        var stack = new StackPanel {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };

        foreach (var action in actions) {
            stack.Children.Add(BuildHeaderActionButton(action));
        }

        return stack;
    }

    /// <summary>
    /// Builds one shell-owned header action button.
    /// </summary>
    /// <param name="action">Action model to render.</param>
    /// <returns>Configured button instance.</returns>
    Button BuildHeaderActionButton(LauncherHeaderAction action) {
        bool isPrimary = action.Kind == LauncherHeaderActionKind.Primary;
        var button = new Button {
            Content = action.Label,
            Height = 38,
            MinWidth = 110,
            Padding = new Thickness(16, 0),
            Cursor = LauncherCursors.Hand,
            IsEnabled = action.IsEnabled,
            Background = isPrimary ? LauncherTheme.AccentLilac : LauncherTheme.PanelBackground,
            BorderBrush = LauncherTheme.Frame,
            BorderThickness = new Thickness(1),
            Foreground = isPrimary ? LauncherTheme.AccentTextOnLight : LauncherTheme.TextPrimary,
            FontWeight = isPrimary ? FontWeight.SemiBold : FontWeight.Medium
        };
        button.Click += (_, _) => action.Callback();
        return button;
    }
}
