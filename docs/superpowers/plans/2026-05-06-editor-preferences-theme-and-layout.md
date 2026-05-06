# Editor Preferences Theme And Layout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expand the editor-global Preferences dialog, add a persisted `Theme` selector that applies only on `Apply`, and make future theme additions catalog-driven instead of hardcoded.

**Architecture:** Introduce a reusable editor theme catalog with stable ids and palette factories, expand the editor preferences model from scale-only to a combined editor-global preferences value object, and update `EditorPreferencesDialog` and `EditorSession` to read and apply that combined state. Keep `ThemeManager` as the runtime theme authority and route persisted theme ids through the catalog.

**Tech Stack:** C#/.NET 9, xUnit, existing editor modal UI components, `ThemeManager`, `EditorPreferencesService`, `EditorSession`

---

### Task 1: Add Theme Catalog And Combined Preferences Model

**Files:**
- Create: `engine/helengine.editor/managers/preferences/EditorThemeDefinition.cs`
- Create: `engine/helengine.editor/managers/preferences/EditorThemeCatalog.cs`
- Create: `engine/helengine.editor/managers/preferences/EditorPreferencesSettings.cs`
- Modify: `engine/helengine.editor/managers/preferences/EditorPreferencesDocument.cs`
- Modify: `engine/helengine.editor/managers/preferences/EditorPreferencesService.cs`
- Test: `engine/helengine.editor.tests/EditorPreferencesServiceTests.cs`

- [ ] **Step 1: Write the failing persistence tests for `ThemeId` and combined preferences**

```csharp
[Fact]
public void Load_WhenPreferencesFileContainsThemeId_ReturnsStoredTheme() {
    File.WriteAllText(
        GetPreferencesFilePath(),
        """
        {
          "uiScaleMode": "Override",
          "uiScalePercent": 150,
          "themeId": "dark"
        }
        """);
    EditorPreferencesService service = new EditorPreferencesService(TempSettingsRootPath);

    EditorPreferencesSettings settings = service.Load();

    Assert.Equal("dark", settings.ThemeId);
    Assert.Equal(EditorUiScaleMode.Override, settings.UiScale.Mode);
    Assert.Equal(150, settings.UiScale.OverridePercent);
}

[Fact]
public void Load_WhenThemeIdIsInvalid_RewritesDefaultTheme() {
    File.WriteAllText(
        GetPreferencesFilePath(),
        """
        {
          "uiScaleMode": "Auto",
          "uiScalePercent": 100,
          "themeId": "missing-theme"
        }
        """);
    EditorPreferencesService service = new EditorPreferencesService(TempSettingsRootPath);

    EditorPreferencesSettings settings = service.Load();

    Assert.Equal(EditorThemeCatalog.DefaultThemeId, settings.ThemeId);
    Assert.Equal(EditorThemeCatalog.DefaultThemeId, ReadThemeIdFromDisk());
}

[Fact]
public void Save_WhenCombinedPreferencesArePersisted_WritesThemeIdAndScaleSettings() {
    EditorPreferencesService service = new EditorPreferencesService(TempSettingsRootPath);

    service.Save(new EditorPreferencesSettings(
        new EditorUiScaleSettings(EditorUiScaleMode.Override, 125),
        "light"));

    Assert.Equal(EditorUiScaleMode.Override, ReadModeFromDisk());
    Assert.Equal(125, ReadPercentFromDisk());
    Assert.Equal("light", ReadThemeIdFromDisk());
}
```

- [ ] **Step 2: Run the service tests to verify they fail for the expected missing API**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPreferencesServiceTests" -v minimal`

Expected: FAIL because `EditorPreferencesService.Load()` still returns `EditorUiScaleSettings`, `EditorPreferencesSettings` does not exist, and `themeId` is not persisted.

