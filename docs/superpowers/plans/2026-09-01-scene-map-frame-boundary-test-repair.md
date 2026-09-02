# Scene Map Frame-Boundary Test Repair Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task.

**Goal:** Restore the four scene-map tests against the current deferred scene-operation contract without making scene loading immediate again.

**Architecture:** `SceneManager.LoadScene` queues work and `Core.CompleteFrameBoundary` commits it at a safe ownership boundary. Scene-map tests must explicitly advance that boundary after startup, mapped redirects, and every round-trip transition before asserting loaded-scene or owned-asset state.

**Tech Stack:** C#/.NET 9, xUnit, HelEngine runtime scene manager

---

### Task 1: Advance deferred scene operations in scene-map tests

**Files:**
- Modify: `engine/helengine.editor.tests/serialization/scene/SceneMapServiceTests.cs`

- [ ] **Step 1: Preserve the four RED failures**

Run the complete `SceneMapServiceTests` class. Confirm the four failing methods observe unloaded scenes or zero owned assets immediately after `LoadScene`/`Update`, while the remaining singleton and mapping tests pass.

- [ ] **Step 2: Commit the authored startup scene at a frame boundary**

After each `LoadScene("StartupScene", SceneLoadMode.Single)`, call `core.CompleteFrameBoundary()` before updating the newly materialized `SceneMapComponent`.

- [ ] **Step 3: Commit the mapped startup redirect**

After `core.Update(...)` causes `SceneMapComponent` to queue its initial mapped scene, call `core.CompleteFrameBoundary()` before asserting loaded scenes or owned assets. In the test that injects an already-loaded scene map directly, replace the extra update used as an implicit wait with the explicit boundary commit.

- [ ] **Step 4: Commit every round-trip transition before assertions**

In each loop, call `core.CompleteFrameBoundary()` after loading `cube_test` and again after loading the mapped menu. Assert scene membership and owned-asset counts only after the corresponding commit. Do not call private immediate loaders or change `SceneManager.LoadScene`.

- [ ] **Step 5: Verify the complete class**

Run `SceneMapServiceTests`. Expected: every test passes, persistent boot scenes remain loaded, mapped menu routing survives all cycles, and owned-asset counts switch only at committed boundaries.

- [ ] **Step 6: Commit only the test repair**

Commit `SceneMapServiceTests.cs` with message `Advance scene map tests at frame boundaries`.
