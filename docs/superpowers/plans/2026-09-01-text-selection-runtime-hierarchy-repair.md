# Text Selection Runtime Hierarchy Repair Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task.

**Goal:** Let selection-enabled runtime text participate safely in recursive scene preparation instead of masking the generated-child defect in font-loading fixtures.

**Architecture:** `TextComponent` owns the generated selection-highlight entity. It must initialize both component and child collections before attaching that entity, matching every traversable runtime entity contract. The source-font fixture will keep selection enabled so runtime loading exercises the real path.

**Tech Stack:** C#/.NET 9, xUnit, HelEngine runtime entity hierarchy

---

### Task 1: Initialize generated selection-highlight children

**Files:**
- Modify: `engine/helengine.core/components/2d/TextComponent.cs`
- Modify: `engine/helengine.editor.tests/TextComponentSelectionTests.cs`
- Preserve in current-format work: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`

- [ ] **Step 1: Add a failing generated-hierarchy regression test**

Create a selection-enabled `TextComponent`, attach it to a fully constructed entity, and run `RuntimeMeshPreparationService.Prepare` over the hierarchy. Assert the generated selection child exists with initialized component and child collections and preparation completes without throwing. Confirm RED currently fails while recursively reading the generated child's null `Children` list.

- [ ] **Step 2: Initialize the owned child collection**

In `TextComponent.EnsureSelectionInfrastructure`, call `SelectionEntityValue.InitChildren()` beside its existing `InitComponents()` call before attaching the generated entity. Do not add null-tolerant traversal to `RuntimeMeshPreparationService`; traversable entities must honor the initialized-hierarchy invariant.

- [ ] **Step 3: Keep runtime font fixtures representative**

Keep `WriteTextComponentPayload` in `RuntimeSceneLoadServiceTests` at `SelectionEnabled = true`. Do not disable selection to bypass recursive preparation.

- [ ] **Step 4: Verify focused selection and runtime-load classes**

Run `TextComponentSelectionTests` and `RuntimeSceneLoadServiceTests`. Expected: both classes pass, including selection-enabled source-font/shared-font runtime loading.

- [ ] **Step 5: Commit the hierarchy repair separately**

Commit `TextComponent.cs` and `TextComponentSelectionTests.cs` with message `Initialize generated text selection hierarchy`. Keep the broader current-format test updates in their own commit.
