# Explicit 2D Render Order Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace bucket-based 2D render ordering with explicit 2D render-order constants so editor menus, overlays, floating panels, and modals render in a deterministic front-to-back stack.

**Architecture:** Introduce one central `RenderOrder2D` table in core and migrate editor UI to explicit byte constants instead of `GetRenderOrderForLayer2D(...)`. Land the work in three vertical slices: first the title-bar/context-menu path that reproduces the `Add` menu bug, then the docked/floating panel stack, and finally the shared controls and modal surfaces before deleting the old 2D bucket API entirely.

**Tech Stack:** C#/.NET 9, Hel engine core/editor UI, xUnit, `dotnet test`

---

## File Structure

### New Files

- `engine/helengine.core/managers/rendering/RenderOrder2D.cs`
  Central explicit byte constants for 2D render order bands and floating bias.
- `engine/helengine.editor.tests/RenderOrder2DStackTests.cs`
  Regression coverage for floating dockables and modal-vs-overlay ordering.

### Modified Files

- `engine/helengine.editor/components/ui/EditorTitleBar.cs`
  Move title bar, buttons, menus, and input shields to explicit overlay constants.
- `engine/helengine.editor/components/ui/ContextMenu.cs`
  Move background, rows, labels, and blockers to explicit overlay constants.
- `engine/helengine.editor.tests/EditorTitleBarTests.cs`
  Add `Add` menu regression and switch render-order assertions to explicit constants.
- `engine/helengine.editor.tests/EditorTitleBarAddMenuTests.cs`
  Keep add-menu structure assertions aligned with the new overlay policy.
- `engine/helengine.editor/components/ui/dock/DockableEntity.cs`
  Replace bucket-derived dockable orders and floating boost with explicit constants and bias.
- `engine/helengine.editor/managers/dock/DockLayoutEngine.cs`
  Use explicit panel/text orders for layout-owned tab-strip visuals.
- `engine/helengine.editor/components/ui/dock/DockTabStrip.cs`
  Use explicit tab background/text orders.
- `engine/helengine.editor/components/ui/dock/DockPreviewOverlay.cs`
  Move docking preview into the overlay band.
- `engine/helengine.editor/components/ui/SceneHierarchyPanel.cs`
  Use explicit panel content orders.
- `engine/helengine.editor/components/ui/LoggerPanel.cs`
  Use explicit panel content orders.
- `engine/helengine.editor/components/ui/PropertiesPanel.cs`
  Use explicit panel content orders.
- `engine/helengine.editor/components/ui/PreviewPanel.cs`
  Use explicit panel content orders.
- `engine/helengine.editor/components/ui/MaterialAssetView.cs`
  Use explicit panel text orders.
- `engine/helengine.editor/components/ui/AssetImportSettingsView.cs`
  Use explicit panel text orders.
