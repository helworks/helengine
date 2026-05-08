# GIMP-Style Color Picker Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the current RGB slider popup with a larger, reusable GIMP-style color picker built around a hue wheel, an inner saturation/value triangle, and a separate alpha slider.

**Architecture:** Keep `EditorColorFieldControl` as the public entry point for every authored `Color` field, but change the shared picker overlay into a dedicated color-authoring surface that owns the wheel, triangle, preview, and alpha slider. Centralize RGB/HSV conversion in `EditorColorUtils` so the wheel and triangle stay deterministic and testable. Leave the popup hosted under the editor modal root so it remains visible, clickable, and reusable for future color fields.

**Tech Stack:** C#, xUnit, the existing helengine editor entity/component UI model, modal overlay hosting, pointer interaction routing, and the shared material schema editor flow.

---

## File Structure

- Modify: `engine/helengine.editor/components/ui/EditorColorUtils.cs`
  - Add shared RGB/HSV conversion helpers and any clamping/normalization routines needed by the wheel and triangle.
- Create: `engine/helengine.editor/components/ui/EditorColorWheelControl.cs`
  - Render the hue wheel, report hue changes from pointer input, and expose a compact reusable control for future color pickers.
- Create: `engine/helengine.editor/components/ui/EditorColorTriangleControl.cs`
  - Render the saturation/value triangle, report live saturation/value changes, and keep the selected point visible.
- Modify: `engine/helengine.editor/components/ui/EditorColorPickerOverlayComponent.cs`
  - Replace the RGB slider popup with the wheel, triangle, preview, hex box synchronization, and separate alpha slider.
- Modify: `engine/helengine.editor/components/ui/EditorColorFieldControl.cs`
  - Keep the swatch/textbox control as the field entry point and ensure it still opens the shared picker overlay.
- Modify: `engine/helengine.editor/components/ui/MaterialAssetView.cs`
  - Keep the material editor wired to the shared color field and picker overlay, including the modal overlay host hookup.
- Modify: `engine/helengine.editor/components/ui/MaterialAssetFieldEditorRow.cs`
  - Keep the color-row model aligned with the reusable field control if the new picker needs a different host or sizing contract.
- Create: `engine/helengine.editor.tests/components/ui/EditorColorUtilsTests.cs`
  - Cover RGB/HSV round-tripping and color normalization behavior.
- Create: `engine/helengine.editor.tests/components/ui/EditorColorPickerOverlayTests.cs`
  - Cover the wheel, triangle, alpha slider, preview, and keyboard/pointer dismissal behavior.
- Modify: `engine/helengine.editor.tests/components/ui/EditorColorFieldControlTests.cs`
  - Update the control-level regression coverage to the new overlay shape and keep the textbox/swatch sync checks.
- Modify: `engine/helengine.editor.tests/components/ui/MaterialAssetViewTests.cs`
  - Update the material view assertions for the larger overlay and the new picker control names.
- Modify: `engine/helengine.editor.tests/components/ui/MaterialAssetViewPointerInteractionTests.cs`
  - Replace the old RGB-slider pointer regression with a wheel or triangle pointer regression that proves the popup is actually clickable.

## Task 1: Add The Color Math Tests First

**Files:**
- Create: `engine/helengine.editor.tests/components/ui/EditorColorUtilsTests.cs`
- Modify: `engine/helengine.editor/components/ui/EditorColorUtils.cs`

- [ ] **Step 1: Write the failing conversion tests**

Create `EditorColorUtilsTests.cs` with pure math coverage for the shared color conversions:

```csharp
using Xunit;

namespace helengine.editor.tests.components.ui;

/// <summary>
/// Verifies the shared color math used by the wheel and triangle picker controls.
/// </summary>
public sealed class EditorColorUtilsTests {
    /// <summary>
    /// Ensures primary RGB colors round-trip through HSV without losing hue or alpha.
    /// </summary>
    [Fact]
    public void RgbToHsv_AndBack_ForPrimaryRed_PreservesTheSameColor() {
        byte4 source = new byte4(255, 0, 0, 128);

        EditorColorUtils.RgbToHsv(source, out double hue, out double saturation, out double value);
        byte4 result = EditorColorUtils.HsvToRgb(hue, saturation, value, source.W);

        Assert.Equal(source, result);
    }

    /// <summary>
    /// Ensures a neutral grayscale color maps to zero saturation and a matching value channel.
    /// </summary>
    [Fact]
    public void RgbToHsv_ForNeutralGray_ProducesZeroSaturation() {
        EditorColorUtils.RgbToHsv(new byte4(96, 96, 96, 255), out double hue, out double saturation, out double value);

        Assert.Equal(0.0, hue, 6);
        Assert.Equal(0.0, saturation, 6);
        Assert.InRange(value, 0.37, 0.38);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail for the expected reason**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorColorUtilsTests" -v minimal`

