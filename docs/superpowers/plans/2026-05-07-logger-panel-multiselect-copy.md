# Logger Panel Multi-Select And Copy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add editor-only multi-row selection, keyboard navigation, right-click `Copy`, and `Ctrl+C` copy support to `LoggerPanel`, with one shared copy path and deterministic selection behavior while new logs are appended and old rows are trimmed.

**Architecture:** Keep the feature local to `LoggerPanel`. The panel owns focus, anchor, selected-row state, row interaction callbacks, context-menu visibility, scroll positioning, and clipboard writes. `LoggerPanelRow` remains a presentational row container with just enough extra structure to forward pointer input. Existing global logger storage stays unchanged, and copy uses the existing `Core.Instance.TextClipboardService` host seam.

**Tech Stack:** C#/.NET 9, editor UI entities/components, existing `ContextMenu` and `ButtonInteractableComponent` patterns, existing `ITextClipboardService`, xUnit.

---

## File Structure

### Modified files

- `engine/helengine.editor/components/ui/LoggerPanel.cs`
  - Add selection state, row input handling, keyboard shortcut handling, context-menu ownership, copy payload creation, trim re-normalization, and scroll visibility logic.
- `engine/helengine.editor/components/ui/LoggerPanelRow.cs`
  - Extend the row container with the row interactable and any row-local metadata needed by the panel.
- `engine/helengine.editor/components/ui/LoggerPanelUpdater.cs`
  - Run the new per-frame logger-panel input and context-menu update flow after log flushing.
- `engine/helengine.editor.tests/LoggerPanelTests.cs`
  - Add focused unit coverage for selection, keyboard behavior, copy, and trim normalization.

### Existing seams the implementation should reuse

- `engine/helengine.core/Core.cs`
  - Use `Core.Instance.TextClipboardService` for copy output.
- `engine/helengine.editor/components/ui/ContextMenu.cs`
  - Reuse the existing context-menu widget instead of inventing a logger-specific menu primitive.
- `engine/helengine.editor/components/ui/SceneHierarchyPanel.cs`
  - Follow its right-click context-menu update pattern when deciding where logger context-menu input should run.

---

### Task 1: Add Panel Selection State And Row Interaction Wiring

**Files:**
- Modify: `engine/helengine.editor/components/ui/LoggerPanel.cs`
- Modify: `engine/helengine.editor/components/ui/LoggerPanelRow.cs`
- Test: `engine/helengine.editor.tests/LoggerPanelTests.cs`

- [ ] **Step 1: Write the failing mouse-selection tests**

Add these tests to `engine/helengine.editor.tests/LoggerPanelTests.cs`:

- `HandleRowPressed_WhenPlainClickOccurs_SelectsOnlyThatRow`
- `HandleRowPressed_WhenControlClickOccurs_TogglesThatRowInsideSelection`
- `HandleRowPressed_WhenShiftClickOccurs_SelectsInclusiveRangeFromAnchor`

Each test should:

- create a panel with multiple log entries
- invoke the row-press path directly through reflection or the row callback
- assert:
  - `FocusedRowIndex`
  - `AnchorRowIndex`
  - `SelectedRowIndices`

Example assertion shape:

```csharp
Assert.Equal(3, GetPrivateField<int>(panel, "FocusedRowIndex"));
Assert.Equal(3, GetPrivateField<int>(panel, "AnchorRowIndex"));
Assert.Equal(new[] { 3 }, GetPrivateField<HashSet<int>>(panel, "SelectedRowIndices").OrderBy(value => value));
```