- `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
  Use explicit panel text orders for property rows and editor-time controls.
- `engine/helengine.editor/components/ui/asset/AssetBrowserPanel.cs`
  Use explicit panel and overlay orders for browser toolbar, rows, and context menus.
- `engine/helengine.editor/components/ui/EditorViewport.cs`
  Move viewport toolbar surfaces to explicit panel/overlay constants.
- `engine/helengine.editor/components/ui/EditorViewportCameraAngleOverlayComponent.cs`
  Move the camera-angle widget into the overlay band.
- `engine/helengine.editor.tests/DockTabStripTests.cs`
  Add assertions for explicit tab orders.
- `engine/helengine.core/components/2d/interactable/ButtonComponent.cs`
  Replace default bucket orders with explicit panel-surface/panel-text defaults.
- `engine/helengine.core/components/2d/interactable/ComboBoxComponent.cs`
  Replace main/list bucket orders with explicit panel and overlay constants.
- `engine/helengine.core/components/2d/interactable/TextBoxComponent.cs`
  Replace default bucket orders with explicit panel constants.
- `engine/helengine.editor/components/ui/asset/AssetPickerModal.cs`
  Move picker panel and browser content to explicit modal constants.
- `engine/helengine.editor/components/ui/asset/OpenFileDialog.cs`
  Move open-scene dialog panel and browser content to explicit modal constants.
- `engine/helengine.editor/components/ui/asset/SaveFileDialog.cs`
  Move save-scene dialog panel and browser content to explicit modal constants.
- `engine/helengine.editor/components/ui/UnsavedChangesDialog.cs`
  Move confirmation dialog to explicit modal constants.
- `engine/helengine.core/CoreInitializationOptions.cs`
  Remove `RenderOrderLayers2D`.
- `engine/helengine.core/managers/ObjectManager.cs`
  Remove `RenderOrderLayers2D` and `GetRenderOrderForLayer2D(...)`.
- `engine/helengine.editor.tests/OpenFileDialogTests.cs`
  Add modal-order assertions.
- `engine/helengine.editor.tests/SaveFileDialogTests.cs`
  Add modal-order assertions.
- `engine/helengine.editor.tests/UnsavedChangesDialogTests.cs`
  Add modal-order assertions.
- `engine/helengine.editor.tests/EditorSessionSceneOpenTests.cs`
  Keep guarded open/new-map flows green after modal migration.
- `engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs`
  Keep guarded save flows green after modal migration.

## Task 1: Introduce Explicit Overlay Orders And Fix The Title-Bar Menu Stack

**Files:**
- Create: `engine/helengine.core/managers/rendering/RenderOrder2D.cs`
- Modify: `engine/helengine.editor/components/ui/EditorTitleBar.cs`
- Modify: `engine/helengine.editor/components/ui/ContextMenu.cs`
- Modify: `engine/helengine.editor.tests/EditorTitleBarTests.cs`
- Modify: `engine/helengine.editor.tests/EditorTitleBarAddMenuTests.cs`

- [ ] **Step 1: Write the failing title-bar overlay regressions**

Extend `engine/helengine.editor.tests/EditorTitleBarTests.cs` with one test for the exact bug:

```csharp
/// <summary>
/// Ensures the Add menu uses the same overlay orders as the File menu and stays above docked panels.
/// </summary>
[Fact]
public void AddMenu_UsesOverlayRenderOrdersAboveDockPanels() {
    InitializeCore();
    EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Main Editor Title");
    ContextMenu addMenu = GetPrivateField<ContextMenu>(titleBar, "AddMenu");

    addMenu.Show(
        new[] {
            new ContextMenuItem("Cube", HandleMenuItemActivated)
        },
        new int2(0, 0),
        new int2(1280, 720));

    RoundedRectComponent menuBackground = FindComponent<RoundedRectComponent>(addMenu.Entity);
    TextComponent menuItemText = FindTextComponent(addMenu.Entity, "Cube");

    Assert.Equal(RenderOrder2D.OverlayBackground, menuBackground.RenderOrder2D);
    Assert.Equal(RenderOrder2D.OverlayForeground, menuItemText.RenderOrder2D);
}
```

Update the existing `FileMenu_UsesOverlayRenderOrdersAboveDockPanels()` assertion in the same file:

```csharp
Assert.Equal(RenderOrder2D.OverlayBackground, menuBackground.RenderOrder2D);
Assert.Equal(RenderOrder2D.OverlayForeground, menuItemText.RenderOrder2D);
```

Also update `CreateBackInteractable(...)` in the same test file so background content uses the future explicit constant instead of the old helper:

```csharp
SpriteComponent sprite = new SpriteComponent {
    Texture = TextureUtils.PixelTexture,
    Size = size,
    RenderOrder2D = RenderOrder2D.PanelBackground
};
```

- [ ] **Step 2: Run the title-bar tests to verify the Add-menu regression fails**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "EditorTitleBarTests|EditorTitleBarAddMenuTests"`

Expected: FAIL because `RenderOrder2D` does not exist yet and `AddMenu` still uses the title-bar background/text orders instead of overlay orders.

- [ ] **Step 3: Add the explicit 2D order table and migrate the title-bar path**

