# Editor DPI Scaling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add editor-global DPI scaling preferences with `Auto` and explicit override modes, then apply the resolved scale live to the current editor UI without restarting the process.

**Architecture:** Keep only host-specific DPI observation and font creation at the WinForms boundary in `MainForm`, but move preference loading, effective-scale resolution, and session refresh orchestration into reusable editor-side types. The editor session and its title bar, dock chrome, and modal dialogs will consume one `EditorUiMetrics` object and expose `ApplyUiMetrics(...)` refresh paths so a saved preference or monitor-DPI change can rebuild fonts and rerun layout in place, and that shared flow can later be reused by Linux and macOS hosts.

**Tech Stack:** C# / .NET 9, WinForms host, existing custom editor UI system, xUnit, existing JSON persistence patterns.

---

## File Map

### Global preferences and scale model
- Create: `engine/helengine.editor/managers/preferences/EditorUiScaleMode.cs`
- Create: `engine/helengine.editor/managers/preferences/EditorUiScaleSettings.cs`
- Create: `engine/helengine.editor/managers/preferences/EditorPreferencesDocument.cs`
- Create: `engine/helengine.editor/managers/preferences/EditorPreferencesService.cs`
- Create: `engine/helengine.editor/managers/preferences/EditorUiMetrics.cs`
- Create: `engine/helengine.editor.tests/EditorPreferencesServiceTests.cs`
- Create: `engine/helengine.editor.tests/EditorUiMetricsTests.cs`

### Scale-aware editor chrome and dialogs
- Modify: `engine/helengine.editor/components/ui/EditorTitleBar.cs`
- Modify: `engine/helengine.editor/components/ui/dock/DockableEntity.cs`
- Modify: `engine/helengine.editor/components/ui/dock/DockTabStrip.cs`
- Modify: `engine/helengine.editor/managers/dock/DockLayoutEngine.cs`
- Modify: `engine/helengine.editor/components/ui/EditorDialogBase.cs`
- Modify: `engine/helengine.editor/components/ui/BuildSettingsDialog.cs`
- Modify: `engine/helengine.editor/components/ui/BuildDialog.cs`
- Modify: `engine/helengine.editor/components/ui/BuildDialogCopySettingsDialog.cs`
- Modify: `engine/helengine.editor/components/ui/ProfilesDialog.cs`
- Modify: `engine/helengine.editor/components/ui/ReparentEntityDialog.cs`
- Modify: `engine/helengine.editor/components/ui/UnsavedChangesDialog.cs`
- Modify: `engine/helengine.editor/components/ui/asset/OpenFileDialog.cs`
- Modify: `engine/helengine.editor/components/ui/asset/SaveFileDialog.cs`
- Modify: `engine/helengine.editor/components/ui/asset/AssetPickerModal.cs`
- Modify: `engine/helengine.editor/components/ui/SceneHierarchyPanel.cs`
- Modify: `engine/helengine.editor/components/ui/PropertiesPanel.cs`
- Modify: `engine/helengine.editor/components/ui/PreviewPanel.cs`
- Modify: `engine/helengine.editor/components/ui/LoggerPanel.cs`
- Modify: `engine/helengine.editor/components/ui/EditorViewport.cs`
- Modify: `engine/helengine.editor.tests/EditorTitleBarTests.cs`
- Modify: `engine/helengine.editor.tests/BuildSettingsDialogTests.cs`

### Preferences modal and editor-session wiring
- Create: `engine/helengine.editor/components/ui/EditorPreferencesDialog.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor.tests/EditorTitleBarAddMenuTests.cs`
- Create: `engine/helengine.editor.tests/EditorPreferencesDialogTests.cs`
- Create: `engine/helengine.editor.tests/EditorSessionPreferencesTests.cs`

### Host integration and live scale application
- Create: `engine/helengine.editor/managers/preferences/EditorUiScaleController.cs`
- Modify: `helengine.ui/helengine.editor.app/MainForm.cs`

The CLI build entrypoint in `helengine.ui/helengine.editor.app/Program.cs` intentionally stays unchanged in this slice because headless builds do not render the editor UI.

## Task 1: Add the global preferences document, service, and scale resolution rules

**Files:**
- Create: `engine/helengine.editor/managers/preferences/EditorUiScaleMode.cs`
- Create: `engine/helengine.editor/managers/preferences/EditorUiScaleSettings.cs`
- Create: `engine/helengine.editor/managers/preferences/EditorPreferencesDocument.cs`
- Create: `engine/helengine.editor/managers/preferences/EditorPreferencesService.cs`
- Create: `engine/helengine.editor.tests/EditorPreferencesServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Add one focused service test file that proves the persistence contract and the override semantics:

```csharp
public sealed class EditorPreferencesServiceTests : IDisposable {
    readonly string TempSettingsRootPath;