- [ ] **Step 2: Run the focused selection tests to verify they fail**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~LoggerPanelTests.HandleRowPressed_WhenPlainClickOccurs_SelectsOnlyThatRow|FullyQualifiedName~LoggerPanelTests.HandleRowPressed_WhenControlClickOccurs_TogglesThatRowInsideSelection|FullyQualifiedName~LoggerPanelTests.HandleRowPressed_WhenShiftClickOccurs_SelectsInclusiveRangeFromAnchor" -v minimal
```

Expected: FAIL because `LoggerPanel` has no selection state or row input callbacks yet.

- [ ] **Step 3: Extend `LoggerPanelRow` with row interaction plumbing**

Update `LoggerPanelRow` so it stores the row interactable used by the panel:

```csharp
public sealed class LoggerPanelRow {
    public LoggerPanelRow(
        EditorEntity entity,
        SpriteComponent background,
        EditorEntity labelHost,
        TextComponent label,
        ButtonInteractableComponent interactable) {
        Entity = entity;
        Background = background;
        LabelHost = labelHost;
        Label = label;
        Interactable = interactable;
    }

    public ButtonInteractableComponent Interactable { get; }
}
```

- [ ] **Step 4: Add logger-panel row selection state and pointer callbacks**

Update `LoggerPanel` with private state and helpers:

```csharp
readonly HashSet<int> SelectedRowIndices;
readonly List<ContextMenuItem> RowContextMenuItems;
readonly ContextMenu RowContextMenu;

int FocusedRowIndex;
int AnchorRowIndex;
int ContextMenuRowIndex;

void HandleRowPressed(int rowIndex) { }
void HandleRowRightPressed(int rowIndex) { }
void SelectSingleRow(int rowIndex) { }
void ToggleRowSelection(int rowIndex) { }
void SelectRangeFromAnchor(int rowIndex) { }
```

Inside `CreateRow()`, add a row-level interactable and wire callbacks back into `LoggerPanel`:

```csharp
var interactable = new ButtonInteractableComponent(HandleRowPrimaryClicked, HandleRowSecondaryClicked);
rowEntity.AddComponent(interactable);
```

Use a captured row index callback strategy that is refreshed during layout, or a row-to-index lookup helper on the panel. Do not let `LoggerPanelRow` own selection decisions.

- [ ] **Step 5: Run the focused selection tests to verify they pass**

Run the command from Step 2 again.

- [ ] **Step 6: Commit the selection-state foundation**

```bash
git add engine/helengine.editor/components/ui/LoggerPanel.cs engine/helengine.editor/components/ui/LoggerPanelRow.cs engine/helengine.editor.tests/LoggerPanelTests.cs
git commit -m "feat: add logger panel row selection state"
```

---

### Task 2: Add Context Menu Copy And Shared Clipboard Output

**Files:**
- Modify: `engine/helengine.editor/components/ui/LoggerPanel.cs`
- Modify: `engine/helengine.editor/components/ui/LoggerPanelUpdater.cs`
- Test: `engine/helengine.editor.tests/LoggerPanelTests.cs`

- [ ] **Step 1: Write the failing copy tests**

Add these tests:

- `CopySelection_WhenMultipleRowsSelected_WritesJoinedVisibleRowsToClipboard`
- `HandleRowRightPressed_WhenClickedRowIsUnselected_SelectsThatRowAndShowsCopyMenu`
- `HandleCopyContextMenuRequested_WhenInvoked_UsesTheSameClipboardPayloadAsKeyboardCopy`

Set `Core.Instance.SetTextClipboardService(new TestTextClipboardService())` in the test, then assert:

```csharp
Assert.Equal(
    string.Join(Environment.NewLine, expectedRows),
    clipboardService.ReadText());
```

For the context-menu test, assert:

- the clicked row becomes the only selection when it was previously unselected
- the menu is visible
- the `Copy` menu item invokes the same payload path

- [ ] **Step 2: Run the focused copy tests to verify they fail**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~LoggerPanelTests.CopySelection_WhenMultipleRowsSelected_WritesJoinedVisibleRowsToClipboard|FullyQualifiedName~LoggerPanelTests.HandleRowRightPressed_WhenClickedRowIsUnselected_SelectsThatRowAndShowsCopyMenu|FullyQualifiedName~LoggerPanelTests.HandleCopyContextMenuRequested_WhenInvoked_UsesTheSameClipboardPayloadAsKeyboardCopy" -v minimal
```

