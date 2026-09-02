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

### Task 3: Verify adjacent lifecycle behavior

**Files:**
- Modify: none

- [ ] **Step 1: Run lifecycle-adjacent suites**

Run the core entity, update-component, render-registration, and editor component execution policy tests most directly adjacent to the modified fixtures. Expected: all selected tests pass and no production lifecycle behavior has been loosened.