- [ ] **Step 3: Add the theme catalog and combined preferences model with stable ids**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Describes one editor theme option with a stable id, display name, and palette resolver.
    /// </summary>
    public sealed class EditorThemeDefinition {
        /// <summary>
        /// Initializes one editor theme definition.
        /// </summary>
        /// <param name="id">Stable persisted theme identifier.</param>
        /// <param name="displayName">User-facing theme label.</param>
        /// <param name="paletteFactory">Factory that resolves the runtime theme palette.</param>
        public EditorThemeDefinition(string id, string displayName, Func<ThemeManager.ThemePalette> paletteFactory) {
            Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Theme id must be provided.", nameof(id)) : id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? throw new ArgumentException("Theme display name must be provided.", nameof(displayName)) : displayName;
            PaletteFactory = paletteFactory ?? throw new ArgumentNullException(nameof(paletteFactory));
        }

        /// <summary>
        /// Gets the stable persisted theme identifier.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the user-facing theme label.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the factory used to resolve the runtime palette.
        /// </summary>
        public Func<ThemeManager.ThemePalette> PaletteFactory { get; }
    }
}
```

```csharp
namespace helengine.editor {
    /// <summary>
    /// Provides the editor-global theme catalog used by preferences and runtime theme application.
    /// </summary>
    public static class EditorThemeCatalog {
        /// <summary>
        /// Gets the default theme id used when no valid persisted theme exists.
        /// </summary>
        public const string DefaultThemeId = "neon-90s";

        /// <summary>
        /// Gets the currently supported editor themes.
        /// </summary>
        public static IReadOnlyList<EditorThemeDefinition> Themes { get; } = new[] {
            new EditorThemeDefinition("neon-90s", "Neon 90s", ThemeManager.CreateNeon90s),
            new EditorThemeDefinition("dark", "Dark", ThemeManager.CreateDarkTheme),
            new EditorThemeDefinition("light", "Light", ThemeManager.CreateLightTheme)
        };

        /// <summary>
        /// Resolves one theme definition by persisted id.
        /// </summary>
        /// <param name="themeId">Stable persisted theme identifier.</param>
        /// <returns>Matching theme definition, or null when none exists.</returns>
        public static EditorThemeDefinition FindById(string themeId) {
            for (int index = 0; index < Themes.Count; index++) {
                if (string.Equals(Themes[index].Id, themeId, StringComparison.Ordinal)) {
                    return Themes[index];
                }
            }

            return null;
        }
    }
}
```

```csharp
namespace helengine.editor {
    /// <summary>
    /// Stores validated editor-global preferences used by the Preferences dialog and session apply flow.
    /// </summary>
    public sealed class EditorPreferencesSettings {
        /// <summary>
        /// Initializes one validated editor-global preferences value object.
        /// </summary>
        /// <param name="uiScale">Validated editor UI scale settings.</param>
        /// <param name="themeId">Stable theme identifier present in the editor theme catalog.</param>
        public EditorPreferencesSettings(EditorUiScaleSettings uiScale, string themeId) {
            UiScale = uiScale ?? throw new ArgumentNullException(nameof(uiScale));
            if (EditorThemeCatalog.FindById(themeId) == null) {
                throw new ArgumentOutOfRangeException(nameof(themeId), "Theme id must resolve through the editor theme catalog.");
            }

            ThemeId = themeId;
        }

        /// <summary>
        /// Gets the validated editor UI scale settings.
        /// </summary>
        public EditorUiScaleSettings UiScale { get; }

        /// <summary>
        /// Gets the persisted theme identifier.
        /// </summary>
        public string ThemeId { get; }
    }
}
```

- [ ] **Step 4: Expand the preferences document and service to load/save combined settings**

```csharp
public sealed class EditorPreferencesDocument {
    /// <summary>
    /// Gets or sets whether the editor UI follows monitor DPI or uses one explicit override.
    /// </summary>
    public EditorUiScaleMode UiScaleMode { get; set; } = EditorUiScaleMode.Auto;

    /// <summary>
    /// Gets or sets the persisted explicit editor UI scale percentage.
    /// </summary>
    public int UiScalePercent { get; set; } = 100;

    /// <summary>
    /// Gets or sets the persisted editor-global theme identifier.
    /// </summary>
    public string ThemeId { get; set; } = EditorThemeCatalog.DefaultThemeId;
}
```

```csharp
public EditorPreferencesSettings Load() {
    EditorPreferencesDocument document = TryLoadDocument();
    if (document != null) {
        try {
            EditorPreferencesSettings settings = new EditorPreferencesSettings(
                new EditorUiScaleSettings(document.UiScaleMode, document.UiScalePercent),
                string.IsNullOrWhiteSpace(document.ThemeId) ? EditorThemeCatalog.DefaultThemeId : document.ThemeId);
            Save(settings);
            return settings;
        } catch {
        }
    }

    EditorPreferencesSettings defaultSettings = CreateDefaultSettings();
    Save(defaultSettings);
    return defaultSettings;
}