Create `engine/helengine.core/managers/rendering/RenderOrder2D.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Defines the explicit front-to-back render-order bands used by 2D engine and editor UI.
    /// </summary>
    public static class RenderOrder2D {
        /// <summary>
        /// Base order for docked panel content backgrounds.
        /// </summary>
        public const byte PanelBackground = 16;
        /// <summary>
        /// Base order for raised panel surfaces such as title bars and toolbar backgrounds.
        /// </summary>
        public const byte PanelSurface = 32;
        /// <summary>
        /// Base order for panel text, outlines, and foreground visuals.
        /// </summary>
        public const byte PanelForeground = 48;
        /// <summary>
        /// Base order for the highest non-overlay visuals that still belong to panel chrome.
        /// </summary>
        public const byte PanelInteractive = 64;
        /// <summary>
        /// Extra order applied to floating dockables so they rise above docked panels.
        /// </summary>
        public const byte FloatingPanelBias = 32;
        /// <summary>
        /// Base order for non-modal overlays such as context menus and viewport widgets.
        /// </summary>
        public const byte OverlayBackground = 160;
        /// <summary>
        /// Foreground order for non-modal overlays.
        /// </summary>
        public const byte OverlayForeground = 176;
        /// <summary>
        /// Highest non-modal order used by transparent input shields and blockers.
        /// </summary>
        public const byte OverlayInput = 192;
        /// <summary>
        /// Base order for modal panel surfaces.
        /// </summary>
        public const byte ModalBackground = 224;
        /// <summary>
        /// Foreground order for modal labels and buttons.
        /// </summary>
        public const byte ModalForeground = 240;
        /// <summary>
        /// Highest modal order used by modal-specific input surfaces.
        /// </summary>
        public const byte ModalInput = 248;
    }
}
```

Modify `engine/helengine.editor/components/ui/EditorTitleBar.cs` so the title-bar orders are explicit and the `AddMenu` matches the `FileMenu` overlay band:

```csharp
BackgroundOrder = RenderOrder2D.PanelSurface;
TextOrder = RenderOrder2D.PanelForeground;
InputSurfaceOrder = RenderOrder2D.OverlayInput;
```

and:

```csharp
FileMenu = new ContextMenu(Font, TitleBarLayerMask, RenderOrder2D.OverlayBackground, RenderOrder2D.OverlayForeground);
RootEntity.AddChild(FileMenu.Entity);
FileMenuItems = BuildFileMenuItems();
AddMenu = new ContextMenu(Font, TitleBarLayerMask, RenderOrder2D.OverlayBackground, RenderOrder2D.OverlayForeground);
RootEntity.AddChild(AddMenu.Entity);
AddMenuItems = BuildAddMenuItems();
```

Modify `engine/helengine.editor/components/ui/ContextMenu.cs` so blockers sit in the explicit overlay-input band and rows/text stay in the overlay bands:

```csharp
BackgroundBlockerSurface = new SpriteComponent {
    Texture = TextureUtils.PixelTexture,
    Color = new byte4(255, 255, 255, 0),
    Size = new int2(0, 0),
    RenderOrder2D = RenderOrder2D.OverlayInput
};
```

and keep row/background defaults explicit:

```csharp
var background = new SpriteComponent {
    Texture = TextureUtils.PixelTexture,
    Color = ThemeManager.Colors.SurfacePrimary,
    RenderOrder2D = Background.RenderOrder2D
};
```

```csharp
var label = new TextComponent {
    Font = Font,
    Text = string.Empty,
    Color = ThemeManager.Colors.InputForegroundPrimary,
    Size = new int2(1, 1),
    RenderOrder2D = TextOrder
};
```

- [ ] **Step 4: Run the title-bar tests to verify the overlay band is correct**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "EditorTitleBarTests|EditorTitleBarAddMenuTests"`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.core/managers/rendering/RenderOrder2D.cs engine/helengine.editor/components/ui/EditorTitleBar.cs engine/helengine.editor/components/ui/ContextMenu.cs engine/helengine.editor.tests/EditorTitleBarTests.cs engine/helengine.editor.tests/EditorTitleBarAddMenuTests.cs
git commit -m "refactor: add explicit overlay render orders"
```

