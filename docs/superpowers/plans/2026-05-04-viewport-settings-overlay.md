# Viewport Settings Overlay Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a right-aligned viewport settings button that opens a keyboard-accessible overlay for grid visibility and live near/far clip-plane control, backed by real per-camera projection state.

**Architecture:** Keep `EditorViewport` as the host that owns the new top-right button and overlay lifetime, but move overlay-specific layout and interaction into a dedicated `EditorViewportSettingsOverlayComponent`. Introduce shared clip-plane state and a reusable projection utility in the core/rendering path so both DirectX11 and Vulkan consume real camera near/far values instead of hardcoded constants. Add a reusable `EditorSlider` control so the near/far UI stays focused and testable.

**Tech Stack:** C#, xUnit, helengine editor UI entities and focus services, shared camera/rendering abstractions, DirectX11 and Vulkan renderer projects

---

## File Structure

- Modify: `engine/helengine.core/model/interfaces/ICamera.cs`
  - Add shared `NearPlaneDistance` and `FarPlaneDistance` properties to the camera abstraction.
- Modify: `engine/helengine.core/components/CameraComponent.cs`
  - Store near/far clip-plane state with default values and validation.
- Create: `engine/helengine.core/utils/CameraProjectionUtils.cs`
  - Centralize validated perspective-projection creation so both backends use the same clip-plane rules.
- Modify: `engine/helengine.directx11/DirectX11Renderer3D.cs`
  - Replace hardcoded perspective near/far values with `CameraProjectionUtils`.
- Modify: `engine/helengine.vulkan/VulkanRenderer3D.cs`
  - Replace hardcoded perspective near/far values with `CameraProjectionUtils`, keeping the existing Vulkan projection adjustment.
- Modify: `engine/helengine.editor/EditorViewportToolbarIconSet.cs`
  - Add a dedicated `SettingsIcon` to the viewport toolbar icon contract.
- Modify: `engine/helengine.editor.windows/content/textures/EditorToolbarIconLoader.cs`
  - Load the new toolbar settings icon from editor content.
- Create: `helengine.ui/helengine.editor.app/content/icons/toolbar/settings.png`
  - Provide the runtime toolbar asset for the new settings button.
- Create: `engine/helengine.editor/components/ui/EditorSliderScaleMode.cs`
  - Define linear vs logarithmic slider mapping modes.
- Create: `engine/helengine.editor/components/ui/EditorSlider.cs`
  - Implement reusable slider visuals, pointer drag, focus styling, and keyboard adjustment.
- Create: `engine/helengine.editor/components/ui/EditorViewportSettingsOverlayComponent.cs`
  - Own overlay layout, dismissal, focus targets, moved grid toggle, near/far sliders, and close button.
- Modify: `engine/helengine.editor/components/ui/EditorViewport.cs`
  - Replace the old grid toolbar button with the right-aligned settings button and wire the overlay into viewport lifecycle and layout.
- Create: `engine/helengine.editor.tests/rendering/CameraProjectionUtilsTests.cs`
  - Cover default clip planes, invalid-value clamping, and projection-matrix output.
- Create: `engine/helengine.editor.tests/EditorSliderTests.cs`
  - Cover slider drag behavior, logarithmic mapping, and keyboard adjustments.
- Create: `engine/helengine.editor.tests/EditorViewportSettingsOverlayTests.cs`
  - Cover overlay open/close behavior, outside-click dismissal, live slider updates, and close-button handling.
- Modify: `engine/helengine.editor.tests/EditorViewportKeyboardFocusTests.cs`
  - Replace grid-button assumptions with settings-button and overlay traversal coverage.
- Modify: `engine/helengine.editor.tests/EditorViewportGridToggleTests.cs`
  - Move grid-toggle assertions to the overlay flow instead of the old toolbar button.
- Modify: `engine/helengine.editor.tests/EditorSessionKeyboardFocusIntegrationTests.cs`
  - Update test icon-set construction to include the new settings icon and keep session-level viewport creation compiling.

## Task 1: Add Camera Clip-Plane State And Shared Projection Utility

**Files:**
- Create: `engine/helengine.editor.tests/rendering/CameraProjectionUtilsTests.cs`
- Modify: `engine/helengine.core/model/interfaces/ICamera.cs`
- Modify: `engine/helengine.core/components/CameraComponent.cs`
- Create: `engine/helengine.core/utils/CameraProjectionUtils.cs`
- Modify: `engine/helengine.directx11/DirectX11Renderer3D.cs:725-726,860-861`
- Modify: `engine/helengine.vulkan/VulkanRenderer3D.cs:574-575`

- [ ] **Step 1: Write the failing clip-plane and projection tests**

Create `engine/helengine.editor.tests/rendering/CameraProjectionUtilsTests.cs` with focused coverage for the new shared behavior:

```csharp
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies validated perspective-projection generation from camera clip-plane state.
    /// </summary>
    public class CameraProjectionUtilsTests {
        /// <summary>
        /// Ensures new camera components expose the current renderer defaults as authored clip-plane state.
        /// </summary>
        [Fact]
        public void CameraComponent_WhenConstructed_UsesDefaultClipPlaneDistances() {
            InitializeCore();
            CameraComponent camera = new CameraComponent();

            Assert.Equal(0.1f, camera.NearPlaneDistance);
            Assert.Equal(100f, camera.FarPlaneDistance);
        }

        /// <summary>
        /// Ensures the shared projection helper uses the authored clip-plane values instead of hardcoded renderer constants.
        /// </summary>
        [Fact]
        public void CreatePerspectiveProjection_WhenCameraUsesCustomClipPlanes_UsesAuthoredNearAndFarDistances() {
            InitializeCore();
            CameraComponent camera = new CameraComponent {
                NearPlaneDistance = 0.25f,
                FarPlaneDistance = 640f
            };

            float4x4 projection = CameraProjectionUtils.CreatePerspectiveProjection(camera, (float)(Math.PI / 4.0), 16f / 9f);
            float expectedM33 = camera.FarPlaneDistance / (camera.NearPlaneDistance - camera.FarPlaneDistance);
            float expectedM43 = camera.NearPlaneDistance * expectedM33;

            Assert.Equal(expectedM33, projection.M33, 5);
            Assert.Equal(expectedM43, projection.M43, 5);
        }

        /// <summary>
        /// Ensures invalid clip-plane values are clamped into a legal perspective range before projection creation.
        /// </summary>
        [Fact]
        public void CreatePerspectiveProjection_WhenCameraUsesInvalidClipPlanes_ClampsToLegalDistances() {
            InitializeCore();
            CameraComponent camera = new CameraComponent {
                NearPlaneDistance = -4f,
                FarPlaneDistance = 0.001f
            };

            float4x4 projection = CameraProjectionUtils.CreatePerspectiveProjection(camera, (float)(Math.PI / 4.0), 1.0f);
            float expectedNear = 0.01f;
            float expectedFar = 0.02f;
            float expectedM33 = expectedFar / (expectedNear - expectedFar);
            float expectedM43 = expectedNear * expectedM33;

            Assert.Equal(expectedM33, projection.M33, 5);
            Assert.Equal(expectedM43, projection.M43, 5);
        }

        /// <summary>
        /// Initializes a core instance so camera components can allocate render queues during these tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core(new CoreInitializationOptions {
                RenderList3DInitialCapacity = 4,
                RenderList2DInitialCapacity = 4
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }
    }
}
```

- [ ] **Step 2: Run the projection tests to verify they fail**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~CameraProjectionUtilsTests" -v minimal`

Expected: FAIL because `CameraComponent` does not expose clip-plane properties yet and `CameraProjectionUtils` does not exist.

- [ ] **Step 3: Add camera clip-plane state and the shared projection helper**

Implement the camera abstraction and shared projection utility with explicit validation constants:

```csharp
namespace helengine {
    /// <summary>
    /// Builds validated perspective projections from authored camera state.
    /// </summary>
    public static class CameraProjectionUtils {
        /// <summary>
        /// Minimum legal near clip-plane distance used by validated perspective projections.
        /// </summary>
        public const float MinimumNearPlaneDistance = 0.01f;

        /// <summary>
        /// Minimum legal separation preserved between the near and far clip planes.
        /// </summary>
        public const float MinimumPlaneSeparation = 0.01f;

        /// <summary>
        /// Creates a validated perspective projection matrix for one camera.
        /// </summary>
        /// <param name="camera">Camera providing clip-plane distances.</param>
        /// <param name="fieldOfView">Vertical field of view in radians.</param>
        /// <param name="aspectRatio">Viewport aspect ratio.</param>
        /// <returns>Perspective projection matrix built from validated clip-plane values.</returns>
        public static float4x4 CreatePerspectiveProjection(ICamera camera, float fieldOfView, float aspectRatio) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }

            float nearPlaneDistance = Math.Max(MinimumNearPlaneDistance, camera.NearPlaneDistance);
            float farPlaneDistance = Math.Max(nearPlaneDistance + MinimumPlaneSeparation, camera.FarPlaneDistance);
            float4x4.CreatePerspectiveFieldOfView(fieldOfView, aspectRatio, nearPlaneDistance, farPlaneDistance, out float4x4 projection);
            return projection;
        }
    }
}
```

Add the corresponding camera properties:

```csharp
/// <summary>
/// Gets or sets the near clip-plane distance used by perspective projection rendering.
/// </summary>
public float NearPlaneDistance { get; set; }

/// <summary>
/// Gets or sets the far clip-plane distance used by perspective projection rendering.
/// </summary>
public float FarPlaneDistance { get; set; }
```

Initialize them in `CameraComponent()` with `0.1f` and `100f`.

- [ ] **Step 4: Switch both 3D backends to the shared projection helper**

Replace each hardcoded `CreatePerspectiveFieldOfView(..., 0.1f, 100f, ...)` call with `CameraProjectionUtils.CreatePerspectiveProjection(...)`:

```csharp
float4x4 projection = CameraProjectionUtils.CreatePerspectiveProjection(camera, (float)Math.PI / 4.0f, viewport.Z / viewport.W);
```

and:

```csharp
float4x4 projection = CameraProjectionUtils.CreatePerspectiveProjection(camera, (float)(Math.PI / 4.0), (float)aspectRatio);
ApplyVulkanProjectionAdjustments(ref projection);
```

- [ ] **Step 5: Run the focused projection slice and backend builds**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~CameraProjectionUtilsTests" -v minimal`

Expected: PASS

Run: `rtk dotnet build engine/helengine.directx11/helengine.directx11.csproj -v minimal`

Expected: `0 errors`

Run: `rtk dotnet build engine/helengine.vulkan/helengine.vulkan.csproj -v minimal`

Expected: `0 errors`

- [ ] **Step 6: Commit the clip-plane and projection utility work**

