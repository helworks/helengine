# Demo Disc Copy Removal And Player Background Input Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the baked demo-disc title and subtitle from the generated city main menu and make player background-input behavior explicit and default-off through the existing input-system toggle.

**Architecture:** Keep the demo-menu change scoped to the demo-disc generation path: update the writer and baked menu scene factory so the generated menu source and baked scene no longer include the title or subtitle entities. Keep background input centralized in `InputSystem` and `IInputBackend`, then lock the default-off behavior with backend and input-system regressions instead of inventing a second host-specific flag.

**Tech Stack:** C#, xUnit, Windows input backend, demo-disc scene writer, editor-side baked menu scene generation.

---

### Task 1: Add Demo Menu Removal Regressions

**Files:**
- Modify: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`
- Test: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`

- [ ] **Step 1: Write the failing provider-source regression**

Add this test near the other writer-source assertions:

```csharp
/// <summary>
/// Ensures generated demo-disc provider source no longer emits the removed title and subtitle copy.
/// </summary>
[Fact]
public void WriteAll_WhenMenuSourcesAreGenerated_DoesNotEmitRemovedTitleOrSubtitleCopy() {
    DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

    writer.WriteAll(ProjectRootPath);

    string providerSourcePath = Path.Combine(ProjectRootPath, "assets", "codebase", "menu", "DemoDiscMenuDefinitionProvider.cs");
    string providerSource = File.ReadAllText(providerSourcePath);

    Assert.DoesNotContain("Helengine Demo Disc", providerSource, StringComparison.Ordinal);
    Assert.DoesNotContain("Lilac nights, bright experiments, and a little street grit.", providerSource, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Write the failing baked-scene hierarchy regression**

Add this helper and test in the same file:

```csharp
/// <summary>
/// Ensures the generated baked scene no longer contains dedicated title or subtitle text entities.
/// </summary>
[Fact]
public void WriteAll_WhenMenuSceneIsGenerated_DoesNotBakeTitleOrSubtitleEntities() {
    DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

    writer.WriteAll(ProjectRootPath);

    SceneAsset sceneAsset = ReadGeneratedSceneAsset();
    SceneEntityAsset menuEntity = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscMenuRoot");
    SceneEntityAsset generatedRoot = Assert.Single(menuEntity.Children, entity => entity.Name == DemoMenuLayout.GeneratedRootEntityName);

    Assert.DoesNotContain(generatedRoot.Children, child => string.Equals(child.Name, "demo-disc-menu-title", StringComparison.Ordinal));
    Assert.DoesNotContain(generatedRoot.Children, child => string.Equals(child.Name, "demo-disc-menu-subtitle", StringComparison.Ordinal));
}
```

And add this helper once:

```csharp
/// <summary>
/// Reads the generated demo-disc scene asset from disk.
/// </summary>
/// <returns>Generated scene asset.</returns>
SceneAsset ReadGeneratedSceneAsset() {
    string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", "DemoDiscMainMenu.helen");
    using FileStream stream = File.OpenRead(scenePath);
    return Assert.IsType<SceneAsset>(EditorAssetBinarySerializer.Deserialize(stream));
}
```

- [ ] **Step 3: Run the writer tests to verify both regressions fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~DemoDiscSceneWriterTests.WriteAll_WhenMenuSourcesAreGenerated_DoesNotEmitRemovedTitleOrSubtitleCopy|FullyQualifiedName~DemoDiscSceneWriterTests.WriteAll_WhenMenuSceneIsGenerated_DoesNotBakeTitleOrSubtitleEntities" -v minimal
```

Expected:
- FAIL because `DemoDiscMenuDefinitionProvider.cs` still contains `Helengine Demo Disc`
- FAIL because the baked scene still contains `demo-disc-menu-title` and `demo-disc-menu-subtitle`

- [ ] **Step 4: Commit the failing tests**

```bash
rtk git add engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs
rtk git commit -m "test: cover demo disc title and subtitle removal"
```

### Task 2: Remove The Demo Disc Title And Subtitle From The Generated Menu

