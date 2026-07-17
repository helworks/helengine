# Tilt Trial platform selector layout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep the two-stage Tilt Trial selector on DS/3DS while showing the level list, selected-level information, actions, and a preview placeholder together on every other platform.

**Architecture:** Add an explicit serialized `UseDetailsStage` mode to the existing `TiltTrialLevelSelectComponent`. The DS/3DS factory sets it to `true`; the standard and PS2 factories set it to `false`. The controller keeps its current staged state machine for handhelds and uses a combined-view branch for larger screens.

**Tech Stack:** C# project-authored gameplay components, generated C# scene factories, xUnit source-level tests, PowerShell build wrapper, Windows host runtime.

---

### Task 1: Add failing source tests for platform modes and placeholders

**Files:**
- Modify: `C:\dev\helprojs\demodisc\assets\codebase\game.tools.tests\TiltTrialLevelSelectLayoutSourceTests.cs`
- Test project: `C:\dev\helprojs\demodisc\user_settings\generated_code\projects\game.tools.tests\game.tools.tests.csproj`

- [ ] **Step 1: Add assertions before changing production code.**

Add these tests to `TiltTrialLevelSelectLayoutSourceTests`:

```csharp
/// <summary>
/// Ensures only the handheld selector enables the two-stage details flow.
/// </summary>
[Fact]
public void Game_scene_factory_enables_details_stage_only_for_handheld_selector() {
    string source = File.ReadAllText(@"C:\dev\helprojs\demodisc\assets\codebase\game.tools\GameSceneFactory.cs");

    int handheldMethodStart = source.IndexOf("EditorEntity CreateHandheldLevelSelectUiEntity()", StringComparison.Ordinal);
    int standardMethodStart = source.IndexOf("EditorEntity CreateLevelSelectUiEntity()", StringComparison.Ordinal);
    int ps2MethodStart = source.IndexOf("EditorEntity CreatePs2LevelSelectUiEntity()", StringComparison.Ordinal);

    Assert.True(handheldMethodStart >= 0);
    Assert.True(standardMethodStart > handheldMethodStart);
    Assert.True(ps2MethodStart > standardMethodStart);

    string handheldMethodSource = source.Substring(handheldMethodStart, standardMethodStart - handheldMethodStart);
    string standardMethodSource = source.Substring(standardMethodStart, ps2MethodStart - standardMethodStart);
    string ps2MethodSource = source.Substring(ps2MethodStart);

    Assert.Contains("UseDetailsStage = true", handheldMethodSource, StringComparison.Ordinal);
    Assert.Contains("UseDetailsStage = false", standardMethodSource, StringComparison.Ordinal);
    Assert.Contains("UseDetailsStage = false", ps2MethodSource, StringComparison.Ordinal);
}

/// <summary>
/// Ensures larger-screen selector layouts provide the static preview placeholder requested by the combined view.
/// </summary>
[Fact]
public void Game_scene_factory_adds_preview_placeholders_to_standard_and_ps2_selectors() {
    string source = File.ReadAllText(@"C:\dev\helprojs\demodisc\assets\codebase\game.tools\GameSceneFactory.cs");

    Assert.Contains("TiltTrialLevelSelectPreviewPanel", source, StringComparison.Ordinal);
    Assert.Contains("TiltTrialLevelSelectPreviewText", source, StringComparison.Ordinal);
    Assert.Contains("TiltTrialPs2LevelSelectPreviewPanel", source, StringComparison.Ordinal);
    Assert.Contains("TiltTrialPs2LevelSelectPreviewText", source, StringComparison.Ordinal);
    Assert.Contains("\"Preview\"", source, StringComparison.Ordinal);
}
```

Add this assertion to the existing controller source test:

```csharp
Assert.Contains("public bool UseDetailsStage { get; set; }", componentSource, StringComparison.Ordinal);
Assert.Contains("if (UseDetailsStage)", componentSource, StringComparison.Ordinal);
Assert.Contains("ShowCombinedView();", componentSource, StringComparison.Ordinal);
```

- [ ] **Step 2: Run the focused test project and confirm RED.**

Run:

```powershell
rtk dotnet test C:\dev\helprojs\demodisc\user_settings\generated_code\projects\game.tools.tests\game.tools.tests.csproj --filter TiltTrialLevelSelectLayoutSourceTests
```

Expected: FAIL because `UseDetailsStage`, `ShowCombinedView`, and the large-screen preview entities are not yet present.

### Task 2: Implement explicit staged versus combined controller behavior

**Files:**
- Modify: `C:\dev\helprojs\demodisc\assets\codebase\game\TiltTrialLevelSelectComponent.cs`

- [ ] **Step 1: Add the serialized mode property.**

After `SelectedIndex`, add:

```csharp
/// <summary>
/// Gets or sets whether accepting a level opens a separate details stage before play.
/// </summary>
public bool UseDetailsStage { get; set; }
```

- [ ] **Step 2: Branch update and action handling on `UseDetailsStage`.**

Keep the existing details-stage branch intact under `if (UseDetailsStage && IsDetailsVisible)`. For the non-staged branch, preserve Up/Down selection and call `PlaySelectedStage()` on Accept instead of `ShowDetails()`.

In `HandleAction`, make `SelectStage` call `ShowDetails()` only when `UseDetailsStage` is true; otherwise call `ShowCombinedView()`. Make `BackToStages` call `ShowStageList()` only in staged mode and `ShowCombinedView()` otherwise.

- [ ] **Step 3: Add combined-view binding initialization.**

Add:

```csharp
/// <summary>
/// Keeps the level list and selected-level details visible together for non-handheld selectors.
/// </summary>
void ShowCombinedView() {
    if (ListPanelEntity == null || DetailsPanelEntity == null) {
        throw new InvalidOperationException("Tilt Trial selector panels are not resolved.");
    }

    IsDetailsVisible = false;
    DetailActionIndex = 1;
    ListPanelEntity.Enabled = true;
    DetailsPanelEntity.Enabled = true;
    ApplyDetailActionSelection();
}
```

At the end of `ResolveUiBindingsWhenNeeded`, call `ShowCombinedView()` when `UseDetailsStage` is false and `ShowStageList()` when it is true.

- [ ] **Step 4: Run the focused test and confirm the controller assertions pass or fail only on factory layout assertions.**

Run the same `dotnet test` command from Task 1. Expected: controller assertions pass; factory mode and preview assertions remain RED.

### Task 3: Configure generated selector roots and add preview placeholders

**Files:**
- Modify: `C:\dev\helprojs\demodisc\assets\codebase\game.tools\GameSceneFactory.cs`

- [ ] **Step 1: Configure the handheld root.**

Change its component construction to:

```csharp
entity.AddComponent(new city.game.TiltTrialLevelSelectComponent {
    UseDetailsStage = true
});
```

- [ ] **Step 2: Configure standard and PS2 roots.**

Change both component constructions to:

```csharp
entity.AddComponent(new city.game.TiltTrialLevelSelectComponent {
    UseDetailsStage = false
});
```

- [ ] **Step 3: Add the standard placeholder.**

Inside the standard details panel after the target-times text, add:

```csharp
Entity previewPanelEntity = CreateRoundedPanelEntity(
    detailsPanelEntity,
    "TiltTrialLevelSelectPreviewPanel",
    new float3(380f, 108f, 0f),
    new int2(320, 260),
    18f,
    2f,
    new byte4(18, 29, 45, 255),
    new byte4(109, 138, 170, 255),
    3);
CreateUiTextEntity(
    previewPanelEntity,
    "TiltTrialLevelSelectPreviewText",
    new float3(20f, 108f, 0.1f),
    "Preview",
    new int2(280, 40),
    1.2f,
    4,
    new byte4(223, 230, 239, 255),
    TextAlignment.Center);
```

- [ ] **Step 4: Add the PS2 placeholder.**

Inside the PS2 details panel after the target-times text, add:

```csharp
Entity previewPanelEntity = CreateRoundedPanelEntity(
    detailsPanelEntity,
    "TiltTrialPs2LevelSelectPreviewPanel",
    new float3(20f, 132f, 0f),
    new int2(284, 94),
    10f,
    1f,
    new byte4(18, 29, 45, 255),
    new byte4(109, 138, 170, 255),
    3);
CreateUiTextEntity(
    previewPanelEntity,
    "TiltTrialPs2LevelSelectPreviewText",
    new float3(12f, 32f, 0.1f),
    "Preview",
    new int2(260, 28),
    0.9f,
    4,
    new byte4(223, 230, 239, 255),
    TextAlignment.Center);
```

Keep the existing PS2 Back button at `(20f, 250f)` and Play button at `(20f, 300f)`; the placeholder ends at y=226, so these controls remain separated from it within the existing 640x448 canvas.

- [ ] **Step 5: Run the focused source tests and confirm GREEN.**

Run:

```powershell
rtk dotnet test C:\dev\helprojs\demodisc\user_settings\generated_code\projects\game.tools.tests\game.tools.tests.csproj --filter TiltTrialLevelSelectLayoutSourceTests
```

Expected: all tests in `TiltTrialLevelSelectLayoutSourceTests` pass.

### Task 4: Regenerate scenes and verify the Windows runtime

**Files:**
- Generated output: `C:\dev\helprojs\demodisc\assets\scenes\games\tilt\tilt_trial.helen` and handheld companion scene.
- Build output: `C:\dev\helprojs\output\windows-demodisc`

- [ ] **Step 1: Regenerate the authored game scenes.**

Run the editor command through the existing streaming wrapper:

```powershell
rtk powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\dev\helworks\helengine\scripts\build-platform.ps1 -Project C:\dev\helprojs\demodisc -Platform windows -Output C:\dev\helprojs\output\windows-demodisc -Configuration Debug
```

Expected: the standard scene contains `UseDetailsStage = false` data and the new standard preview placeholder; the handheld companion retains the staged scene and preview placeholder.

- [ ] **Step 2: Verify serialized source markers before launch.**

Run:

```powershell
rtk rg -a -n "TiltTrialLevelSelectPreviewPanel|TiltTrialPs2LevelSelectPreviewPanel|TiltTrialLevelSelectBackButton" C:\dev\helprojs\demodisc\assets\scenes\games\tilt\tilt_trial.helen
```

Expected: standard preview and action entity names are present.

- [ ] **Step 3: Launch the rebuilt Windows executable and observe it past selector load.**

Start `C:\dev\helprojs\output\windows-demodisc\helengine_windows.exe` with that directory as the working directory. Confirm the process remains alive after entering Tilt Trial and that the selector presents the list and details simultaneously. Do not use screenshots unless explicitly authorized.

- [ ] **Step 4: Run the smallest final verification set.**

Run the focused source tests again and inspect the Windows diagnostics log for absence of the previous selector-resolution fatal exception.

### Task 5: Review the final diff and commit implementation

**Files:**
- Review: `C:\dev\helprojs\demodisc\assets\codebase\game\TiltTrialLevelSelectComponent.cs`
- Review: `C:\dev\helprojs\demodisc\assets\codebase\game.tools\GameSceneFactory.cs`
- Review: `C:\dev\helprojs\demodisc\assets\codebase\game.tools.tests\TiltTrialLevelSelectLayoutSourceTests.cs`

- [ ] **Step 1: Inspect the scoped diff.**

Run:

```powershell
rtk git -C C:\dev\helprojs\demodisc diff -- assets/codebase/game/TiltTrialLevelSelectComponent.cs assets/codebase/game.tools/GameSceneFactory.cs assets/codebase/game.tools.tests/TiltTrialLevelSelectLayoutSourceTests.cs
```

Confirm no generated scene files were manually edited and no unrelated worktree changes are staged.

- [ ] **Step 2: Commit only the implementation files.**

```powershell
rtk git -C C:\dev\helprojs\demodisc add assets/codebase/game/TiltTrialLevelSelectComponent.cs assets/codebase/game.tools/GameSceneFactory.cs assets/codebase/game.tools.tests/TiltTrialLevelSelectLayoutSourceTests.cs
rtk git -C C:\dev\helprojs\demodisc commit -m "feat: keep Tilt Trial selector combined off handhelds"
```