```bash
git add engine/helengine.core/model/interfaces/ICamera.cs engine/helengine.core/components/CameraComponent.cs engine/helengine.core/utils/CameraProjectionUtils.cs engine/helengine.directx11/DirectX11Renderer3D.cs engine/helengine.vulkan/VulkanRenderer3D.cs engine/helengine.editor.tests/rendering/CameraProjectionUtilsTests.cs
git commit -m "feat: add camera clip plane projection state"
```

## Task 2: Add Settings-Button Plumbing And Right-Aligned Toolbar Layout

**Files:**
- Modify: `engine/helengine.editor/EditorViewportToolbarIconSet.cs`
- Modify: `engine/helengine.editor.windows/content/textures/EditorToolbarIconLoader.cs`
- Create: `helengine.ui/helengine.editor.app/content/icons/toolbar/settings.png`
- Modify: `engine/helengine.editor/components/ui/EditorViewport.cs`
- Modify: `engine/helengine.editor.tests/EditorViewportKeyboardFocusTests.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionKeyboardFocusIntegrationTests.cs`

- [ ] **Step 1: Write the failing settings-button focus and layout tests**

Extend `engine/helengine.editor.tests/EditorViewportKeyboardFocusTests.cs` with focused assertions for the new toolbar button:

```csharp
[Fact]
public void EditorViewport_DefinesDedicatedSettingsToolbarMembers() {
    FieldInfo focusTargetField = typeof(EditorViewport).GetField("SettingsButtonFocusTarget", BindingFlags.Instance | BindingFlags.NonPublic);
    FieldInfo backgroundField = typeof(EditorViewport).GetField("SettingsButtonBackground", BindingFlags.Instance | BindingFlags.NonPublic);
    FieldInfo interactableField = typeof(EditorViewport).GetField("SettingsButtonInteractable", BindingFlags.Instance | BindingFlags.NonPublic);

    Assert.NotNull(focusTargetField);
    Assert.NotNull(backgroundField);
    Assert.NotNull(interactableField);
}

[Fact]
public void LayoutToolbar_WhenViewportIsSized_RightAlignsSettingsButton() {
    InitializeCore();
    EditorViewport viewport = CreateViewport();
    viewport.Size = new int2(640, 360);

    InvokePrivateMethod(viewport, "UpdateViewport");

    EditorEntity settingsButtonRoot = GetPrivateField<EditorEntity>(viewport, "SettingsButtonRoot");
    Assert.True(settingsButtonRoot.Position.X > 560f, "Expected the settings button to sit near the right edge of a 640px toolbar.");
}
```

Update the test helper icon-set constructors in both viewport test files to account for the new settings icon argument:

```csharp
return new EditorViewportToolbarIconSet(
    CreateIconTexture(),
    CreateIconTexture(),
    CreateIconTexture(),
    CreateIconTexture(),
    CreateIconTexture(),
    CreateIconTexture(),
    CreateIconTexture(),
    CreateIconTexture(),
    CreateIconTexture(),
    CreateIconTexture());
```

- [ ] **Step 2: Run the viewport focus tests to verify they fail**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorViewportKeyboardFocusTests|FullyQualifiedName~EditorSessionKeyboardFocusIntegrationTests" -v minimal`

Expected: FAIL because `EditorViewport` does not define settings-button members and `EditorViewportToolbarIconSet` does not accept a settings icon yet.

- [ ] **Step 3: Add the new settings icon contract and runtime asset**

Update the toolbar icon contract:

```csharp
/// <summary>
/// Gets the texture used by the viewport settings button.
/// </summary>
public RuntimeTexture SettingsIcon { get; }
```

Load it from the editor content root:

```csharp
static readonly string SettingsIconPath = Path.Combine("content", "icons", "toolbar", "settings.png");
RuntimeTexture settingsIcon = LoadTexture(content, applicationRootPath, SettingsIconPath);
```

Then add `helengine.ui/helengine.editor.app/content/icons/toolbar/settings.png` at the same size and visual weight as the existing toolbar PNGs.

- [ ] **Step 4: Replace the old grid toolbar button with a right-aligned settings button shell**

In `EditorViewport.cs`, rename the grid-button fields to settings-button fields, initialize the new button with `ToolbarIcons.SettingsIcon`, and right-align it during `LayoutToolbar()`:

```csharp
void InitializeSettingsButton() {
    EditorEntity buttonRoot = new EditorEntity {
        LayerMask = LayerMask,
        Position = float3.Zero
    };

    SpriteComponent buttonBackground = new SpriteComponent {
        Size = new int2(ToolButtonWidth, ToolButtonHeight),
        Color = ThemeManager.Colors.SurfaceInput,
        RenderOrder = ToolbarSurfaceOrder
    };

    SpriteComponent buttonIcon = new SpriteComponent {
        Texture = ToolbarIcons.SettingsIcon,
        Size = new int2(ToolIconSize, ToolIconSize),
        Color = new byte4(255, 255, 255, 224),
        RenderOrder = ToolbarForegroundOrder
    };

    SettingsButtonFocusTarget = new EditorFocusTarget(
        ToolbarFocusGroup,
        ToolModes.Length,
        false,
        () => Enabled && buttonRoot.Enabled,
        ContainsSettingsButtonPoint,
        isFocused => {
            SettingsButtonKeyboardFocusState = isFocused;
            UpdateSettingsButtonVisuals();
        },
        key => key == Keys.Enter || key == Keys.Space,
        key => ToggleSettingsOverlay());
}
```

Position it near the right edge:

```csharp
float settingsButtonX = Math.Max(
    ToolbarPadding,
    toolbarWidth - ToolbarPadding - ToolButtonWidth);
SettingsButtonRoot.Position = new float3(settingsButtonX, buttonY, 0.1f);
```