    public EditorPreferencesServiceTests() {
        TempSettingsRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-preferences-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempSettingsRootPath);
    }

    [Fact]
    public void Load_WhenPreferencesFileIsMissing_ReturnsAutoAndCreatesDocument() {
        EditorPreferencesService service = new EditorPreferencesService(TempSettingsRootPath);

        EditorUiScaleSettings settings = service.Load();

        Assert.Equal(EditorUiScaleMode.Auto, settings.Mode);
        Assert.Equal(100, settings.OverridePercent);
        Assert.True(File.Exists(Path.Combine(TempSettingsRootPath, "preferences.json")));
    }

    [Fact]
    public void Load_WhenPreferencesFileContainsOverride_ReturnsStoredOverride() {
        File.WriteAllText(
            Path.Combine(TempSettingsRootPath, "preferences.json"),
            """
            {
              "uiScaleMode": "Override",
              "uiScalePercent": 150
            }
            """);
        EditorPreferencesService service = new EditorPreferencesService(TempSettingsRootPath);

        EditorUiScaleSettings settings = service.Load();

        Assert.Equal(EditorUiScaleMode.Override, settings.Mode);
        Assert.Equal(150, settings.OverridePercent);
        Assert.Equal(1.5, settings.ResolveEffectiveScale(192));
    }

    [Fact]
    public void ResolveEffectiveScale_WhenModeIsOverride_IgnoresMonitorDpi() {
        EditorUiScaleSettings settings = new EditorUiScaleSettings(EditorUiScaleMode.Override, 125);

        Assert.Equal(1.25, settings.ResolveEffectiveScale(96));
        Assert.Equal(1.25, settings.ResolveEffectiveScale(192));
    }

    [Fact]
    public void Load_WhenPreferencesFileIsMalformed_RewritesDefaultDocument() {
        string preferencesFilePath = Path.Combine(TempSettingsRootPath, "preferences.json");
        File.WriteAllText(preferencesFilePath, "{ invalid json");
        EditorPreferencesService service = new EditorPreferencesService(TempSettingsRootPath);

        EditorUiScaleSettings settings = service.Load();

        Assert.Equal(EditorUiScaleMode.Auto, settings.Mode);
        Assert.Contains("\"uiScaleMode\": \"Auto\"", File.ReadAllText(preferencesFilePath));
    }

    [Fact]
    public void Load_WhenPreferencesFileContainsUnsupportedPercent_RewritesDefaultDocument() {
        string preferencesFilePath = Path.Combine(TempSettingsRootPath, "preferences.json");
        File.WriteAllText(
            preferencesFilePath,
            """
            {
              "uiScaleMode": "Override",
              "uiScalePercent": 90
            }
            """);
        EditorPreferencesService service = new EditorPreferencesService(TempSettingsRootPath);

        EditorUiScaleSettings settings = service.Load();

        Assert.Equal(EditorUiScaleMode.Auto, settings.Mode);
        Assert.Contains("\"uiScalePercent\": 100", File.ReadAllText(preferencesFilePath));
    }
}
```

- [ ] **Step 2: Run the targeted test to verify it fails**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~EditorPreferencesServiceTests -v minimal
```

Expected: FAIL because the preferences types and service do not exist yet.

- [ ] **Step 3: Write the minimal implementation**

Create the persistence and resolution types with explicit validation and default regeneration:

```csharp
namespace helengine.editor {
    /// <summary>
    /// Controls whether the editor uses monitor DPI or one explicit user-selected UI scale.
    /// </summary>
    public enum EditorUiScaleMode {
        Auto,
        Override
    }

    /// <summary>
    /// Stores one validated editor UI scale selection.
    /// </summary>
    public sealed class EditorUiScaleSettings {
        static readonly int[] SupportedPercents = [75, 100, 125, 150, 175, 200];

        public EditorUiScaleSettings(EditorUiScaleMode mode, int overridePercent) {
            if (!IsSupportedPercent(overridePercent)) {
                throw new ArgumentOutOfRangeException(nameof(overridePercent), "UI scale override percent must be one of the supported editor values.");
            }

            Mode = mode;
            OverridePercent = overridePercent;
        }

        public EditorUiScaleMode Mode { get; }

        public int OverridePercent { get; }

        public double ResolveEffectiveScale(int monitorDpi) {
            if (monitorDpi <= 0) {
                throw new ArgumentOutOfRangeException(nameof(monitorDpi), "Monitor DPI must be greater than zero.");
            }

            if (Mode == EditorUiScaleMode.Auto) {
                return monitorDpi / 96d;
            }

            return OverridePercent / 100d;
        }

        static bool IsSupportedPercent(int percent) {
            for (int index = 0; index < SupportedPercents.Length; index++) {
                if (SupportedPercents[index] == percent) {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Stores the persisted editor-global UI scale preference document.
    /// </summary>
    public sealed class EditorPreferencesDocument {
        public EditorUiScaleMode UiScaleMode { get; set; } = EditorUiScaleMode.Auto;

        public int UiScalePercent { get; set; } = 100;
    }

    /// <summary>
    /// Loads and persists editor-global preferences stored in one JSON file.
    /// </summary>
    public sealed class EditorPreferencesService {
        static JsonSerializerOptions JsonSerializerOptions { get; } = new() {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        readonly string PreferencesRootPath;

        string PreferencesFilePath => Path.Combine(PreferencesRootPath, "preferences.json");

        public EditorPreferencesService(string preferencesRootPath) {
            if (string.IsNullOrWhiteSpace(preferencesRootPath)) {
                throw new ArgumentException("Preferences root path must be provided.", nameof(preferencesRootPath));
            }

            PreferencesRootPath = Path.GetFullPath(preferencesRootPath);
        }

        public EditorUiScaleSettings Load() {
            EditorPreferencesDocument document = TryLoadDocument();
            if (document == null) {
                EditorUiScaleSettings defaults = new EditorUiScaleSettings(EditorUiScaleMode.Auto, 100);
                Save(defaults);
                return defaults;
            }

            EditorUiScaleSettings settings = new EditorUiScaleSettings(document.UiScaleMode, document.UiScalePercent);
            Save(settings);
            return settings;
        }

        public void Save(EditorUiScaleSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            Directory.CreateDirectory(PreferencesRootPath);
            EditorPreferencesDocument document = new EditorPreferencesDocument {
                UiScaleMode = settings.Mode,
                UiScalePercent = settings.OverridePercent
            };
            File.WriteAllText(PreferencesFilePath, JsonSerializer.Serialize(document, JsonSerializerOptions));
        }

        EditorPreferencesDocument TryLoadDocument() {
            if (!File.Exists(PreferencesFilePath)) {
                return null;
            }

            try {
                string json = File.ReadAllText(PreferencesFilePath);
                return JsonSerializer.Deserialize<EditorPreferencesDocument>(json, JsonSerializerOptions);
            } catch {
                return null;
            }
        }
    }
}
```