Expected: FAIL because the logger panel does not own a context menu or clipboard path yet.

- [ ] **Step 3: Add one shared copy path and a row context menu**

Update `LoggerPanel` to own:

```csharp
readonly ContextMenu RowContextMenu;
readonly List<ContextMenuItem> RowContextMenuItems;

void CopySelection() { }
string BuildSelectedRowsText() { }
void ShowRowContextMenu(int rowIndex, int2 localPointerPosition) { }
void HideRowContextMenu() { }
```

Build the menu once in the constructor:

```csharp
RowContextMenuItems = new List<ContextMenuItem> {
    new ContextMenuItem("Copy", CopySelection)
};

RowContextMenu = new ContextMenu(font, LayerMask, RenderOrder2D.OverlayBackground, RenderOrder2D.OverlayForeground);
AddChild(RowContextMenu.Entity);
```

Write clipboard text through the existing host service only:

```csharp
Core.Instance.TextClipboardService.WriteText(BuildSelectedRowsText());
```

- [ ] **Step 4: Update the updater so the context menu receives per-frame input**

Extend `LoggerPanelUpdater.Update()` to run the context-menu/input update flow after `FlushPendingEntries()`:

```csharp
public override void Update() {
    panel.FlushPendingEntries();
    panel.UpdateContextMenuInput();
    panel.UpdateKeyboardInput();
}
```

Add matching internal methods on `LoggerPanel` and follow the existing `SceneHierarchyPanel` / `AssetBrowserPanel` pattern for menu visibility and click-away dismissal.

- [ ] **Step 5: Run the focused copy tests to verify they pass**

Run the command from Step 2 again.

- [ ] **Step 6: Commit context-menu copy support**

```bash
git add engine/helengine.editor/components/ui/LoggerPanel.cs engine/helengine.editor/components/ui/LoggerPanelUpdater.cs engine/helengine.editor.tests/LoggerPanelTests.cs
git commit -m "feat: add logger panel copy context menu"
```

---

### Task 3: Add Keyboard Focus Navigation, Shortcut Handling, And Auto-Scroll

**Files:**
- Modify: `engine/helengine.editor/components/ui/LoggerPanel.cs`
- Modify: `engine/helengine.editor/components/ui/LoggerPanelUpdater.cs`
- Test: `engine/helengine.editor.tests/LoggerPanelTests.cs`

- [ ] **Step 1: Write the failing keyboard-navigation tests**

Add these tests:

- `UpdateKeyboardInput_WhenDownIsPressed_SelectsOnlyTheNextFocusedRow`
- `UpdateKeyboardInput_WhenShiftDownIsPressed_ExtendsSelectionFromAnchor`
- `UpdateKeyboardInput_WhenControlDownIsPressed_MovesFocusWithoutClearingSelection`
- `UpdateKeyboardInput_WhenControlSpaceIsPressed_TogglesTheFocusedRow`
- `UpdateKeyboardInput_WhenControlCIsPressedWithNoSelection_CopiesTheFocusedRow`
- `EnsureFocusedRowVisible_WhenFocusMovesPastVisibleWindow_AdjustsScrollOffset`

For the scrolling test, assert the panel updates a scroll-offset field or content-root Y translation consistently:

```csharp
Assert.True(GetPrivateField<int>(panel, "FirstVisibleRowIndex") > 0);
Assert.True(GetPrivateField<EditorEntity>(panel, "contentRoot").Position.Y < expectedBaselineY);
```