- [ ] **Step 5: Run the focused toolbar tests to verify they pass**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorViewportKeyboardFocusTests|FullyQualifiedName~EditorSessionKeyboardFocusIntegrationTests" -v minimal`

Expected: PASS

- [ ] **Step 6: Commit the settings-button plumbing**

```bash
git add engine/helengine.editor/EditorViewportToolbarIconSet.cs engine/helengine.editor.windows/content/textures/EditorToolbarIconLoader.cs engine/helengine.editor/components/ui/EditorViewport.cs engine/helengine.editor.tests/EditorViewportKeyboardFocusTests.cs engine/helengine.editor.tests/EditorSessionKeyboardFocusIntegrationTests.cs helengine.ui/helengine.editor.app/content/icons/toolbar/settings.png
git commit -m "feat: add viewport settings toolbar button"
```

## Task 3: Add A Reusable Slider Control With Pointer And Keyboard Support

**Files:**
- Create: `engine/helengine.editor/components/ui/EditorSliderScaleMode.cs`
- Create: `engine/helengine.editor/components/ui/EditorSlider.cs`
- Create: `engine/helengine.editor.tests/EditorSliderTests.cs`

- [ ] **Step 1: Write the failing slider behavior tests**

Create `engine/helengine.editor.tests/EditorSliderTests.cs` with focused control-level coverage:

```csharp
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies reusable editor slider interaction behavior.
    /// </summary>
    public class EditorSliderTests {
        /// <summary>
        /// Ensures pointer dragging updates slider value and raises the live change event.
        /// </summary>
        [Fact]
        public void SetNormalizedValue_WhenPointerDragMovesThumb_RaisesValueChangedWithMappedValue() {
            EditorSlider slider = CreateSlider(0.01, 10.0, 0.1, EditorSliderScaleMode.Logarithmic);
            double? observedValue = null;
            slider.ValueChanged += value => observedValue = value;

            slider.SetNormalizedValue(0.5);

            Assert.NotNull(observedValue);
            Assert.InRange(observedValue.Value, 0.3, 0.4);
        }

        /// <summary>
        /// Ensures logarithmic mapping keeps the midpoint above the linear midpoint for wide ranges.
        /// </summary>
        [Fact]
        public void SetNormalizedValue_WhenScaleModeIsLogarithmic_UsesLogarithmicMapping() {
            EditorSlider slider = CreateSlider(1.0, 5000.0, 100.0, EditorSliderScaleMode.Logarithmic);

            slider.SetNormalizedValue(0.5);

            Assert.InRange(slider.Value, 60.0, 90.0);
        }

        /// <summary>
        /// Ensures keyboard adjustments move the slider by the configured step while preserving bounds.
        /// </summary>
        [Fact]
        public void AdjustFromKey_WhenArrowKeysArePressed_MovesValueWithinRange() {
            EditorSlider slider = CreateSlider(0.01, 10.0, 0.5, EditorSliderScaleMode.Linear);

            slider.AdjustFromKey(Keys.Right);
            slider.AdjustFromKey(Keys.Left);
            slider.AdjustFromKey(Keys.Left);

            Assert.Equal(0.49, slider.Value, 2);
        }

        EditorSlider CreateSlider(double minimumValue, double maximumValue, double initialValue, EditorSliderScaleMode scaleMode) {
            return new EditorSlider(minimumValue, maximumValue, initialValue, scaleMode, 120, 10);
        }
    }
}
```

- [ ] **Step 2: Run the slider tests to verify they fail**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSliderTests" -v minimal`

Expected: FAIL because `EditorSlider` and `EditorSliderScaleMode` do not exist yet.

- [ ] **Step 3: Implement the reusable slider control**

Create the scale-mode enum:

```csharp
namespace helengine {
    /// <summary>
    /// Defines how one editor slider maps normalized track positions into authored values.
    /// </summary>
    public enum EditorSliderScaleMode {
        Linear,
        Logarithmic
    }
}
```

Create the slider class with focusable value logic and live events:

```csharp
namespace helengine {
    /// <summary>
    /// Reusable editor slider that supports pointer dragging and keyboard adjustment.
    /// </summary>
    public class EditorSlider : EditorEntity {
        /// <summary>
        /// Raised whenever the authored slider value changes.
        /// </summary>
        public event Action<double> ValueChanged;

        /// <summary>
        /// Gets the current authored slider value.
        /// </summary>
        public double Value { get; private set; }

        /// <summary>
        /// Sets the authored slider value directly after clamping it into the legal range.
        /// </summary>
        /// <param name="value">Authored slider value to apply.</param>
        public void SetValue(double value) {
            double clampedValue = Math.Clamp(value, MinimumValue, MaximumValue);
            if (Math.Abs(clampedValue - Value) <= 0.000001) {
                return;
            }

            Value = clampedValue;
            ValueChanged?.Invoke(Value);
        }

        /// <summary>
        /// Sets the slider value from a normalized track position.
        /// </summary>
        /// <param name="normalizedValue">Track position from 0 to 1.</param>
        public void SetNormalizedValue(double normalizedValue) {
            double clampedNormalizedValue = Math.Clamp(normalizedValue, 0.0, 1.0);
            double nextValue = ScaleMode == EditorSliderScaleMode.Logarithmic
                ? Math.Exp(Math.Log(MinimumValue) + ((Math.Log(MaximumValue) - Math.Log(MinimumValue)) * clampedNormalizedValue))
                : MinimumValue + ((MaximumValue - MinimumValue) * clampedNormalizedValue);
            SetValue(nextValue);
        }

        /// <summary>
        /// Applies one keyboard adjustment for the provided key.
        /// </summary>
        /// <param name="key">Adjustment key routed by the focus target.</param>
        public void AdjustFromKey(Keys key) {
            double delta = key == Keys.Right ? KeyboardStep : -KeyboardStep;
            SetValue(Value + delta);
        }
    }
}
```