public void Save(EditorPreferencesSettings settings) {
    if (settings == null) {
        throw new ArgumentNullException(nameof(settings));
    }

    Directory.CreateDirectory(Path.GetDirectoryName(PreferencesFilePath));
    EditorPreferencesDocument document = new EditorPreferencesDocument {
        UiScaleMode = settings.UiScale.Mode,
        UiScalePercent = settings.UiScale.OverridePercent,
        ThemeId = settings.ThemeId
    };

    string json = JsonSerializer.Serialize(document, JsonSerializerOptions);
    File.WriteAllText(PreferencesFilePath, json);
}
```

- [ ] **Step 5: Run the service tests to verify the combined settings model passes**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPreferencesServiceTests" -v minimal`

Expected: PASS with the new `ThemeId` round-trip and fallback behavior covered.

- [ ] **Step 6: Commit**

```bash
git add engine/helengine.editor/managers/preferences/EditorThemeDefinition.cs engine/helengine.editor/managers/preferences/EditorThemeCatalog.cs engine/helengine.editor/managers/preferences/EditorPreferencesSettings.cs engine/helengine.editor/managers/preferences/EditorPreferencesDocument.cs engine/helengine.editor/managers/preferences/EditorPreferencesService.cs engine/helengine.editor.tests/EditorPreferencesServiceTests.cs
git commit -m "feat: add editor theme catalog and preferences model"
```

### Task 2: Update The Scale Controller And Session Preferences State To Use Combined Preferences

**Files:**
- Modify: `engine/helengine.editor/managers/preferences/EditorUiScaleController.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Test: `engine/helengine.editor.tests/EditorSessionPreferencesTests.cs`

- [ ] **Step 1: Write the failing session tests for combined apply behavior**

```csharp
[Fact]
public void HandlePreferencesDialogConfirmed_WhenInvoked_AppliesThemeAndRaisesUiScaleSettingsChanged() {
    ThemeManager.ThemePalette originalTheme = ThemeManager.Current;
    EditorSession session = CreateSessionForPreferences();
    EditorUiScaleSettings raisedSettings = null;
    session.UiScaleSettingsChanged += settings => raisedSettings = settings;

    InvokePrivate(
        session,
        "HandlePreferencesDialogConfirmed",
        new EditorPreferencesSettings(
            new EditorUiScaleSettings(EditorUiScaleMode.Override, 175),
            "dark"));

    Assert.NotNull(raisedSettings);
    Assert.Equal(EditorUiScaleMode.Override, raisedSettings.Mode);
    Assert.Equal("dark", GetPrivateField<string>(session, "CurrentThemeId"));
    Assert.NotSame(originalTheme, ThemeManager.Current);
}

[Fact]
public void HandlePreferencesDialogCanceled_WhenPendingThemeWasChanged_DoesNotApplyTheme() {
    ThemeManager.ThemePalette originalTheme = ThemeManager.Current;
    EditorSession session = CreateSessionForPreferences();

    InvokePrivate(session, "HandlePreferencesDialogCanceled");

    Assert.Same(originalTheme, ThemeManager.Current);
}
```

- [ ] **Step 2: Run the session preferences tests to verify they fail on the old scale-only signature**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionPreferencesTests" -v minimal`

Expected: FAIL because `HandlePreferencesDialogConfirmed(...)` still accepts `EditorUiScaleSettings`, and `EditorSession` does not track/apply a persisted `ThemeId`.

- [ ] **Step 3: Update the scale controller to keep working with the combined preferences object**

```csharp
public sealed class EditorUiScaleController {
    /// <summary>
    /// Gets the current validated editor-global preferences loaded from persistence.
    /// </summary>
    EditorPreferencesSettings CurrentPreferences;

    public EditorUiScaleController(EditorPreferencesService preferencesService) {
        PreferencesService = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));
        CurrentPreferences = PreferencesService.Load();
    }

    public EditorPreferencesSettings Load() {
        CurrentPreferences = PreferencesService.Load();
        return CurrentPreferences;
    }

    public EditorUiMetrics ResolveMetrics(int monitorDpi) {
        return new EditorUiMetrics(CurrentPreferences.UiScale.ResolveEffectiveScale(monitorDpi));
    }

    public EditorPreferencesSettings ApplyUserSelection(EditorPreferencesSettings settings) {
        if (settings == null) {
            throw new ArgumentNullException(nameof(settings));
        }

        PreferencesService.Save(settings);
        CurrentPreferences = settings;
        return CurrentPreferences;
    }

    public bool ShouldReapplyForMonitorDpiChange() {
        return CurrentPreferences.UiScale.Mode == EditorUiScaleMode.Auto;
    }
}
```

