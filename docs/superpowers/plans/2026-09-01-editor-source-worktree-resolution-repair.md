# Editor Source Worktree Resolution Repair Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task.

**Goal:** Make source-build tests resolve the real shared HelEngine checkout from linked worktrees located outside the main repository, while removing editor tests that reach into unrelated console repositories.

**Architecture:** `EditorSourceBuildWorkspaceLocator` will retain its simple sibling-worktree path fast path, then resolve standard Git linked-worktree metadata from the worktree's `.git` pointer when the main checkout lives elsewhere. Editor-local build tests will use current native fixtures. DS and Windows native-source assertions will live in their owning repositories, not the editor suite.

**Tech Stack:** C#/.NET 9, xUnit, Git linked-worktree metadata

---

### Task 1: Resolve shared roots through linked-worktree metadata

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorSourceBuildWorkspaceLocator.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorSourceBuildWorkspaceLocatorTests.cs`

- [ ] **Step 1: Add a failing non-sibling linked-worktree test**

Create isolated temporary directories representing a main checkout and a worktree beneath a different `.worktrees` parent. Put the editor project marker in both roots. Put a standard `.git` pointer file in the worktree that references `<main>/.git/worktrees/<name>`, set `HELENGINE_SOURCE_ROOT` to the worktree, and assert `ResolveSharedHelEngineRootPath()` returns the main checkout. Confirm RED currently reports the worktree container as the missing shared root.

- [ ] **Step 2: Parse only the standard linked-worktree pointer**

Add class-level methods that read `<worktree>/.git` only when it is a file beginning with `gitdir:`, canonicalize an absolute or worktree-relative target, validate the `.git/worktrees/<name>` directory shape, derive the main checkout root, and accept it only when the HelEngine editor project marker exists. Do not invoke Git, scan arbitrary parents, or trust an unvalidated pointer.

- [ ] **Step 3: Preserve existing fast paths and diagnostics**

Keep the environment override and ordinary repository behavior unchanged. In `ResolveSharedHelEngineRootPath`, use the linked-worktree resolver only after the conventional `.worktrees` parent candidate lacks the marker; throw the existing clear error if neither candidate validates.

- [ ] **Step 4: Verify the complete locator class**

Run `EditorSourceBuildWorkspaceLocatorTests`. Expected: existing override tests and the new detached-parent linked-worktree test pass.

- [ ] **Step 5: Commit the locator repair**

Commit the two files with message `Resolve shared source root from git worktrees`.

### Task 2: Bring the build-graph fixture onto current native identity

**Files:**
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`

- [ ] **Step 1: Preserve the focused identity RED failure**

Run `RunCookAssets_writes_scene_outputs_beneath_workspace_cook_root_without_duplicate_cooked_segment`. Confirm its native MainMenu scene lacks embedded identity.

- [ ] **Step 2: Embed deterministic identity in the fixture**

Update the scoped native scene writer to assign a deterministic lowercase 32-character `AuthoringAssetId`. Do not add a sidecar or weaken identity reconciliation.

- [ ] **Step 3: Verify the focused cook-assets test and commit**

Commit only the test file with message `Update build graph scene fixture identity`.

### Task 3: Remove native-platform source audits from the editor suite

**Files:**
- Delete: `engine/helengine.editor.tests/NintendoDsRenderManager3DSourceTests.cs`
- Delete: `engine/helengine.editor.tests/WindowsStandardMaterialFallbackSourceTests.cs`

- [ ] **Step 1: Delete the two cross-repository test files**

These tests construct paths to sibling `helengine-ds` and `helengine-windows` repositories and therefore cannot be hermetic editor tests. Do not replace them with environment-specific path discovery. Their platform-native contracts belong in the owning platform repositories.

- [ ] **Step 2: Verify no editor test references sibling platform worktrees**

Search editor tests for `helengine-ds`, `helengine-windows`, and path construction that escapes `ResolveHelEngineRootPath()` into a sibling repository. Expected: zero remaining cross-repository native-source audits.

- [ ] **Step 3: Commit the removals**

Commit the two deletions with message `Remove external platform source audits`.

### Task 4: Verify source-build consumers carefully

**Files:**
- Modify: none

- [ ] **Step 1: Run the asset-cook class**

Run `EditorPlatformAssetCookServiceTests`. Expected: the two shared-root failures are gone.

- [ ] **Step 2: Run build-graph tests individually before the class**

Run the worktree bootstrap test, then the committed point-shadow base test, then the complete `EditorPlatformBuildGraphRunnerTests` class. Monitor for `codegen.exe` UI/application errors; if any appears, terminate that process immediately and stop rather than retrying. Expected: all selected tests pass without UI.