**Files:**
- Modify: `tools/demo-disc-scene-writer/DemoDiscSceneWriter.cs`
- Modify: `engine/helengine.editor/managers/menu/DemoMenuSceneAssetFactory.cs`
- Modify: `engine/helengine.core/menu/MenuDefinition.cs`
- Test: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`

- [ ] **Step 1: Relax the menu-definition contract so the demo menu can omit title text**

Update `engine/helengine.core/menu/MenuDefinition.cs` so empty title and subtitle values are valid while font paths and panels remain required.

Replace the constructor guard and assignments with:

```csharp
public MenuDefinition(
    string title,
    string subtitle,
    string initialPanelId,
    string titleFontPath,
    string bodyFontPath,
    byte4 backgroundColor,
    byte4 surfaceColor,
    byte4 surfaceBorderColor,
    byte4 accentColor,
    byte4 accentSecondaryColor,
    byte4 textColor,
    byte4 mutedTextColor,
    MenuPanelDefinition[] panels) {
    if (title == null) {
        throw new ArgumentNullException(nameof(title));
    }
    if (subtitle == null) {
        throw new ArgumentNullException(nameof(subtitle));
    }
    if (string.IsNullOrWhiteSpace(initialPanelId)) {
        throw new ArgumentException("Initial panel id must be provided.", nameof(initialPanelId));
    }
    if (string.IsNullOrWhiteSpace(titleFontPath)) {
        throw new ArgumentException("Title font path must be provided.", nameof(titleFontPath));
    }
    if (string.IsNullOrWhiteSpace(bodyFontPath)) {
        throw new ArgumentException("Body font path must be provided.", nameof(bodyFontPath));
    }
    if (panels == null) {
        throw new ArgumentNullException(nameof(panels));
    }
    if (panels.Length == 0) {
        throw new InvalidOperationException("Menu definitions must contain at least one panel.");
    }

    Title = title;
    Subtitle = subtitle;
    InitialPanelId = initialPanelId;
    TitleFontPath = titleFontPath;
    BodyFontPath = bodyFontPath;
    BackgroundColor = backgroundColor;
    SurfaceColor = surfaceColor;
    SurfaceBorderColor = surfaceBorderColor;
    AccentColor = accentColor;
    AccentSecondaryColor = accentSecondaryColor;
    TextColor = textColor;
    MutedTextColor = mutedTextColor;
    Panels = panels;
}
```

Also update the XML comments so `Title` and `Subtitle` describe them as optional menu copy.

- [ ] **Step 2: Remove the strings from the demo-disc writer source of truth**

In `tools/demo-disc-scene-writer/DemoDiscSceneWriter.cs`, change both `BuildMenuDefinition()` and `BuildMenuDefinitionProviderSource()` so they emit empty title/subtitle values:

```csharp
return new MenuDefinition(
    string.Empty,
    string.Empty,
    "main",
    "Fonts/DemoDiscTitle.ttf",
    "Fonts/DemoDiscBody.ttf",
    new byte4(30, 17, 41, 255),
    new byte4(60, 41, 76, 232),
    new byte4(135, 94, 163, 255),
    new byte4(201, 147, 255, 255),
    new byte4(118, 219, 209, 255),
    new byte4(249, 243, 255, 255),
    new byte4(211, 198, 228, 255),
    new[] {
        // existing panel definitions stay unchanged
    });
```

And in the generated-source builder:

```csharp
builder.AppendLine("            return new MenuDefinition(");
builder.AppendLine("                string.Empty,");
builder.AppendLine("                string.Empty,");
builder.AppendLine("                \"main\",");
```

- [ ] **Step 3: Skip baking the title and subtitle entities when the strings are empty**

In `engine/helengine.editor/managers/menu/DemoMenuSceneAssetFactory.cs`, replace the unconditional text creation with guarded emission:

```csharp
List<SceneEntityAsset> children = new List<SceneEntityAsset>();
children.Add(BuildBackgroundEntityAsset("demo-disc-menu-background", new float3(0f, 0f, 0f), new int2(DemoMenuLayout.CanvasWidth, DemoMenuLayout.CanvasHeight), 0f, 0f, definition.BackgroundColor, definition.BackgroundColor, 10));
children.Add(BuildBackgroundEntityAsset("demo-disc-menu-accent", new float3(72f, 64f, 0f), new int2(18, 520), 9f, 0f, definition.AccentSecondaryColor, definition.AccentSecondaryColor, 20));