- [ ] **Step 4: Update the editor session preferences state and apply path**

```csharp
/// <summary>
/// Current editor-global preferences reflected by the Preferences dialog.
/// </summary>
EditorPreferencesSettings CurrentEditorPreferences;

/// <summary>
/// Current persisted theme identifier resolved through the editor theme catalog.
/// </summary>
string CurrentThemeId;

void HandlePreferencesRequested() {
    preferencesDialog.Show(CurrentEditorPreferences);
}

void HandlePreferencesDialogConfirmed(EditorPreferencesSettings settings) {
    if (settings == null) {
        throw new ArgumentNullException(nameof(settings));
    }

    CurrentEditorPreferences = settings;
    CurrentUiScaleSettings = settings.UiScale;
    CurrentThemeId = settings.ThemeId;
    ApplyTheme(CurrentThemeId);
    preferencesDialog.Hide();
    if (UiScaleSettingsChanged != null) {
        UiScaleSettingsChanged(settings.UiScale);
    }
}

void ApplyTheme(string themeId) {
    EditorThemeDefinition theme = EditorThemeCatalog.FindById(themeId) ?? throw new InvalidOperationException("Theme id must resolve through the editor theme catalog.");
    ThemeManager.SetTheme(theme.PaletteFactory());
    ReapplyViewportThemeBackground();
}
```

- [ ] **Step 5: Run the session preferences tests to verify combined theme-and-scale apply behavior**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionPreferencesTests" -v minimal`

Expected: PASS with the combined preferences confirmation path and theme apply assertions.

- [ ] **Step 6: Commit**

```bash
git add engine/helengine.editor/managers/preferences/EditorUiScaleController.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/EditorSessionPreferencesTests.cs
git commit -m "feat: wire editor session preferences through theme catalog"
```

### Task 3: Expand The Preferences Dialog And Add The Theme Selector

**Files:**
- Modify: `engine/helengine.editor/components/ui/EditorPreferencesDialog.cs`
- Test: `engine/helengine.editor.tests/EditorPreferencesDialogTests.cs`

- [ ] **Step 1: Write the failing dialog tests for the larger shell and theme selector**

```csharp
[Fact]
public void Constructor_WithDefaultMetrics_UsesLargerFutureFacingMinimumSize() {
    EditorPreferencesDialog dialog = new EditorPreferencesDialog(CreateFont(), new EditorUiMetrics(1d));

    Assert.Equal(new int2(560, 420), GetDialogMinimumSize(dialog));
}

[Fact]
public void Show_WhenOpened_LoadsThemeSelectionAndPositionsThemeControlsImmediately() {
    EditorPreferencesDialog dialog = new EditorPreferencesDialog(CreateFont(), new EditorUiMetrics(1d));

    dialog.Show(new EditorPreferencesSettings(
        new EditorUiScaleSettings(EditorUiScaleMode.Override, 150),
        "light"));

    ComboBoxComponent themeComboBox = GetPrivateField<ComboBoxComponent>(dialog, "ThemeComboBox");
    EditorEntity themeComboBoxHost = GetPrivateField<EditorEntity>(dialog, "ThemeComboBoxHost");
    EditorEntity themeLabelHost = GetPrivateField<EditorEntity>(dialog, "ThemeLabelHost");

    Assert.Equal("Light", themeComboBox.SelectedItem);
    Assert.NotEqual(float3.Zero, themeLabelHost.LocalPosition);
    Assert.NotEqual(float3.Zero, themeComboBoxHost.LocalPosition);
}

[Fact]
public void HandleApplyClicked_WhenThemeAndScaleAreSelected_RaisesCombinedPreferences() {
    EditorPreferencesDialog dialog = new EditorPreferencesDialog(CreateFont(), new EditorUiMetrics(1d));
    EditorPreferencesSettings raisedSettings = null;
    dialog.ConfirmRequested += settings => raisedSettings = settings;

    dialog.Show(new EditorPreferencesSettings(
        new EditorUiScaleSettings(EditorUiScaleMode.Auto, 100),
        "neon-90s"));

    ComboBoxComponent themeComboBox = GetPrivateField<ComboBoxComponent>(dialog, "ThemeComboBox");
    ComboBoxComponent scaleModeComboBox = GetPrivateField<ComboBoxComponent>(dialog, "ScaleModeComboBox");
    ComboBoxComponent scalePercentComboBox = GetPrivateField<ComboBoxComponent>(dialog, "ScalePercentComboBox");
    themeComboBox.SelectedIndex = 1;
    scaleModeComboBox.SelectedIndex = 1;
    scalePercentComboBox.SelectedIndex = 3;

    InvokePrivate(dialog, "HandleApplyClicked");

    Assert.NotNull(raisedSettings);
    Assert.Equal("dark", raisedSettings.ThemeId);
    Assert.Equal(EditorUiScaleMode.Override, raisedSettings.UiScale.Mode);
    Assert.Equal(150, raisedSettings.UiScale.OverridePercent);
}
```