- [ ] **Step 4: Run the targeted test to verify it passes**

Run the same `dotnet test` command again.

Expected: PASS. The service should now create defaults, round-trip override values, and rewrite malformed input.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/preferences/EditorUiScaleMode.cs engine/helengine.editor/managers/preferences/EditorUiScaleSettings.cs engine/helengine.editor/managers/preferences/EditorPreferencesDocument.cs engine/helengine.editor/managers/preferences/EditorPreferencesService.cs engine/helengine.editor.tests/EditorPreferencesServiceTests.cs
git commit -m "feat: add editor ui scale preferences service"
```

## Task 2: Introduce `EditorUiMetrics` and make title-bar and dock chrome scale-aware

**Files:**
- Create: `engine/helengine.editor/managers/preferences/EditorUiMetrics.cs`
- Modify: `engine/helengine.editor/components/ui/EditorTitleBar.cs`
- Modify: `engine/helengine.editor/components/ui/dock/DockableEntity.cs`
- Modify: `engine/helengine.editor/components/ui/dock/DockTabStrip.cs`
- Modify: `engine/helengine.editor/managers/dock/DockLayoutEngine.cs`
- Modify: `engine/helengine.editor.tests/EditorTitleBarTests.cs`
- Create: `engine/helengine.editor.tests/EditorUiMetricsTests.cs`

- [ ] **Step 1: Write the failing tests**

Add one metrics unit test and one title-bar chrome test that proves scaled values are used:

```csharp
[Fact]
public void Constructor_WhenScaleIsOnePointFive_ScalesSharedChromeMetrics() {
    EditorUiMetrics metrics = new EditorUiMetrics(1.5);

    Assert.Equal(18, metrics.UiFontPixelSize);
    Assert.Equal(23, metrics.SnapModifierFontPixelSize);
    Assert.Equal(54, metrics.HostTitleBarHeight);
    Assert.Equal(30, metrics.DockTitleBarHeight);
}

[Fact]
public void Constructor_WithScaledMetrics_RendersEditorLogoUsingScaledPaddingAndSize() {
    InitializeCore();
    RuntimeTexture iconTexture = new TestRuntimeTexture {
        Width = 128,
        Height = 128
    };
    EditorUiMetrics metrics = new EditorUiMetrics(1.5);

    EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), metrics, 1280, 720, "Main Editor Title", iconTexture);

    EditorEntity iconEntity = GetPrivateField<EditorEntity>(titleBar, "IconEntity");
    SpriteComponent iconSprite = GetPrivateField<SpriteComponent>(titleBar, "IconSprite");

    Assert.Equal(9f, iconEntity.Position.X);
    Assert.Equal(9f, iconEntity.Position.Y);
    Assert.Equal(new int2(36, 36), iconSprite.Size);
    Assert.Equal(54, titleBar.Height);
}
```

- [ ] **Step 2: Run the targeted tests to verify they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~EditorUiMetricsTests\|FullyQualifiedName~EditorTitleBarTests -v minimal
```

Expected: FAIL because `EditorUiMetrics` does not exist and `EditorTitleBar` still hard-codes unscaled constants.

- [ ] **Step 3: Write the minimal implementation**

Add one metrics type and update the title bar and dock foundation to consume it while keeping the old constructors delegating to `EditorUiMetrics.Default`:

```csharp
public sealed class EditorUiMetrics {
    public static EditorUiMetrics Default { get; } = new EditorUiMetrics(1d);

    public EditorUiMetrics(double scale) {
        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0d) {
            throw new ArgumentOutOfRangeException(nameof(scale), "Editor UI scale must be a finite value greater than zero.");
        }

        Scale = scale;
    }

    public double Scale { get; }

    public int UiFontPixelSize => ScalePixels(12);

    public int SnapModifierFontPixelSize => ScalePixels(15);

    public int HostTitleBarHeight => ScalePixels(EditorTitleBar.HeightPixels);

    public int HostTitleBarIconSize => ScalePixels(24);

    public int HostTitleBarIconPadding => ScalePixels(6);

    public int DockTitleBarHeight => ScalePixels(DockableEntity.TitleBarHeight);

    public int DialogHeaderHeight => ScalePixels(32);

    public int ScalePixels(int pixels) {
        return Math.Max(1, (int)Math.Round(pixels * Scale));
    }
}

public class EditorTitleBar {
    FontAsset Font;
    EditorUiMetrics Metrics;

    public EditorTitleBar(FontAsset font, int windowWidth, int windowHeight, string titleText, RuntimeTexture iconTexture = null)
        : this(font, EditorUiMetrics.Default, windowWidth, windowHeight, titleText, iconTexture) {
    }

    public EditorTitleBar(FontAsset font, EditorUiMetrics metrics, int windowWidth, int windowHeight, string titleText, RuntimeTexture iconTexture = null) {
        Font = font ?? throw new ArgumentNullException(nameof(font));
        Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        TitleValue = titleText ?? string.Empty;
        BackgroundOrder = RenderOrder2D.PanelSurface;
        TextOrder = RenderOrder2D.PanelForeground;
        InputSurfaceOrder = RenderOrder2D.OverlayInput;
        HostSize = new int2(Math.Max(1, windowWidth), Math.Max(Metrics.HostTitleBarHeight, windowHeight));
        Background = new SpriteComponent {
            Texture = TextureUtils.PixelTexture,
            Color = ThemeManager.Colors.SurfacePrimary,
            Size = new int2(HostSize.X, Metrics.HostTitleBarHeight),
            RenderOrder2D = BackgroundOrder
        };
        IconEntity.Position = new float3(Metrics.HostTitleBarIconPadding, Metrics.HostTitleBarIconPadding, 0f);
        IconSprite.Size = new int2(Metrics.HostTitleBarIconSize, Metrics.HostTitleBarIconSize);
    }

    public int Height => Metrics.HostTitleBarHeight;

    public void ApplyUiMetrics(FontAsset font, EditorUiMetrics metrics) {
        if (font == null) {
            throw new ArgumentNullException(nameof(font));
        }
        if (metrics == null) {
            throw new ArgumentNullException(nameof(metrics));
        }

        Font = font;
        Metrics = metrics;
        TitleTextComponent.Font = font;
        UpdateLayout(HostSize.X, HostSize.Y);
    }
}
```

Mirror the same pattern in `DockableEntity`: keep `TitleBarHeight` as the unscaled base constant, add one `EditorUiMetrics Metrics` field, expose one instance `TitleBarHeightPixels` property, and replace direct size/layout math in `Size`, `OnSizeChanged`, and `DockLayoutEngine` call sites with the scaled property.

- [ ] **Step 4: Run the targeted tests to verify they pass**

Run the same `dotnet test` command again.

Expected: PASS. Shared scale math should be deterministic, and the title bar should now expose scaled height, icon padding, and icon size.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/preferences/EditorUiMetrics.cs engine/helengine.editor/components/ui/EditorTitleBar.cs engine/helengine.editor/components/ui/dock/DockableEntity.cs engine/helengine.editor/components/ui/dock/DockTabStrip.cs engine/helengine.editor/managers/dock/DockLayoutEngine.cs engine/helengine.editor.tests/EditorTitleBarTests.cs engine/helengine.editor.tests/EditorUiMetricsTests.cs
git commit -m "feat: scale editor title bar and dock chrome"
```

## Task 3: Propagate metrics into modal dialogs and the first-pass dock panels

**Files:**
- Modify: `engine/helengine.editor/components/ui/EditorDialogBase.cs`
- Modify: `engine/helengine.editor/components/ui/BuildSettingsDialog.cs`
- Modify: `engine/helengine.editor/components/ui/BuildDialog.cs`
- Modify: `engine/helengine.editor/components/ui/BuildDialogCopySettingsDialog.cs`
- Modify: `engine/helengine.editor/components/ui/ProfilesDialog.cs`
- Modify: `engine/helengine.editor/components/ui/ReparentEntityDialog.cs`
- Modify: `engine/helengine.editor/components/ui/UnsavedChangesDialog.cs`
- Modify: `engine/helengine.editor/components/ui/asset/OpenFileDialog.cs`
- Modify: `engine/helengine.editor/components/ui/asset/SaveFileDialog.cs`
- Modify: `engine/helengine.editor/components/ui/asset/AssetPickerModal.cs`
- Modify: `engine/helengine.editor/components/ui/SceneHierarchyPanel.cs`
- Modify: `engine/helengine.editor/components/ui/PropertiesPanel.cs`
- Modify: `engine/helengine.editor/components/ui/PreviewPanel.cs`
- Modify: `engine/helengine.editor/components/ui/LoggerPanel.cs`
- Modify: `engine/helengine.editor/components/ui/EditorViewport.cs`
- Modify: `engine/helengine.editor.tests/BuildSettingsDialogTests.cs`

- [ ] **Step 1: Write the failing tests**

Extend the dialog tests so they prove scaled metrics change the actual layout sizes instead of only storing the metrics object:

```csharp
[Fact]
public void Constructor_WithScaledMetrics_UsesScaledHeaderAndPanelSize() {
    BuildSettingsDialog dialog = new BuildSettingsDialog(CreateFont(), new EditorUiMetrics(1.5));
    RoundedRectComponent panelBackground = GetPrivateField<RoundedRectComponent>(dialog, "PanelBackground");
    SpriteComponent headerBackground = GetPrivateField<SpriteComponent>(dialog, "HeaderBackground");

    Assert.Equal(new int2(630, 354), panelBackground.Size);
    Assert.Equal(48, headerBackground.Size.Y);
}