## Task 2: Migrate Dockables, Panel Content, And Floating Bias

**Files:**
- Create: `engine/helengine.editor.tests/RenderOrder2DStackTests.cs`
- Modify: `engine/helengine.editor/components/ui/dock/DockableEntity.cs`
- Modify: `engine/helengine.editor/managers/dock/DockLayoutEngine.cs`
- Modify: `engine/helengine.editor/components/ui/dock/DockTabStrip.cs`
- Modify: `engine/helengine.editor/components/ui/dock/DockPreviewOverlay.cs`
- Modify: `engine/helengine.editor/components/ui/SceneHierarchyPanel.cs`
- Modify: `engine/helengine.editor/components/ui/LoggerPanel.cs`
- Modify: `engine/helengine.editor/components/ui/PropertiesPanel.cs`
- Modify: `engine/helengine.editor/components/ui/PreviewPanel.cs`
- Modify: `engine/helengine.editor/components/ui/MaterialAssetView.cs`
- Modify: `engine/helengine.editor/components/ui/AssetImportSettingsView.cs`
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Modify: `engine/helengine.editor/components/ui/asset/AssetBrowserPanel.cs`
- Modify: `engine/helengine.editor/components/ui/EditorViewport.cs`
- Modify: `engine/helengine.editor/components/ui/EditorViewportCameraAngleOverlayComponent.cs`
- Modify: `engine/helengine.editor.tests/DockTabStripTests.cs`

- [ ] **Step 1: Write the failing floating-panel and modal-stack tests**

Create `engine/helengine.editor.tests/RenderOrder2DStackTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the explicit editor 2D render stack for dockables, overlays, and modals.
    /// </summary>
    public class RenderOrder2DStackTests {
        /// <summary>
        /// Ensures floating dockables sit above docked panels after the bucket helper is removed.
        /// </summary>
        [Fact]
        public void FloatingDockable_AppliesExplicitBiasAboveDockedPanels() {
            InitializeCore();
            FontAsset font = CreateFont();
            DockableEntity docked = new DockableEntity(font);
            DockableEntity floating = new DockableEntity(font);

            docked.IsDocked = true;
            floating.IsDocked = false;

            SpriteComponent dockedTitleBar = FindTitleBarSprite(docked);
            SpriteComponent floatingTitleBar = FindTitleBarSprite(floating);

            Assert.True(floatingTitleBar.RenderOrder2D > dockedTitleBar.RenderOrder2D);
            Assert.True(floatingTitleBar.RenderOrder2D < RenderOrder2D.OverlayBackground);
        }

        /// <summary>
        /// Initializes the core services required for dockable render-order tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(null, new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Creates a font asset with deterministic glyph metrics for dockable tests.
        /// </summary>
        /// <returns>Font asset used by the dockables created in the test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['T'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f)
            };

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                16f,
                64,
                64);
        }

        /// <summary>
        /// Finds the title-bar sprite registered on one dockable.
        /// </summary>
        /// <param name="dockable">Dockable whose title-bar sprite should be returned.</param>
        /// <returns>Title-bar sprite component for the supplied dockable.</returns>
        SpriteComponent FindTitleBarSprite(DockableEntity dockable) {
            for (int i = 0; i < dockable.Components.Count; i++) {
                if (dockable.Components[i] is SpriteComponent sprite && sprite.Size.Y == DockableEntity.TitleBarHeight) {
                    return sprite;
                }
            }

            throw new InvalidOperationException("Expected the dockable title-bar sprite to exist.");
        }
    }
}
```

Extend `engine/helengine.editor.tests/DockTabStripTests.cs`:

```csharp
Assert.Equal(RenderOrder2D.PanelInteractive, tabs[0].Background.RenderOrder2D);
Assert.True(tabs[0].Label.RenderOrder2D > tabs[0].Background.RenderOrder2D);
```