Use explicit child entities inside the class for the track and thumb so overlay code can host the slider as one focused unit.

- [ ] **Step 4: Run the slider tests to verify they pass**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSliderTests" -v minimal`

Expected: PASS

- [ ] **Step 5: Commit the reusable slider control**

```bash
git add engine/helengine.editor/components/ui/EditorSliderScaleMode.cs engine/helengine.editor/components/ui/EditorSlider.cs engine/helengine.editor.tests/EditorSliderTests.cs
git commit -m "feat: add reusable editor slider"
```

## Task 4: Add The Viewport Settings Overlay Shell, Dismissal, And Moved Grid Toggle

**Files:**
- Create: `engine/helengine.editor/components/ui/EditorViewportSettingsOverlayComponent.cs`
- Modify: `engine/helengine.editor/components/ui/EditorViewport.cs`
- Create: `engine/helengine.editor.tests/EditorViewportSettingsOverlayTests.cs`
- Modify: `engine/helengine.editor.tests/EditorViewportGridToggleTests.cs`
- Modify: `engine/helengine.editor.tests/EditorViewportKeyboardFocusTests.cs`

- [ ] **Step 1: Write the failing overlay interaction and grid-toggle tests**

Create `engine/helengine.editor.tests/EditorViewportSettingsOverlayTests.cs` with the shell interaction coverage:

```csharp
using System.Reflection;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies viewport settings overlay lifetime and focus behavior.
    /// </summary>
    public class EditorViewportSettingsOverlayTests {
        /// <summary>
        /// Ensures keyboard activation of the settings button opens the overlay and focuses the first overlay control.
        /// </summary>
        [Fact]
        public void ActivateSettingsButton_WhenOverlayIsClosed_OpensOverlayAndFocusesGridToggle() {
            InitializeCore();
            EditorViewport viewport = CreateViewport();
            EditorFocusTarget settingsTarget = GetPrivateField<EditorFocusTarget>(viewport, "SettingsButtonFocusTarget");
            EditorViewportSettingsOverlayComponent overlayComponent = GetPrivateField<EditorViewportSettingsOverlayComponent>(viewport, "SettingsOverlayComponent");

            settingsTarget.ActivateFromKey(Keys.Enter);

            Assert.True(overlayComponent.IsOpen);
            Assert.Same(
                overlayComponent.GridToggleFocusTarget,
                GetPrivateStaticField<IFocusTarget>(typeof(EditorKeyboardFocusService), "FocusedTarget"));
        }

        /// <summary>
        /// Ensures tab traversal moves through the overlay controls in the expected order.
        /// </summary>
        [Fact]
        public void HandleTab_WhenOverlayIsOpen_TraversesGridNearFarAndCloseControlsInOrder() {
            InitializeCore();
            EditorViewport viewport = CreateViewport();
            EditorViewportSettingsOverlayComponent overlayComponent = GetPrivateField<EditorViewportSettingsOverlayComponent>(viewport, "SettingsOverlayComponent");
            EditorFocusTarget settingsTarget = GetPrivateField<EditorFocusTarget>(viewport, "SettingsButtonFocusTarget");

            settingsTarget.ActivateFromKey(Keys.Enter);
            EditorKeyboardFocusService.HandleTab(true);
            Assert.Same(overlayComponent.NearPlaneFocusTarget, GetPrivateStaticField<IFocusTarget>(typeof(EditorKeyboardFocusService), "FocusedTarget"));

            EditorKeyboardFocusService.HandleTab(true);
            Assert.Same(overlayComponent.FarPlaneFocusTarget, GetPrivateStaticField<IFocusTarget>(typeof(EditorKeyboardFocusService), "FocusedTarget"));

            EditorKeyboardFocusService.HandleTab(true);
            Assert.Same(overlayComponent.CloseButtonFocusTarget, GetPrivateStaticField<IFocusTarget>(typeof(EditorKeyboardFocusService), "FocusedTarget"));
        }

        /// <summary>
        /// Ensures clicking outside the overlay closes it and returns focus to the settings button.
        /// </summary>
        [Fact]
        public void Update_WhenPointerClicksOutsideOverlay_ClosesOverlayAndRestoresSettingsButtonFocus() {
            InitializeCore();
            EditorViewport viewport = CreateViewport();
            EditorViewportSettingsOverlayComponent overlayComponent = GetPrivateField<EditorViewportSettingsOverlayComponent>(viewport, "SettingsOverlayComponent");
            EditorFocusTarget settingsTarget = GetPrivateField<EditorFocusTarget>(viewport, "SettingsButtonFocusTarget");

            settingsTarget.ActivateFromKey(Keys.Enter);
            overlayComponent.HandleOutsidePointerPressed(new int2(4, 4), settingsTarget);

            Assert.False(overlayComponent.IsOpen);
            Assert.Same(
                settingsTarget,
                GetPrivateStaticField<IFocusTarget>(typeof(EditorKeyboardFocusService), "FocusedTarget"));
        }

        /// <summary>
        /// Ensures the overlay also closes from the keyboard escape path.
        /// </summary>
        [Fact]
        public void HandleEscape_WhenOverlayIsOpen_ClosesOverlayAndRestoresSettingsButtonFocus() {
            InitializeCore();
            EditorViewport viewport = CreateViewport();
            EditorViewportSettingsOverlayComponent overlayComponent = GetPrivateField<EditorViewportSettingsOverlayComponent>(viewport, "SettingsOverlayComponent");
            EditorFocusTarget settingsTarget = GetPrivateField<EditorFocusTarget>(viewport, "SettingsButtonFocusTarget");

            settingsTarget.ActivateFromKey(Keys.Enter);
            overlayComponent.HandleEscapeKey(settingsTarget);

            Assert.False(overlayComponent.IsOpen);
            Assert.Same(
                settingsTarget,
                GetPrivateStaticField<IFocusTarget>(typeof(EditorKeyboardFocusService), "FocusedTarget"));
        }

        /// <summary>
        /// Ensures keyboard activation of the close button closes the overlay without changing viewport ownership.
        /// </summary>
        [Fact]
        public void ActivateCloseButton_WhenOverlayIsOpen_ClosesOverlay() {
            InitializeCore();
            EditorViewport viewport = CreateViewport();
            EditorViewportSettingsOverlayComponent overlayComponent = GetPrivateField<EditorViewportSettingsOverlayComponent>(viewport, "SettingsOverlayComponent");
            EditorFocusTarget settingsTarget = GetPrivateField<EditorFocusTarget>(viewport, "SettingsButtonFocusTarget");

            settingsTarget.ActivateFromKey(Keys.Enter);

            overlayComponent.CloseButtonFocusTarget.ActivateFromKey(Keys.Enter);

            Assert.False(overlayComponent.IsOpen);
        }

        T GetPrivateStaticField<T>(Type type, string fieldName) {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
            return (T)field.GetValue(null);
        }
    }
}
```

