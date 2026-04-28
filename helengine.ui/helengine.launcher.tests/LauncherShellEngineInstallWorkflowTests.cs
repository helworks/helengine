using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using helengine.editor.launcher.Models;
using helengine.editor.launcher.Services;
using helengine.editor.launcher.Views;
using helengine.editor.launcher.Views.Pages;
using Xunit;

namespace helengine.editor.launcher.tests;

/// <summary>
/// Verifies the launcher shell wires catalog planning, mocked installs, and uninstall cleanup prompts into the engines workflow.
/// </summary>
public sealed class LauncherShellEngineInstallWorkflowTests : IDisposable {
    /// <summary>
    /// Stores the isolated temporary root used by the current test instance.
    /// </summary>
    readonly string TempRootPath;

    /// <summary>
    /// Stores the isolated recents file path used by the current test instance.
    /// </summary>
    readonly string ProjectsFilePath;

    /// <summary>
    /// Creates one isolated launcher test root for the current test instance.
    /// </summary>
    public LauncherShellEngineInstallWorkflowTests() {
        TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-launcher-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempRootPath);
        ProjectsFilePath = Path.Combine(TempRootPath, "projects.json");
    }

    /// <summary>
    /// Deletes the isolated launcher test root after the current test completes.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(TempRootPath)) {
            Directory.Delete(TempRootPath, true);
        }
    }

    /// <summary>
    /// Ensures selecting one platform on the engines page requests planning and renders the resulting artifact summary.
    /// </summary>
    [AvaloniaFact]
    public async Task EnginesWorkflow_WhenPlatformIsSelected_ShowsPlanArtifactSummary() {
        LauncherShell shell = CreateShell();

        ClickButton(shell, "engine versions");
        CheckBox checkBox = FindNamedControl<CheckBox>(shell, "PlatformCheckbox_1_2_3_android");
        checkBox.IsChecked = true;
        checkBox.RaiseEvent(new RoutedEventArgs(ToggleButton.IsCheckedChangedEvent));

        await WaitForConditionAsync(() => FindTextBlocks(shell).Any(text => text.Text.Contains("android-sdk", StringComparison.Ordinal)));

        Assert.Contains(FindTextBlocks(shell), text => text.Text.Contains("android-sdk", StringComparison.Ordinal));
        Assert.Contains(FindTextBlocks(shell), text => text.Text.Contains("android-builder", StringComparison.Ordinal));
    }

    /// <summary>
    /// Ensures confirming an install after selecting platforms runs the mocked executor and updates launcher status.
    /// </summary>
    [AvaloniaFact]
    public async Task EnginesWorkflow_WhenInstallIsConfirmed_InstallsEngineAndUpdatesStatus() {
        LauncherShell shell = CreateShell();

        ClickButton(shell, "engine versions");
        CheckBox checkBox = FindNamedControl<CheckBox>(shell, "PlatformCheckbox_1_2_3_android");
        checkBox.IsChecked = true;
        checkBox.RaiseEvent(new RoutedEventArgs(ToggleButton.IsCheckedChangedEvent));
        ClickNamedButton(shell, "InstallSelectedButton_1_2_3");

        await WaitForConditionAsync(() => FindTextBlocks(shell).Any(text => text.Text.Contains("Installed engine helengine 1.2.3", StringComparison.Ordinal)));

        Assert.True(Directory.Exists(Path.Combine(TempRootPath, "engines", "helengine-1.2.3")));
        Assert.Contains(FindTextBlocks(shell), text => text.Text.Contains("Installed engine helengine 1.2.3", StringComparison.Ordinal));
    }

    /// <summary>
    /// Ensures uninstalling an installed engine surfaces the cleanup prompt with the newly unused shared artifacts.
    /// </summary>
    [AvaloniaFact]
    public async Task EnginesWorkflow_WhenUninstallLeavesUnusedArtifacts_ShowsCleanupPrompt() {
        LauncherShell shell = CreateShell();

        ClickButton(shell, "engine versions");
        CheckBox checkBox = FindNamedControl<CheckBox>(shell, "PlatformCheckbox_1_2_3_android");
        checkBox.IsChecked = true;
        checkBox.RaiseEvent(new RoutedEventArgs(ToggleButton.IsCheckedChangedEvent));
        ClickNamedButton(shell, "InstallSelectedButton_1_2_3");
        await WaitForConditionAsync(() => Directory.Exists(Path.Combine(TempRootPath, "engines", "helengine-1.2.3")));

        ClickNamedButton(shell, "UninstallButton_1_2_3");

        await WaitForConditionAsync(() => FindTextBlocks(shell).Any(text => text.Text.Contains("android-sdk", StringComparison.Ordinal)));

        Assert.Contains(FindTextBlocks(shell), text => text.Text.Contains("android-sdk", StringComparison.Ordinal));
        Assert.Contains(FindButtons(shell), button => Equals(button.Content, "remove unused artifacts"));
        Assert.Contains(FindButtons(shell), button => Equals(button.Content, "keep shared artifacts"));
    }

    /// <summary>
    /// Creates one launcher shell wired to isolated managed roots and the mocked platform catalog.
    /// </summary>
    /// <returns>Configured launcher shell.</returns>
    LauncherShell CreateShell() {
        FakeLauncherInstallRootLocator locator = new FakeLauncherInstallRootLocator {
            EngineInstallRootPath = Path.Combine(TempRootPath, "engines"),
            SharedToolchainRootPath = Path.Combine(TempRootPath, "toolchains")
        };
        EngineInstallManager engineManager = new EngineInstallManager(new LauncherInstallRootResolver(locator));
        return new LauncherShell(
            engineManager,
            new ProjectScaffolder(),
            new RecentProjectsService(ProjectsFilePath),
            new FakeLauncherStoragePicker(string.Empty),
            new ProjectFileLoader(),
            new HomeView(),
            new NewProjectView(),
            new EnginesView(),
            new MockEnginePlatformCatalog());
    }

    /// <summary>
    /// Raises one click event on the first button that matches the supplied label.
    /// </summary>
    /// <param name="root">Control tree that contains the button.</param>
    /// <param name="label">Displayed button label to click.</param>
    static void ClickButton(Control root, string label) {
        Button button = root.GetLogicalDescendants().OfType<Button>().First(currentButton => Equals(currentButton.Content, label));
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    /// <summary>
    /// Raises one click event on the button with the supplied control name.
    /// </summary>
    /// <param name="root">Control tree that contains the button.</param>
    /// <param name="name">Control name to click.</param>
    static void ClickNamedButton(Control root, string name) {
        Button button = FindNamedControl<Button>(root, name);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    /// <summary>
    /// Finds one named control inside the supplied logical tree.
    /// </summary>
    /// <typeparam name="T">Expected control type.</typeparam>
    /// <param name="root">Control tree root to inspect.</param>
    /// <param name="name">Control name to match.</param>
    /// <returns>Matching control.</returns>
    static T FindNamedControl<T>(Control root, string name) where T : Control {
        T control = root.GetLogicalDescendants().OfType<T>().FirstOrDefault(currentControl => currentControl.Name == name);
        return control ?? throw new InvalidOperationException($"Could not find control named {name}.");
    }

    /// <summary>
    /// Collects the text blocks currently rendered under the supplied control.
    /// </summary>
    /// <param name="root">Control tree root to inspect.</param>
    /// <returns>Rendered text blocks.</returns>
    static IReadOnlyList<TextBlock> FindTextBlocks(Control root) {
        return root.GetLogicalDescendants().OfType<TextBlock>().ToList();
    }

    /// <summary>
    /// Collects the buttons currently rendered under the supplied control.
    /// </summary>
    /// <param name="root">Control tree root to inspect.</param>
    /// <returns>Rendered buttons.</returns>
    static IReadOnlyList<Button> FindButtons(Control root) {
        return root.GetLogicalDescendants().OfType<Button>().ToList();
    }

    /// <summary>
    /// Waits until the supplied condition succeeds or times out.
    /// </summary>
    /// <param name="condition">Condition that must become true.</param>
    static async Task WaitForConditionAsync(Func<bool> condition) {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline) {
            if (condition()) {
                return;
            }

            await Task.Delay(25);
        }

        Assert.True(condition(), "Timed out waiting for the expected launcher condition.");
    }
}