- [ ] **Step 2: Run the focused keyboard tests to verify they fail**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~LoggerPanelTests.UpdateKeyboardInput_WhenDownIsPressed_SelectsOnlyTheNextFocusedRow|FullyQualifiedName~LoggerPanelTests.UpdateKeyboardInput_WhenShiftDownIsPressed_ExtendsSelectionFromAnchor|FullyQualifiedName~LoggerPanelTests.UpdateKeyboardInput_WhenControlDownIsPressed_MovesFocusWithoutClearingSelection|FullyQualifiedName~LoggerPanelTests.UpdateKeyboardInput_WhenControlSpaceIsPressed_TogglesTheFocusedRow|FullyQualifiedName~LoggerPanelTests.UpdateKeyboardInput_WhenControlCIsPressedWithNoSelection_CopiesTheFocusedRow|FullyQualifiedName~LoggerPanelTests.EnsureFocusedRowVisible_WhenFocusMovesPastVisibleWindow_AdjustsScrollOffset" -v minimal
```

Expected: FAIL because `LoggerPanel` does not handle keyboard focus, shortcuts, or scrolling yet.

- [ ] **Step 3: Add keyboard selection and copy handling**

Implement panel-local keyboard helpers:

```csharp
int FirstVisibleRowIndex;

internal void UpdateKeyboardInput() { }
void MoveFocusBy(int delta, bool preserveSelection, bool extendSelection) { }
void ToggleFocusedRowSelection() { }
void EnsureFocusedRowVisible() { }
```

Handle these cases exactly:

- `Up` / `Down`: move focus, clear selection, select only focused row, update anchor
- `Shift+Up` / `Shift+Down`: move focus, preserve anchor, select inclusive range
- `Ctrl+Up` / `Ctrl+Down`: move focus only
- `Ctrl+Space`: toggle focused row in the selected set and update anchor
- `Ctrl+C`: call `CopySelection()`, falling back to the focused row when selection is empty

- [ ] **Step 4: Add row-window scrolling tied to the focused row**

Do not invent pixel-freeform scrolling. Keep this row-based:

```csharp
int GetVisibleRowCapacity() {
    return Math.Max(1, (Size.Y - TitleBarHeightPixels) / GetRowHeightPixels());
}
```

When focus moves outside the current visible range:

- if focused row is above `FirstVisibleRowIndex`, clamp upward
- if focused row is below `FirstVisibleRowIndex + visibleCapacity - 1`, clamp downward

Apply the translation through the row host instead of changing entry order:

```csharp
contentRoot.Position = new float3(0f, TitleBarHeightPixels - (FirstVisibleRowIndex * GetRowHeightPixels()), 0.05f);
```

Update row layout to continue using absolute row indices while the root offset determines visibility.

- [ ] **Step 5: Run the focused keyboard tests to verify they pass**

Run the command from Step 2 again.

- [ ] **Step 6: Commit keyboard navigation and scroll behavior**

```bash
git add engine/helengine.editor/components/ui/LoggerPanel.cs engine/helengine.editor/components/ui/LoggerPanelUpdater.cs engine/helengine.editor.tests/LoggerPanelTests.cs
git commit -m "feat: add logger panel keyboard navigation"
```

---

### Task 4: Add Visual Selection States And Trim Re-Normalization

**Files:**
- Modify: `engine/helengine.editor/components/ui/LoggerPanel.cs`
- Test: `engine/helengine.editor.tests/LoggerPanelTests.cs`

- [ ] **Step 1: Write the failing visual-state and trim tests**

Add these tests:

- `LayoutRows_WhenRowIsSelected_UsesSelectedBackgroundTint`
- `LayoutRows_WhenRowIsFocusedAndSelected_UsesFocusedSelectedTint`
- `AppendEntry_WhenOldRowsAreTrimmed_ShiftsSelectionFocusAndAnchorDownward`
- `AppendEntry_WhenTrimRemovesFocusedRow_ClampsFocusToTheNearestRemainingRow`

Example trim assertions:

```csharp
Assert.Equal(expectedFocusedRowIndex, GetPrivateField<int>(panel, "FocusedRowIndex"));
Assert.Equal(expectedAnchorRowIndex, GetPrivateField<int>(panel, "AnchorRowIndex"));
Assert.Equal(expectedSelection, GetPrivateField<HashSet<int>>(panel, "SelectedRowIndices").OrderBy(value => value));
```

- [ ] **Step 2: Run the focused visual-state and trim tests to verify they fail**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~LoggerPanelTests.LayoutRows_WhenRowIsSelected_UsesSelectedBackgroundTint|FullyQualifiedName~LoggerPanelTests.LayoutRows_WhenRowIsFocusedAndSelected_UsesFocusedSelectedTint|FullyQualifiedName~LoggerPanelTests.AppendEntry_WhenOldRowsAreTrimmed_ShiftsSelectionFocusAndAnchorDownward|FullyQualifiedName~LoggerPanelTests.AppendEntry_WhenTrimRemovesFocusedRow_ClampsFocusToTheNearestRemainingRow" -v minimal
```