- [ ] **Step 2: Run the dialog tests to verify they fail before UI changes**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPreferencesDialogTests" -v minimal`

Expected: FAIL because the dialog is still scale-only, smaller, and does not expose a theme combo box or combined confirm payload.

- [ ] **Step 3: Expand the dialog form and bind theme options from the catalog**

```csharp
public const int PanelWidth = 560;
public const int PanelHeight = 420;

readonly EditorEntity ThemeLabelHost;
readonly TextComponent ThemeLabel;
readonly EditorEntity ThemeComboBoxHost;
readonly ComboBoxComponent ThemeComboBox;

EditorPreferencesSettings CurrentSettings;

static string[] ThemeItems => EditorThemeCatalog.Themes.Select(theme => theme.DisplayName).ToArray();

ThemeLabelHost = CreateDialogHost();
DialogPanelRoot.AddChild(ThemeLabelHost);
ThemeLabel = CreateDialogLabel("Theme");
ThemeLabelHost.AddComponent(ThemeLabel);

ThemeComboBoxHost = CreateDialogHost();
DialogPanelRoot.AddChild(ThemeComboBoxHost);
ThemeComboBox = new ComboBoxComponent(GetFieldSize(), DialogFont, ThemeItems, 0);
ConfigureDialogComboBox(ThemeComboBox);
ThemeComboBoxHost.AddComponent(ThemeComboBox);
```

- [ ] **Step 4: Update the dialog show/apply/layout logic to use combined preferences**

```csharp
public event Action<EditorPreferencesSettings> ConfirmRequested;

public void Show(EditorPreferencesSettings settings) {
    if (settings == null) {
        throw new ArgumentNullException(nameof(settings));
    }

    CurrentSettings = settings;
    ResetDialogPositioning();
    SetThemeSelection(settings.ThemeId);
    SetScaleModeSelection(settings.UiScale.Mode);
    SetScalePercentSelection(settings.UiScale.OverridePercent);
    UpdateScalePercentEnabled(settings.UiScale.Mode == EditorUiScaleMode.Override);
    Enabled = true;
    ShowDialogImmediately();
}

void HandleApplyClicked() {
    EditorPreferencesSettings selectedSettings = new EditorPreferencesSettings(
        new EditorUiScaleSettings(ResolveSelectedMode(), ResolveSelectedPercent()),
        ResolveSelectedThemeId());
    CurrentSettings = selectedSettings;
    Hide();
    if (ConfirmRequested != null) {
        ConfirmRequested(selectedSettings);
    }
}

void LayoutContent() {
    int themeLabelTop = GetPanelPaddingPixels() + GetHeaderHeightPixels() + GetSectionSpacingPixels();
    int themeComboTop = themeLabelTop + GetLabelHeightPixels() + GetLabelFieldSpacingPixels();
    int scaleModeLabelTop = themeComboTop + GetFieldHeightPixels() + GetSectionSpacingPixels();
    int scaleModeComboTop = scaleModeLabelTop + GetLabelHeightPixels() + GetLabelFieldSpacingPixels();
    int scalePercentLabelTop = scaleModeComboTop + GetFieldHeightPixels() + GetSectionSpacingPixels();
    int scalePercentComboTop = scalePercentLabelTop + GetLabelHeightPixels() + GetLabelFieldSpacingPixels();

    ThemeLabelHost.Position = new float3(GetPanelPaddingPixels(), themeLabelTop, 0.2f);
    ThemeComboBoxHost.Position = new float3(GetPanelPaddingPixels(), themeComboTop, 0.2f);
    ScaleModeLabelHost.Position = new float3(GetPanelPaddingPixels(), scaleModeLabelTop, 0.2f);
    ScaleModeComboBoxHost.Position = new float3(GetPanelPaddingPixels(), scaleModeComboTop, 0.2f);
    ScalePercentLabelHost.Position = new float3(GetPanelPaddingPixels(), scalePercentLabelTop, 0.2f);
    ScalePercentComboBoxHost.Position = new float3(GetPanelPaddingPixels(), scalePercentComboTop, 0.2f);
}
```

