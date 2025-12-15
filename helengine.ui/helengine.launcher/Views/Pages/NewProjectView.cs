using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using helengine.editor.launcher.Models;
using helengine.editor.launcher.Theme;

namespace helengine.editor.launcher.Views.Pages;

public sealed class NewProjectView : UserControl {
    readonly TextBox projectNameBox;
    readonly TextBox projectLocationBox;
    readonly ComboBox engineSelector;
    readonly TextBlock formStatus;

    string defaultLocation = string.Empty;

    public event EventHandler? BackRequested;
    public event EventHandler? BrowseLocationRequested;
    public event EventHandler<NewProjectRequest>? CreateRequested;

    public NewProjectView() {
        projectNameBox = new TextBox {
            Watermark = "my-helengine-project",
            Background = LauncherTheme.InputBackground,
            Foreground = LauncherTheme.TextPrimary,
            BorderBrush = LauncherTheme.Frame
        };
        projectLocationBox = new TextBox {
            Watermark = "pick a folder",
            Background = LauncherTheme.InputBackground,
            Foreground = LauncherTheme.TextPrimary,
            BorderBrush = LauncherTheme.Frame
        };
        engineSelector = new ComboBox {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            Background = LauncherTheme.InputBackground,
            Foreground = LauncherTheme.TextPrimary,
            BorderBrush = LauncherTheme.Frame
        };
        formStatus = new TextBlock { Foreground = LauncherTheme.Warning, FontSize = 12 };

        Content = BuildContent();
    }

    Control BuildContent() {
        var root = new Border {
            Padding = new Thickness(14),
            Background = LauncherTheme.CardBackground,
            BorderBrush = LauncherTheme.Frame,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8)
        };
        var stack = new StackPanel { Spacing = 16 };

        var headerGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), VerticalAlignment = VerticalAlignment.Center };
        var backButton = new Button {
            Content = "back",
            MinWidth = 90,
            Background = LauncherTheme.PanelBackground,
            BorderBrush = LauncherTheme.Frame,
            BorderThickness = new Thickness(1),
            Foreground = LauncherTheme.TextPrimary
        };
        backButton.Click += (_, _) => BackRequested?.Invoke(this, EventArgs.Empty);
        headerGrid.Children.Add(backButton);

        var title = new TextBlock {
            Text = "Create new project",
            FontSize = 22,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            Foreground = LauncherTheme.TextPrimary
        };
        Grid.SetColumn(title, 1);
        headerGrid.Children.Add(title);
        stack.Children.Add(headerGrid);

        stack.Children.Add(LabeledField("Project name", projectNameBox));

        var locationGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        locationGrid.Children.Add(projectLocationBox);
        var browse = new Button {
            Content = "browse",
            MinWidth = 80,
            Margin = new Thickness(10, 0, 0, 0),
            Background = LauncherTheme.PanelBackground,
            BorderBrush = LauncherTheme.Frame,
            BorderThickness = new Thickness(1),
            Foreground = LauncherTheme.TextPrimary
        };
        browse.Click += (_, _) => BrowseLocationRequested?.Invoke(this, EventArgs.Empty);
        Grid.SetColumn(browse, 1);
        locationGrid.Children.Add(browse);
        stack.Children.Add(LabeledField("Project location", locationGrid));

        stack.Children.Add(LabeledField("Engine version", engineSelector));

        stack.Children.Add(formStatus);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var createBtn = new Button {
            Content = "create project",
            MinWidth = 140,
            Background = LauncherTheme.AccentLilac,
            BorderBrush = LauncherTheme.Frame,
            BorderThickness = new Thickness(1),
            Foreground = LauncherTheme.AccentTextOnLight,
            FontWeight = FontWeight.SemiBold
        };
        createBtn.Click += (_, _) => OnCreate();
        var resetBtn = new Button {
            Content = "clear",
            MinWidth = 80,
            Background = LauncherTheme.PanelBackground,
            BorderBrush = LauncherTheme.Frame,
            BorderThickness = new Thickness(1),
            Foreground = LauncherTheme.TextPrimary
        };
        resetBtn.Click += (_, _) => ResetForm();
        actions.Children.Add(createBtn);
        actions.Children.Add(resetBtn);
        stack.Children.Add(actions);

        root.Child = stack;
        return root;
    }

    Control LabeledField(string label, Control field) {
        var wrapper = new StackPanel { Spacing = 6 };
        wrapper.Children.Add(new TextBlock { Text = label, Foreground = LauncherTheme.TextSecondary });
        wrapper.Children.Add(field);
        return wrapper;
    }

    public void SetProjectLocation(string location) {
        defaultLocation = location;
        projectLocationBox.Text = location;
    }

    public void SetEngines(IEnumerable<EngineInstall> engines) {
        var list = engines.ToList();
        engineSelector.ItemsSource = list;
        engineSelector.SelectedIndex = list.Count > 0 ? Math.Max(engineSelector.SelectedIndex, 0) : -1;
    }

    public void ResetForm() {
        projectNameBox.Text = string.Empty;
        projectLocationBox.Text = defaultLocation;
        engineSelector.SelectedIndex = engineSelector.ItemCount > 0 ? 0 : -1;
        ClearStatus();
    }

    public void ShowStatus(string message, bool isError = false) {
        formStatus.Text = message;
        formStatus.Foreground = isError ? LauncherTheme.Danger : LauncherTheme.Warning;
    }

    public void ClearStatus() {
        formStatus.Text = string.Empty;
    }

    EngineInstall? GetSelectedEngine() => engineSelector.SelectedItem as EngineInstall;

    void OnCreate() {
        ClearStatus();

        string projectName = projectNameBox.Text?.Trim() ?? string.Empty;
        string projectLocation = projectLocationBox.Text?.Trim() ?? string.Empty;
        var engine = GetSelectedEngine();

        if (string.IsNullOrWhiteSpace(projectName)) {
            ShowStatus("Enter a project name.", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(projectLocation)) {
            ShowStatus("Choose where to place the project.", true);
            return;
        }

        if (engine == null) {
            ShowStatus("Pick an engine version first.", true);
            return;
        }

        CreateRequested?.Invoke(this, new NewProjectRequest(projectName, projectLocation, engine));
    }
}

public sealed record NewProjectRequest(string ProjectName, string ProjectLocation, EngineInstall Engine);
