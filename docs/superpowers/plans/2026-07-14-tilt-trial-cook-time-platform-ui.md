# Tilt Trial Cook-Time Platform UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Tilt Trial gameplay level scenes author-owned and platform-neutral while cook-time-expanded console and DS/3DS UI blueprints supply the correct presentation without shipping the other UI family.

**Architecture:** Use the existing `BlueprintAsset`/`BlueprintInstanceComponent` pipeline as the prefab system. Each manually authored Tilt Trial gameplay scene references a console presentation blueprint and a handheld presentation blueprint; per-entity platform existence overrides retain exactly one instance before the packager expands blueprints and gathers dependencies. Tilt Trial’s level-select menu remains separately generated for handheld because its layout is intentionally different; gameplay levels are never regenerated.

**Tech Stack:** C#/.NET 9, Helengine editor scene/blueprint serialization, platform entity-existence overrides, demo-disc scripted modules, xUnit, DS and 3DS build pipeline.

---

## File Structure

- Modify: `engine/helengine.editor.tests/BlueprintBuildPackagingIntegrationTests.cs`
  - Prove platform-pruned blueprint instance roots do not expand or contribute dependencies to cooked output.
- Modify only if the new test fails: `engine/helengine.editor/managers/project/BlueprintPackagedSceneExpansionService.cs` and its caller in `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
  - Resolve entity existence overrides before blueprint expansion and asset collection.
- Create: `C:\dev\helprojs\demodisc\assets\codebase\game\TiltTrialSessionAction.cs`
  - Defines presentation-independent gameplay commands.
- Create: `C:\dev\helprojs\demodisc\assets\codebase\game\TiltTrialPresentationActionComponent.cs`
  - Bridges a serialized 2D interactable press to a `TiltTrialSessionAction`.
- Modify: `C:\dev\helprojs\demodisc\assets\codebase\game\TiltTrialSessionState.cs`
  - Adds the paused state.
- Modify: `C:\dev\helprojs\demodisc\assets\codebase\game\TiltTrialSessionComponent.cs`
  - Consumes semantic actions from physical controls and touch controls; resolves presentation entities by stable names instead of child index.
- Modify: `C:\dev\helprojs\demodisc\assets\codebase\game\DemoTiltFollowCameraComponent.cs` and `DemoTiltSpeedTextComponent.cs`
  - Resolve level-owned targets by stable entity name so presentation blueprints carry no copied scene entity ids.
- Create: `C:\dev\helprojs\demodisc\assets\codebase\game.tools\TiltTrialGameplayPresentationBlueprintGenerator.cs`
  - Generates the console and handheld presentation blueprints only.
- Create: `C:\dev\helprojs\demodisc\assets\codebase\game.tools\GenerateTiltTrialGameplayPresentationBlueprintsCommand.cs`
  - Exposes explicit editor command `menu.generate-tilt-trial-presentation-blueprints`.
- Create: `C:\dev\helprojs\demodisc\assets\codebase\game.tools\TiltTrialHandheldLevelSelectSceneFactory.cs`
  - Authors the separate DS/3DS level-select scene with passive top-screen detail and interactive bottom-screen list.
- Modify: `C:\dev\helprojs\demodisc\assets\codebase\game.tools\GameSceneGenerator.cs`, `GameSceneFactory.cs`, and `GameSceneCatalog.cs`
  - Stop rewriting gameplay levels; retain selector generation and add its handheld counterpart.
- Modify: `C:\dev\helprojs\demodisc\assets\codebase\game.tools.tests\TiltTrialSceneGenerationSourceTests.cs`, `TiltTrialBuildConfigTests.cs`, and create `TiltTrialPlatformPresentationSourceTests.cs`
  - Cover the no-regeneration boundary, generated blueprint contract, and handheld selector selection.
- Generate through the command, never hand edit: `C:\dev\helprojs\demodisc\assets\blueprints\games\tilt\TiltTrialConsolePresentation.hblueprint` and `TiltTrialHandheldPresentation.hblueprint`
  - These are disposable generated UI assets, not level scenes.
- Modify: `C:\dev\helprojs\demodisc\user_settings\build_config.json`
  - Select `tilt_trial_ds` for DS/3DS while keeping each gameplay level logical id unchanged.

### Task 1: Prove Blueprint Pruning Happens Before Expansion And Dependency Collection

**Files:**
- Modify: `engine/helengine.editor.tests/BlueprintBuildPackagingIntegrationTests.cs`
- Modify only if required by the failing test: `engine/helengine.editor/managers/project/BlueprintPackagedSceneExpansionService.cs`
- Modify only if required by the failing test: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`