- [ ] **Step 2: Run the dockable and tab-strip tests to verify they fail**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "RenderOrder2DStackTests|DockTabStripTests"`

Expected: FAIL because dockables and tab-strip visuals still derive their orders from `GetRenderOrderForLayer2D(...)`.

- [ ] **Step 3: Move dockables, panels, and overlays to explicit panel/overlay constants**

Modify `engine/helengine.editor/components/ui/dock/DockableEntity.cs`:

```csharp
backgroundOrder = RenderOrder2D.PanelBackground;
surfaceOrder = RenderOrder2D.PanelSurface;
textOrder = RenderOrder2D.PanelForeground;
```

Replace the floating boost logic with the explicit bias:

```csharp
int boost = 0;
if (!isDocked) {
    boost = RenderOrder2D.FloatingPanelBias;
}
foreach (var entry in renderOrderBaseline) {
    int adjusted = entry.Value + boost;
    if (adjusted > byte.MaxValue) {
        adjusted = byte.MaxValue;
    }
    entry.Key.RenderOrder2D = (byte)adjusted;
}
```

Modify `engine/helengine.editor/components/ui/dock/DockTabStrip.cs`:

```csharp
tabBackgroundOrder = RenderOrder2D.PanelInteractive;
tabTextOrder = (byte)(RenderOrder2D.PanelInteractive + 1);
```

Modify `engine/helengine.editor/managers/dock/DockLayoutEngine.cs` and `engine/helengine.editor/components/ui/dock/DockPreviewOverlay.cs`:

```csharp
byte surfaceOrder = RenderOrder2D.PanelSurface;
byte textOrder = RenderOrder2D.PanelForeground;
```

```csharp
highlight.RenderOrder2D = RenderOrder2D.OverlayForeground;
```

Migrate the panel-content files to explicit constants:

```csharp
rowBackgroundOrder = RenderOrder2D.PanelSurface;
rowTextOrder = RenderOrder2D.PanelForeground;
```

Use that exact replacement pattern in:

- `engine/helengine.editor/components/ui/SceneHierarchyPanel.cs`
- `engine/helengine.editor/components/ui/LoggerPanel.cs`
- `engine/helengine.editor/components/ui/PropertiesPanel.cs`
- `engine/helengine.editor/components/ui/PreviewPanel.cs`
- `engine/helengine.editor/components/ui/MaterialAssetView.cs`
- `engine/helengine.editor/components/ui/AssetImportSettingsView.cs`
- `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`

Move browser menus and viewport overlays into explicit overlay bands:

```csharp
byte menuBackgroundOrder = RenderOrder2D.OverlayBackground;
byte menuTextOrder = RenderOrder2D.OverlayForeground;
```

```csharp
ToolbarSurfaceOrder = RenderOrder2D.PanelSurface;
ToolbarForegroundOrder = RenderOrder2D.PanelForeground;
OverlayBackgroundRenderOrder = RenderOrder2D.OverlayBackground;
OverlayTextRenderOrder = RenderOrder2D.OverlayForeground;
```

Apply those replacements in:

- `engine/helengine.editor/components/ui/asset/AssetBrowserPanel.cs`
- `engine/helengine.editor/components/ui/EditorViewport.cs`
- `engine/helengine.editor/components/ui/EditorViewportCameraAngleOverlayComponent.cs`

- [ ] **Step 4: Run the dockable, panel, and title-bar regressions**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "RenderOrder2DStackTests|DockTabStripTests|EditorTitleBarTests|AssetBrowserTabVisibilityTests"`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/components/ui/dock/DockableEntity.cs engine/helengine.editor/managers/dock/DockLayoutEngine.cs engine/helengine.editor/components/ui/dock/DockTabStrip.cs engine/helengine.editor/components/ui/dock/DockPreviewOverlay.cs engine/helengine.editor/components/ui/SceneHierarchyPanel.cs engine/helengine.editor/components/ui/LoggerPanel.cs engine/helengine.editor/components/ui/PropertiesPanel.cs engine/helengine.editor/components/ui/PreviewPanel.cs engine/helengine.editor/components/ui/MaterialAssetView.cs engine/helengine.editor/components/ui/AssetImportSettingsView.cs engine/helengine.editor/components/ui/ComponentPropertiesView.cs engine/helengine.editor/components/ui/asset/AssetBrowserPanel.cs engine/helengine.editor/components/ui/EditorViewport.cs engine/helengine.editor/components/ui/EditorViewportCameraAngleOverlayComponent.cs engine/helengine.editor.tests/RenderOrder2DStackTests.cs engine/helengine.editor.tests/DockTabStripTests.cs
git commit -m "refactor: move dockables and overlays to explicit 2d orders"
```

## Task 3: Migrate Shared Controls And Modals, Then Delete The Old 2D Bucket API

**Files:**
- Modify: `engine/helengine.core/components/2d/interactable/ButtonComponent.cs`
- Modify: `engine/helengine.core/components/2d/interactable/ComboBoxComponent.cs`
- Modify: `engine/helengine.core/components/2d/interactable/TextBoxComponent.cs`
- Modify: `engine/helengine.editor/components/ui/asset/AssetPickerModal.cs`
- Modify: `engine/helengine.editor/components/ui/asset/OpenFileDialog.cs`
- Modify: `engine/helengine.editor/components/ui/asset/SaveFileDialog.cs`
- Modify: `engine/helengine.editor/components/ui/UnsavedChangesDialog.cs`
- Modify: `engine/helengine.core/CoreInitializationOptions.cs`
- Modify: `engine/helengine.core/managers/ObjectManager.cs`
- Modify: `engine/helengine.editor.tests/OpenFileDialogTests.cs`
- Modify: `engine/helengine.editor.tests/SaveFileDialogTests.cs`
- Modify: `engine/helengine.editor.tests/UnsavedChangesDialogTests.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionSceneOpenTests.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs`

- [ ] **Step 1: Write the failing modal-order regressions**

Extend `engine/helengine.editor.tests/OpenFileDialogTests.cs`:

```csharp
/// <summary>
/// Ensures the open-scene dialog uses modal render orders above non-modal overlays.
/// </summary>
[Fact]
public void Constructor_UsesModalRenderOrdersAboveOverlayBand() {
    OpenFileDialog dialog = new OpenFileDialog(CreateFont(), ProjectRootPath);

    RoundedRectComponent panelBackground = GetPrivateField<RoundedRectComponent>(dialog, "PanelBackground");
    Assert.Equal(RenderOrder2D.ModalBackground, panelBackground.RenderOrder2D);
}
```

Extend `engine/helengine.editor.tests/SaveFileDialogTests.cs`:

```csharp
/// <summary>
/// Ensures the save-scene dialog uses the modal foreground band for its labels.
/// </summary>
[Fact]
public void Constructor_UsesModalForegroundForDialogLabels() {
    SaveFileDialog dialog = new SaveFileDialog(CreateFont(), ProjectRootPath);

    TextComponent headerText = GetPrivateField<TextComponent>(dialog, "HeaderText");
    Assert.Equal(RenderOrder2D.ModalForeground, headerText.RenderOrder2D);
}
```

Extend `engine/helengine.editor.tests/UnsavedChangesDialogTests.cs`:

```csharp
/// <summary>
/// Ensures the unsaved-changes dialog occupies the modal band above overlay menus.
/// </summary>
[Fact]
public void Constructor_UsesModalBand() {
    UnsavedChangesDialog dialog = new UnsavedChangesDialog(CreateFont());

    FieldInfo panelBackgroundField = typeof(UnsavedChangesDialog).GetField("PanelBackground", BindingFlags.Instance | BindingFlags.NonPublic);
    RoundedRectComponent panelBackground = Assert.IsType<RoundedRectComponent>(panelBackgroundField.GetValue(dialog));
    Assert.Equal(RenderOrder2D.ModalBackground, panelBackground.RenderOrder2D);
}
```

- [ ] **Step 2: Run the modal tests to verify they fail**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "OpenFileDialogTests|SaveFileDialogTests|UnsavedChangesDialogTests"`