Rewrite `engine/helengine.editor.tests/EditorViewportGridToggleTests.cs` to route through the overlay instead of the removed toolbar button:

```csharp
[Fact]
public void ActivateOverlayGridToggle_WhenPressed_TogglesViewportGridVisibility() {
    EditorViewport viewport = CreateViewportForGridTesting((ushort)(EditorLayerMasks.SceneObjects | EditorLayerMasks.SceneGizmo));
    EditorViewportSettingsOverlayComponent overlayComponent = GetPrivateField<EditorViewportSettingsOverlayComponent>(viewport, "SettingsOverlayComponent");
    EditorFocusTarget settingsTarget = GetPrivateField<EditorFocusTarget>(viewport, "SettingsButtonFocusTarget");

    settingsTarget.ActivateFromKey(Keys.Enter);
    overlayComponent.GridToggleFocusTarget.ActivateFromKey(Keys.Space);

    Assert.Equal(
        (ushort)(EditorLayerMasks.SceneObjects | EditorLayerMasks.SceneGizmo | EditorLayerMasks.SceneGrid),
        viewport.Camera.LayerMask);
}
```

- [ ] **Step 2: Run the overlay and grid-toggle tests to verify they fail**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorViewportSettingsOverlayTests|FullyQualifiedName~EditorViewportGridToggleTests|FullyQualifiedName~EditorViewportKeyboardFocusTests" -v minimal`

Expected: FAIL because the overlay component, overlay focus targets, and open/close state do not exist yet.

- [ ] **Step 3: Implement the overlay component and move grid-toggle ownership into it**

Create `EditorViewportSettingsOverlayComponent.cs` as the dedicated overlay owner. The component should create its child UI on attach, register focus targets while open, and expose explicit open/close methods:

```csharp
namespace helengine {
    /// <summary>
    /// Owns the non-modal viewport settings overlay shown from the top-right toolbar button.
    /// </summary>
    public class EditorViewportSettingsOverlayComponent : UpdateComponent {
        /// <summary>
        /// Gets whether the overlay is currently visible.
        /// </summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// Gets the focus target bound to the overlay grid toggle row.
        /// </summary>
        public EditorFocusTarget GridToggleFocusTarget { get; private set; }

        /// <summary>
        /// Gets the focus target bound to the overlay close button.
        /// </summary>
        public EditorFocusTarget CloseButtonFocusTarget { get; private set; }

        /// <summary>
        /// Gets the focus target bound to the near-plane slider.
        /// </summary>
        public EditorFocusTarget NearPlaneFocusTarget { get; private set; }

        /// <summary>
        /// Gets the focus target bound to the far-plane slider.
        /// </summary>
        public EditorFocusTarget FarPlaneFocusTarget { get; private set; }

        /// <summary>
        /// Gets the slider that controls the viewport near clip plane.
        /// </summary>
        public EditorSlider NearPlaneSlider { get; private set; }

        /// <summary>
        /// Gets the slider that controls the viewport far clip plane.
        /// </summary>
        public EditorSlider FarPlaneSlider { get; private set; }

        /// <summary>
        /// Opens the overlay and focuses its first control.
        /// </summary>
        public void Open() {
            if (IsOpen) {
                return;
            }

            IsOpen = true;
            OverlayRoot.Enabled = true;
            EditorKeyboardFocusService.SetFocusedTarget(GridToggleFocusTarget);
        }

        /// <summary>
        /// Closes the overlay and returns focus to the provided settings button target.
        /// </summary>
        /// <param name="settingsButtonFocusTarget">Settings-button focus target that should regain focus.</param>
        public void Close(EditorFocusTarget settingsButtonFocusTarget) {
            if (!IsOpen) {
                return;
            }

            IsOpen = false;
            OverlayRoot.Enabled = false;
            if (settingsButtonFocusTarget != null) {
                EditorKeyboardFocusService.SetFocusedTarget(settingsButtonFocusTarget);
            }
        }

        /// <summary>
        /// Closes the overlay when one pointer press lands outside its bounds.
        /// </summary>
        /// <param name="screenPoint">Pointer location in screen coordinates.</param>
        /// <param name="settingsButtonFocusTarget">Settings-button focus target that should regain focus.</param>
        public void HandleOutsidePointerPressed(int2 screenPoint, EditorFocusTarget settingsButtonFocusTarget) {
            if (!IsOpen || ContainsOverlayPoint(screenPoint)) {
                return;
            }

            Close(settingsButtonFocusTarget);
        }

        /// <summary>
        /// Closes the overlay in response to an escape-key request.
        /// </summary>
        /// <param name="settingsButtonFocusTarget">Settings-button focus target that should regain focus.</param>
        public void HandleEscapeKey(EditorFocusTarget settingsButtonFocusTarget) {
            if (!IsOpen) {
                return;
            }

            Close(settingsButtonFocusTarget);
        }
    }
}
```