- [ ] **Step 1: Write the failing package integration test**

Add `Package_WhenPlatformPrunesBlueprintInstance_ExcludesItsExpandedEntitiesAndAssetReferences` beside the existing blueprint packaging test. Create two blueprints with unique root names and unique material references, then create one scene with two `BlueprintInstanceComponent` roots. Mark the console instance absent on `ds` and the handheld instance absent on `windows`.

```csharp
SceneEntityAsset consoleInstance = CreateBlueprintInstance(
    "Console Presentation",
    "Blueprints/TiltTrialConsolePresentation.hblueprint",
    "ds",
    false);
SceneEntityAsset handheldInstance = CreateBlueprintInstance(
    "Handheld Presentation",
    "Blueprints/TiltTrialHandheldPresentation.hblueprint",
    "windows",
    false);

PackageAndReadScene("ds", new[] { consoleInstance, handheldInstance }, out SceneAsset packagedScene, out string buildRootPath);

Assert.DoesNotContain(packagedScene.RootEntities, entity => entity.Name == "Console Presentation");
SceneEntityAsset handheldRoot = Assert.Single(packagedScene.RootEntities, entity => entity.Name == "Handheld Presentation");
Assert.Contains(handheldRoot.Children, entity => entity.Name == "TiltTrialHandheldPresentationRoot");
Assert.False(File.Exists(Path.Combine(buildRootPath, "cooked", "materials", "console-only.hasset")));
Assert.True(File.Exists(Path.Combine(buildRootPath, "cooked", "materials", "handheld-only.hasset")));
```