[Fact]
public void UpdateLayout_WithScaledMetrics_UsesScaledDialogChrome() {
    BuildSettingsDialog dialog = new BuildSettingsDialog(CreateFont(), new EditorUiMetrics(1.5));
    dialog.Show(CreateAvailablePlatforms("windows"), new List<string> { "windows" });
    dialog.UpdateLayout(1280, 720);

    EditorEntity cancelButtonHost = GetPrivateField<EditorEntity>(dialog, "CancelButtonHost");
    ButtonComponent cancelButton = GetPrivateField<ButtonComponent>(dialog, "CancelButton");

    Assert.Equal(new int2(132, 33), cancelButton.Size);
    Assert.True(cancelButtonHost.Position.Y > 0f);
}
```

- [ ] **Step 2: Run the targeted test to verify it fails**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~BuildSettingsDialogTests -v minimal
```

Expected: FAIL because the dialog constructors still bake fixed widths, heights, and button sizes.

- [ ] **Step 3: Write the minimal implementation**

Thread `EditorUiMetrics` through the dialog base, dialog subclasses, and the first-pass dock panels, while preserving the old one-argument constructors for existing callers:

```csharp
public abstract class EditorDialogBase : EditorEntity, IAnchorBoundsProvider {
    FontAsset Font;
    EditorUiMetrics Metrics;
    readonly int BaseDialogWidth;
    readonly int BaseDialogHeight;
    readonly int BaseDialogHeaderHeight;

    protected EditorDialogBase(string dialogName, string dialogTitle, FontAsset font, int dialogWidth, int dialogHeight, int dialogHeaderHeight)
        : this(dialogName, dialogTitle, font, EditorUiMetrics.Default, dialogWidth, dialogHeight, dialogHeaderHeight) {
    }

    protected EditorDialogBase(string dialogName, string dialogTitle, FontAsset font, EditorUiMetrics metrics, int dialogWidth, int dialogHeight, int dialogHeaderHeight) {
        Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        BaseDialogWidth = dialogWidth;
        BaseDialogHeight = dialogHeight;
        BaseDialogHeaderHeight = dialogHeaderHeight;
        DialogWidth = Metrics.ScalePixels(BaseDialogWidth);
        DialogHeight = Metrics.ScalePixels(BaseDialogHeight);
        DialogHeaderHeight = Metrics.ScalePixels(BaseDialogHeaderHeight);
        PanelBackground.Size = new int2(DialogWidth, DialogHeight);
        HeaderBackground.Size = new int2(DialogWidth - CloseButtonWidth, DialogHeaderHeight);
        CloseButtonHost.Position = new float3(DialogWidth - CloseButtonWidth, 0f, 0.2f);
    }

    public void ApplyUiMetrics(FontAsset font, EditorUiMetrics metrics) {
        Font = font ?? throw new ArgumentNullException(nameof(font));
        Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        DialogWidth = Metrics.ScalePixels(BaseDialogWidth);
        DialogHeight = Metrics.ScalePixels(BaseDialogHeight);
        DialogHeaderHeight = Metrics.ScalePixels(BaseDialogHeaderHeight);
        UpdateLayout(HostSize.X, HostSize.Y);
    }
}

public class BuildSettingsDialog : EditorDialogBase {
    public BuildSettingsDialog(FontAsset font) : this(font, EditorUiMetrics.Default) {
    }

    public BuildSettingsDialog(FontAsset font, EditorUiMetrics metrics)
        : base("BuildSettingsDialog", "Build Platforms", font, metrics, PanelWidth, PanelHeight, HeaderHeight) {
        DialogMinimumSize = new int2(metrics.ScalePixels(PanelWidth), metrics.ScalePixels(PanelHeight));
        CancelButton = new ButtonComponent("Cancel", new int2(metrics.ScalePixels(88), metrics.ScalePixels(22)), DialogFont, HandleCancelClicked, 0f);
        SaveButton = new ButtonComponent("Save", new int2(metrics.ScalePixels(88), metrics.ScalePixels(22)), DialogFont, HandleSaveClicked, 0f);
    }
}
```

For the rest of this task, make the same change explicitly in each first-pass consumer:
- `BuildDialog`, `BuildDialogCopySettingsDialog`, `ProfilesDialog`, `ReparentEntityDialog`, and `UnsavedChangesDialog`: add one `EditorUiMetrics` constructor overload, scale their fixed panel/button sizes from base constants, and add one `ApplyUiMetrics(FontAsset font, EditorUiMetrics metrics)` method that updates fonts plus `DialogMinimumSize`.
- `OpenFileDialog`, `SaveFileDialog`, and `AssetPickerModal`: keep their base min/max constants unscaled, then scale `PanelSize`, header height, footer button sizes, and content spacing through the new metrics object during `Show()` and `UpdateLayout(...)`.
- `SceneHierarchyPanel`, `PropertiesPanel`, `PreviewPanel`, `LoggerPanel`, and `EditorViewport`: add metrics-aware constructor overloads plus one `ApplyUiMetrics(...)` method, then replace direct offsets such as `contentRoot.Position = new float3(0, TitleBarHeight, 0.05f);` with `contentRoot.Position = new float3(0, TitleBarHeightPixels, 0.05f);`.

- [ ] **Step 4: Run the targeted test to verify it passes**

Run the same `dotnet test` command again.