if (!string.IsNullOrWhiteSpace(definition.Title)) {
    children.Add(BuildTextEntityAsset("demo-disc-menu-title", new float3(96f, 56f, 0.1f), definition.Title, definition.TitleFontPath, definition.TextColor, new int2(600, 64), 40));
}
if (!string.IsNullOrWhiteSpace(definition.Subtitle)) {
    children.Add(BuildTextEntityAsset("demo-disc-menu-subtitle", new float3(100f, 118f, 0.1f), definition.Subtitle, definition.BodyFontPath, definition.MutedTextColor, new int2(700, 36), 41));
}
```

Do not add placeholder entities or fake invisible text rows.

- [ ] **Step 4: Run the focused writer regressions**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~DemoDiscSceneWriterTests.WriteAll_WhenMenuSourcesAreGenerated_DoesNotEmitRemovedTitleOrSubtitleCopy|FullyQualifiedName~DemoDiscSceneWriterTests.WriteAll_WhenMenuSceneIsGenerated_DoesNotBakeTitleOrSubtitleEntities|FullyQualifiedName~DemoDiscSceneWriterTests.WriteAll_WhenMenuSceneIsGenerated_BakesTheMenuHierarchyIntoTheScene" -v minimal
```

Expected:
- PASS for the two new removal tests
- PASS for the existing baked-hierarchy regression

- [ ] **Step 5: Regenerate the city menu source from the writer**

Run:

```bash
rtk dotnet run --project tools/demo-disc-scene-writer/helengine.demo-disc.scene-writer.csproj -- C:\dev\helprojs\city
```

Expected:
- tool exits successfully
- `C:\dev\helprojs\city\assets\codebase\menu\DemoDiscMenuDefinitionProvider.cs` no longer contains the removed strings

- [ ] **Step 6: Commit the demo-disc removal implementation**

```bash
rtk git add tools/demo-disc-scene-writer/DemoDiscSceneWriter.cs engine/helengine.editor/managers/menu/DemoMenuSceneAssetFactory.cs engine/helengine.core/menu/MenuDefinition.cs engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs
rtk git commit -m "fix: remove demo disc title and subtitle copy"
```

### Task 3: Add Explicit Default-Off Background Input Regressions

**Files:**
- Modify: `engine/helengine.editor.tests/testing/TestInputBackend.cs`
- Modify: `engine/helengine.editor.tests/InputSystemTests.cs`
- Test: `engine/helengine.editor.tests/InputSystemTests.cs`

- [ ] **Step 1: Extend the test backend to model background-input policy**

In `engine/helengine.editor.tests/testing/TestInputBackend.cs`, add the interface property and use it for both keyboard and mouse-button suppression:

```csharp
/// <summary>
/// Gets or sets whether the backend should continue reporting keyboard and button input while inactive.
/// </summary>
public bool ReceiveInputInBackground { get; set; }
```

Update `CaptureFrame()` and add a keyboard helper:

```csharp
public InputFrameState CaptureFrame() {
    InputFrameState frame = new InputFrameState();
    frame.Keyboard = CaptureKeyboardState();
    frame.Mouse = CaptureMouseState();
    frame.Gamepads = Gamepads;
    frame.GamepadCount = GamepadCount;
    return frame;
}

KeyboardState CaptureKeyboardState() {
    if (IsForegroundActive || ReceiveInputInBackground) {
        return KeyboardState;
    }

    return new KeyboardState();
}
```

Update the mouse helper:

```csharp
MouseState CaptureMouseState() {
    MouseState state = MouseState;
    if (IsForegroundActive || ReceiveInputInBackground) {
        return state;
    }

    state.LeftButton = ButtonState.Released;
    state.MiddleButton = ButtonState.Released;
    state.RightButton = ButtonState.Released;
    state.XButton1 = ButtonState.Released;
    state.XButton2 = ButtonState.Released;
    return state;
}
```

- [ ] **Step 2: Write the default-off inactive-keyboard regression**

Add this test to `engine/helengine.editor.tests/InputSystemTests.cs`:

```csharp
/// <summary>
/// Ensures inactive hosts suppress keyboard transitions by default.
/// </summary>
[Fact]
public void EarlyUpdate_WhenWindowIsInactiveAndBackgroundInputIsDisabled_DoesNotCaptureKeyboardTransitions() {
    TestInputBackend input = InitializeCore();
    input.IsForegroundActive = false;

    input.SetKeyboardState(new KeyboardState());
    input.EarlyUpdate();
    input.Update();

    input.SetKeyboardState(new KeyboardState(Keys.Enter));
    input.EarlyUpdate();

    Assert.False(Core.Instance.InputSystem.WasKeyPressed(Keys.Enter));
}
```

