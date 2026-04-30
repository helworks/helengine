# Properties Panel Component Shell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add reusable component headers to the Properties panel with collapse/expand behavior and a confirmation modal before removing components from the selected entity.

**Architecture:** Add a reusable component-section shell in the editor-side Properties panel flow rather than mixing generic chrome into component-specific field rendering. Keep component field editors in `ComponentPropertiesView`, move per-section UI state and remove-confirmation coordination into the Properties panel layer, and use one dedicated modal for component removal so the behavior stays consistent with other editor dialogs.

**Tech Stack:** C#/.NET 9, helengine editor UI entities/components, xUnit editor tests

---

## File Structure

- Modify: `engine/helengine.editor/components/ui/PropertiesPanel.cs`
  - Wrap each rendered component section in a reusable header/body shell.
  - Track per-component collapsed state for the currently selected entity.
  - Wire header click and remove-click events.
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
  - Keep component-specific body rendering separate from shell chrome.
  - Expose body layout/height information in a way the shell can host.
- Create: `engine/helengine.editor/components/ui/PropertiesComponentSection.cs`
  - Shared UI wrapper for one component card header, body visibility, and fixed `X`.
- Create: `engine/helengine.editor/components/ui/RemoveComponentDialog.cs`
  - Confirmation modal for removing one component from one entity.
- Modify: `engine/helengine.editor/EditorSession.cs`
  - Construct the dialog, route confirm/cancel events, and keep the entity selected after removal.
- Modify: `engine/helengine.editor/components/ui/PropertiesPanelUpdater.cs`
  - Ensure refreshed component shells stay in sync after collapse and removal.
- Test: `engine/helengine.editor.tests/PropertiesPanelMutationTests.cs`
  - Collapse/expand and component-shell visibility regressions.
- Create: `engine/helengine.editor.tests/RemoveComponentDialogTests.cs`
  - Modal behavior and wording coverage.
- Create: `engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs`
  - Header click, `X` click, and per-component collapsed-state coverage.
- Modify: `engine/helengine.editor.tests/EditorSessionSceneHierarchyReparentTests.cs`
  - Reuse existing selection-refresh patterns if helpful for selection persistence after removal.

### Task 1: Add the remove-component confirmation dialog

**Files:**
- Create: `engine/helengine.editor/components/ui/RemoveComponentDialog.cs`
- Test: `engine/helengine.editor.tests/RemoveComponentDialogTests.cs`

- [ ] **Step 1: Write the failing dialog tests**

Add tests for:
- dialog title is `Remove Component`
- message includes component name and entity name
- `Remove` raises confirm
- `Cancel` closes without confirm

- [ ] **Step 2: Run the dialog tests to verify they fail**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~RemoveComponentDialogTests" -v minimal`
Expected: FAIL because the dialog class does not exist yet

- [ ] **Step 3: Implement the dialog**

Create `RemoveComponentDialog.cs` using the existing `EditorDialogBase` pattern:
- header/title
- message text
- `Remove` button
- `Cancel` button
- confirm/cancel events
- `Show(entityName, componentName)` method

- [ ] **Step 4: Run the dialog tests to verify they pass**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~RemoveComponentDialogTests" -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/components/ui/RemoveComponentDialog.cs engine/helengine.editor.tests/RemoveComponentDialogTests.cs
git commit -m "Add remove component confirmation dialog"
```

### Task 2: Add the reusable component section shell

**Files:**
- Create: `engine/helengine.editor/components/ui/PropertiesComponentSection.cs`
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Test: `engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs`

- [ ] **Step 1: Write the failing section-shell tests**

Add tests for:
- one component section renders a header with the component name
- clicking the header toggles collapsed state
- collapsed sections hide the body
- the fixed `X` is present on the right edge

- [ ] **Step 2: Run the section-shell tests to verify they fail**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~PropertiesPanelComponentShellTests" -v minimal`
Expected: FAIL because the shell class and wiring do not exist yet

- [ ] **Step 3: Implement the reusable shell**

Create `PropertiesComponentSection.cs` with:
- title-bar background
- title text
- remove button host
- collapsed/expanded body host
- events for header click and remove click

Adjust `ComponentPropertiesView.cs` only enough to keep body content rendering separate and hostable inside the new shell.

