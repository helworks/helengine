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

public sealed class EnginesView : UserControl, ILauncherPage {
    readonly ItemsControl EngineList;
    readonly TextBlock StatusText;
    readonly TextBlock EmptyState;

    public event EventHandler? BackRequested;
    public event EventHandler? InstallFromLocalRequested;

    public EnginesView() {
        EngineList = new ItemsControl();
        StatusText = new TextBlock { FontSize = 12, Foreground = LauncherTheme.Warning };
        EmptyState = new TextBlock {
            Text = "no engine versions installed yet",
            Foreground = LauncherTheme.TextMuted,
            FontStyle = FontStyle.Italic,
            IsVisible = false
        };

        Content = BuildContent();
    }

    public LauncherHeaderState BuildHeaderState() {
        return new LauncherHeaderState(
            "Engine builds",
            "Manage the local helengine builds available to new projects.",
            new List<LauncherHeaderAction> {
                new LauncherHeaderAction("back", LauncherHeaderActionKind.Secondary, true, () => BackRequested?.Invoke(this, EventArgs.Empty)),
                new LauncherHeaderAction("install from folder", LauncherHeaderActionKind.Primary, true, () => InstallFromLocalRequested?.Invoke(this, EventArgs.Empty))
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
        var stack = new StackPanel { Spacing = 14 };

        stack.Children.Add(StatusText);

        EngineList.ItemTemplate = new FuncDataTemplate<EngineInstall>((install, _) => BuildEngineCard(install));
        stack.Children.Add(EngineList);
        stack.Children.Add(EmptyState);

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
        EngineList.ItemsSource = list;
        EmptyState.IsVisible = list.Count == 0;
    }

    public void SetStatus(string message, bool isError = false) {
        StatusText.Text = message;
        StatusText.Foreground = isError ? LauncherTheme.Danger : LauncherTheme.Warning;
    }

    public void ClearStatus() {
        StatusText.Text = string.Empty;
    }
}