Expected: FAIL because `RgbToHsv` and `HsvToRgb` do not exist yet.

- [ ] **Step 3: Add the shared conversion helpers**

Add the conversion helpers to `EditorColorUtils.cs` so the picker controls can stay thin:

```csharp
public static void RgbToHsv(byte4 color, out double hue, out double saturation, out double value) {
    double r = color.X / 255.0;
    double g = color.Y / 255.0;
    double b = color.Z / 255.0;
    double max = Math.Max(r, Math.Max(g, b));
    double min = Math.Min(r, Math.Min(g, b));
    double delta = max - min;

    value = max;
    saturation = max <= 0.0 ? 0.0 : delta / max;
    hue = ResolveHue(r, g, b, max, delta);
}

public static byte4 HsvToRgb(double hue, double saturation, double value, byte alpha) {
    double normalizedHue = NormalizeHue(hue);
    double clampedSaturation = Math.Clamp(saturation, 0.0, 1.0);
    double clampedValue = Math.Clamp(value, 0.0, 1.0);
    double chroma = clampedValue * clampedSaturation;
    double hueSector = normalizedHue / 60.0;
    double x = chroma * (1.0 - Math.Abs((hueSector % 2.0) - 1.0));
    double match = clampedValue - chroma;

    double red;
    double green;
    double blue;

    if (hueSector < 1.0) {
        red = chroma;
        green = x;
        blue = 0.0;
    } else if (hueSector < 2.0) {
        red = x;
        green = chroma;
        blue = 0.0;
    } else if (hueSector < 3.0) {
        red = 0.0;
        green = chroma;
        blue = x;
    } else if (hueSector < 4.0) {
        red = 0.0;
        green = x;
        blue = chroma;
    } else if (hueSector < 5.0) {
        red = x;
        green = 0.0;
        blue = chroma;
    } else {
        red = chroma;
        green = 0.0;
        blue = x;
    }

    return new byte4(
        (byte)Math.Clamp((int)Math.Round((red + match) * 255.0, MidpointRounding.AwayFromZero), 0, 255),
        (byte)Math.Clamp((int)Math.Round((green + match) * 255.0, MidpointRounding.AwayFromZero), 0, 255),
        (byte)Math.Clamp((int)Math.Round((blue + match) * 255.0, MidpointRounding.AwayFromZero), 0, 255),
        alpha);
}
```

Keep the math deterministic and clamp invalid values instead of inventing defaults.

- [ ] **Step 4: Run the conversion tests again**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorColorUtilsTests" -v minimal`

Expected: PASS.

- [ ] **Step 5: Commit the color math slice**

```bash
git add engine/helengine.editor/components/ui/EditorColorUtils.cs engine/helengine.editor.tests/components/ui/EditorColorUtilsTests.cs
git commit -m "feat: add shared color space helpers"
```

## Task 2: Build The Wheel, Triangle, And Alpha Popup Shell

**Files:**
- Create: `engine/helengine.editor/components/ui/EditorColorWheelControl.cs`
- Create: `engine/helengine.editor/components/ui/EditorColorTriangleControl.cs`
- Modify: `engine/helengine.editor/components/ui/EditorColorPickerOverlayComponent.cs`
- Modify: `engine/helengine.editor.tests/components/ui/EditorColorPickerOverlayTests.cs`

- [ ] **Step 1: Write the failing overlay interaction tests**

Create `EditorColorPickerOverlayTests.cs` with interaction-level coverage for the new popup shape:

Copy the existing `CreateFont()` helper from `EditorColorFieldControlTests.cs` into this new test file so the overlay can be instantiated in isolation.

```csharp
using Xunit;

namespace helengine.editor.tests.components.ui;

/// <summary>
/// Verifies the reusable color picker overlay behaves like a GIMP-style wheel picker.
/// </summary>
public sealed class EditorColorPickerOverlayTests {
    /// <summary>
    /// Ensures the overlay exposes the wheel, triangle, and alpha slider as distinct authored controls.
    /// </summary>
    [Fact]
    public void Open_WhenPickerIsCreated_ShowsWheelTriangleAndAlphaSlider() {
        Core core = new Core();
        core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        EditorEntity host = new EditorEntity();
        EditorColorPickerOverlayComponent overlay = new EditorColorPickerOverlayComponent(CreateFont(), 1);
        host.AddComponent(overlay);

        overlay.Open(new byte4(32, 64, 96, 255));

        Assert.NotNull(overlay.HueWheelControl);
        Assert.NotNull(overlay.SaturationValueTriangleControl);
        Assert.NotNull(overlay.AlphaSliderControl);
        Assert.True(overlay.IsOpen);
    }

    /// <summary>
    /// Ensures dragging the hue wheel updates the current color immediately.
    /// </summary>
    [Fact]
    public void HueWheel_WhenDragged_UpdatesTheCurrentHexValue() {
        Core core = new Core();
        core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        EditorEntity host = new EditorEntity();
        EditorColorPickerOverlayComponent overlay = new EditorColorPickerOverlayComponent(CreateFont(), 1);
        host.AddComponent(overlay);
        overlay.Open(new byte4(255, 0, 0, 255));

        overlay.HueWheelControl.SetHueForTest(120.0);

        Assert.Equal("#00ff00", overlay.HexTextBoxControl.Text);
    }
}
```

