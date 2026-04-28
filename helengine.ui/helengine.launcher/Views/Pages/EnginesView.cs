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

/// <summary>
/// Renders the launcher engine-management surface, including catalog-backed platform selection, plan summaries, and uninstall prompts.
/// </summary>
public sealed class EnginesView : UserControl, ILauncherPage {
    readonly StackPanel EngineCardsHost;
    readonly TextBlock StatusText;
    readonly TextBlock EmptyState;

    IReadOnlyList<EngineCatalogEntry> CatalogEntries = Array.Empty<EngineCatalogEntry>();
    IReadOnlyList<EngineInstall> InstalledEngines = Array.Empty<EngineInstall>();
    Dictionary<string, HashSet<string>> SelectedPlatformsByEngineVersion = new();
    Dictionary<string, PlatformInstallPlan> PlansByEngineVersion = new();
    UnusedArtifactRemovalDecision PendingUnusedArtifactDecision = new(string.Empty, Array.Empty<InstalledArtifact>());
    bool HasPendingUnusedArtifactDecision;

    public event EventHandler? BackRequested;
    public event EventHandler? InstallFromLocalRequested;
    public event Action<PlatformInstallSelection> PlanRequested = delegate { };
    public event Action<PlatformInstallSelection> InstallRequested = delegate { };
    public event Action<string> UninstallRequested = delegate { };
    public event Action<string> RemoveUnusedArtifactsRequested = delegate { };
    public event Action<string> KeepSharedArtifactsRequested = delegate { };

