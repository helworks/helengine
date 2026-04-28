# Scene Hierarchy Tree View Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the scene hierarchy into a collapsible tree view with arrow-only mouse toggles and keyboard navigation for visible rows.

**Architecture:** Keep `SceneHierarchyPanel` as the owner of hierarchy presentation state by adding per-entity expansion tracking and filtering the existing flattened row list to visible branches only. Extend `SceneHierarchyRow` with arrow presentation metadata, update pointer hit testing to distinguish arrow toggles from row selection, and route arrow-key input through the existing keyboard-focus pipeline so focused hierarchy rows can expand, collapse, and move across visible rows.

**Tech Stack:** C#, xUnit, helengine editor UI components, existing `EditorKeyboardFocusService`

---

### Task 1: Tree Row Affordance And Visible-Branch Layout

**Files:**
- Modify: `engine/helengine.editor/components/ui/SceneHierarchyPanel.cs`
- Modify: `engine/helengine.editor/components/ui/SceneHierarchyRow.cs`
- Test: `engine/helengine.editor.tests/SceneHierarchyPanelTests.cs`

- [ ] **Step 1: Write the failing mouse-behavior tests**

Add tests to `SceneHierarchyPanelTests` that:
- create a parent entity with one child,
- assert the child row is initially visible,
- click the parent row arrow region and assert the child row disappears,
- click the arrow again and assert the child row reappears,
- click the row body and assert selection changes without collapsing the branch,
- right-click the parent row and assert the `Reparent` context menu still opens.

- [ ] **Step 2: Run the targeted tests to verify they fail**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneHierarchyPanelTests" -v minimal`

Expected: FAIL because the hierarchy rows do not yet expose an arrow region or collapse descendants.

- [ ] **Step 3: Write the minimal tree-row implementation**

Update `SceneHierarchyRow` to carry:
- the arrow host visual,
- the arrow text component,
- per-row `HasChildren`,
- per-row `IsExpanded`,
- per-row arrow hit-region metadata.

Update `SceneHierarchyPanel` to:
- add persistent expanded state keyed by `Entity`,
- default newly seen parent entities to expanded,
- prune stale expansion entries during `RefreshHierarchy()`,
- flatten only visible branches,
- render `>` for collapsed parents and `v` for expanded parents,
- hide the arrow for leaf rows,
- distinguish arrow clicks from row-body clicks,
- keep row-body selection behavior unchanged,
- preserve right-click context-menu behavior.

- [ ] **Step 4: Run the targeted tests to verify they pass**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneHierarchyPanelTests" -v minimal`

Expected: PASS for the new tree-row mouse tests and the existing context-menu selection tests.

### Task 2: Keyboard Navigation For Focused Hierarchy Rows

**Files:**
- Modify: `engine/helengine.editor/components/ui/SceneHierarchyPanel.cs`
- Modify: `engine/helengine.editor/components/EditorKeyboardFocusUpdateComponent.cs`
- Test: `engine/helengine.editor.tests/SceneHierarchyPanelKeyboardFocusTests.cs`

- [ ] **Step 1: Write the failing keyboard tests**

Add tests to `SceneHierarchyPanelKeyboardFocusTests` that:
- focus a parent row, press `Right`, and assert its child becomes visible when collapsed,
- focus an expanded parent row, press `Left`, and assert its child becomes hidden,
- focus successive rows and assert `Up` / `Down` move only across currently visible rows,
- assert collapsing a parent keeps focus on that parent when the child becomes hidden.

- [ ] **Step 2: Run the targeted keyboard tests to verify they fail**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneHierarchyPanelKeyboardFocusTests" -v minimal`

Expected: FAIL because the editor does not yet route arrow keys into focused hierarchy rows and the panel does not implement tree-view keyboard behavior.

- [ ] **Step 3: Write the minimal keyboard implementation**

Update `SceneHierarchyPanel` so each row focus target can respond to:
- `Up` by focusing the previous visible row,
- `Down` by focusing the next visible row,
- `Right` by expanding a collapsed parent row,
- `Left` by collapsing an expanded parent row.

Update `EditorKeyboardFocusUpdateComponent` to route `Up`, `Down`, `Left`, and `Right` through `EditorKeyboardFocusService.HandleActivationKey(...)` when they are newly pressed.

Keep the implementation scoped to visible rows and preserve focus when collapse hides descendants.

- [ ] **Step 4: Run the targeted keyboard tests to verify they pass**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneHierarchyPanelKeyboardFocusTests" -v minimal`

Expected: PASS for the new tree-view keyboard tests and the existing row-activation focus tests.

### Task 3: Focused Regression Verification

**Files:**
- Modify: `engine/helengine.editor/components/ui/SceneHierarchyPanel.cs`
- Modify: `engine/helengine.editor/components/ui/SceneHierarchyRow.cs`
- Modify: `engine/helengine.editor/components/EditorKeyboardFocusUpdateComponent.cs`
- Test: `engine/helengine.editor.tests/SceneHierarchyPanelTests.cs`
- Test: `engine/helengine.editor.tests/SceneHierarchyPanelKeyboardFocusTests.cs`

- [ ] **Step 1: Run the combined hierarchy regression suite**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneHierarchyPanelTests|FullyQualifiedName~SceneHierarchyPanelKeyboardFocusTests" -v minimal`

Expected: PASS.

- [ ] **Step 2: Refactor only if needed**

If the implementation duplicated arrow-layout math or visible-row lookup logic, extract the minimal private methods inside `SceneHierarchyPanel` needed to keep the panel readable without widening scope.

- [ ] **Step 3: Re-run the combined hierarchy regression suite**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneHierarchyPanelTests|FullyQualifiedName~SceneHierarchyPanelKeyboardFocusTests" -v minimal`

Expected: PASS with no new failures in the touched hierarchy areas.
