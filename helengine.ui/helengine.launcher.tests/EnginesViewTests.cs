using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Interactivity;
using helengine.editor.launcher.Models;
using helengine.editor.launcher.Services;
using helengine.editor.launcher.Views.Pages;
using Xunit;

namespace helengine.editor.launcher.tests;

/// <summary>
/// Verifies the engines page renders catalog-backed engine installs and platform selectors.
/// </summary>
public sealed class EnginesViewTests {
    /// <summary>
    /// Ensures the engines page renders catalog versions and per-platform selectors once catalog data is supplied.
    /// </summary>
    [AvaloniaFact]
    public void SetCatalogEngines_WhenCatalogEntriesExist_RendersEngineVersionsAndPlatformSelectors() {
        EnginesView view = new EnginesView();

        view.SetCatalogEngines(
            new MockEnginePlatformCatalog().GetAvailableEngines(),
            new[] {
                new EngineInstall {
                    Version = "1.2.3",
                    InstallPath = "/tmp/helengine-1.2.3"
                }
            });

        Assert.Contains(FindTextBlocks(view), text => text.Text == "1.2.3");
        Assert.Contains(FindTextBlocks(view), text => text.Text == "2.0.0");
        Assert.NotNull(FindNamedControl<CheckBox>(view, "PlatformCheckbox_1_2_3_android"));
        Assert.NotNull(FindNamedControl<CheckBox>(view, "PlatformCheckbox_1_2_3_windows"));
        Assert.NotNull(FindNamedControl<CheckBox>(view, "PlatformCheckbox_2_0_0_linux"));
        Assert.NotNull(FindNamedControl<Button>(view, "InstallSelectedButton_1_2_3"));
        Assert.NotNull(FindNamedControl<Button>(view, "UninstallButton_1_2_3"));
    }

    /// <summary>
    /// Ensures toggling one platform selector raises a plan request for the matching engine and selected platform.
    /// </summary>
    [AvaloniaFact]
    public void PlatformCheckbox_WhenChecked_RaisesPlanRequestedForThatSelection() {
        EnginesView view = new EnginesView();
        PlatformInstallSelection receivedSelection = null;
        view.PlanRequested += selection => receivedSelection = selection;
        view.SetCatalogEngines(new MockEnginePlatformCatalog().GetAvailableEngines(), Array.Empty<EngineInstall>());

        CheckBox checkBox = FindNamedControl<CheckBox>(view, "PlatformCheckbox_1_2_3_android");
        checkBox.IsChecked = true;
        checkBox.RaiseEvent(new RoutedEventArgs(ToggleButton.IsCheckedChangedEvent));

        Assert.NotNull(receivedSelection);
        Assert.Equal("1.2.3", receivedSelection.EngineVersion);
        Assert.Equal(new[] { "android" }, receivedSelection.PlatformIds);
    }

    /// <summary>
    /// Finds one named control inside the supplied logical tree.
    /// </summary>
    /// <typeparam name="T">Expected control type.</typeparam>
    /// <param name="root">Control tree root to inspect.</param>
    /// <param name="name">Control name to match.</param>
    /// <returns>Matching control when present; otherwise <c>null</c>.</returns>
    static T FindNamedControl<T>(Control root, string name) where T : Control {
        return root.GetLogicalDescendants().OfType<T>().FirstOrDefault(currentControl => currentControl.Name == name);
    }

    /// <summary>
    /// Collects every text block rendered under the supplied control.
    /// </summary>
    /// <param name="root">Control tree root to inspect.</param>
    /// <returns>Rendered text blocks.</returns>
    static IReadOnlyList<TextBlock> FindTextBlocks(Control root) {
        return root.GetLogicalDescendants().OfType<TextBlock>().ToList();
    }
}