- [ ] **Step 2: Run the focused test and record the current ordering behavior**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BlueprintBuildPackagingIntegrationTests" -v minimal
```

Expected: the new test fails if a pruned blueprint expands or its material is cooked.

- [ ] **Step 3: Apply the minimal cook-order fix if the test failed**

`EditorWindowsBuildScenePackager.Package` currently expands blueprints before `RewriteSceneAsset` prunes absent platform roots. Extract the root/child existence walk from `RewriteSceneAsset` into `PruneEntitySubtreesForTargetPlatform(SceneAsset sceneAsset)` and call it immediately before `BlueprintExpansionService.Expand`:

```csharp
SceneAsset packagedSceneAsset = LoadSceneAsset(sceneId, sceneSourcePath);
PruneEntitySubtreesForTargetPlatform(packagedSceneAsset);
BlueprintExpansionService.Expand(packagedSceneAsset);
packagedSceneAsset.Id = sceneId;
RewriteSceneAsset(packagedSceneAsset, fullBuildRootPath);
```

The extracted method removes only roots and children whose `PlatformExistenceOverrides` resolve false for `TargetPlatformId`; `RewriteSceneAsset` remains responsible for transform/component overrides and asset rewriting. Do not add runtime filtering or retain a dormant instance record.

- [ ] **Step 4: Re-run the focused test**

Run the command from Step 2.

Expected: PASS; the cooked DS payload contains only the handheld expanded blueprint and its material dependency.

- [ ] **Step 5: Commit the engine regression coverage and any required ordering fix**

```powershell
rtk git add engine/helengine.editor.tests/BlueprintBuildPackagingIntegrationTests.cs engine/helengine.editor/managers/project/BlueprintPackagedSceneExpansionService.cs engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs
rtk git commit -m "test: verify platform-pruned blueprints do not cook assets"
```

Only add production files that changed in Step 3.

### Task 2: Make Tilt Trial Presentation Commands Independent Of UI Layout

**Files:**
- Create: `C:\dev\helprojs\demodisc\assets\codebase\game\TiltTrialSessionAction.cs`
- Create: `C:\dev\helprojs\demodisc\assets\codebase\game\TiltTrialPresentationActionComponent.cs`
- Modify: `C:\dev\helprojs\demodisc\assets\codebase\game\TiltTrialSessionState.cs`
- Modify: `C:\dev\helprojs\demodisc\assets\codebase\game\TiltTrialSessionComponent.cs`
- Modify: `C:\dev\helprojs\demodisc\assets\codebase\game.tools.tests\TiltTrialSceneGenerationSourceTests.cs`
- Create: `C:\dev\helprojs\demodisc\assets\codebase\game.tools.tests\TiltTrialPresentationActionSourceTests.cs`

- [ ] **Step 1: Write source-level tests for the action contract**

Add assertions that the action enum contains `TogglePause`, `Restart`, and `ReturnToLevelSelect`; the session exposes `RequestAction(TiltTrialSessionAction action)`; and the presentation action component subscribes to an `InteractableComponent` release rather than polling platform-specific input.

```csharp
Assert.Contains("TogglePause", actionSource, StringComparison.Ordinal);
Assert.Contains("Restart", actionSource, StringComparison.Ordinal);
Assert.Contains("ReturnToLevelSelect", actionSource, StringComparison.Ordinal);
Assert.Contains("public void RequestAction(TiltTrialSessionAction action)", sessionSource, StringComparison.Ordinal);
Assert.Contains("PointerInteraction.Release", presentationSource, StringComparison.Ordinal);
Assert.Contains("RequestAction(Action)", presentationSource, StringComparison.Ordinal);
```

- [ ] **Step 2: Run the game-tools tests to verify the contract is absent**

Run:

```powershell
rtk dotnet test C:\dev\helprojs\demodisc\user_settings\generated_code\projects\game.tools.tests\game.tools.tests.csproj --filter "FullyQualifiedName~TiltTrial" -v minimal
```

Expected: FAIL because the enum, component, and command method do not yet exist.

- [ ] **Step 3: Implement semantic session actions and touch bridge**

Create the action enum:

```csharp
namespace city.game {
    public enum TiltTrialSessionAction {
        TogglePause,
        Restart,
        ReturnToLevelSelect,
        NavigatePrevious,
        NavigateNext,
        Accept
    }
}
```

Add `Paused` to `TiltTrialSessionState`. `RequestAction` must:

- toggle only `Playing` and `Paused` for `TogglePause`, applying `SetGameplayUpdatesSuppressed(true)` while paused
- reload `CurrentLevel.SceneId` for `Restart`
- load `TiltTrialSceneIds.LevelSelectSceneId` for `ReturnToLevelSelect`
- reuse existing overlay navigation and accept behavior for `NavigatePrevious`, `NavigateNext`, and `Accept`

Replace direct input branches in `UpdatePlayingState`, `UpdateResultsOverlay`, and `UpdateFailedOverlay` with physical-input-to-action translation followed by `RequestAction`. Preserve desktop keyboard behavior and map handheld physical controls to the same actions.

`TiltTrialPresentationActionComponent` owns an `Action` property, finds the required `InteractableComponent` on its entity, subscribes in `ComponentAdded`, unsubscribes in `ComponentRemoved`, and invokes the session only on `PointerInteraction.Release`.

```csharp
void HandleCursorEvent(int2 position, int2 delta, PointerInteraction interaction) {
    if (interaction != PointerInteraction.Release) {
        return;
    }

    ResolveRequiredSession().RequestAction(Action);
}
```

- [ ] **Step 4: Replace positional UI dependency lookup with stable entity names**

Declare constants on `TiltTrialSessionComponent` for `TiltTrialTimerText`, `TiltTrialCoinText`, `TiltTrialResultsOverlay`, `TiltTrialResultsTitleText`, `TiltTrialResultsBodyText`, `TiltTrialFailOverlay`, `TiltTrialFailTitleText`, and `TiltTrialFailBodyText`. Resolve them by recursive stable-name search under the presentation root; never use `Parent.Children[index]` for UI bindings.

This permits console and handheld blueprints to use different hierarchy/layout without changing session behavior.

- [ ] **Step 5: Re-run the focused tests and commit**

Run the command from Step 2.

Expected: PASS.

```powershell
git -C C:\dev\helprojs\demodisc add assets/codebase/game/TiltTrialSessionAction.cs assets/codebase/game/TiltTrialPresentationActionComponent.cs assets/codebase/game/TiltTrialSessionState.cs assets/codebase/game/TiltTrialSessionComponent.cs assets/codebase/game.tools.tests/TiltTrialSceneGenerationSourceTests.cs assets/codebase/game.tools.tests/TiltTrialPresentationActionSourceTests.cs
git -C C:\dev\helprojs\demodisc commit -m "feat: add presentation-independent Tilt Trial actions"
```

### Task 3: Generate Console And Handheld Presentation Blueprints Only

**Files:**
- Create: `C:\dev\helprojs\demodisc\assets\codebase\game.tools\TiltTrialGameplayPresentationBlueprintGenerator.cs`
- Create: `C:\dev\helprojs\demodisc\assets\codebase\game.tools\GenerateTiltTrialGameplayPresentationBlueprintsCommand.cs`
- Modify: `C:\dev\helprojs\demodisc\assets\codebase\game.tools\GameSceneGenerator.cs`
- Modify: `C:\dev\helprojs\demodisc\assets\codebase\game.tools\GameSceneFactory.cs`
- Modify: `C:\dev\helprojs\demodisc\assets\codebase\game.tools.tests\TiltTrialSceneGenerationSourceTests.cs`
- Create: `C:\dev\helprojs\demodisc\assets\codebase\game.tools.tests\TiltTrialPlatformPresentationSourceTests.cs`

- [ ] **Step 1: Write failing tests for the authoring boundary**

Assert that `GameSceneGenerator.Generate` no longer calls `CreateTiltTrialLevelScenes` or `WriteScene` for `tilt_trial_level_01` through `tilt_trial_level_05`. Assert that the new command invokes the blueprint generator and the generator writes exactly these paths:

```csharp
const string ConsoleBlueprintRelativePath = "blueprints/games/tilt/TiltTrialConsolePresentation.hblueprint";
const string HandheldBlueprintRelativePath = "blueprints/games/tilt/TiltTrialHandheldPresentation.hblueprint";
```

Also assert the handheld blueprint defines `TiltTrialTopScreenCamera`, `TiltTrialBottomScreenCamera`, `TiltTrialBottomScreenViewport`, and action entities for pause, restart, and back.

- [ ] **Step 2: Run the focused tests and verify they fail**

Run:

```powershell
rtk dotnet test C:\dev\helprojs\demodisc\user_settings\generated_code\projects\game.tools.tests\game.tools.tests.csproj --filter "FullyQualifiedName~TiltTrialSceneGenerationSourceTests|FullyQualifiedName~TiltTrialPlatformPresentationSourceTests" -v minimal
```

Expected: FAIL because generation still writes Tilt Trial gameplay scenes and no blueprint generator exists.

- [ ] **Step 3: Implement the blueprint generator**

The generator must use `BlueprintSaveService` to create one editable generated root per blueprint and save only the two blueprint assets. Do not call `SceneSaveService` for a Tilt Trial gameplay `*.helen` file.

Create these roots and keep their runtime lookup names stable:

```text
TiltTrialConsolePresentationRoot
  TiltTrialCamera
  TiltTrialUi