- [ ] **Step 3: Write the explicit-enable inactive-input regression**

Add this test beside the previous one:

```csharp
/// <summary>
/// Ensures enabling background input allows inactive hosts to report keyboard and mouse-button input.
/// </summary>
[Fact]
public void EarlyUpdate_WhenBackgroundInputIsEnabled_CapturesInactiveKeyboardAndMouseButtonInput() {
    TestInputBackend input = InitializeCore();
    input.IsForegroundActive = false;
    Core.Instance.InputSystem.SetBackgroundInputEnabled(true);

    input.SetKeyboardState(new KeyboardState());
    input.SetMouseState(new MouseState(40, 40, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
    input.EarlyUpdate();
    input.Update();

    input.SetKeyboardState(new KeyboardState(Keys.Enter));
    input.SetMouseState(new MouseState(40, 40, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
    input.EarlyUpdate();

    Assert.True(Core.Instance.InputSystem.WasKeyPressed(Keys.Enter));
    Assert.Equal(ButtonState.Pressed, Core.Instance.InputSystem.CurrentFrame.Mouse.LeftButton);
}
```

- [ ] **Step 4: Run the focused input regressions to verify the new tests fail first**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~InputSystemTests.EarlyUpdate_WhenWindowIsInactiveAndBackgroundInputIsDisabled_DoesNotCaptureKeyboardTransitions|FullyQualifiedName~InputSystemTests.EarlyUpdate_WhenBackgroundInputIsEnabled_CapturesInactiveKeyboardAndMouseButtonInput" -v minimal
```

Expected:
- FAIL before the test backend honors `ReceiveInputInBackground`

- [ ] **Step 5: Re-run the same focused input regressions after the backend change**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~InputSystemTests.EarlyUpdate_WhenWindowIsInactiveAndBackgroundInputIsDisabled_DoesNotCaptureKeyboardTransitions|FullyQualifiedName~InputSystemTests.EarlyUpdate_WhenBackgroundInputIsEnabled_CapturesInactiveKeyboardAndMouseButtonInput|FullyQualifiedName~InputSystemTests.Update_WhenWindowIsInactiveAndPointerMovesOverInteractable_RaisesHover" -v minimal
```

Expected:
- PASS for both new tests
- PASS for the existing inactive-hover regression

- [ ] **Step 6: Commit the background-input regressions**

```bash
rtk git add engine/helengine.editor.tests/testing/TestInputBackend.cs engine/helengine.editor.tests/InputSystemTests.cs
rtk git commit -m "test: cover player background input policy"
```

### Task 4: Normalize The Engine Background-Input Policy Path

**Files:**
- Modify: `engine/helengine.input/IInputBackend.cs`
- Modify: `engine/helengine.input/InputSystem.cs`
- Modify: `engine/helengine.core.windows/input/InputBackendWindows.cs`
- Test: `engine/helengine.editor.tests/InputSystemTests.cs`

- [ ] **Step 1: Verify the interface and system expose the background-input policy explicitly**

Normalize these members if they do not already match this shape exactly.

`engine/helengine.input/IInputBackend.cs`:

```csharp
/// <summary>
/// Gets or sets whether the backend should continue reporting keyboard and button input while its host window is not foreground active.
/// </summary>
bool ReceiveInputInBackground { get; set; }
```

`engine/helengine.input/InputSystem.cs`:

```csharp
/// <summary>
/// Enables or disables raw keyboard and mouse-button capture while the host window is not foreground active.
/// </summary>
/// <param name="isEnabled">True when the backend should continue reporting background input.</param>
public void SetBackgroundInputEnabled(bool isEnabled) {
    ReceiveInputInBackground = isEnabled;
    ApplyBackgroundInputPolicy();
}

void ApplyBackgroundInputPolicy() {
    if (Backend == null) {
        return;
    }

    Backend.ReceiveInputInBackground = ReceiveInputInBackground;
}
```

Do not add a second player-specific boolean anywhere else.