Expected: PASS. The dialog base and first-pass panel chrome should now respect scaled widths, heights, headers, and button sizes.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/components/ui/EditorDialogBase.cs engine/helengine.editor/components/ui/BuildSettingsDialog.cs engine/helengine.editor/components/ui/BuildDialog.cs engine/helengine.editor/components/ui/BuildDialogCopySettingsDialog.cs engine/helengine.editor/components/ui/ProfilesDialog.cs engine/helengine.editor/components/ui/ReparentEntityDialog.cs engine/helengine.editor/components/ui/UnsavedChangesDialog.cs engine/helengine.editor/components/ui/asset/OpenFileDialog.cs engine/helengine.editor/components/ui/asset/SaveFileDialog.cs engine/helengine.editor/components/ui/asset/AssetPickerModal.cs engine/helengine.editor/components/ui/SceneHierarchyPanel.cs engine/helengine.editor/components/ui/PropertiesPanel.cs engine/helengine.editor/components/ui/PreviewPanel.cs engine/helengine.editor/components/ui/LoggerPanel.cs engine/helengine.editor/components/ui/EditorViewport.cs engine/helengine.editor.tests/BuildSettingsDialogTests.cs
git commit -m "feat: scale editor dialogs and panel layout"
```

## Task 4: Add the Preferences dialog and wire it through the File menu and editor session

**Files:**
- Create: `engine/helengine.editor/components/ui/EditorPreferencesDialog.cs`
- Modify: `engine/helengine.editor/components/ui/EditorTitleBar.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor.tests/EditorTitleBarAddMenuTests.cs`
- Create: `engine/helengine.editor.tests/EditorPreferencesDialogTests.cs`
- Create: `engine/helengine.editor.tests/EditorSessionPreferencesTests.cs`

- [ ] **Step 1: Write the failing tests**

Add one title-bar menu test, one modal test, and one editor-session wiring test:

```csharp
[Fact]
public void ToggleFileMenu_ShowsPreferencesAfterSaveMapAs() {
    EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Hel");

    InvokePrivate(titleBar, "ToggleFileMenu");

    ContextMenu fileMenu = GetPrivateField<ContextMenu>(titleBar, "FileMenu");
    List<ContextMenuItem> activeItems = GetPrivateField<List<ContextMenuItem>>(fileMenu, "ActiveItems");

    Assert.Collection(
        activeItems,
        item => Assert.Equal("New Map", item.Label),
        item => Assert.Equal("Open Map...", item.Label),
        item => Assert.Equal("Save Map", item.Label),
        item => Assert.Equal("Save Map As...", item.Label),
        item => Assert.Equal("Preferences...", item.Label));
}

[Fact]
public void Show_WhenOverrideModeSelected_EnablesOverridePercentSelector() {
    EditorPreferencesDialog dialog = new EditorPreferencesDialog(CreateFont(), new EditorUiMetrics(1d));

    dialog.Show(new EditorUiScaleSettings(EditorUiScaleMode.Override, 150));

    ComboBoxComponent scaleModeComboBox = GetPrivateField<ComboBoxComponent>(dialog, "ScaleModeComboBox");
    ComboBoxComponent scalePercentComboBox = GetPrivateField<ComboBoxComponent>(dialog, "ScalePercentComboBox");

    Assert.Equal("Override", scaleModeComboBox.SelectedItem);
    Assert.True(scalePercentComboBox.Enabled);
    Assert.Equal("150%", scalePercentComboBox.SelectedItem);
}

[Fact]
public void HandlePreferencesDialogConfirmed_WhenInvoked_RaisesUiScaleSettingsChanged() {
    EditorSession session = CreateSessionForPreferences();
    EditorUiScaleSettings raisedSettings = null;
    session.UiScaleSettingsChanged += settings => raisedSettings = settings;

    InvokePrivate(session, "HandlePreferencesDialogConfirmed", new EditorUiScaleSettings(EditorUiScaleMode.Override, 175));

    Assert.NotNull(raisedSettings);
    Assert.Equal(EditorUiScaleMode.Override, raisedSettings.Mode);
    Assert.Equal(175, raisedSettings.OverridePercent);
}

EditorSession CreateSessionForPreferences() {
    EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));
    SetPrivateField(session, "preferencesDialog", new EditorPreferencesDialog(CreateFont(), EditorUiMetrics.Default));
    SetPrivateField(session, "CurrentUiScaleSettings", new EditorUiScaleSettings(EditorUiScaleMode.Auto, 100));
    return session;
}
```

- [ ] **Step 2: Run the targeted tests to verify they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~EditorTitleBarAddMenuTests\|FullyQualifiedName~EditorPreferencesDialogTests\|FullyQualifiedName~EditorSessionPreferencesTests -v minimal
```

Expected: FAIL because the File menu has no `Preferences...` entry, the dialog does not exist, and `EditorSession` does not yet surface a UI-scale change event.

- [ ] **Step 3: Write the minimal implementation**

Add the focused preferences modal and route it through the existing title-bar/session command flow:

```csharp
public sealed class EditorPreferencesDialog : EditorDialogBase {
    readonly ComboBoxComponent ScaleModeComboBox;
    readonly ComboBoxComponent ScalePercentComboBox;
    readonly ButtonComponent ApplyButton;
    readonly ButtonComponent CancelButton;
    EditorUiScaleSettings CurrentSettings;

    public event Action<EditorUiScaleSettings> ConfirmRequested;
    public event Action CancelRequested;

    public EditorPreferencesDialog(FontAsset font, EditorUiMetrics metrics)
        : base("EditorPreferencesDialog", "Preferences", font, metrics, 360, 200, 32) {
        ScaleModeComboBox = new ComboBoxComponent(new int2(220, 24), DialogFont, new[] { "Auto", "Override" }, 0);
        ScalePercentComboBox = new ComboBoxComponent(new int2(220, 24), DialogFont, new[] { "75%", "100%", "125%", "150%", "175%", "200%" }, 1);
        ApplyButton = new ButtonComponent("Apply", new int2(88, 22), DialogFont, HandleApplyClicked, 0f);
        CancelButton = new ButtonComponent("Cancel", new int2(88, 22), DialogFont, HandleCancelClicked, 0f);
        ScaleModeComboBox.SelectionChanged += HandleScaleModeSelectionChanged;
    }

    public void Show(EditorUiScaleSettings settings) {
        if (settings == null) {
            throw new ArgumentNullException(nameof(settings));
        }

        CurrentSettings = settings;
        ScaleModeComboBox.SelectedItem = settings.Mode.ToString();
        ScalePercentComboBox.SelectedItem = settings.OverridePercent.ToString() + "%";
        ScalePercentComboBox.Enabled = settings.Mode == EditorUiScaleMode.Override;
        Enabled = true;
    }

    void HandleApplyClicked() {
        EditorUiScaleMode mode = string.Equals(ScaleModeComboBox.SelectedItem, "Override", StringComparison.Ordinal) ? EditorUiScaleMode.Override : EditorUiScaleMode.Auto;
        int percent = int.Parse(ScalePercentComboBox.SelectedItem.TrimEnd('%'));
        ConfirmRequested?.Invoke(new EditorUiScaleSettings(mode, percent));
    }

    void HandleCancelClicked() {
        Hide();
        CancelRequested?.Invoke();
    }

    void HandleScaleModeSelectionChanged(string selectedItem) {
        ScalePercentComboBox.Enabled = string.Equals(selectedItem, "Override", StringComparison.Ordinal);
    }
}

public class EditorTitleBar {
    public event Action PreferencesRequested;

    IReadOnlyList<ContextMenuItem> BuildFileMenuItems() {
        return new ContextMenuItem[] {
            new ContextMenuItem("New Map", RaiseNewMapRequested),
            new ContextMenuItem("Open Map...", RaiseOpenMapRequested),
            new ContextMenuItem("Save Map", RaiseSaveMapRequested),
            new ContextMenuItem("Save Map As...", RaiseSaveMapAsRequested),
            new ContextMenuItem("Preferences...", RaisePreferencesRequested)
        };
    }
}

public class EditorSession {
    readonly EditorPreferencesDialog preferencesDialog;
    EditorUiScaleSettings CurrentUiScaleSettings;

    public event Action<EditorUiScaleSettings> UiScaleSettingsChanged;

    void HandlePreferencesRequested() {
        preferencesDialog.Show(CurrentUiScaleSettings);
    }

    void HandlePreferencesDialogConfirmed(EditorUiScaleSettings settings) {
        if (settings == null) {
            throw new ArgumentNullException(nameof(settings));
        }

        CurrentUiScaleSettings = settings;
        preferencesDialog.Hide();
        UiScaleSettingsChanged?.Invoke(settings);
    }
}
```

In `EditorSession`, instantiate `preferencesDialog`, subscribe to `titleBar.PreferencesRequested`, and keep the current scale settings available so reopening the dialog reflects the current mode and percent.

- [ ] **Step 4: Run the targeted tests to verify they pass**

Run the same `dotnet test` command again.

Expected: PASS. The File menu should expose `Preferences...`, the modal should reflect `Auto` versus `Override`, and the session should publish confirmed scale changes.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/components/ui/EditorPreferencesDialog.cs engine/helengine.editor/components/ui/EditorTitleBar.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/EditorTitleBarAddMenuTests.cs engine/helengine.editor.tests/EditorPreferencesDialogTests.cs engine/helengine.editor.tests/EditorSessionPreferencesTests.cs
git commit -m "feat: add editor preferences dialog"
```

## Task 5: Apply the preference live in `MainForm` and refresh the current editor session

**Files:**
- Create: `engine/helengine.editor/managers/preferences/EditorUiScaleController.cs`
- Modify: `helengine.ui/helengine.editor.app/MainForm.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionPreferencesTests.cs`

- [ ] **Step 1: Write the failing tests**

Extend `EditorSessionPreferencesTests` so one integration-style session test proves a live scale update refreshes editor chrome:

```csharp
[Fact]
public void ApplyUiScale_WhenCalled_UpdatesScaledTitleBarAndDialogChrome() {
    EditorUiMetrics initialMetrics = new EditorUiMetrics(1d);
    EditorUiMetrics scaledMetrics = new EditorUiMetrics(1.5);
    EditorSession session = CreateSessionForPreferences(initialMetrics);
    FontAsset uiFont = CreateFont(18f);
    FontAsset snapFont = CreateFont(23f);

    session.ApplyUiScale(new EditorUiScaleSettings(EditorUiScaleMode.Override, 150), scaledMetrics, uiFont, snapFont);

    EditorTitleBar titleBar = GetPrivateField<EditorTitleBar>(session, "titleBar");
    EditorPreferencesDialog dialog = GetPrivateField<EditorPreferencesDialog>(session, "preferencesDialog");

    Assert.Equal(54, titleBar.Height);
    Assert.Equal(new int2(540, 300), dialog.DialogMinimumSize);
}

EditorSession CreateSessionForPreferences(EditorUiMetrics metrics) {
    EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));
    SetPrivateField(session, "titleBar", new EditorTitleBar(CreateFont(16f), metrics, 1280, 720, "Hel"));
    SetPrivateField(session, "preferencesDialog", new EditorPreferencesDialog(CreateFont(16f), metrics));
    return session;
}
```

This test gives the feature one live-session assertion instead of only checking stored preferences or isolated widgets.

- [ ] **Step 2: Run the targeted test to verify it fails**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~EditorSessionPreferencesTests -v minimal
```