Expected: FAIL because dialogs still compute orders from the old top-layer bucket logic.

- [ ] **Step 3: Migrate shared controls and modal surfaces to explicit constants**

Modify `engine/helengine.core/components/2d/interactable/ButtonComponent.cs`:

```csharp
byte backgroundOrder = RenderOrder2D.PanelSurface;
byte textOrder = RenderOrder2D.PanelForeground;
if (HasRenderOrderOverrides) {
    backgroundOrder = BackgroundRenderOrder;
    textOrder = TextRenderOrder;
}
```

Modify `engine/helengine.core/components/2d/interactable/ComboBoxComponent.cs`:

```csharp
backgroundOrder = RenderOrder2D.PanelSurface;
textOrder = RenderOrder2D.PanelForeground;
listBackgroundOrder = RenderOrder2D.OverlayBackground;
listTextOrder = RenderOrder2D.OverlayForeground;
```

Modify `engine/helengine.core/components/2d/interactable/TextBoxComponent.cs`:

```csharp
byte backgroundOrder = RenderOrder2D.PanelSurface;
byte textOrder = RenderOrder2D.PanelForeground;
```

Modify the modal files so panel and text orders stop depending on `RenderOrderLayers2D`:

```csharp
PanelOrder = RenderOrder2D.ModalBackground;
TextOrder = RenderOrder2D.ModalForeground;
byte toolbarOrder = RenderOrder2D.ModalBackground;
byte rowBackgroundOrder = RenderOrder2D.ModalBackground;
byte iconBackgroundOrder = RenderOrder2D.ModalBackground;
```