- [ ] **Step 2: Run the overlay tests to verify they fail**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorColorPickerOverlayTests" -v minimal`

Expected: FAIL because the wheel and triangle controls do not exist yet and the overlay still uses RGB sliders.

- [ ] **Step 3: Replace the RGB popup shell with the new picker controls**

Rewrite `EditorColorPickerOverlayComponent.cs` so it owns a larger popup shell and the three authored color controls:

```csharp
public sealed class EditorColorPickerOverlayComponent : UpdateComponent {
    public EditorColorWheelControl HueWheelControl { get; private set; }
    public EditorColorTriangleControl SaturationValueTriangleControl { get; private set; }
    public EditorSlider AlphaSliderControl { get; private set; }
    public TextBoxComponent HexTextBoxControl { get; private set; }
    public RoundedRectComponent PreviewBackground { get; private set; }
}
```

Implementation rules:

- remove the full-panel interactable that previously stole pointer hits
- keep dismissal in `Update()` through the existing outside-click path
- size the overlay larger than the current RGB popup so the wheel and triangle have room
- use modal overlay render orders so the popup stays above the material editor
- keep the preview square and hex textbox visible in the right-hand panel area
- use `EditorSlider` for alpha with a linear `0..255` range

The wheel should control hue, the triangle should control saturation/value, and the alpha slider should only change alpha. Each control should raise a live change event that the overlay converts back into a `byte4`.

- [ ] **Step 4: Run the overlay tests again**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorColorPickerOverlayTests" -v minimal`

Expected: PASS.

- [ ] **Step 5: Commit the popup shell**

```bash
git add engine/helengine.editor/components/ui/EditorColorWheelControl.cs engine/helengine.editor/components/ui/EditorColorTriangleControl.cs engine/helengine.editor/components/ui/EditorColorPickerOverlayComponent.cs engine/helengine.editor.tests/components/ui/EditorColorPickerOverlayTests.cs
git commit -m "feat: add gimp-style color picker shell"
```

## Task 3: Rewire The Material Editor To Use The New Popup

**Files:**
- Modify: `engine/helengine.editor/components/ui/EditorColorFieldControl.cs`
- Modify: `engine/helengine.editor/components/ui/MaterialAssetView.cs`
- Modify: `engine/helengine.editor/components/ui/MaterialAssetFieldEditorRow.cs`
- Modify: `engine/helengine.editor.tests/components/ui/EditorColorFieldControlTests.cs`
- Modify: `engine/helengine.editor.tests/components/ui/MaterialAssetViewTests.cs`
- Modify: `engine/helengine.editor.tests/components/ui/MaterialAssetViewPointerInteractionTests.cs`

- [ ] **Step 1: Update the material and field-control tests to the new popup shape**

Adjust the existing material and color-control tests so they assert the picker still opens and remains clickable, but no longer assume RGB sliders exist:

Reuse the setup patterns already present in `EditorColorFieldControlTests.cs` and `MaterialAssetViewTests.cs` so the new assertions stay focused on picker behavior instead of re-deriving the whole editor harness.

```csharp
[Fact]
public void EditorColorFieldControl_WhenSwatchIsClicked_RequestsTheSharedPicker() {
    bool pickerRequested = false;
    control.PickerRequested += () => pickerRequested = true;
    interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Release);
    Assert.True(pickerRequested);
}

[Fact]
public void MaterialView_WhenColorPickerIsOpened_HostsTheOverlayUnderTheModalRoot() {
    EditorColorPickerOverlayComponent overlay = Assert.Single(modalHost.Components.OfType<EditorColorPickerOverlayComponent>());
    Assert.True(overlay.IsOpen);
    Assert.NotNull(overlay.HueWheelControl);
    Assert.NotNull(overlay.SaturationValueTriangleControl);
    Assert.NotNull(overlay.AlphaSliderControl);
}

[Fact]
public void Show_WhenColorPickerIsOpened_StillAllowsPointerInteractionOnTheWheel() {
    overlay.HueWheelControl.SetHueForTest(120.0);
    Assert.Equal("#00ff00", overlay.HexTextBoxControl.Text);
}
```

