using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using helengine.editor.launcher.Models;
using helengine.editor.launcher.Theme;

namespace helengine.editor.launcher.Views.Pages;

public sealed class EnginesView : UserControl {
    readonly ItemsControl engineList;
    readonly TextBlock statusText;
    readonly TextBlock emptyState;

    public event EventHandler? BackRequested;
    public event EventHandler? InstallFromLocalRequested;

    public EnginesView() {
        engineList = new ItemsControl();
        statusText = new TextBlock { FontSize = 12, Foreground = LauncherTheme.Warning };
        emptyState = new TextBlock {
            Text = "no engine versions installed yet",
            Foreground = LauncherTheme.TextMuted,
            FontStyle = FontStyle.Italic,
            IsVisible = false
        };

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
        var stack = new StackPanel { Spacing = 14 };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        var backButton = new Button {
            Content = "back",
            MinWidth = 90,
            Background = LauncherTheme.PanelBackground,
            BorderBrush = LauncherTheme.Frame,
            BorderThickness = new Thickness(1),
            Foreground = LauncherTheme.TextPrimary
        };
        backButton.Click += (_, _) => BackRequested?.Invoke(this, EventArgs.Empty);
        header.Children.Add(backButton);

        var title = new TextBlock {
            Text = "Engine builds",
            FontSize = 22,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            Foreground = LauncherTheme.TextPrimary
        };
        Grid.SetColumn(title, 1);
        header.Children.Add(title);

        var actions = new StackPanel {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(10, 0, 0, 0)
        };
        var installLocal = new Button {
            Content = "install from folder",
            MinWidth = 150,
            Background = LauncherTheme.AccentLilac,
            BorderBrush = LauncherTheme.Frame,
            BorderThickness = new Thickness(1),
            Foreground = LauncherTheme.AccentTextOnLight,
            FontWeight = FontWeight.SemiBold
        };
        installLocal.Click += (_, _) => InstallFromLocalRequested?.Invoke(this, EventArgs.Empty);
        actions.Children.Add(installLocal);
        actions.Children.Add(new Button {
            Content = "from web",
            IsEnabled = false,
            MinWidth = 120,
            Background = LauncherTheme.PanelBackground,
            BorderBrush = LauncherTheme.Frame,
            BorderThickness = new Thickness(1),
            Foreground = LauncherTheme.TextSecondary
        });
        Grid.SetColumn(actions, 2);
        header.Children.Add(actions);
        stack.Children.Add(header);

        stack.Children.Add(statusText);

        engineList.ItemTemplate = new FuncDataTemplate<EngineInstall>((install, _) => BuildEngineCard(install));
        stack.Children.Add(engineList);
        stack.Children.Add(emptyState);

        root.Child = stack;
        return root;
    }

    Control BuildEngineCard(EngineInstall install) {
        var border = new Border {
            Background = LauncherTheme.PanelBackground,
            BorderBrush = LauncherTheme.Frame,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 10)
        };

        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(new TextBlock { Text = install.Summary, FontWeight = FontWeight.SemiBold, Foreground = LauncherTheme.TextPrimary });
        stack.Children.Add(new TextBlock { Text = install.InstallPath, Foreground = LauncherTheme.TextSecondary });
        if (!string.IsNullOrWhiteSpace(install.DetectedFrom)) {
            stack.Children.Add(new TextBlock { Text = $"from {install.DetectedFrom}", Foreground = LauncherTheme.TextMuted, FontSize = 11 });
        }
        stack.Children.Add(new TextBlock { Text = $"source: {install.Source}", Foreground = LauncherTheme.TextMuted, FontSize = 11 });

        border.Child = stack;
        return border;
    }

    public void SetEngines(IEnumerable<EngineInstall> installs) {
        var list = installs.ToList();
        engineList.ItemsSource = list;
        emptyState.IsVisible = list.Count == 0;
    }

    public void SetStatus(string message, bool isError = false) {
        statusText.Text = message;
        statusText.Foreground = isError ? LauncherTheme.Danger : LauncherTheme.Warning;
    }

    public void ClearStatus() {
        statusText.Text = string.Empty;
    }
}