TiltTrialHandheldPresentationRoot
  TiltTrialTopScreenCamera
  TiltTrialBottomScreenCamera
    TiltTrialBottomScreenViewport
      TiltTrialUi
      TiltTrialPauseTouchButton
      TiltTrialRestartTouchButton
      TiltTrialBackTouchButton
```

The console blueprint owns the existing full-screen HUD and third-person camera. The handheld blueprint owns a top-screen 3D camera with viewport `(0, 0, 1, 1)` and a bottom-screen UI camera with viewport `(0, 1, 1, 1)`, `CameraDrawOrder = 1`, and a `256 x 192` ancestor-camera viewport.

Each handheld touch target contains `RoundedRectComponent`, `TextComponent`, `InteractableComponent`, and `TiltTrialPresentationActionComponent`. The action values are respectively `TogglePause`, `Restart`, and `ReturnToLevelSelect`. Use the same bottom-screen body font source used by `NintendoDsRenderingSceneScaffoldFactory`; do not introduce a DS-specific font import path.

Move all presentation-only construction out of `GameSceneFactory`: gameplay camera, gameplay UI, speed HUD, overlays, and presentation fonts. Keep level metadata, stage geometry, physics, player, goal, coins, and lighting out of the blueprint generator.

- [ ] **Step 4: Make level-owned scripts resolve presentation-independent targets**

Update `DemoTiltFollowCameraComponent` and `DemoTiltSpeedTextComponent` so their target lookup resolves the shared player by the stable name `TiltTrialPlayerSphere` after the blueprint expands. Remove source-generation code that serializes a level-specific entity id into presentation components. The generated blueprints must be reusable by all five levels.

- [ ] **Step 5: Re-run focused tests and commit**

Run the command from Step 2.

Expected: PASS.

```powershell
git -C C:\dev\helprojs\demodisc add assets/codebase/game.tools/TiltTrialGameplayPresentationBlueprintGenerator.cs assets/codebase/game.tools/GenerateTiltTrialGameplayPresentationBlueprintsCommand.cs assets/codebase/game.tools/GameSceneGenerator.cs assets/codebase/game.tools/GameSceneFactory.cs assets/codebase/game/DemoTiltFollowCameraComponent.cs assets/codebase/game/DemoTiltSpeedTextComponent.cs assets/codebase/game.tools.tests/TiltTrialSceneGenerationSourceTests.cs assets/codebase/game.tools.tests/TiltTrialPlatformPresentationSourceTests.cs
git -C C:\dev\helprojs\demodisc commit -m "feat: generate Tilt Trial presentation blueprints"
```

### Task 4: Author The Handheld-Only Tilt Trial Level Select

**Files:**
- Create: `C:\dev\helprojs\demodisc\assets\codebase\game.tools\TiltTrialHandheldLevelSelectSceneFactory.cs`
- Modify: `C:\dev\helprojs\demodisc\assets\codebase\game.tools\GameSceneCatalog.cs`
- Modify: `C:\dev\helprojs\demodisc\assets\codebase\game.tools\GameSceneGenerator.cs`
- Modify: `C:\dev\helprojs\demodisc\assets\codebase\game\TiltTrialLevelSelectComponent.cs`
- Create: `C:\dev\helprojs\demodisc\assets\codebase\game\TiltTrialLevelSelectTouchActionComponent.cs`
- Modify: `C:\dev\helprojs\demodisc\assets\codebase\game.tools.tests\TiltTrialLevelSelectLayoutSourceTests.cs`

- [ ] **Step 1: Write failing tests for the dedicated selector scene**

Add assertions for `TiltTrialHandheldSceneId = "tilt_trial_ds"`, the factory’s two cameras, and bottom-screen row interactables. The test must also assert that `GameSceneGenerator` writes the handheld selector but still does not write gameplay levels.

```csharp
Assert.Contains("public const string TiltTrialHandheldSceneId = \"tilt_trial_ds\";", catalogSource, StringComparison.Ordinal);
Assert.Contains("TiltTrialTopScreenCamera", handheldFactorySource, StringComparison.Ordinal);
Assert.Contains("TiltTrialBottomScreenCamera", handheldFactorySource, StringComparison.Ordinal);
Assert.Contains("TiltTrialLevelSelectTouchActionComponent", handheldFactorySource, StringComparison.Ordinal);
Assert.DoesNotContain("CreateTiltTrialLevelScenes()", generatorSource, StringComparison.Ordinal);
```

- [ ] **Step 2: Run the selector tests and verify they fail**

Run:

```powershell
rtk dotnet test C:\dev\helprojs\demodisc\user_settings\generated_code\projects\game.tools.tests\game.tools.tests.csproj --filter "FullyQualifiedName~TiltTrialLevelSelectLayoutSourceTests|FullyQualifiedName~TiltTrialSceneGenerationSourceTests" -v minimal
```

Expected: FAIL because no separate handheld selector exists.

- [ ] **Step 3: Implement the separate handheld selector and touch selection**

`TiltTrialHandheldLevelSelectSceneFactory` writes only `tilt_trial_ds`. Its top screen displays title, selected-level name, time targets, and preview/details. Its bottom screen displays the selectable level rows and physical-control hint.

`TiltTrialLevelSelectTouchActionComponent` stores a zero-based level index, listens for `PointerInteraction.Release` on a sibling `InteractableComponent`, and calls a new public `TiltTrialLevelSelectComponent.RequestLevelSelection(int index)`. That method validates `0 <= index < TiltTrialLevelCatalog.CreateEntries().Count`, updates the selected index, and loads the selected level when the release action is accepted. The existing keyboard and gamepad navigation continues to call the same selection and activation methods.

Do not add DS branches to `TiltTrialLevelSelectComponent`; the handheld scene supplies the touch targets.

- [ ] **Step 4: Generate and inspect only the selector artifact**

Invoke `menu.generate-game-scenes` after rebuilding the editor module. Confirm that it writes `assets/scenes/games/tilt/tilt_trial_ds.helen` and does not change any `tilt_trial_level_0*.helen` timestamp or content.

Run:

```powershell
rtk dotnet build C:\dev\helprojs\demodisc\city.sln -v minimal
```

Expected: PASS before invoking the editor command.

- [ ] **Step 5: Re-run tests and commit**

Run the command from Step 2.

Expected: PASS.

```powershell
git -C C:\dev\helprojs\demodisc add assets/codebase/game.tools/TiltTrialHandheldLevelSelectSceneFactory.cs assets/codebase/game.tools/GameSceneCatalog.cs assets/codebase/game.tools/GameSceneGenerator.cs assets/codebase/game/TiltTrialLevelSelectComponent.cs assets/codebase/game/TiltTrialLevelSelectTouchActionComponent.cs assets/codebase/game.tools.tests/TiltTrialLevelSelectLayoutSourceTests.cs
git -C C:\dev\helprojs\demodisc commit -m "feat: add handheld Tilt Trial level select"
```

### Task 5: Wire Blueprint Instances Into Authored Gameplay Scenes Without Regenerating Them

**Files:**
- Generate: `C:\dev\helprojs\demodisc\assets\blueprints\games\tilt\TiltTrialConsolePresentation.hblueprint`
- Generate: `C:\dev\helprojs\demodisc\assets\blueprints\games\tilt\TiltTrialHandheldPresentation.hblueprint`
- Manually author once in each existing level scene through the editor: `C:\dev\helprojs\demodisc\assets\scenes\games\tilt\tilt_trial_level_01.helen` through `tilt_trial_level_05.helen`
- Modify: `C:\dev\helprojs\demodisc\assets\codebase\game.tools.tests\TiltTrialPlatformPresentationSourceTests.cs`

- [ ] **Step 1: Generate the two blueprint assets through the explicit command**

Run the command `menu.generate-tilt-trial-presentation-blueprints` after rebuilding the `game.tools` module. Confirm both `.hblueprint` files exist. Do not invoke `menu.generate-game-scenes` as part of this step.

- [ ] **Step 2: Add the two blueprint instance roots to each manually authored gameplay scene**

For every Tilt Trial gameplay level, add two root entities in the editor:

```text
TiltTrialConsolePresentation -> BlueprintInstanceComponent("blueprints/games/tilt/TiltTrialConsolePresentation.hblueprint")
TiltTrialHandheldPresentation -> BlueprintInstanceComponent("blueprints/games/tilt/TiltTrialHandheldPresentation.hblueprint")
```

Use the existing platform tabs to configure their entity existence:

| Instance root | Windows and conventional consoles | DS | 3DS |
| --- | --- | --- | --- |
| `TiltTrialConsolePresentation` | exists | absent | absent |
| `TiltTrialHandheldPresentation` | absent | exists | exists |

Remove the legacy generated `TiltTrialCamera` and `TiltTrialUi` roots only after the corresponding blueprint instances are attached. Do not alter stage, player, goal, coin, lighting, physics, or level-metadata entities.

- [ ] **Step 3: Write a post-authoring scene-load regression test**

Extend `engine/helengine.editor.tests/DemodiscTiltTrialSceneLoadTests.cs` with a test that loads `tilt_trial_level_01.helen`, finds both `BlueprintInstanceComponent` roots, and verifies no legacy top-level `TiltTrialCamera` or `TiltTrialUi` root remains.

```csharp
Assert.Contains(loaded.RootEntities, entity => entity.Name == "TiltTrialConsolePresentation");
Assert.Contains(loaded.RootEntities, entity => entity.Name == "TiltTrialHandheldPresentation");
Assert.DoesNotContain(loaded.RootEntities, entity => entity.Name == "TiltTrialCamera");
Assert.DoesNotContain(loaded.RootEntities, entity => entity.Name == "TiltTrialUi");
```

- [ ] **Step 4: Run scene-load regression coverage**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~DemodiscTiltTrialSceneLoadTests|FullyQualifiedName~DemodiscTiltTrialEditorSessionCloseTests" -v minimal
```

