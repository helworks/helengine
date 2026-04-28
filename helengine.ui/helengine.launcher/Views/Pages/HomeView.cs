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

public sealed class HomeView : UserControl, ILauncherPage {
    public event EventHandler? CreateProjectRequested;
    public event EventHandler? BrowseProjectRequested;
    public event EventHandler? ManageEnginesRequested;

    readonly StackPanel ProjectListPanel;

    public HomeView() {
        VerticalAlignment = VerticalAlignment.Top;
        ProjectListPanel = new StackPanel { Spacing = 10 };
        Content = BuildContent();
        SetProjects(Array.Empty<RecentProject>());
    }

    public LauncherHeaderState BuildHeaderState() {
        return new LauncherHeaderState(
            string.Empty,
            string.Empty,
            new List<LauncherHeaderAction> {
                new LauncherHeaderAction("create project", LauncherHeaderActionKind.Primary, true, () => CreateProjectRequested?.Invoke(this, EventArgs.Empty)),
                new LauncherHeaderAction("browse project", LauncherHeaderActionKind.Secondary, true, () => BrowseProjectRequested?.Invoke(this, EventArgs.Empty)),
                new LauncherHeaderAction("engine versions", LauncherHeaderActionKind.Secondary, true, () => ManageEnginesRequested?.Invoke(this, EventArgs.Empty))
            });
    }

    Control BuildContent() {
        var stack = new StackPanel {
            Spacing = 16,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 4, 0, 0)
        };

        var projectsSection = new StackPanel { Spacing = 8 };
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
            Child = ProjectListPanel
        };

        projectsSection.Children.Add(projectsBorder);
        stack.Children.Add(projectsSection);
        return stack;
    }

    public void SetProjects(IEnumerable<RecentProject> projects) {
        var list = projects?.ToList() ?? new List<RecentProject>();
        ProjectListPanel.Children.Clear();

        if (list.Count == 0) {
            ProjectListPanel.Children.Add(new TextBlock {
                Text = "No projects yet. Create or browse to add one.",
                Foreground = LauncherTheme.TextSecondary
            });
            return;
        }

        foreach (var project in list) {
            ProjectListPanel.Children.Add(BuildProjectCard(project));
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
        if (!string.IsNullOrWhiteSpace(project.RequiredEngineVersion)) {
            stack.Children.Add(new TextBlock {
                Text = BuildRequiredEngineVersionText(project),
                Foreground = LauncherTheme.TextSecondary,
                FontSize = 12
            });
        }

        if (project.SupportedPlatforms.Count > 0) {
            stack.Children.Add(new TextBlock {
                Text = BuildSupportedPlatformsText(project),
                Foreground = LauncherTheme.TextSecondary,
                FontSize = 12
            });
        }

        stack.Children.Add(meta);

        border.Child = stack;
        return border;
    }

    /// <summary>
    /// Builds the recent-project text that describes the exact engine version required by the canonical project file.
    /// </summary>
    /// <param name="project">Recent-project entry currently being rendered.</param>
    /// <returns>Formatted engine-version text.</returns>
    static string BuildRequiredEngineVersionText(RecentProject project) {
        return $"requires engine {project.RequiredEngineVersion}";
    }

    /// <summary>
    /// Builds the recent-project text that lists supported platform identifiers from the canonical project file.
    /// </summary>
    /// <param name="project">Recent-project entry currently being rendered.</param>
    /// <returns>Formatted supported-platform text.</returns>
    static string BuildSupportedPlatformsText(RecentProject project) {
        return $"platforms {string.Join(", ", project.SupportedPlatforms)}";
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
