using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
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
}
