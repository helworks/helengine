using Avalonia;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using helengine.editor.launcher.Models;
using helengine.editor.launcher.Views.Pages;
using Xunit;

namespace helengine.editor.launcher.tests;

/// <summary>
/// Verifies the launcher home view renders canonical engine-version and platform metadata from recent projects.
/// </summary>
public sealed class HomeViewTests {
    /// <summary>
    /// Ensures recent-project cards render the required engine version and supported platforms from the shared project file.
    /// </summary>
    [AvaloniaFact]
    public void SetProjects_WhenProjectContainsEngineAndPlatformMetadata_RendersThem() {
        HomeView view = new HomeView();
        view.SetProjects(
            new[] {
                new RecentProject {
                    Name = "Sample Project",
                    Path = "/projects/sample/project.heproj",
                    RequiredEngineVersion = "0.4.0",
                    SupportedPlatforms = new[] { "windows", "future-console" },
                    Created = DateTime.UtcNow.AddDays(-4),
                    LastOpened = DateTime.UtcNow.AddDays(-1),
                    Version = "2.0.0",
                    Description = "Shared project file"
                }
            });

        IReadOnlyList<TextBlock> textBlocks = view.GetLogicalDescendants().OfType<TextBlock>().ToList();

        Assert.Contains(textBlocks, text => text.Text == "requires engine 0.4.0");
        Assert.Contains(textBlocks, text => text.Text == "platforms windows, future-console");
    }

    /// <summary>
    /// Ensures the recent-project card background changes while the pointer hovers the project button and resets after the pointer leaves.
    /// </summary>
    [AvaloniaFact]
    public void SetProjects_WhenProjectButtonIsHovered_ChangesAndRestoresCardBackground() {
        HomeView view = new HomeView();
        view.SetProjects(
            new[] {
                new RecentProject {
                    Name = "Sample Project",
                    Path = "/projects/sample/project.heproj",
                    RequiredEngineVersion = "0.4.0",
                    SupportedPlatforms = new[] { "windows" },
                    Created = DateTime.UtcNow.AddDays(-4),
                    LastOpened = DateTime.UtcNow.AddDays(-1),
                    Version = "2.0.0",
                    Description = "Shared project file"
                }
            });

        Button button = view.GetLogicalDescendants().OfType<Button>().Single();
        Border border = button.Content as Border ?? throw new InvalidOperationException("Expected the recent-project button to contain a card border.");
        Color initialColor = GetBackgroundColor(border);

        button.RaiseEvent(CreatePointerEventArgs(button, InputElement.PointerEnteredEvent));
        Color hoveredColor = GetBackgroundColor(border);

        button.RaiseEvent(CreatePointerEventArgs(button, InputElement.PointerExitedEvent));
        Color restoredColor = GetBackgroundColor(border);

        Assert.NotEqual(initialColor, hoveredColor);
        Assert.Equal(initialColor, restoredColor);
    }

    /// <summary>
    /// Reads the solid background color from one project-card border.
    /// </summary>
    /// <param name="border">Project-card border to inspect.</param>
    /// <returns>Solid background color applied to the border.</returns>
    static Color GetBackgroundColor(Border border) {
        ISolidColorBrush background = border.Background as ISolidColorBrush ?? throw new InvalidOperationException("Expected the project-card background to be a solid color brush.");
        return background.Color;
    }

    /// <summary>
    /// Creates one pointer event args instance for launcher hover tests.
    /// </summary>
    /// <param name="button">Button that raises the pointer event.</param>
    /// <param name="routedEvent">Pointer routed event to raise.</param>
    /// <returns>Configured pointer event args for the supplied button.</returns>
    static PointerEventArgs CreatePointerEventArgs(Button button, RoutedEvent routedEvent) {
        return new PointerEventArgs(
            routedEvent,
            button,
            new Pointer(1, PointerType.Mouse, true),
            button,
            new Point(0, 0),
            0,
            new PointerPointProperties(),
            KeyModifiers.None);
    }
}