Wire it into `EditorViewport`:

```csharp
InitializeSettingsButton();
InitializeSnapControls();
SettingsOverlayComponent = new EditorViewportSettingsOverlayComponent(Camera, Font, ToolbarIcons.GridIcon, LayerMask, ToolbarSurfaceOrder, ToolbarForegroundOrder);
AddComponent(SettingsOverlayComponent);
```

Replace `InitializeGridButton()` and `HandleGridButtonCursor(...)` with settings-button open/close behavior and an overlay-owned `GridToggleFocusTarget`.

- [ ] **Step 4: Run the overlay shell tests to verify they pass**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorViewportSettingsOverlayTests|FullyQualifiedName~EditorViewportGridToggleTests|FullyQualifiedName~EditorViewportKeyboardFocusTests" -v minimal`

Expected: PASS

- [ ] **Step 5: Commit the overlay shell and moved grid toggle**

```bash
git add engine/helengine.editor/components/ui/EditorViewportSettingsOverlayComponent.cs engine/helengine.editor/components/ui/EditorViewport.cs engine/helengine.editor.tests/EditorViewportSettingsOverlayTests.cs engine/helengine.editor.tests/EditorViewportGridToggleTests.cs engine/helengine.editor.tests/EditorViewportKeyboardFocusTests.cs
git commit -m "feat: add viewport settings overlay shell"
```

## Task 5: Bind Live Near/Far Sliders To The Viewport Camera

**Files:**
- Modify: `engine/helengine.editor/components/ui/EditorViewportSettingsOverlayComponent.cs`
- Modify: `engine/helengine.editor/components/ui/EditorViewport.cs`
- Modify: `engine/helengine.editor.tests/EditorViewportSettingsOverlayTests.cs`

- [ ] **Step 1: Write the failing live-update and clamp tests**

Extend `engine/helengine.editor.tests/EditorViewportSettingsOverlayTests.cs` with live camera binding coverage:

```csharp
[Fact]
public void SetNearSliderValue_WhenOverlayIsOpen_UpdatesCameraNearPlaneDistanceImmediately() {
    InitializeCore();
    EditorViewport viewport = CreateViewport();
    EditorFocusTarget settingsTarget = GetPrivateField<EditorFocusTarget>(viewport, "SettingsButtonFocusTarget");
    EditorViewportSettingsOverlayComponent overlayComponent = GetPrivateField<EditorViewportSettingsOverlayComponent>(viewport, "SettingsOverlayComponent");
    CameraComponent camera = viewport.Camera;
    settingsTarget.ActivateFromKey(Keys.Enter);

    overlayComponent.NearPlaneSlider.SetValue(0.5);

    Assert.Equal(0.5f, camera.NearPlaneDistance);
}

[Fact]
public void SetFarSliderValue_WhenOverlayIsOpen_UpdatesCameraFarPlaneDistanceImmediately() {
    InitializeCore();
    EditorViewport viewport = CreateViewport();
    EditorFocusTarget settingsTarget = GetPrivateField<EditorFocusTarget>(viewport, "SettingsButtonFocusTarget");
    EditorViewportSettingsOverlayComponent overlayComponent = GetPrivateField<EditorViewportSettingsOverlayComponent>(viewport, "SettingsOverlayComponent");
    CameraComponent camera = viewport.Camera;
    settingsTarget.ActivateFromKey(Keys.Enter);

    overlayComponent.FarPlaneSlider.SetValue(750.0);

    Assert.Equal(750f, camera.FarPlaneDistance);
}

[Fact]
public void SetNearSliderValue_WhenDraggedPastFarPlane_ClampsBelowFarPlaneByMinimumSeparation() {
    InitializeCore();
    EditorViewport viewport = CreateViewport();
    EditorFocusTarget settingsTarget = GetPrivateField<EditorFocusTarget>(viewport, "SettingsButtonFocusTarget");
    EditorViewportSettingsOverlayComponent overlayComponent = GetPrivateField<EditorViewportSettingsOverlayComponent>(viewport, "SettingsOverlayComponent");
    CameraComponent camera = viewport.Camera;
    camera.FarPlaneDistance = 2.0f;
    settingsTarget.ActivateFromKey(Keys.Enter);

    overlayComponent.NearPlaneSlider.SetValue(4.0);

    Assert.Equal(1.99f, camera.NearPlaneDistance, 2);
}
```

