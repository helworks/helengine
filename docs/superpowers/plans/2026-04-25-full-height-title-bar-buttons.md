# Full Height Title Bar Buttons Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every button in the editor title bar span the full title-bar height with no top or bottom gap.

**Architecture:** Keep the change localized to `EditorTitleBar` by replacing the shared vertical button inset with full-height button sizing. Lock the behavior in with focused `EditorTitleBarTests` coverage that inspects both left-side menu buttons and right-side window-control buttons.

**Tech Stack:** C#, xUnit, WinForms-hosted editor test projects, Helengine UI entity/component layout system

---

### Task 1: Add failing title-bar layout tests

**Files:**
- Modify: `engine/helengine.editor.tests/EditorTitleBarTests.cs`
- Test: `engine/helengine.editor.tests/EditorTitleBarTests.cs`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public void Layout_UsesFullHeightForLeftSideTitleBarButtons() {
    InitializeCore();
    EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Main Editor Title");

    EditorEntity fileButtonEntity = GetPrivateField<EditorEntity>(titleBar, "FileMenuButtonEntity");
    EditorEntity addButtonEntity = GetPrivateField<EditorEntity>(titleBar, "AddMenuButtonEntity");

    AssertTitleBarButtonUsesFullHeight(fileButtonEntity);
    AssertTitleBarButtonUsesFullHeight(addButtonEntity);
}

[Fact]
public void Layout_UsesFullHeightForRightSideWindowControlButtons() {
    InitializeCore();
    EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Main Editor Title");

    EditorEntity minimizeButtonEntity = GetPrivateField<EditorEntity>(titleBar, "MinimizeButtonEntity");
    EditorEntity maximizeButtonEntity = GetPrivateField<EditorEntity>(titleBar, "MaximizeButtonEntity");
    EditorEntity closeButtonEntity = GetPrivateField<EditorEntity>(titleBar, "CloseButtonEntity");

    AssertTitleBarButtonUsesFullHeight(minimizeButtonEntity);
    AssertTitleBarButtonUsesFullHeight(maximizeButtonEntity);
    AssertTitleBarButtonUsesFullHeight(closeButtonEntity);
}

void AssertTitleBarButtonUsesFullHeight(EditorEntity buttonEntity) {
    RoundedRectComponent background = FindComponent<RoundedRectComponent>(buttonEntity);
    InteractableComponent interactable = FindComponent<InteractableComponent>(buttonEntity);

    Assert.Equal(0f, buttonEntity.Position.Y);
    Assert.Equal(EditorTitleBar.HeightPixels, background.Size.Y);
    Assert.Equal(EditorTitleBar.HeightPixels, interactable.Size.Y);
}
```

- [x] **Step 2: Run test to verify it fails**

Run:

```powershell
$env:DOTNET_CLI_HOME='C:\dev\helengine\.dotnet-cli'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_NOLOGO='1'
dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~EditorTitleBarTests.Layout_UsesFullHeight' -v minimal
```

Expected: FAIL because `EditorTitleBar` still places buttons at `Y = 6` with `Height = 24`.

- [ ] **Step 3: Commit**

```bash
git add engine/helengine.editor.tests/EditorTitleBarTests.cs
git commit -m "test: cover full-height title bar buttons"
```

### Task 2: Implement full-height title-bar buttons

**Files:**
- Modify: `engine/helengine.editor/components/ui/EditorTitleBar.cs`
- Verify: `engine/helengine.editor.tests/EditorTitleBarTests.cs`

- [x] **Step 1: Write minimal implementation**

```csharp
const int ButtonTop = 0;
const int ButtonHeight = HeightPixels;
```

Apply the shared constants everywhere `EditorTitleBar` positions or sizes title-bar buttons so:

- `FileMenuButtonEntity.Position.Y == 0`
- `AddMenuButtonEntity.Position.Y == 0`
- `MinimizeButtonEntity.Position.Y == 0`
- `MaximizeButtonEntity.Position.Y == 0`
- `CloseButtonEntity.Position.Y == 0`
- every title-bar `ButtonComponent` receives `new int2(width, HeightPixels)`

- [x] **Step 2: Run test to verify it passes**

Run:

```powershell
$env:DOTNET_CLI_HOME='C:\dev\helengine\.dotnet-cli'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_NOLOGO='1'
dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~EditorTitleBarTests.Layout_UsesFullHeight' -v minimal
```

Expected: PASS.

- [x] **Step 3: Run adjacent title-bar coverage**

Run:

```powershell
$env:DOTNET_CLI_HOME='C:\dev\helengine\.dotnet-cli'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_NOLOGO='1'
dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~EditorTitleBarTests' -v minimal
```

Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add engine/helengine.editor/components/ui/EditorTitleBar.cs engine/helengine.editor.tests/EditorTitleBarTests.cs
git commit -m "feat: make title bar buttons full height"
```