Expected: FAIL because `EditorSession` has no `ApplyUiScale(...)` path and `MainForm` never rebuilds fonts or metrics after startup.

- [ ] **Step 3: Write the minimal implementation**

Add one shared editor-side scale controller plus one thin WinForms host adapter and one session refresh method:

```csharp
public sealed class EditorUiScaleController {
    readonly EditorPreferencesService PreferencesService;
    EditorUiScaleSettings CurrentSettings;

    public EditorUiScaleController(EditorPreferencesService preferencesService) {
        PreferencesService = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));
        CurrentSettings = PreferencesService.Load();
    }

    public EditorUiScaleSettings Load() {
        CurrentSettings = PreferencesService.Load();
        return CurrentSettings;
    }

    public EditorUiMetrics ResolveMetrics(int monitorDpi) {
        return new EditorUiMetrics(CurrentSettings.ResolveEffectiveScale(monitorDpi));
    }

    public EditorUiScaleSettings ApplyUserSelection(EditorUiScaleSettings settings) {
        if (settings == null) {
            throw new ArgumentNullException(nameof(settings));
        }

        PreferencesService.Save(settings);
        CurrentSettings = settings;
        return CurrentSettings;
    }

    public bool ShouldReapplyForMonitorDpiChange() {
        return CurrentSettings.Mode == EditorUiScaleMode.Auto;
    }
}

public partial class MainForm : Form, IResizeBorderState, ITitleBarDragRestoreState, IWindowForegroundState {
    EditorUiScaleController uiScaleController;

    private void InitializeEditor() {
        uiScaleController = new EditorUiScaleController(new EditorPreferencesService(ResolveEditorPreferencesRootPath()));
        EditorUiScaleSettings initialSettings = uiScaleController.Load();
        EditorUiMetrics initialMetrics = uiScaleController.ResolveMetrics(DeviceDpi);
        FontAsset uiFont = CreateUiFont(initialMetrics);
        FontAsset snapModifierFont = CreateSnapModifierFont(initialMetrics);
        ...
        editorSession.UiScaleSettingsChanged += HandleUiScaleSettingsChanged;
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e) {
        base.OnDpiChanged(e);

        if (uiScaleController.ShouldReapplyForMonitorDpiChange()) {
            ReapplyCurrentUiScale();
        }
    }

    void HandleUiScaleSettingsChanged(EditorUiScaleSettings settings) {
        uiScaleController.ApplyUserSelection(settings);
        ReapplyCurrentUiScale();
    }

    void ReapplyCurrentUiScale() {
        EditorUiScaleSettings settings = uiScaleController.Load();
        EditorUiMetrics metrics = uiScaleController.ResolveMetrics(DeviceDpi);
        FontAsset uiFont = CreateUiFont(metrics);
        FontAsset snapModifierFont = CreateSnapModifierFont(metrics);
        editorSession.ApplyUiScale(settings, metrics, uiFont, snapModifierFont);
        UpdateMinimumWindowSize();
    }
}
```

Use `MainForm` only for:
- resolving the per-host preferences root path
- observing `OnDpiChanged`
- creating host font assets from `EditorUiMetrics`

Keep the settings state machine and the auto-vs-override decision logic in `EditorUiScaleController` so the same editor-side flow can be reused by non-WinForms hosts later.

- [ ] **Step 4: Run the targeted test to verify it passes**

Run the same `dotnet test` command again.

Expected: PASS. The session-level refresh path should update scaled title-bar and dialog values without tearing down the process.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/preferences/EditorUiScaleController.cs helengine.ui/helengine.editor.app/MainForm.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/EditorSessionPreferencesTests.cs
git commit -m "feat: apply editor dpi scale live"
```

## Task 6: Run the focused verification slice and check the branch state

**Files:**
- No new files expected

- [ ] **Step 1: Run the focused DPI-scaling test slice**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~EditorPreferencesServiceTests\|FullyQualifiedName~EditorUiMetricsTests\|FullyQualifiedName~EditorTitleBarTests\|FullyQualifiedName~BuildSettingsDialogTests\|FullyQualifiedName~EditorPreferencesDialogTests\|FullyQualifiedName~EditorSessionPreferencesTests -v minimal
```

Expected: PASS for the new persistence, metrics, menu, dialog, and live-refresh coverage.

- [ ] **Step 2: Run one broader editor regression slice**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~EditorTitleBarAddMenuTests\|FullyQualifiedName~EditorTitleBarBuildMenuTests\|FullyQualifiedName~EditorSessionSceneOpenTests\|FullyQualifiedName~EditorSessionBuildSettingsTests -v minimal
```

Expected: PASS. File menu changes should not break existing title-bar, scene-open, or build-settings flows.

- [ ] **Step 3: Check branch status**

Run:

```bash
rtk git status --short
```

Expected: Only intentional DPI-scaling edits remain in the working tree.

- [ ] **Step 4: Commit the finished feature**

```bash
git add engine/helengine.editor/managers/preferences engine/helengine.editor/components/ui engine/helengine.editor/managers/dock engine/helengine.editor/EditorSession.cs helengine.ui/helengine.editor.app/MainForm.cs engine/helengine.editor.tests
git commit -m "feat: add editor dpi scaling preferences"
```