Expected: PASS; the editor can still load and close the user-authored first level with expanded blueprint UI.

- [ ] **Step 5: Commit the generated UI assets, intentional scene references, and regression test**

```powershell
git -C C:\dev\helprojs\demodisc add assets/blueprints/games/tilt/TiltTrialConsolePresentation.hblueprint assets/blueprints/games/tilt/TiltTrialHandheldPresentation.hblueprint assets/scenes/games/tilt/tilt_trial_level_01.helen assets/scenes/games/tilt/tilt_trial_level_02.helen assets/scenes/games/tilt/tilt_trial_level_03.helen assets/scenes/games/tilt/tilt_trial_level_04.helen assets/scenes/games/tilt/tilt_trial_level_05.helen
git -C C:\dev\helworks\helengine add engine/helengine.editor.tests/DemodiscTiltTrialSceneLoadTests.cs
git -C C:\dev\helprojs\demodisc commit -m "feat: attach Tilt Trial presentation blueprints"
```

Commit the engine regression test separately if the engine and demo-disc repositories do not share one Git root.

### Task 6: Select The Handheld Menu And Verify DS/3DS Packages

**Files:**
- Modify: `C:\dev\helprojs\demodisc\user_settings\build_config.json`
- Modify: `C:\dev\helprojs\demodisc\assets\codebase\game.tools.tests\TiltTrialBuildConfigTests.cs`
- Modify: `engine/helengine.editor.tests/BlueprintBuildPackagingIntegrationTests.cs` only if Task 1 needs additional real-project coverage

