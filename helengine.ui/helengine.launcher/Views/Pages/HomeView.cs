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

public sealed class HomeView : UserControl {
    public event EventHandler? CreateProjectRequested;
    public event EventHandler? BrowseProjectRequested;
    public event EventHandler? ManageEnginesRequested;

    readonly StackPanel projectListPanel;

    public HomeView() {
        VerticalAlignment = VerticalAlignment.Top;
        projectListPanel = new StackPanel { Spacing = 10 };
        Content = BuildContent();
        SetProjects(Array.Empty<RecentProject>());
    }

    Control BuildContent() {
        var stack = new StackPanel {
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(10, 12, 10, 0)
        };

        var grid = new Grid {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto")
        };

        grid.Children.Add(MakeButton("create project", () => CreateProjectRequested?.Invoke(this, EventArgs.Empty), 0));
        grid.Children.Add(MakeButton("browse project", () => BrowseProjectRequested?.Invoke(this, EventArgs.Empty), 1));
        grid.Children.Add(MakeButton("engine versions", () => ManageEnginesRequested?.Invoke(this, EventArgs.Empty), 2));

        stack.Children.Add(grid);

        var projectsSection = new StackPanel { Spacing = 8, Margin = new Thickness(0, 18, 0, 0) };
        projectsSection.Children.Add(new TextBlock {
            Text = "recent projects",
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = LauncherTheme.TextPrimary
        });

        var projectsBorder = new Border {
            Padding = new Thickness(12),
            Background = LauncherTheme.CardBackground,
            BorderBrush = LauncherTheme.Frame,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = projectListPanel
        };

        projectsSection.Children.Add(projectsBorder);
        stack.Children.Add(projectsSection);
        return stack;
    }

    Control MakeButton(string text, Action onClick, int column) {
        var btn = new Button {
            Content = text,
            Height = 52,
            Width = 170,
            FontSize = 15,
            Margin = column < 2 ? new Thickness(0, 0, 12, 0) : new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = LauncherTheme.AccentLilac,
            BorderBrush = LauncherTheme.Frame,
            BorderThickness = new Thickness(1),
            Foreground = LauncherTheme.AccentTextOnLight,
            FontWeight = FontWeight.SemiBold
        };
        btn.Click += (_, _) => onClick();
        Grid.SetColumn(btn, column);
        return btn;
    }

    public void SetProjects(IEnumerable<RecentProject> projects) {
        var list = projects?.ToList() ?? new List<RecentProject>();
        projectListPanel.Children.Clear();

        if (list.Count == 0) {
            projectListPanel.Children.Add(new TextBlock {
                Text = "No projects yet. Create or browse to add one.",
                Foreground = LauncherTheme.TextSecondary
            });
            return;
        }

        foreach (var project in list) {
            projectListPanel.Children.Add(BuildProjectCard(project));
        }
    }

    Control BuildProjectCard(RecentProject project) {
        var border = new Border {
            Padding = new Thickness(12),
            Background = LauncherTheme.PanelBackground,
            BorderBrush = LauncherTheme.Frame,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6)
        };

        var name = string.IsNullOrWhiteSpace(project.Name) ? System.IO.Path.GetFileName(project.Path) : project.Name;

        var meta = new TextBlock {
            Text = BuildMeta(project),
            Foreground = LauncherTheme.TextSecondary,
            FontSize = 12
        };

        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(new TextBlock {
            Text = name,
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = LauncherTheme.TextPrimary
        });
        stack.Children.Add(new TextBlock {
            Text = project.Path,
            Foreground = LauncherTheme.TextSecondary,
            FontSize = 12
        });
        stack.Children.Add(meta);

        border.Child = stack;
        return border;
    }

    static string BuildMeta(RecentProject project) {
        string lastOpened = FormatRelative(project.LastOpened);
        string created = project.Created == default ? "unknown" : project.Created.ToString("MMM dd, yyyy");
        return $"Last opened {lastOpened} • Created {created}";
    }

    static string FormatRelative(DateTime dateTime) {
        var timeSpan = DateTime.UtcNow - dateTime;

        if (timeSpan.TotalDays > 7) {
            return dateTime.ToString("MMM dd, yyyy");
        }

        if (timeSpan.TotalDays >= 1) {
            int days = (int)timeSpan.TotalDays;
            return $"{days} day{(days >= 2 ? "s" : "")} ago";
        }

        if (timeSpan.TotalHours >= 1) {
            int hours = (int)timeSpan.TotalHours;
            return $"{hours} hour{(hours >= 2 ? "s" : "")} ago";
        }

        if (timeSpan.TotalMinutes >= 1) {
            int minutes = (int)timeSpan.TotalMinutes;
            return $"{minutes} minute{(minutes >= 2 ? "s" : "")} ago";
        }

        return "just now";
    }
}