Apply that replacement pattern in:

- `engine/helengine.editor/components/ui/asset/AssetPickerModal.cs`
- `engine/helengine.editor/components/ui/asset/OpenFileDialog.cs`
- `engine/helengine.editor/components/ui/asset/SaveFileDialog.cs`
- `engine/helengine.editor/components/ui/UnsavedChangesDialog.cs`

- [ ] **Step 4: Delete the 2D bucket API after the codebase is migrated**

Modify `engine/helengine.core/CoreInitializationOptions.cs`:

```csharp
/// <summary>
/// Gets or sets the number of 3D render order layers available for convenience helpers.
/// </summary>
public byte RenderOrderLayers3D { get; set; } = 4;
```

and remove the old 2D property/validation block entirely.

Modify `engine/helengine.core/managers/ObjectManager.cs`:

```csharp
RenderOrderLayers3D = settings.RenderOrderLayers3D;
```

Remove the old property and method entirely:

```csharp
public byte RenderOrderLayers2D { get; private set; } = 4;
public byte GetRenderOrderForLayer2D(int layerIndex) {
    return GetOrderForLayer(layerIndex, RenderOrderLayers2D);
}
```

Keep `GetRenderOrderForLayer3D(...)`, `GetUpdateOrderForLayer(...)`, and the shared `GetOrderForLayer(...)` helper intact for 3D and update ordering.

- [ ] **Step 5: Run the focused regressions and verify the old API is gone**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "OpenFileDialogTests|SaveFileDialogTests|UnsavedChangesDialogTests|EditorSessionSceneOpenTests|EditorSessionSceneSaveTests|EditorTitleBarTests|RenderOrder2DStackTests"`

Expected: PASS

Run: `rg -n "GetRenderOrderForLayer2D\\(|RenderOrderLayers2D\\b" engine -g "*.cs"`

Expected: no results

- [ ] **Step 6: Commit**

```bash
git add engine/helengine.core/components/2d/interactable/ButtonComponent.cs engine/helengine.core/components/2d/interactable/ComboBoxComponent.cs engine/helengine.core/components/2d/interactable/TextBoxComponent.cs engine/helengine.editor/components/ui/asset/AssetPickerModal.cs engine/helengine.editor/components/ui/asset/OpenFileDialog.cs engine/helengine.editor/components/ui/asset/SaveFileDialog.cs engine/helengine.editor/components/ui/UnsavedChangesDialog.cs engine/helengine.core/CoreInitializationOptions.cs engine/helengine.core/managers/ObjectManager.cs engine/helengine.editor.tests/OpenFileDialogTests.cs engine/helengine.editor.tests/SaveFileDialogTests.cs engine/helengine.editor.tests/UnsavedChangesDialogTests.cs engine/helengine.editor.tests/EditorSessionSceneOpenTests.cs engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs
git commit -m "refactor: remove bucket-based 2d render ordering"
```

## Task 4: Run Full Verification And Close The Slice

**Files:**
- No code changes expected

- [ ] **Step 1: Run the render-order regression suite**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "EditorTitleBarTests|EditorTitleBarAddMenuTests|RenderOrder2DStackTests|DockTabStripTests|OpenFileDialogTests|SaveFileDialogTests|UnsavedChangesDialogTests|AssetBrowserTabVisibilityTests|EditorSessionSceneOpenTests|EditorSessionSceneSaveTests"`