- [ ] **Step 1: Write the failing build-config assertion**

Require DS and 3DS to select `tilt_trial_ds`, select all five shared gameplay scene ids, and not select the desktop `tilt_trial` selector scene.

```csharp
Assert.Contains("tilt_trial_ds", dsSelectedSceneIds);
Assert.Contains("tilt_trial_level_01", dsSelectedSceneIds);
Assert.Contains("tilt_trial_level_05", dsSelectedSceneIds);
Assert.DoesNotContain("tilt_trial", dsSelectedSceneIds);
```

- [ ] **Step 2: Run the test and verify it fails**

Run:

```powershell
rtk dotnet test C:\dev\helprojs\demodisc\user_settings\generated_code\projects\game.tools.tests\game.tools.tests.csproj --filter "FullyQualifiedName~TiltTrialBuildConfigTests" -v minimal
```

Expected: FAIL until DS and 3DS select the handheld selector id.

- [ ] **Step 3: Update DS and 3DS build selections**

Replace `tilt_trial` with `tilt_trial_ds` in only the DS and 3DS platform sections. Preserve the five shared gameplay ids and their relative order. Windows and conventional-console builds retain `tilt_trial`.

The existing generated boot-scene mapping recognizes the `_ds` suffix and maps logical `tilt_trial` requests to `tilt_trial_ds`; do not add a runtime branch to Tilt Trial scene navigation.