- [ ] **Step 2: Run the focused material-editor tests to verify they fail**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorColorFieldControlTests|FullyQualifiedName~MaterialAssetViewTests|FullyQualifiedName~MaterialAssetViewPointerInteractionTests" -v minimal`

Expected: FAIL because the material view and tests still expect the old RGB picker shape.

- [ ] **Step 3: Wire the field control and material view to the new overlay**

Keep `EditorColorFieldControl` as the inline textbox plus swatch control, but connect the swatch to the redesigned overlay:

```csharp
control.PickerRequested += () => HandleColorFieldPickerRequested(platformId, field.FieldId, control);
ColorPickerOverlay.SetColor(control.Value);
ColorPickerOverlay.Open(control.Value);
```

In `MaterialAssetView`, keep the active-color bookkeeping and modal overlay host behavior intact. The only meaningful change is that the active control must now synchronize against the wheel, triangle, and alpha slider instead of RGB sliders.

- [ ] **Step 4: Run the focused material-editor tests again**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorColorFieldControlTests|FullyQualifiedName~MaterialAssetViewTests|FullyQualifiedName~MaterialAssetViewPointerInteractionTests" -v minimal`

Expected: PASS.

- [ ] **Step 5: Commit the editor wiring**

```bash
git add engine/helengine.editor/components/ui/EditorColorFieldControl.cs engine/helengine.editor/components/ui/MaterialAssetView.cs engine/helengine.editor/components/ui/MaterialAssetFieldEditorRow.cs engine/helengine.editor.tests/components/ui/EditorColorFieldControlTests.cs engine/helengine.editor.tests/components/ui/MaterialAssetViewTests.cs engine/helengine.editor.tests/components/ui/MaterialAssetViewPointerInteractionTests.cs
git commit -m "feat: wire material editor to gimp-style color picker"
```

## Task 4: Final Verification

**Files:**
- Verify: `engine/helengine.editor/components/ui/EditorColorUtils.cs`
- Verify: `engine/helengine.editor/components/ui/EditorColorWheelControl.cs`
- Verify: `engine/helengine.editor/components/ui/EditorColorTriangleControl.cs`
- Verify: `engine/helengine.editor/components/ui/EditorColorPickerOverlayComponent.cs`
- Verify: `engine/helengine.editor/components/ui/MaterialAssetView.cs`
- Verify: `engine/helengine.editor.tests/components/ui/EditorColorUtilsTests.cs`
- Verify: `engine/helengine.editor.tests/components/ui/EditorColorPickerOverlayTests.cs`
- Verify: `engine/helengine.editor.tests/components/ui/EditorColorFieldControlTests.cs`
- Verify: `engine/helengine.editor.tests/components/ui/MaterialAssetViewTests.cs`
- Verify: `engine/helengine.editor.tests/components/ui/MaterialAssetViewPointerInteractionTests.cs`

- [ ] **Step 1: Run the focused color-picker slice**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorColorUtilsTests|FullyQualifiedName~EditorColorPickerOverlayTests|FullyQualifiedName~EditorColorFieldControlTests|FullyQualifiedName~MaterialAssetViewTests|FullyQualifiedName~MaterialAssetViewPointerInteractionTests" -v minimal`

Expected: PASS.

- [ ] **Step 2: Build the editor projects**

Run: `rtk dotnet build engine/helengine.editor/helengine.editor.csproj -v minimal`

Expected: `0 errors`.

Run: `rtk dotnet build engine/helengine.editor.tests/helengine.editor.tests.csproj -v minimal`

Expected: `0 errors`.

- [ ] **Step 3: Run the full solution test slice**

Run: `rtk dotnet test helengine.ui/helengine.sln --nologo -m:1 -v minimal`

Expected: PASS. If the repository still has an unrelated existing compiler failure in another project, stop and fix that blocker before claiming completion.

- [ ] **Step 4: Commit the final verified color picker**

```bash
git add engine/helengine.editor/components/ui/EditorColorUtils.cs engine/helengine.editor/components/ui/EditorColorWheelControl.cs engine/helengine.editor/components/ui/EditorColorTriangleControl.cs engine/helengine.editor/components/ui/EditorColorPickerOverlayComponent.cs engine/helengine.editor/components/ui/EditorColorFieldControl.cs engine/helengine.editor/components/ui/MaterialAssetView.cs engine/helengine.editor/components/ui/MaterialAssetFieldEditorRow.cs engine/helengine.editor.tests/components/ui/EditorColorUtilsTests.cs engine/helengine.editor.tests/components/ui/EditorColorPickerOverlayTests.cs engine/helengine.editor.tests/components/ui/EditorColorFieldControlTests.cs engine/helengine.editor.tests/components/ui/MaterialAssetViewTests.cs engine/helengine.editor.tests/components/ui/MaterialAssetViewPointerInteractionTests.cs
git commit -m "feat: add gimp-style color picker"
```