- [ ] **Step 5: Run the dialog tests to verify the larger shell and theme selector pass**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPreferencesDialogTests" -v minimal`

Expected: PASS with theme binding, combined confirm behavior, and larger dialog assertions.

- [ ] **Step 6: Commit**

```bash
git add engine/helengine.editor/components/ui/EditorPreferencesDialog.cs engine/helengine.editor.tests/EditorPreferencesDialogTests.cs
git commit -m "feat: add theme selector to editor preferences dialog"
```

### Task 4: Run The Focused Regression Pass And Store The New Design In Project Memory

**Files:**
- Modify: `engine/helengine.editor.tests/EditorPreferencesDialogTests.cs`
- Modify: `engine/helengine.editor.tests/EditorPreferencesServiceTests.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionPreferencesTests.cs`
- Record: Graphiti memory entry for the editor preferences theme/layout work

- [ ] **Step 1: Run the focused preferences-and-theme regression suite**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPreferencesDialogTests|FullyQualifiedName~EditorPreferencesServiceTests|FullyQualifiedName~EditorSessionPreferencesTests|FullyQualifiedName~EditorTitleBarTests|FullyQualifiedName~EditorSessionSceneOpenTests" -v minimal`

Expected: PASS, covering dialog layout, persistence recovery, apply-time theme behavior, existing title-bar theme-sensitive visuals, and viewport background updates.

- [ ] **Step 2: If any theme-sensitive regressions appear, add the minimal missing assertions or fixes**

```csharp
[Fact]
public void HandlePreferencesDialogConfirmed_WhenThemeChanges_UpdatesSceneViewportBackground() {
    EditorSession session = CreateSessionForPreferences();

    InvokePrivate(
        session,
        "HandlePreferencesDialogConfirmed",
        new EditorPreferencesSettings(
            new EditorUiScaleSettings(EditorUiScaleMode.Auto, 100),
            "light"));

    Assert.Equal(ThemeManager.Current.Colors.BackgroundPrimary, GetSceneViewportBackground(session));
}
```

- [ ] **Step 3: Store the completed editor preferences theme/layout work in Graphiti**

Run this memory write after the code and tests are green:

```text
Name: Editor preferences theme selector and larger dialog
Group: helengine
Body: Expanded the editor-global Preferences dialog to a larger future-facing shell, introduced a registry-driven editor theme catalog with stable ids, persisted ThemeId alongside UI scale settings, and updated EditorSession to apply theme changes only on Apply. Added regression coverage for preferences dialog layout, service persistence/fallback, and session apply behavior.
```

- [ ] **Step 4: Commit the final verification-only or follow-up test adjustments if needed**

```bash
git add engine/helengine.editor.tests/EditorPreferencesDialogTests.cs engine/helengine.editor.tests/EditorPreferencesServiceTests.cs engine/helengine.editor.tests/EditorSessionPreferencesTests.cs
git commit -m "test: cover editor preferences theme selection"
```

## Spec Coverage Check

- Larger Preferences dialog with future headroom: covered in Task 3.
- Registry-driven theme options with stable ids and display names: covered in Task 1.
- Editor-global combined preferences object instead of scale-only boundary: covered in Tasks 1 and 2.
- Persisted `ThemeId` with fallback and normalization: covered in Task 1.
- Apply-only theme behavior with no live preview: covered in Tasks 2 and 3.
- Session runtime application through `ThemeManager` without a second theme state: covered in Task 2.
- Dialog, service, and session regression coverage: covered in Tasks 1 through 4.

## Notes For Execution

- Keep theme ids stable once introduced. Tests and persisted preferences should depend on ids, not display names.
- Do not serialize `ThemePalette` instances or runtime colors into preferences.
- Keep project-scoped settings untouched; this work is editor-global only.
- If changing `EditorUiScaleController` impacts callers beyond `EditorSession`, update those callers in the same task instead of layering adapters.