- [ ] **Step 4: Run the section-shell tests to verify they pass**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~PropertiesPanelComponentShellTests" -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/components/ui/PropertiesComponentSection.cs engine/helengine.editor/components/ui/ComponentPropertiesView.cs engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs
git commit -m "Add properties panel component section shell"
```

### Task 3: Integrate component shells into the Properties panel

**Files:**
- Modify: `engine/helengine.editor/components/ui/PropertiesPanel.cs`
- Modify: `engine/helengine.editor/components/ui/PropertiesPanelUpdater.cs`
- Test: `engine/helengine.editor.tests/PropertiesPanelMutationTests.cs`

- [ ] **Step 1: Write the failing Properties panel tests**

Add tests for:
- component sections start expanded
- collapsing one component does not collapse another
- collapse state is local to the panel state and hides the body rows

- [ ] **Step 2: Run the Properties panel tests to verify they fail**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~PropertiesPanelMutationTests|FullyQualifiedName~PropertiesPanelComponentShellTests" -v minimal`
Expected: FAIL because the panel does not yet use the shell

- [ ] **Step 3: Implement the panel integration**

Update `PropertiesPanel.cs` to:
- create one `PropertiesComponentSection` per component
- store collapsed state keyed by component instance or stable section identity
- keep rendering order and body layout correct after collapse/expand

Update `PropertiesPanelUpdater.cs` if needed so the panel rebuild respects section state after refreshes.

- [ ] **Step 4: Run the Properties panel tests to verify they pass**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~PropertiesPanelMutationTests|FullyQualifiedName~PropertiesPanelComponentShellTests" -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/components/ui/PropertiesPanel.cs engine/helengine.editor/components/ui/PropertiesPanelUpdater.cs engine/helengine.editor.tests/PropertiesPanelMutationTests.cs engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs
git commit -m "Integrate component shells into properties panel"
```

### Task 4: Wire remove confirmation and component deletion

**Files:**
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/components/ui/PropertiesPanel.cs`
- Modify: `engine/helengine.editor/components/ui/RemoveComponentDialog.cs`
- Test: `engine/helengine.editor.tests/PropertiesPanelMutationTests.cs`
- Test: `engine/helengine.editor.tests/RemoveComponentDialogTests.cs`

- [ ] **Step 1: Write the failing removal-flow tests**

Add tests for:
- clicking `X` opens the remove modal
- confirming removes the component from the selected entity
- cancelling does not remove it
- selection stays on the same entity after confirm

- [ ] **Step 2: Run the removal-flow tests to verify they fail**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~PropertiesPanelMutationTests|FullyQualifiedName~RemoveComponentDialogTests" -v minimal`
Expected: FAIL because the remove flow is not connected yet

- [ ] **Step 3: Implement remove-component flow**

Update `EditorSession.cs` to:
- construct `RemoveComponentDialog`
- handle show/hide
- remove the chosen component from the selected entity on confirm
- refresh the Properties panel and preserve selection

Update `PropertiesPanel.cs` to forward remove requests for the selected component section.

- [ ] **Step 4: Run the removal-flow tests to verify they pass**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~PropertiesPanelMutationTests|FullyQualifiedName~RemoveComponentDialogTests" -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/EditorSession.cs engine/helengine.editor/components/ui/PropertiesPanel.cs engine/helengine.editor/components/ui/RemoveComponentDialog.cs engine/helengine.editor.tests/PropertiesPanelMutationTests.cs engine/helengine.editor.tests/RemoveComponentDialogTests.cs
git commit -m "Add component removal flow to properties panel"
```

### Task 5: Final verification

**Files:**
- Verify only; no new files required unless a regression fix is needed

- [ ] **Step 1: Run focused editor tests**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~PropertiesPanelComponentShellTests|FullyQualifiedName~PropertiesPanelMutationTests|FullyQualifiedName~RemoveComponentDialogTests" -v minimal`
Expected: PASS

- [ ] **Step 2: Run related existing panel and selection tests**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionSceneHierarchyReparentTests|FullyQualifiedName~ComponentPropertiesView|FullyQualifiedName~PropertiesPanel" -v minimal`
Expected: PASS, or narrow and document any unrelated existing failures

- [ ] **Step 3: Build the editor**

Run: `rtk dotnet build engine/helengine.editor/helengine.editor.csproj -v minimal`
Expected: `0 errors`

- [ ] **Step 4: Commit any final regression adjustments**

```bash
git add .
git commit -m "Polish properties panel component shell behavior"
```