    public EnginesView() {
        EngineCardsHost = new StackPanel { Spacing = 12 };
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
            "Manage the local helengine builds and shared platform dependencies available to new projects.",
            new List<LauncherHeaderAction> {
                new LauncherHeaderAction("back", LauncherHeaderActionKind.Secondary, true, () => BackRequested?.Invoke(this, EventArgs.Empty)),
                new LauncherHeaderAction("install from folder", LauncherHeaderActionKind.Primary, true, () => InstallFromLocalRequested?.Invoke(this, EventArgs.Empty))
            });
    }

    Control BuildContent() {
        Border root = new() {
            Padding = new Thickness(14),
            Background = LauncherTheme.CardBackground,
            BorderBrush = LauncherTheme.Frame,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8)
        };
        StackPanel stack = new() { Spacing = 14 };

        stack.Children.Add(StatusText);
        stack.Children.Add(EngineCardsHost);
        stack.Children.Add(EmptyState);

        root.Child = stack;
        return root;
    }

    public void SetCatalogEngines(IEnumerable<EngineCatalogEntry> catalogEntries, IEnumerable<EngineInstall> installs) {
        CatalogEntries = catalogEntries.ToArray();
        InstalledEngines = installs.ToArray();
        RemoveUnavailableSelections();
        RemoveUnavailablePlans();
        if (HasPendingUnusedArtifactDecision && !ContainsInstalledEngine(PendingUnusedArtifactDecision.EngineVersion)) {
            HasPendingUnusedArtifactDecision = false;
        }

        RebuildEngineCards();
    }

    public void SetEngines(IEnumerable<EngineInstall> installs) {
        SetCatalogEngines(Array.Empty<EngineCatalogEntry>(), installs);
    }

    public void SetPlan(string engineVersion, PlatformInstallPlan plan) {
        PlansByEngineVersion[engineVersion] = plan;
        RebuildEngineCards();
    }

    public void ClearPlan(string engineVersion) {
        if (PlansByEngineVersion.Remove(engineVersion)) {
            RebuildEngineCards();
        }
    }

    public void ShowUnusedArtifactPrompt(UnusedArtifactRemovalDecision decision) {
        PendingUnusedArtifactDecision = decision;
        HasPendingUnusedArtifactDecision = true;
        RebuildEngineCards();
    }

    public void ClearUnusedArtifactPrompt() {
        if (HasPendingUnusedArtifactDecision) {
            HasPendingUnusedArtifactDecision = false;
            RebuildEngineCards();
        }
    }

    public void SetStatus(string message, bool isError = false) {
        StatusText.Text = message;
        StatusText.Foreground = isError ? LauncherTheme.Danger : LauncherTheme.Warning;
    }

    public void ClearStatus() {
        StatusText.Text = string.Empty;
    }

    void RebuildEngineCards() {
        EngineCardsHost.Children.Clear();

        if (HasPendingUnusedArtifactDecision) {
            EngineCardsHost.Children.Add(BuildUnusedArtifactPrompt(PendingUnusedArtifactDecision));
        }

        for (int index = 0; index < CatalogEntries.Count; index++) {
            EngineCardsHost.Children.Add(BuildCatalogEngineCard(CatalogEntries[index]));
        }

        for (int index = 0; index < InstalledEngines.Count; index++) {
            EngineInstall install = InstalledEngines[index];
            if (!ContainsCatalogEntry(install.Version)) {
                EngineCardsHost.Children.Add(BuildInstalledOnlyCard(install));
            }
        }

        EmptyState.IsVisible = EngineCardsHost.Children.Count == 0;
    }

    Control BuildCatalogEngineCard(EngineCatalogEntry entry) {
        Border border = CreateCardBorder();
        StackPanel stack = new() { Spacing = 10 };
        int installedEngineIndex = FindInstalledEngineIndex(entry.EngineVersion);
        bool isInstalled = installedEngineIndex >= 0;

        stack.Children.Add(new TextBlock {
            Text = entry.EngineVersion,
            FontWeight = FontWeight.SemiBold,
            Foreground = LauncherTheme.TextPrimary
        });
        stack.Children.Add(new TextBlock {
            Text = isInstalled ? $"installed at {InstalledEngines[installedEngineIndex].InstallPath}" : "not installed yet",
            Foreground = LauncherTheme.TextSecondary
        });

        stack.Children.Add(new TextBlock {
            Text = "Platforms",
            Foreground = LauncherTheme.TextSecondary,
            FontWeight = FontWeight.Medium
        });

        for (int index = 0; index < entry.PlatformRequirements.Count; index++) {
            stack.Children.Add(BuildPlatformCheckBox(entry.EngineVersion, entry.PlatformRequirements[index]));
        }

        Button installButton = BuildActionButton(
            $"InstallSelectedButton_{SanitizeControlName(entry.EngineVersion)}",
            "install selected platforms",
            SelectedPlatformsByEngineVersion.ContainsKey(entry.EngineVersion) && SelectedPlatformsByEngineVersion[entry.EngineVersion].Count > 0);
        installButton.Click += (_, _) => InstallRequested(new PlatformInstallSelection(entry.EngineVersion, GetSelectedPlatforms(entry.EngineVersion)));
        stack.Children.Add(installButton);

        if (isInstalled) {
            Button uninstallButton = BuildActionButton(
                $"UninstallButton_{SanitizeControlName(entry.EngineVersion)}",
                "uninstall",
                true);
            uninstallButton.Click += (_, _) => UninstallRequested(entry.EngineVersion);
            stack.Children.Add(uninstallButton);
        }

        if (PlansByEngineVersion.ContainsKey(entry.EngineVersion)) {
            stack.Children.Add(BuildPlanPanel(PlansByEngineVersion[entry.EngineVersion]));
        }

        border.Child = stack;
        return border;
    }

    Control BuildInstalledOnlyCard(EngineInstall install) {
        Border border = CreateCardBorder();
        StackPanel stack = new() { Spacing = 4 };
        stack.Children.Add(new TextBlock { Text = install.Summary, FontWeight = FontWeight.SemiBold, Foreground = LauncherTheme.TextPrimary });
        stack.Children.Add(new TextBlock { Text = install.InstallPath, Foreground = LauncherTheme.TextSecondary });
        if (!string.IsNullOrWhiteSpace(install.DetectedFrom)) {
            stack.Children.Add(new TextBlock { Text = $"from {install.DetectedFrom}", Foreground = LauncherTheme.TextMuted, FontSize = 11 });
        }

        stack.Children.Add(new TextBlock { Text = $"source: {install.Source}", Foreground = LauncherTheme.TextMuted, FontSize = 11 });

        Button uninstallButton = BuildActionButton(
            $"UninstallButton_{SanitizeControlName(install.Version)}",
            "uninstall",
            true);
        uninstallButton.Click += (_, _) => UninstallRequested(install.Version);
        stack.Children.Add(uninstallButton);

        border.Child = stack;
        return border;
    }

    Control BuildPlanPanel(PlatformInstallPlan plan) {
        Border border = new() {
            Background = LauncherTheme.InputBackground,
            BorderBrush = LauncherTheme.Frame,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8)
        };
        StackPanel stack = new() { Spacing = 4 };
        stack.Children.Add(new TextBlock {
            Text = "install plan",
            Foreground = LauncherTheme.TextSecondary,
            FontWeight = FontWeight.Medium
        });

        for (int index = 0; index < plan.ReusableArtifacts.Count; index++) {
            PlatformInstallPlanArtifactStatus status = plan.ReusableArtifacts[index];
            stack.Children.Add(new TextBlock {
                Text = $"reusable {status.PlatformId}: {status.Identity.Id} {status.Identity.Version}",
                Foreground = LauncherTheme.TextMuted
            });
        }

        for (int index = 0; index < plan.MissingArtifacts.Count; index++) {
            PlatformInstallPlanArtifactStatus status = plan.MissingArtifacts[index];
            stack.Children.Add(new TextBlock {
                Text = $"download {status.PlatformId}: {status.Identity.Id} {status.Identity.Version}",
                Foreground = LauncherTheme.TextPrimary
            });
        }

        for (int index = 0; index < plan.BlockingIssues.Count; index++) {
            stack.Children.Add(new TextBlock {
                Text = plan.BlockingIssues[index],
                Foreground = LauncherTheme.Danger
            });
        }

        border.Child = stack;
        return border;
    }

    Control BuildUnusedArtifactPrompt(UnusedArtifactRemovalDecision decision) {
        Border border = new() {
            Background = LauncherTheme.PanelBackground,
            BorderBrush = LauncherTheme.Frame,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10)
        };
        StackPanel stack = new() { Spacing = 8 };
        stack.Children.Add(new TextBlock {
            Text = $"Removing engine {decision.EngineVersion} leaves shared artifacts that are no longer used.",
            Foreground = LauncherTheme.TextPrimary,
            FontWeight = FontWeight.Medium
        });

        for (int index = 0; index < decision.UnusedArtifacts.Count; index++) {
            InstalledArtifact artifact = decision.UnusedArtifacts[index];
            stack.Children.Add(new TextBlock {
                Text = $"{artifact.Identity.Id} {artifact.Identity.Version}",
                Foreground = LauncherTheme.TextSecondary
            });
        }

        StackPanel buttonRow = new() {
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };
        Button removeButton = BuildActionButton("RemoveUnusedArtifactsButton", "remove unused artifacts", true);
        removeButton.Click += (_, _) => RemoveUnusedArtifactsRequested(decision.EngineVersion);
        buttonRow.Children.Add(removeButton);

        Button keepButton = BuildActionButton("KeepSharedArtifactsButton", "keep shared artifacts", true);
        keepButton.Click += (_, _) => KeepSharedArtifactsRequested(decision.EngineVersion);
        buttonRow.Children.Add(keepButton);

        stack.Children.Add(buttonRow);
        border.Child = stack;
        return border;
    }

    Control BuildPlatformCheckBox(string engineVersion, EnginePlatformRequirement requirement) {
        CheckBox checkBox = new() {
            Name = $"PlatformCheckbox_{SanitizeControlName(engineVersion)}_{SanitizeControlName(requirement.PlatformId)}",
            Content = requirement.PlatformId,
            Foreground = LauncherTheme.TextPrimary,
            IsChecked = IsPlatformSelected(engineVersion, requirement.PlatformId)
        };
        checkBox.IsCheckedChanged += (_, _) => UpdateSelection(engineVersion, requirement.PlatformId, checkBox.IsChecked == true);
        return checkBox;
    }

    Border CreateCardBorder() {
        return new Border {
            Background = LauncherTheme.PanelBackground,
            BorderBrush = LauncherTheme.Frame,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10)
        };
    }

    Button BuildActionButton(string name, string label, bool isEnabled) {
        return new Button {
            Name = name,
            Content = label,
            Cursor = LauncherCursors.Hand,
            IsEnabled = isEnabled,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = LauncherTheme.AccentLilac,
            Foreground = LauncherTheme.AccentTextOnLight,
            BorderBrush = LauncherTheme.Frame,
            BorderThickness = new Thickness(1),
            FontWeight = FontWeight.SemiBold
        };
    }

    void UpdateSelection(string engineVersion, string platformId, bool isSelected) {
        if (!SelectedPlatformsByEngineVersion.ContainsKey(engineVersion)) {
            SelectedPlatformsByEngineVersion[engineVersion] = new HashSet<string>(StringComparer.Ordinal);
        }

        HashSet<string> selectedPlatforms = SelectedPlatformsByEngineVersion[engineVersion];
        if (selectedPlatforms == null) {
            selectedPlatforms = new HashSet<string>(StringComparer.Ordinal);
            SelectedPlatformsByEngineVersion[engineVersion] = selectedPlatforms;
        }

        if (isSelected) {
            selectedPlatforms.Add(platformId);
        } else if (selectedPlatforms.Contains(platformId)) {
            selectedPlatforms.Remove(platformId);
        }

        if (selectedPlatforms.Count == 0) {
            SelectedPlatformsByEngineVersion.Remove(engineVersion);
        }

        PlanRequested(new PlatformInstallSelection(engineVersion, GetSelectedPlatforms(engineVersion)));
    }

    IReadOnlyList<string> GetSelectedPlatforms(string engineVersion) {
        if (SelectedPlatformsByEngineVersion.ContainsKey(engineVersion)) {
            HashSet<string> selectedPlatforms = SelectedPlatformsByEngineVersion[engineVersion];
            return selectedPlatforms.OrderBy(platformId => platformId, StringComparer.Ordinal).ToArray();
        }

        return Array.Empty<string>();
    }

    bool IsPlatformSelected(string engineVersion, string platformId) {
        if (SelectedPlatformsByEngineVersion.ContainsKey(engineVersion)) {
            HashSet<string> selectedPlatforms = SelectedPlatformsByEngineVersion[engineVersion];
            return selectedPlatforms.Contains(platformId);
        }

        return false;
    }

    void RemoveUnavailableSelections() {
        HashSet<string> availableEngineVersions = CatalogEntries
            .Select(entry => entry.EngineVersion)
            .ToHashSet(StringComparer.Ordinal);
        string[] keys = SelectedPlatformsByEngineVersion.Keys.ToArray();
        for (int index = 0; index < keys.Length; index++) {
            if (!availableEngineVersions.Contains(keys[index])) {
                SelectedPlatformsByEngineVersion.Remove(keys[index]);
            }
        }
    }

    void RemoveUnavailablePlans() {
        HashSet<string> visibleEngineVersions = CatalogEntries
            .Select(entry => entry.EngineVersion)
            .Concat(InstalledEngines.Select(install => install.Version))
            .ToHashSet(StringComparer.Ordinal);
        string[] keys = PlansByEngineVersion.Keys.ToArray();
        for (int index = 0; index < keys.Length; index++) {
            if (!visibleEngineVersions.Contains(keys[index])) {
                PlansByEngineVersion.Remove(keys[index]);
            }
        }
    }

    bool ContainsCatalogEntry(string engineVersion) {
        for (int index = 0; index < CatalogEntries.Count; index++) {
            if (string.Equals(CatalogEntries[index].EngineVersion, engineVersion, StringComparison.Ordinal)) {
                return true;
            }
        }

        return false;
    }

    bool ContainsInstalledEngine(string engineVersion) {
        for (int index = 0; index < InstalledEngines.Count; index++) {
            if (string.Equals(InstalledEngines[index].Version, engineVersion, StringComparison.Ordinal)) {
                return true;
            }
        }

        return false;
    }

    int FindInstalledEngineIndex(string engineVersion) {
        for (int index = 0; index < InstalledEngines.Count; index++) {
            if (string.Equals(InstalledEngines[index].Version, engineVersion, StringComparison.Ordinal)) {
                return index;
            }
        }

        return -1;
    }

    static string SanitizeControlName(string value) {
        return value.Replace('.', '_').Replace('-', '_');
    }
}