- [ ] **Step 3: Add explicit row visual-state resolution**

Add helpers on `LoggerPanel`:

```csharp
byte4 ResolveRowBackgroundColor(int rowIndex) { }
bool IsRowSelected(int rowIndex) { }
bool IsRowFocused(int rowIndex) { }
```

Keep striping for unselected rows, but override with stronger colors when selected and focused. Reuse theme colors that already exist in `ThemeManager.Colors`; do not hardcode new color constants inside the row loop unless no suitable theme color exists.

- [ ] **Step 4: Re-normalize selection state when old entries are trimmed**

Update `AppendEntry(LogEntry entry)` so it shifts selection metadata whenever `entries.RemoveRange(0, removeCount)` runs:

```csharp
void ShiftSelectionStateAfterTrim(int removeCount) { }
```

Behavior:

- subtract `removeCount` from `FocusedRowIndex` and `AnchorRowIndex`
- subtract `removeCount` from every selected row index
- discard any negative indices
- clamp focus and anchor to the nearest valid row when rows remain
- clear focus/anchor/selection when the panel becomes empty

- [ ] **Step 5: Run the focused visual-state and trim tests to verify they pass**

Run the command from Step 2 again.

- [ ] **Step 6: Commit visual states and trim handling**

```bash
git add engine/helengine.editor/components/ui/LoggerPanel.cs engine/helengine.editor.tests/LoggerPanelTests.cs
git commit -m "feat: stabilize logger panel selection state"
```

---

### Task 5: Run End-To-End Logger Panel Regression Coverage

**Files:**
- Test: `engine/helengine.editor.tests/LoggerPanelTests.cs`

- [ ] **Step 1: Run the full logger-panel suite**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~LoggerPanelTests" -v minimal
```

Expected: PASS for the original layout test plus all newly added selection/copy/keyboard/trim tests.

- [ ] **Step 2: Run one adjacent editor-input regression slice**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~ContextMenuInteractionTests|FullyQualifiedName~TextBoxComponentKeyboardFocusTests|FullyQualifiedName~SceneHierarchyPanelTests" -v minimal
```

Expected: PASS so the logger changes did not regress shared context-menu or clipboard behavior.

- [ ] **Step 3: Commit the completed logger-panel feature after green verification**

```bash
git add engine/helengine.editor/components/ui/LoggerPanel.cs engine/helengine.editor/components/ui/LoggerPanelRow.cs engine/helengine.editor/components/ui/LoggerPanelUpdater.cs engine/helengine.editor.tests/LoggerPanelTests.cs
git commit -m "feat: add logger panel multiselect copy"
```

---

## Self-Review

### Spec coverage

- Multi-select with mouse: covered by Task 1.
- Right-click context menu with `Copy`: covered by Task 2.
- `Ctrl+C` shared copy path: covered by Tasks 2 and 3.
- Keyboard focus and range selection: covered by Task 3.
- Auto-scroll to keep focused row visible: covered by Task 3.
- Visual selected/focused states and trim stability: covered by Task 4.

### Placeholder scan

- No `TODO`, `TBD`, or “add appropriate handling” placeholders remain.
- Every task names exact files, targeted test names, and verification commands.

### Type consistency

- `SelectedRowIndices`, `FocusedRowIndex`, `AnchorRowIndex`, and `FirstVisibleRowIndex` are used consistently across tasks.
- `CopySelection()` remains the single shared copy path in all tasks.
- `RowContextMenu` is the only context-menu object introduced for this feature.