- [ ] **Step 2: Keep the Windows backend default-off and foreground-gated**

In `engine/helengine.core.windows/input/InputBackendWindows.cs`, verify the existing guards stay in this form:

```csharp
if (!ReceiveInputInBackground && !IsWindowForegroundActive()) {
    CapturedKeys.Clear();
    return new KeyboardState();
}
```

and:

```csharp
if (!ReceiveInputInBackground && !IsWindowForegroundActive()) {
    ReleaseAllButtons(ref mouseState);
} else {
    MouseButtons buttons = Control.MouseButtons;
    mouseState.LeftButton = (buttons & MouseButtons.Left) == MouseButtons.Left ? ButtonState.Pressed : ButtonState.Released;
    mouseState.MiddleButton = (buttons & MouseButtons.Middle) == MouseButtons.Middle ? ButtonState.Pressed : ButtonState.Released;
    mouseState.RightButton = (buttons & MouseButtons.Right) == MouseButtons.Right ? ButtonState.Pressed : ButtonState.Released;
    mouseState.XButton1 = (buttons & MouseButtons.XButton1) == MouseButtons.XButton1 ? ButtonState.Pressed : ButtonState.Released;
    mouseState.XButton2 = (buttons & MouseButtons.XButton2) == MouseButtons.XButton2 ? ButtonState.Pressed : ButtonState.Released;
}
```

The policy must remain default-off unless a host explicitly enables it through `InputSystem`.

- [ ] **Step 3: Run the background-input slice against the real input system**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~InputSystemTests.Update_WhenWindowIsInactiveAndPointerMovesOverInteractable_RaisesHover|FullyQualifiedName~InputSystemTests.EarlyUpdate_WhenWindowIsInactiveAndBackgroundInputIsDisabled_DoesNotCaptureKeyboardTransitions|FullyQualifiedName~InputSystemTests.EarlyUpdate_WhenBackgroundInputIsEnabled_CapturesInactiveKeyboardAndMouseButtonInput|FullyQualifiedName~InputSystemTests.EarlyUpdate_WhenKeyboardStateChangesWithoutExplicitActivation_StillCapturesThePressedKey" -v minimal
```

Expected:
- PASS

- [ ] **Step 4: Commit the input policy normalization**

```bash
rtk git add engine/helengine.input/IInputBackend.cs engine/helengine.input/InputSystem.cs engine/helengine.core.windows/input/InputBackendWindows.cs engine/helengine.editor.tests/InputSystemTests.cs
rtk git commit -m "fix: keep player background input disabled by default"
```

### Task 5: Final Verification And Regenerated Output Check

**Files:**
- Verify: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`
- Verify: `engine/helengine.editor.tests/InputSystemTests.cs`
- Verify: `C:\dev\helprojs\city\assets\codebase\menu\DemoDiscMenuDefinitionProvider.cs`

- [ ] **Step 1: Run the combined focused verification suite**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~DemoDiscSceneWriterTests|FullyQualifiedName~InputSystemTests" -v minimal
```

Expected:
- all targeted demo-disc writer and input-system tests pass

If unrelated pre-existing `InputSystemTests` pointer-wrap regressions remain, rerun the focused subset from Tasks 3 and 4 and record that the wrap failures are unrelated.

- [ ] **Step 2: Verify the regenerated city provider source is clean**

Run:

```bash
rtk rg -n "Helengine Demo Disc|Lilac nights, bright experiments, and a little street grit." C:\dev\helprojs\city\assets\codebase\menu\DemoDiscMenuDefinitionProvider.cs
```

Expected:
- no matches

- [ ] **Step 3: Stage the regenerated city menu source if this repository is responsible for carrying that authored output**

```bash
rtk git add tools/demo-disc-scene-writer/DemoDiscSceneWriter.cs engine/helengine.core/menu/MenuDefinition.cs engine/helengine.editor/managers/menu/DemoMenuSceneAssetFactory.cs engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs engine/helengine.editor.tests/testing/TestInputBackend.cs engine/helengine.editor.tests/InputSystemTests.cs
```

If the city project is being versioned separately, stage its regenerated provider source there instead of trying to commit it from the engine repository.

- [ ] **Step 4: Create the final integration commit**

```bash
rtk git commit -m "fix: remove demo disc copy and normalize player input policy"
```
