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

public sealed class NewProjectView : UserControl, ILauncherPage {
    readonly TextBox ProjectNameBox;
    readonly TextBox ProjectLocationBox;
    readonly ComboBox EngineSelector;
    readonly TextBlock FormStatus;

    string DefaultLocation = string.Empty;

    public event EventHandler? BackRequested;
    public event EventHandler? BrowseLocationRequested;
    public event EventHandler<NewProjectRequest>? CreateRequested;

    public NewProjectView() {
        ProjectNameBox = new TextBox {
            Name = "ProjectNameTextBox",
            Watermark = "my-helengine-project",
            Background = LauncherTheme.InputBackground,
            Foreground = LauncherTheme.TextPrimary,
            BorderBrush = LauncherTheme.Frame
        };
        ProjectLocationBox = new TextBox {
            Name = "ProjectLocationTextBox",
            Watermark = "pick a folder",
            Background = LauncherTheme.InputBackground,
            Foreground = LauncherTheme.TextPrimary,
            BorderBrush = LauncherTheme.Frame
        };
        EngineSelector = new ComboBox {
            Name = "EngineSelector",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            Background = LauncherTheme.InputBackground,
            Foreground = LauncherTheme.TextPrimary,
            BorderBrush = LauncherTheme.Frame
        };
        FormStatus = new TextBlock {
            Name = "NewProjectFormStatus",
            Foreground = LauncherTheme.Warning,
            FontSize = 12
        };

        Content = BuildContent();
    }

    public LauncherHeaderState BuildHeaderState() {
        return new LauncherHeaderState(
            "Create new project",
            "Choose a name, location, and engine build for the new workspace.",
            new List<LauncherHeaderAction> {
                new LauncherHeaderAction("back", LauncherHeaderActionKind.Secondary, true, () => BackRequested?.Invoke(this, EventArgs.Empty)),
                new LauncherHeaderAction("browse", LauncherHeaderActionKind.Secondary, true, () => BrowseLocationRequested?.Invoke(this, EventArgs.Empty)),
                new LauncherHeaderAction("create project", LauncherHeaderActionKind.Primary, true, OnCreate),
                new LauncherHeaderAction("clear", LauncherHeaderActionKind.Secondary, true, ResetForm)
            });
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

        stack.Children.Add(LabeledField("Project name", ProjectNameBox));
        stack.Children.Add(LabeledField("Project location", ProjectLocationBox));

        stack.Children.Add(LabeledField("Engine version", EngineSelector));

        stack.Children.Add(FormStatus);

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
        DefaultLocation = location;
        ProjectLocationBox.Text = location;
    }

    public void SetEngines(IEnumerable<EngineInstall> engines) {
        var list = engines.ToList();
        EngineSelector.ItemsSource = list;
        EngineSelector.SelectedIndex = list.Count > 0 ? Math.Max(EngineSelector.SelectedIndex, 0) : -1;
    }

    public void ResetForm() {
        ProjectNameBox.Text = string.Empty;
        ProjectLocationBox.Text = DefaultLocation;
        EngineSelector.SelectedIndex = EngineSelector.ItemCount > 0 ? 0 : -1;
        ClearStatus();
    }

    public void ShowStatus(string message, bool isError = false) {
        FormStatus.Text = message;
        FormStatus.Foreground = isError ? LauncherTheme.Danger : LauncherTheme.Warning;
    }

    public void ClearStatus() {
        FormStatus.Text = string.Empty;
    }

    EngineInstall? GetSelectedEngine() => EngineSelector.SelectedItem as EngineInstall;

    void OnCreate() {
        ClearStatus();

        string projectName = ProjectNameBox.Text?.Trim() ?? string.Empty;
        string projectLocation = ProjectLocationBox.Text?.Trim() ?? string.Empty;
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