- [ ] **Step 4: Run focused validation and real target builds**

Run:

```powershell
rtk dotnet test C:\dev\helprojs\demodisc\user_settings\generated_code\projects\game.tools.tests\game.tools.tests.csproj --filter "FullyQualifiedName~TiltTrial" -v minimal
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BlueprintBuildPackagingIntegrationTests|FullyQualifiedName~DemodiscTiltTrialSceneLoadTests|FullyQualifiedName~DemodiscTiltTrialEditorSessionCloseTests" -v minimal
rtk powershell -NoProfile -ExecutionPolicy Bypass -File C:\dev\helworks\helengine\artifacts\build-platform.ps1 -Project C:\dev\helprojs\demodisc\project.heproj -Platform ds -Output C:\dev\helprojs\demodisc\ds-tilt-trial-platform-ui -Configuration Release
rtk powershell -NoProfile -ExecutionPolicy Bypass -File C:\dev\helworks\helengine\artifacts\build-platform.ps1 -Project C:\dev\helprojs\demodisc\project.heproj -Platform 3ds -Output C:\dev\helprojs\demodisc\3ds-tilt-trial-platform-ui -Configuration Release
```

Expected: all tests pass; both packages contain shared gameplay scenes plus only handheld UI dependencies, while the packaged selector resolves to `tilt_trial_ds`.

- [ ] **Step 5: Commit the target selection**

```powershell
git -C C:\dev\helprojs\demodisc add user_settings/build_config.json assets/codebase/game.tools.tests/TiltTrialBuildConfigTests.cs
git -C C:\dev\helprojs\demodisc commit -m "build: select handheld Tilt Trial menu for DS"
```

## Final Verification

- [ ] Run `rtk git diff --check` in both repositories.
- [ ] Confirm `menu.generate-game-scenes` leaves all five `tilt_trial_level_0*.helen` files untouched.
- [ ] Open and close `tilt_trial_level_01.helen` in the editor after blueprint expansion; the editor must not crash or retain disposed entities.
- [ ] Launch the DS build with Tilt Trial open. Verify 3D renders only on the top screen and HUD plus pause/restart/back controls render only on the bottom screen.
- [ ] Launch the 3DS build and verify it uses the same handheld presentation asset set.
