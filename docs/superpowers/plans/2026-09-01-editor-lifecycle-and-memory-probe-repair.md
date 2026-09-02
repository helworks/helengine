# Editor Lifecycle Fixture and Memory Probe Repair Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task.

**Goal:** Restore component tests that bypass the current entity lifecycle and repair the production `SceneMemoryProbeComponent` measurement regression.

**Architecture:** Component registration remains gated by initialized entity hierarchy. Tests that intend to exercise live components must initialize the hierarchy after attaching their full component set. Separately, `SceneMemoryProbeComponent.EmitMeasurement` must execute its existing measurement path instead of returning before it.

**Tech Stack:** C#/.NET 9, xUnit, HelEngine entity/component lifecycle

---

### Task 1: Bring component fixtures onto the initialized hierarchy lifecycle

**Files:**
- Modify: `engine/helengine.editor.tests/components/ReferenceCanvasFitComponentTests.cs`
- Modify: `engine/helengine.editor.tests/ViewportAndAnchorLayoutTests.cs`
- Modify: `engine/helengine.editor.tests/EntityHierarchyEnabledStateTests.cs`
- Modify: `engine/helengine.editor.tests/DebugComponentTests.cs`
- Modify: `engine/helengine.editor.tests/FPSComponentTests.cs`
- Modify: `engine/helengine.editor.tests/TextBoxComponentKeyboardFocusTests.cs`
- Modify: `engine/helengine.editor.tests/TextComponentSelectionTests.cs`
- Modify: `engine/helengine.editor.tests/EditorUpdateComponentExecutionPolicyTests.cs`
- Modify: `engine/helengine.editor.tests/CoreTimingTests.cs`
- Modify: `engine/helengine.editor.tests/AnimationPlayerComponentTests.cs`
- Modify if confirmed by focused rerun: `engine/helengine.editor.tests/PointerInteractableHitResolverTests.cs`

- [ ] **Step 1: Preserve representative RED evidence**

Run one failing class from layout, enabled-state, overlay, text input, and timing. Confirm the expected values/registrations are absent because test entities have not run `InitializeHierarchy()`.

- [ ] **Step 2: Initialize complete test hierarchies**

Update only the fixture builders and individual arrangements that intend to model an attached/live entity. Attach all components and children first, then call `InitializeHierarchy()` once on the root. Do not change `Entity.AddComponent`, `EditorEntity.InitializeEditorEntity`, or production initialization semantics. Avoid double initialization in helpers whose callers already initialize the hierarchy.

- [ ] **Step 3: Supply the current direct-drag input state**

In the direct TextBox cursor-drag test, set mouse-left `Pressed` in addition to its existing pointer state because current `TextBoxComponent` begins selection only on a press transition. Do not relax the production input guard.

- [ ] **Step 4: Verify each modified class**

Run complete class filters for all listed files. Modify `PointerInteractableHitResolverTests` only if its focused rerun confirms the same lifecycle root. Expected: the lifecycle cluster passes without production lifecycle changes.

- [ ] **Step 5: Commit only the lifecycle fixture repairs**

Commit the exact modified test files with message `Initialize editor component test hierarchies`.

### Task 2: Restore scene memory probe measurement emission

**Files:**
- Modify: `engine/helengine.core/components/SceneMemoryProbeComponent.cs`
- Test: `engine/helengine.editor.tests/SceneMemoryProbeComponentTests.cs`

- [ ] **Step 1: Preserve the complete RED class result**

Run `SceneMemoryProbeComponentTests`. Expected: six failures show no measurement, no step completion, or no loop restart.

- [ ] **Step 2: Remove the unreachable-path regression**

Delete only the unconditional early `return` at the beginning of `EmitMeasurement` so the existing measurement, completion, looping, and disposal logic executes. Do not redesign the probe or change its public contract.

- [ ] **Step 3: Verify the complete probe class**

Run `SceneMemoryProbeComponentTests`. Expected: all tests pass.

- [ ] **Step 4: Commit only the probe repair**

Commit `SceneMemoryProbeComponent.cs` with message `Restore scene memory probe measurements`.

### Task 1A: Keep the FPS overlay compact for detail-only platform rows

**Files:**
- Modify: `engine/helengine.core/components/2d/FPSComponent.cs`
- Modify: `engine/helengine.editor.tests/FPSComponentTests.cs`

- [ ] **Step 1: Preserve the post-lifecycle RED evidence**

After initializing the four FPS test hierarchies, run `FPSComponentTests`. Expected: 20 pass and two detail-only cases fail because `ShouldUsePlatformOwnedOverlayTextRows` activates on detail/additional text without a platform update or render row.

- [ ] **Step 2: Require a platform summary-row override**

Change `ShouldUsePlatformOwnedOverlayTextRows` so explicit platform text mode activates only when `PerformanceOverlayUpdateText` or `PerformanceOverlayRenderText` is non-empty. Detail and additional rows remain visible when supplied alongside either summary override, but detail-only diagnostics do not expand the generic compact two-line FPS overlay. Preserve platform-owned presentation and metrics fallback behavior.

- [ ] **Step 3: Verify the complete FPS class**

Run `FPSComponentTests`. Expected: all 22 tests pass, the generic summary remains compact for detail-only inputs, full platform row sets remain visible, and platform-owned presentation publishes the compact resolved rows.

- [ ] **Step 4: Commit the FPS contract repair**

Commit `FPSComponent.cs` and `FPSComponentTests.cs` together with message `Keep detail-only FPS overlays compact`.

### Task 3: Verify adjacent lifecycle behavior

**Files:**
- Modify: none

- [ ] **Step 1: Run lifecycle-adjacent suites**

Run the core entity, update-component, render-registration, and editor component execution policy tests most directly adjacent to the modified fixtures. Expected: all selected tests pass and no production lifecycle behavior has been loosened.

### Task 2A: Advance probe test scenes at frame boundaries

**Files:**
- Modify: `engine/helengine.editor.tests/SceneMemoryProbeComponentTests.cs`

- [ ] **Step 1: Record the post-production-fix RED split**

With the `EmitMeasurement` early return removed, run `SceneMemoryProbeComponentTests`. Expected: the stable log test passes because `core.Draw()` commits its bootstrap load; the other five tests fail before attaching the probe because their queued bootstrap scenes have not crossed a frame boundary.

- [ ] **Step 2: Commit setup loads explicitly**

Call `core.CompleteFrameBoundary()` after the bootstrap `LoadScene` in every test that immediately inspects `LoadedScenes`. In the unload test, queue both the persistent bootstrap single load and the additive target load, then commit the boundary before selecting the bootstrap root.

- [ ] **Step 3: Commit probe-issued scene actions before observing them**

For single-load, additive-load, and unload probe steps, keep the first update that starts the probe and the next update that queues the scene action. Call `core.CompleteFrameBoundary()` after the action-issuing update, then perform the following update that emits the measurement. Preserve the production rule that `LoadScene` and `UnloadScene` remain deferred.

- [ ] **Step 4: Verify the full probe class GREEN**

Run `SceneMemoryProbeComponentTests`. Expected: all six tests pass, including loading, unloading, looping, and stable measurement logging.

- [ ] **Step 5: Commit the completed probe repair**

Commit `SceneMemoryProbeComponent.cs` and `SceneMemoryProbeComponentTests.cs` together with message `Restore scene memory probe measurements` so the production repair and its current-frame-boundary test contract remain atomic.
