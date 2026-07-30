# Viewport Snap Numeric Input Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let users type the Ctrl and Shift transform snap values directly in the viewport toolbar.

**Architecture:** `EditorViewport` will replace its per-slot sprite/text readouts with `TextBoxComponent` instances. Submit handlers will parse invariant-culture finite values and write them to `TransformGizmoSnapSettingsService`, which remains the single source of truth for tool-mode and snap-slot state. Existing toolbar synchronization will refresh the textboxes after typed edits, button adjustments, and tool-mode changes.

**Tech Stack:** C#, .NET 9, helengine editor UI, xUnit.

---

### Task 1: Specify typed snap-value behavior with failing tests

**Files:**
- Modify: `engine/helengine.editor.tests/EditorViewportKeyboardFocusTests.cs`

- [ ] **Step 1: Write the failing textbox submission tests**

Add tests that read `TextBoxComponent[] SnapValueTextBoxes` by reflection. For the first test, set `viewport.ToolMode` to `Translate`, focus slot zero, assign `"2.5"`, remove focus, and assert that only `Translate/Snap1` becomes `2.5`. Change the mode to `Rotate` and assert the same field refreshes to the rotate value rather than retaining `2.5`.

```csharp
[Fact]
public void SubmitSnapValueTextBox_WhenTextIsValid_UpdatesOnlyTheActiveToolSlot() {
    InitializeCore();
    EditorViewport viewport = CreateViewport();
    TextBoxComponent[] snapValueTextBoxes = GetPrivateField<TextBoxComponent[]>(viewport, "SnapValueTextBoxes");

    viewport.ToolMode = EditorViewportToolMode.Translate;
    snapValueTextBoxes[0].IsFocused = true;
    snapValueTextBoxes[0].Text = "2.5";
    snapValueTextBoxes[0].IsFocused = false;

    Assert.Equal(2.5, TransformGizmoSnapSettingsService.GetSnapValue(EditorViewportToolMode.Translate, TransformGizmoSnapSlot.Snap1));
    Assert.Equal(5.0, TransformGizmoSnapSettingsService.GetSnapValue(EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap1));
    viewport.ToolMode = EditorViewportToolMode.Rotate;
    Assert.Equal("5", snapValueTextBoxes[0].Text);
}
```

Add a second test that submits `"invalid"` to slot one and asserts the service value remains unchanged and the field restores its formatted value.

```csharp
[Fact]
public void SubmitSnapValueTextBox_WhenTextIsInvalid_RestoresCurrentFormattedValue() {
    InitializeCore();
    EditorViewport viewport = CreateViewport();
    TextBoxComponent[] snapValueTextBoxes = GetPrivateField<TextBoxComponent[]>(viewport, "SnapValueTextBoxes");

    viewport.ToolMode = EditorViewportToolMode.Rotate;
    snapValueTextBoxes[1].IsFocused = true;
    snapValueTextBoxes[1].Text = "invalid";
    snapValueTextBoxes[1].IsFocused = false;

    Assert.Equal(15.0, TransformGizmoSnapSettingsService.GetSnapValue(EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap2));
    Assert.Equal("15", snapValueTextBoxes[1].Text);
}
```

- [ ] **Step 2: Run the focused test class and verify it fails**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorViewportKeyboardFocusTests"
```

Expected: compilation fails because `EditorViewport` does not yet declare `SnapValueTextBoxes`.

### Task 2: Replace snap readouts with editable textboxes

**Files:**
- Modify: `engine/helengine.editor/components/ui/EditorViewport.cs:204-214,746-840,1174-1191`
- Test: `engine/helengine.editor.tests/EditorViewportKeyboardFocusTests.cs`

- [ ] **Step 1: Replace the visual arrays and build textboxes**

Replace `SpriteComponent[] SnapValueBackgrounds` and `TextComponent[] SnapValueTexts` with `TextBoxComponent[] SnapValueTextBoxes`. In `CreateSnapControlGroup`, remove the manually-created background and child text entity, then create one textbox on `valueRoot`:

```csharp
TextBoxComponent valueTextBox = new TextBoxComponent(new int2(SnapValueWidth, ToolButtonHeight), Font);
valueTextBox.SetRenderOrders(ToolbarSurfaceOrder, ToolbarForegroundOrder);
int capturedSlotIndex = slotIndex;
valueTextBox.Submitted += textBox => HandleSnapValueSubmitted(capturedSlotIndex, textBox);
valueRoot.AddComponent(valueTextBox);
SnapValueTextBoxes[slotIndex] = valueTextBox;
```

Update `UpdateToolbarTextFonts` to assign the current `Font` to each existing snap textbox.

- [ ] **Step 2: Add submit and parse methods**

Add `HandleSnapValueSubmitted(int slotIndex, TextBoxComponent textBox)`. It must reject invalid slot indexes, parse using `double.TryParse` with `NumberStyles.Float` and `CultureInfo.InvariantCulture`, reject non-finite values with `double.IsFinite`, restore current text on invalid input, and otherwise call:

```csharp
TransformGizmoSnapSettingsService.SetSnapValue(ToolMode, SnapSlots[slotIndex], value);
UpdateSnapControlTexts();
```

Use a documented class method `TryParseFiniteSnapValue(string text, out double value)` for the parse rule; do not add local functions.

- [ ] **Step 3: Synchronize textboxes from the snap service**

In `UpdateSnapControlTexts`, iterate `SnapValueTextBoxes`, retrieve each active `ToolMode`/slot value from `TransformGizmoSnapSettingsService`, apply `FormatSnapValue`, and keep `LayoutToolbar()` after the text refresh. This preserves existing +/− button and tool-mode synchronization.

- [ ] **Step 4: Run the focused test class and verify it passes**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorViewportKeyboardFocusTests"
```

Expected: all `EditorViewportKeyboardFocusTests` pass.

- [ ] **Step 5: Run the snap service regression tests**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~TransformGizmoSnapSettingsServiceTests"
```

Expected: all snap-service tests pass.

- [ ] **Step 6: Commit the implementation**

```powershell
git add -- engine/helengine.editor/components/ui/EditorViewport.cs engine/helengine.editor.tests/EditorViewportKeyboardFocusTests.cs
git commit -m "add editable viewport snap values"
```