- [ ] **Step 2: Run the near/far overlay tests to verify they fail**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorViewportSettingsOverlayTests" -v minimal`

Expected: FAIL because the overlay does not create near/far sliders or bind them to camera clip-plane values yet.

- [ ] **Step 3: Hook the overlay sliders to live camera state and enforce cross-clamping**

Bind slider `ValueChanged` handlers to explicit overlay methods:

```csharp
void HandleNearPlaneSliderValueChanged(double value) {
    float nextNearPlaneDistance = (float)Math.Min(
        value,
        Camera.FarPlaneDistance - CameraProjectionUtils.MinimumPlaneSeparation);
    Camera.NearPlaneDistance = nextNearPlaneDistance;
    OverlayNearPlaneSlider.SetValue(nextNearPlaneDistance);
    RefreshNearFarValueLabels();
}

void HandleFarPlaneSliderValueChanged(double value) {
    float minimumFarPlaneDistance = Camera.NearPlaneDistance + CameraProjectionUtils.MinimumPlaneSeparation;
    float nextFarPlaneDistance = (float)Math.Max(value, minimumFarPlaneDistance);
    Camera.FarPlaneDistance = nextFarPlaneDistance;
    OverlayFarPlaneSlider.SetValue(nextFarPlaneDistance);
    RefreshNearFarValueLabels();
}
```

Format the numeric labels inline with the spec:

```csharp
string FormatPlaneDistance(float value) {
    if (value < 10f) {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
    if (value < 100f) {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }
    return value.ToString("0", CultureInfo.InvariantCulture);
}
```

Also route `Keys.Left` and `Keys.Right` through each slider focus target so keyboard users can change values without leaving the overlay.

- [ ] **Step 4: Run the full overlay slice to verify it passes**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorViewportSettingsOverlayTests|FullyQualifiedName~EditorSliderTests|FullyQualifiedName~EditorViewportGridToggleTests|FullyQualifiedName~EditorViewportKeyboardFocusTests" -v minimal`

Expected: PASS

- [ ] **Step 5: Commit the live near/far binding**

```bash
git add engine/helengine.editor/components/ui/EditorViewportSettingsOverlayComponent.cs engine/helengine.editor/components/ui/EditorViewport.cs engine/helengine.editor.tests/EditorViewportSettingsOverlayTests.cs
git commit -m "feat: add live viewport clip plane controls"
```

## Task 6: Final Verification

**Files:**
- Verify: `engine/helengine.core/model/interfaces/ICamera.cs`
- Verify: `engine/helengine.core/components/CameraComponent.cs`
- Verify: `engine/helengine.core/utils/CameraProjectionUtils.cs`
- Verify: `engine/helengine.directx11/DirectX11Renderer3D.cs`
- Verify: `engine/helengine.vulkan/VulkanRenderer3D.cs`
- Verify: `engine/helengine.editor/components/ui/EditorSlider.cs`
- Verify: `engine/helengine.editor/components/ui/EditorViewportSettingsOverlayComponent.cs`
- Verify: `engine/helengine.editor/components/ui/EditorViewport.cs`

- [ ] **Step 1: Run the focused viewport and rendering regression slice**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~CameraProjectionUtilsTests|FullyQualifiedName~EditorSliderTests|FullyQualifiedName~EditorViewportSettingsOverlayTests|FullyQualifiedName~EditorViewportGridToggleTests|FullyQualifiedName~EditorViewportKeyboardFocusTests|FullyQualifiedName~EditorSessionKeyboardFocusIntegrationTests|FullyQualifiedName~DirectX11RendererPlannedExecutionTests" -v minimal`

Expected: PASS

- [ ] **Step 2: Build the affected projects**

Run: `rtk dotnet build engine/helengine.editor/helengine.editor.csproj -v minimal`

Expected: `0 errors`

Run: `rtk dotnet build engine/helengine.editor.windows/helengine.editor.windows.csproj -v minimal`

Expected: `0 errors`

Run: `rtk dotnet build engine/helengine.directx11/helengine.directx11.csproj -v minimal`

Expected: `0 errors`

Run: `rtk dotnet build engine/helengine.vulkan/helengine.vulkan.csproj -v minimal`

Expected: `0 errors`

- [ ] **Step 3: Commit the final verified state**

```bash
git add engine/helengine.core/model/interfaces/ICamera.cs engine/helengine.core/components/CameraComponent.cs engine/helengine.core/utils/CameraProjectionUtils.cs engine/helengine.directx11/DirectX11Renderer3D.cs engine/helengine.vulkan/VulkanRenderer3D.cs engine/helengine.editor/EditorViewportToolbarIconSet.cs engine/helengine.editor.windows/content/textures/EditorToolbarIconLoader.cs engine/helengine.editor/components/ui/EditorSliderScaleMode.cs engine/helengine.editor/components/ui/EditorSlider.cs engine/helengine.editor/components/ui/EditorViewportSettingsOverlayComponent.cs engine/helengine.editor/components/ui/EditorViewport.cs engine/helengine.editor.tests/rendering/CameraProjectionUtilsTests.cs engine/helengine.editor.tests/EditorSliderTests.cs engine/helengine.editor.tests/EditorViewportSettingsOverlayTests.cs engine/helengine.editor.tests/EditorViewportGridToggleTests.cs engine/helengine.editor.tests/EditorViewportKeyboardFocusTests.cs engine/helengine.editor.tests/EditorSessionKeyboardFocusIntegrationTests.cs helengine.ui/helengine.editor.app/content/icons/toolbar/settings.png
git commit -m "feat: add viewport settings overlay"
```
