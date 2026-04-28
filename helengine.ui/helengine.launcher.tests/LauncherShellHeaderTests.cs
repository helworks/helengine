using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia;
using helengine.editor.launcher.Views;
using Xunit;

namespace helengine.editor.launcher.tests;

/// <summary>
/// Verifies the launcher shell owns the active-page header instead of leaving page actions inside body layouts.
/// </summary>
public sealed class LauncherShellHeaderTests {
    /// <summary>
    /// Ensures the home page renders its actions inside the shared shell header area.
    /// </summary>
    [AvaloniaFact]
    public void LauncherShell_WhenHomeIsActive_RendersHomeActionsInSharedHeader() {
        LauncherShell shell = new LauncherShell();

        TextBlock headerTitleText = FindNamedControl<TextBlock>(shell, "HeaderTitleText");
        TextBlock headerSubtitleText = FindNamedControl<TextBlock>(shell, "HeaderSubtitleText");
        ContentControl headerActionsHost = FindNamedControl<ContentControl>(shell, "HeaderActionsHost");

        Assert.NotNull(headerTitleText);
        Assert.NotNull(headerSubtitleText);
        Assert.NotNull(headerActionsHost);
        Assert.Equal(string.Empty, headerTitleText.Text);
        Assert.Equal(string.Empty, headerSubtitleText.Text);

        IReadOnlyList<Button> headerButtons = FindButtons(headerActionsHost);

        Assert.Contains(headerButtons, button => Equals(button.Content, "create project"));
        Assert.Contains(headerButtons, button => Equals(button.Content, "browse project"));
        Assert.Contains(headerButtons, button => Equals(button.Content, "engine versions"));
    }

    /// <summary>
    /// Ensures navigating to the new-project page refreshes the shell-owned header actions.
    /// </summary>
    [AvaloniaFact]
    public void LauncherShell_WhenNewProjectIsActive_RendersNewProjectActionsInSharedHeader() {
        LauncherShell shell = new LauncherShell();

        ClickButton(shell, "create project");

        ContentControl headerActionsHost = FindNamedControl<ContentControl>(shell, "HeaderActionsHost");
        Assert.NotNull(headerActionsHost);

        IReadOnlyList<Button> headerButtons = FindButtons(headerActionsHost);

        Assert.Contains(headerButtons, button => Equals(button.Content, "back"));
        Assert.Contains(headerButtons, button => Equals(button.Content, "browse"));
        Assert.Contains(headerButtons, button => Equals(button.Content, "create project"));
        Assert.Contains(headerButtons, button => Equals(button.Content, "clear"));
    }

    /// <summary>
    /// Ensures navigating to the engines page refreshes the shell-owned header actions.
    /// </summary>
    [AvaloniaFact]
    public void LauncherShell_WhenEnginesIsActive_RendersEnginesActionsInSharedHeader() {
        LauncherShell shell = new LauncherShell();

        ClickButton(shell, "engine versions");

        ContentControl headerActionsHost = FindNamedControl<ContentControl>(shell, "HeaderActionsHost");
        Assert.NotNull(headerActionsHost);

        IReadOnlyList<Button> headerButtons = FindButtons(headerActionsHost);

        Assert.Contains(headerButtons, button => Equals(button.Content, "back"));
        Assert.Contains(headerButtons, button => Equals(button.Content, "install from folder"));
    }

    /// <summary>
    /// Ensures the shell uses a tighter working layout so the header and content waste less space.
    /// </summary>
    [AvaloniaFact]
    public void LauncherShell_UsesCompactHeaderAndContentPadding() {
        LauncherShell shell = new LauncherShell();

        Border topBarBorder = FindNamedControl<Border>(shell, "TopBarBorder");
        Border contentSurfaceBorder = FindNamedControl<Border>(shell, "ContentSurfaceBorder");

        Assert.NotNull(topBarBorder);
        Assert.NotNull(contentSurfaceBorder);
        Assert.Equal(new Thickness(16, 12), topBarBorder.Padding);
        Assert.Equal(new Thickness(14), contentSurfaceBorder.Padding);
    }

    /// <summary>
    /// Raises one click event on the first button that matches the supplied label.
    /// </summary>
    /// <param name="root">Control tree that contains the button.</param>
    /// <param name="label">Displayed button label to click.</param>
    static void ClickButton(Control root, string label) {
        Button button = FindButtons(root).First(button => Equals(button.Content, label));
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    /// <summary>
    /// Collects every button that exists inside the supplied control tree.
    /// </summary>
    /// <param name="root">Control tree root to inspect.</param>
    /// <returns>Buttons discovered under the supplied root.</returns>
    static IReadOnlyList<Button> FindButtons(Control root) {
        List<Button> buttons = new List<Button>();
        CollectButtons(root, buttons);
        return buttons;
    }

    /// <summary>
    /// Finds one named control inside the supplied logical tree.
    /// </summary>
    /// <typeparam name="T">Expected control type.</typeparam>
    /// <param name="root">Control tree root to inspect.</param>
    /// <param name="name">Control name to match.</param>
    /// <returns>Matching control or <c>null</c> when it does not exist.</returns>
    static T FindNamedControl<T>(Control root, string name) where T : Control {
        if (root is T rootMatch && root.Name == name) {
            return rootMatch;
        }

        return root.GetLogicalDescendants()
            .OfType<T>()
            .FirstOrDefault(control => control.Name == name);
    }

    /// <summary>
    /// Walks the logical child tree to collect all nested buttons.
    /// </summary>
    /// <param name="root">Current control being inspected.</param>
    /// <param name="buttons">Accumulated result list.</param>
    static void CollectButtons(Control root, List<Button> buttons) {
        if (root is Button button) {
            buttons.Add(button);
        }

        foreach (Control child in root.GetLogicalDescendants().OfType<Control>()) {
            if (child is Button nestedButton) {
                buttons.Add(nestedButton);
            }
        }
    }
}
