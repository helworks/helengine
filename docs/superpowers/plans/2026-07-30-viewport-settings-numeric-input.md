# Viewport Settings Numeric Input Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow manual numeric entry for the four viewport settings sliders without changing any other editor slider.

**Architecture:** `EditorViewportSettingsOverlayComponent` will replace its read-only value text hosts with `TextBoxComponent` hosts. Each textbox commits its parsed invariant-culture value through the owning `EditorSlider`, retaining current range and camera-projection validation. The slider value-change handlers will update the textbox display while a synchronization flag prevents reentrant edits.

**Tech Stack:** C#/.NET 9, helengine editor UI, `TextBoxComponent`, xUnit.

---

### Task 1: Add failing textbox commit regression tests

**Files:**
- Modify: `engine/helengine.editor.tests/EditorViewportSettingsOverlayTests.cs`

- [ ] **Step 1: Write failing tests for valid and invalid near-plane text commits**

```csharp
[Fact]
public void SubmitNearPlaneValueTextBox_WhenTextIsValid_UpdatesCameraThroughSlider() {
    InitializeCore();
    EditorViewport viewport = CreateViewport();
    EditorViewportSettingsOverlayComponent overlay = GetPrivateField<EditorViewportSettingsOverlayComponent>(viewport, "SettingsOverlayComponent");

    overlay.Open();
    overlay.NearPlaneValueTextBox.Text = "0.75";
    overlay.NearPlaneValueTextBox.IsFocused = true;
    overlay.NearPlaneValueTextBox.IsFocused = false;

    Assert.Equal(0.75f, viewport.Camera.NearPlaneDistance, 3);
    Assert.Equal("0.75", overlay.NearPlaneValueTextBox.Text);
}

[Fact]
public void SubmitNearPlaneValueTextBox_WhenTextIsInvalid_RestoresCurrentSliderValue() {
    InitializeCore();
    EditorViewport viewport = CreateViewport();
    EditorViewportSettingsOverlayComponent overlay = GetPrivateField<EditorViewportSettingsOverlayComponent>(viewport, "SettingsOverlayComponent");

    overlay.Open();
    overlay.NearPlaneValueTextBox.Text = "not-a-number";
    overlay.NearPlaneValueTextBox.IsFocused = true;
    overlay.NearPlaneValueTextBox.IsFocused = false;

    Assert.Equal(0.1f, viewport.Camera.NearPlaneDistance, 3);
    Assert.Equal("0.1", overlay.NearPlaneValueTextBox.Text);
}
```

- [ ] **Step 2: Run the new tests and verify they fail because the textbox property does not exist**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~SubmitNearPlaneValueTextBox" --logger "console;verbosity=minimal"
```

Expected: compilation failure stating that `NearPlaneValueTextBox` is unavailable.

### Task 2: Replace the four value labels with textboxes

**Files:**
- Modify: `engine/helengine.editor/components/ui/EditorViewportSettingsOverlayComponent.cs`

- [ ] **Step 1: Add textbox fields and public accessors for Pixels / Unit, Near Plane, Far Plane, and Camera Speed**

Replace each `TextComponent` value field with a `TextBoxComponent` field. Expose the four controls with read-only public accessors matching the names `PixelsPerWorldUnitValueTextBox`, `NearPlaneValueTextBox`, `FarPlaneValueTextBox`, and `ManualCameraSpeedValueTextBox`.

- [ ] **Step 2: Create textbox hosts in the existing row builders**

```csharp
EditorEntity valueRoot = CreateChildRoot();
OverlayRoot.AddChild(valueRoot);

NearPlaneValueTextBox = new TextBoxComponent(new int2(SliderValueWidth, SliderHeight), Font);
NearPlaneValueTextBox.Submitted += HandleNearPlaneValueTextBoxSubmitted;
NearPlaneValueTextBox.SetRenderOrders(RenderOrder2D.OverlayBackground, RenderOrder2D.OverlayForeground);
valueRoot.AddComponent(NearPlaneValueTextBox);
```

Use the same structure for the other three rows, each with its own submit handler.

- [ ] **Step 3: Parse submissions with invariant culture and update the owning slider**

```csharp
void HandleNearPlaneValueTextBoxSubmitted(TextBoxComponent textBox) {
    if (IsSynchronizingState) {
        return;
    }

    if (!double.TryParse(textBox.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value) ||
        double.IsNaN(value) ||
        double.IsInfinity(value)) {
        SynchronizeFromState();
        return;
    }

    NearPlaneSliderInternal.SetValue(value);
}
```

Apply the same finite-number parsing to the other fields. Pixels / Unit parses an integer and assigns its slider; all four settings rely on their existing slider and camera validation for range clamping.

- [ ] **Step 4: Synchronize textboxes from slider state and lay out their hosts**

Replace value-label formatting with `Text` assignments on each textbox while `IsSynchronizingState` is true. Change `LayoutSliderRow` to accept a `TextBoxComponent` and position its parent at the existing value X/Y coordinate.

- [ ] **Step 5: Run the Task 1 tests and verify they pass**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~SubmitNearPlaneValueTextBox" --logger "console;verbosity=minimal"
```

Expected: 2 passed, 0 failed.

### Task 3: Cover all controls and slider-to-textbox synchronization

**Files:**
- Modify: `engine/helengine.editor.tests/EditorViewportSettingsOverlayTests.cs`

- [ ] **Step 1: Add a failing test that a dragged slider refreshes its visible textbox**

```csharp
[Fact]
public void SetFarPlaneSliderValue_WhenChanged_RefreshesFarPlaneValueTextBox() {
    InitializeCore();
    EditorViewport viewport = CreateViewport();
    EditorViewportSettingsOverlayComponent overlay = GetPrivateField<EditorViewportSettingsOverlayComponent>(viewport, "SettingsOverlayComponent");

    overlay.Open();
    overlay.FarPlaneSlider.SetValue(750.0);

    Assert.Equal("750", overlay.FarPlaneValueTextBox.Text);
}
```

- [ ] **Step 2: Run the synchronization test and verify it fails before the synchronization implementation is complete**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~RefreshesFarPlaneValueTextBox --logger "console;verbosity=minimal"
```

Expected: failure showing the textbox does not contain the slider value.

- [ ] **Step 3: Complete each row's formatting update and verify the synchronization test passes**

Use the existing display precision: integer formatting for Pixels / Unit and Far Plane, and the overlay's existing decimal formatting for Near Plane and Camera Speed.

- [ ] **Step 4: Run the focused overlay suite**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorViewportSettingsOverlayTests|FullyQualifiedName~EditorViewportGridToggleTests" --logger "console;verbosity=minimal"
```

Expected: 0 failures.

- [ ] **Step 5: Commit the implementation and tests**

```powershell
git add engine/helengine.editor/components/ui/EditorViewportSettingsOverlayComponent.cs engine/helengine.editor.tests/EditorViewportSettingsOverlayTests.cs
git commit -m "add numeric viewport settings inputs"
```
