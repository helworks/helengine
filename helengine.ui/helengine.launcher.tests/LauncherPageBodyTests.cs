using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using helengine.editor.launcher.Views.Pages;
using Xunit;

namespace helengine.editor.launcher.tests;

/// <summary>
/// Verifies launcher pages only render body content after the shell takes ownership of header actions.
/// </summary>
public sealed class LauncherPageBodyTests {
    /// <summary>
    /// Ensures the home page keeps the recent-project surface without rendering its old top action row.
    /// </summary>
    [AvaloniaFact]
    public void HomeView_DoesNotRenderEmbeddedTopActionRow() {
        HomeView view = new HomeView();

        Assert.Contains(FindTextBlocks(view), text => text.Text == "recent projects");
        Assert.DoesNotContain(FindButtons(view), button => Equals(button.Content, "browse project"));
        Assert.DoesNotContain(FindButtons(view), button => Equals(button.Content, "create project"));
        Assert.DoesNotContain(FindButtons(view), button => Equals(button.Content, "engine versions"));
    }

    /// <summary>
    /// Ensures the new-project page keeps the form fields without rendering an embedded header strip.
    /// </summary>
    [AvaloniaFact]
    public void NewProjectView_DoesNotRenderEmbeddedHeaderActionStrip() {
        NewProjectView view = new NewProjectView();

        Assert.Contains(FindTextBlocks(view), text => text.Text == "Project name");
        Assert.Contains(FindTextBlocks(view), text => text.Text == "Project location");
        Assert.Contains(FindTextBlocks(view), text => text.Text == "Engine version");
        Assert.DoesNotContain(FindButtons(view), button => Equals(button.Content, "back"));
        Assert.DoesNotContain(FindButtons(view), button => Equals(button.Content, "browse"));
        Assert.DoesNotContain(FindButtons(view), button => Equals(button.Content, "create project"));
        Assert.DoesNotContain(FindButtons(view), button => Equals(button.Content, "clear"));
    }

    /// <summary>
    /// Ensures the engines page keeps its status and list content without rendering an embedded header row.
    /// </summary>
    [AvaloniaFact]
    public void EnginesView_DoesNotRenderEmbeddedHeaderActionStrip() {
        EnginesView view = new EnginesView();

        Assert.Contains(FindTextBlocks(view), text => text.Text == "no engine versions installed yet");
        Assert.DoesNotContain(FindButtons(view), button => Equals(button.Content, "back"));
        Assert.DoesNotContain(FindButtons(view), button => Equals(button.Content, "install from folder"));
        Assert.DoesNotContain(FindButtons(view), button => Equals(button.Content, "from web"));
    }

    /// <summary>
    /// Collects every button rendered under the supplied control.
    /// </summary>
    /// <param name="root">Control tree root to inspect.</param>
    /// <returns>Buttons discovered in the logical tree.</returns>
    static IReadOnlyList<Button> FindButtons(Control root) {
        return root.GetLogicalDescendants().OfType<Button>().ToList();
    }

    /// <summary>
    /// Collects every text block rendered under the supplied control.
    /// </summary>
    /// <param name="root">Control tree root to inspect.</param>
    /// <returns>Text blocks discovered in the logical tree.</returns>
    static IReadOnlyList<TextBlock> FindTextBlocks(Control root) {
        return root.GetLogicalDescendants().OfType<TextBlock>().ToList();
    }
}
