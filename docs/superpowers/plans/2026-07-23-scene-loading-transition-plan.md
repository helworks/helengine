# Scene Loading Transition Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Route every normal Demo Disc scene change through an engine-owned, persistent loading scene with truthful normalized progress.

**Architecture:** `SceneManager` owns transition state and advances one stage at a frame boundary. A resumable runtime scene-load operation materializes one root entity at a time and provides progress from its known root count. A Demo Disc loading scene is additive and `DontUnload`; its component observes `SceneManager` state to show a blocking, bottom-aligned progress bar.

**Tech Stack:** C#/.NET 9, Helengine runtime scene assets, generated Demo Disc authoring scenes, xUnit.

---

### Task 1: Define the runtime transition contract

**Files:**
- Create: `engine/helengine.core/scene/runtime/SceneTransitionState.cs`
- Modify: `engine/helengine.core/scene/runtime/SceneManager.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneManagerTests.cs`

- [ ] **Step 1: Write failing state tests**

Add tests that request a transition and assert `IsSceneTransitionActive`, `TransitionTargetSceneId`, and `SceneTransitionProgress` begin as `true`, target id, and `0f`; after completion assert inactive and `1f`.

- [ ] **Step 2: Run the targeted tests**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~SceneManagerTests --no-restore`

Expected: failing assertions because no transition API/state exists.

- [ ] **Step 3: Add the state model and public request API**

Create `SceneTransitionState` with target scene id, progress, and stage. Add `SceneManager.RequestSceneTransition(string sceneId)` and read-only state properties. Keep raw `LoadScene`/`UnloadScene` untouched for additive infrastructure and diagnostics.

- [ ] **Step 4: Re-run targeted tests**

Expected: the contract tests pass while existing raw load tests remain unchanged.

### Task 2: Make scene materialization resumable

**Files:**
- Create: `engine/helengine.core/scene/runtime/RuntimeSceneLoadOperation.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeSceneLoadService.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`

- [ ] **Step 1: Write a failing incremental-load test**

Create a scene with multiple root entities. Assert each `Advance()` materializes at most one root, progress stays in `[0f,1f]`, is monotonic, and the completed result contains all initialized roots and owned assets.

- [ ] **Step 2: Run the focused loader tests**

Expected: failure because `LoadTracked` materializes all roots synchronously.

- [ ] **Step 3: Implement `RuntimeSceneLoadOperation`**

Begin owned-asset tracking once, materialize one root per `Advance()`, initialize all roots when the final root completes, and complete owned-asset tracking exactly once. Retain `LoadTracked` as a compatibility wrapper that advances an operation to completion.

- [ ] **Step 4: Re-run focused loader tests**

Expected: incremental and existing synchronous compatibility tests pass.

### Task 3: Advance transitions across frame boundaries

**Files:**
- Modify: `engine/helengine.core/scene/runtime/SceneManager.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneManagerTests.cs`

- [ ] **Step 1: Write failing lifecycle tests**

Assert a transition preserves `DontUnload` scenes, removes normal scenes, publishes progress after unload and each materialized root, registers the target only on completion, and resets state after a load exception.

- [ ] **Step 2: Run focused lifecycle tests**

Expected: failure because `CommitPendingOperationsAtFrameBoundary` fully loads a scene in one call.

- [ ] **Step 3: Implement staged transition advancement**

Handle request, non-persistent unload, content asset acquisition, incremental operation advances, loaded-scene registration, `SceneLoaded` dispatch, and transient-asset release in ordered transition stages. Clamp each public progress update to 0–1 and release partially created roots/assets on exceptions before marking the transition inactive.

- [ ] **Step 4: Re-run engine transition tests**

Expected: all `SceneManagerTests` and `RuntimeSceneLoadServiceTests` pass.

### Task 4: Author the persistent Demo Disc loading scene

**Files:**
- Create: `assets/codebase/menu/SceneLoadingScreenComponent.cs`
- Create: `assets/codebase/menu.tools/SceneLoadingScreenFactory.cs`
- Modify: `assets/codebase/menu.tools/DemoDiscSceneGenerator.cs`
- Test: `assets/codebase/menu.tools.tests/SceneLoadingScreenSourceTests.cs`

- [ ] **Step 1: Write failing authoring tests**

Assert the generated loading scene has `DontUnload = true`, a final overlay camera, an opaque background, a bottom progress track/fill, and references from the component to its visible nodes.

- [ ] **Step 2: Run the focused Demo Disc authoring tests**

Expected: failure because no loading-scene factory/component exists.

- [ ] **Step 3: Implement the loading presentation**

Use `SceneManager.IsSceneTransitionActive` and `SceneTransitionProgress` to enable the overlay, block menu input while visible, and set the fill width from the viewport-scaled bar width. Hide only after the manager reports a stable completed transition.

- [ ] **Step 4: Register scene generation and boot loading**

Generate the loading scene with the existing menu scene generator. Load it additively with the main menu bootstrap path so it survives all later single-scene transitions.

- [ ] **Step 5: Re-run focused Demo Disc tests**

Expected: source and runtime component tests pass.

### Task 5: Migrate every normal Demo Disc transition

**Files:**
- Modify: `assets/codebase/menu/MenuComponent.cs`
- Modify: `assets/codebase/menu/DemoDiscReturnToMenuComponent.cs`
- Modify: `assets/codebase/menu/NintendoDsReturnOverlayComponent.cs`
- Modify: `assets/codebase/game/TiltTrialLevelSelectComponent.cs`
- Modify: `assets/codebase/game/TiltTrialSessionComponent.cs`
- Modify: `assets/codebase/game/ZombislayerSessionComponent.cs`
- Test: `assets/codebase/menu.tools.tests/SceneLoadingTransitionSourceTests.cs`

- [ ] **Step 1: Write failing adoption tests**

Assert every listed normal game/menu transition calls `RequestSceneTransition`; assert splash startup retains its additive raw menu load.

- [ ] **Step 2: Run adoption tests**

Expected: failure because callers directly use `LoadScene(..., SceneLoadMode.Single)`.

- [ ] **Step 3: Replace direct normal transitions**

Replace only game/menu single-load calls with the transition request API. Do not change explicit additive bootstrap or diagnostic loading operations.

- [ ] **Step 4: Run all focused tests**

Run engine scene-manager/loader tests and Demo Disc menu/game transition tests.

### Task 6: Build and verify Windows Demo Disc

**Files:**
- No source changes expected.

- [ ] **Step 1: Build Windows output**

Run: `scripts/build-platform.ps1 -Project C:\dev\helprojs\demodisc -Platform windows -Output C:\dev\helprojs\demodisc\output\windows -Configuration Debug`

- [ ] **Step 2: Verify artifact and behavior**

Confirm `output/windows/helengine_windows.exe` has a fresh timestamp. Verify a menu selection displays the loading overlay, progress advances from 0 to 1, input is blocked, and the selected scene activates after completion.

- [ ] **Step 3: Commit source changes only**

Stage only engine/Demo Disc source, authored scenes, and tests. Exclude all platform build folders and executables.