Expected: PASS

- [ ] **Step 2: Run the full editor test suite**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj'`

Expected: PASS for the full suite, with only pre-existing warnings.

- [ ] **Step 3: Review spec coverage before closing**

Checklist:

- `GetRenderOrderForLayer2D(...)` no longer exists.
- `RenderOrderLayers2D` no longer exists.
- `RenderOrder2D` defines the explicit 2D stack in one place.
- `File` and `Add` menus render above docked panels.
- Menu blockers and input shields still own hover and click over content behind them.
- Floating dockables stay above docked panels without using bucket math.
- Viewport overlays stay above panel content but below modals.
- Save/open/picker/unsaved dialogs occupy the modal band above overlays.

- [ ] **Step 4: Commit the final verified state**

```bash
git status --short
git add engine/helengine.core/managers/rendering/RenderOrder2D.cs engine/helengine.core/CoreInitializationOptions.cs engine/helengine.core/managers/ObjectManager.cs engine/helengine.core/components/2d/interactable/ButtonComponent.cs engine/helengine.core/components/2d/interactable/ComboBoxComponent.cs engine/helengine.core/components/2d/interactable/TextBoxComponent.cs engine/helengine.editor/components/ui/EditorTitleBar.cs engine/helengine.editor/components/ui/ContextMenu.cs engine/helengine.editor/components/ui/dock/DockableEntity.cs engine/helengine.editor/managers/dock/DockLayoutEngine.cs engine/helengine.editor/components/ui/dock/DockTabStrip.cs engine/helengine.editor/components/ui/dock/DockPreviewOverlay.cs engine/helengine.editor/components/ui/SceneHierarchyPanel.cs engine/helengine.editor/components/ui/LoggerPanel.cs engine/helengine.editor/components/ui/PropertiesPanel.cs engine/helengine.editor/components/ui/PreviewPanel.cs engine/helengine.editor/components/ui/MaterialAssetView.cs engine/helengine.editor/components/ui/AssetImportSettingsView.cs engine/helengine.editor/components/ui/ComponentPropertiesView.cs engine/helengine.editor/components/ui/asset/AssetBrowserPanel.cs engine/helengine.editor/components/ui/EditorViewport.cs engine/helengine.editor/components/ui/EditorViewportCameraAngleOverlayComponent.cs engine/helengine.editor/components/ui/asset/AssetPickerModal.cs engine/helengine.editor/components/ui/asset/OpenFileDialog.cs engine/helengine.editor/components/ui/asset/SaveFileDialog.cs engine/helengine.editor/components/ui/UnsavedChangesDialog.cs engine/helengine.editor.tests/EditorTitleBarTests.cs engine/helengine.editor.tests/EditorTitleBarAddMenuTests.cs engine/helengine.editor.tests/RenderOrder2DStackTests.cs engine/helengine.editor.tests/DockTabStripTests.cs engine/helengine.editor.tests/OpenFileDialogTests.cs engine/helengine.editor.tests/SaveFileDialogTests.cs engine/helengine.editor.tests/UnsavedChangesDialogTests.cs engine/helengine.editor.tests/EditorSessionSceneOpenTests.cs engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs
git commit -m "refactor: adopt explicit 2d render orders"
```
