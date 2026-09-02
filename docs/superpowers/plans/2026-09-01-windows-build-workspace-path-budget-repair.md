# Windows Build Workspace Path Budget Repair Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task.

**Goal:** Keep default Windows platform-build workspaces short enough for CMake and MSVC object/PDB paths while preserving project isolation and unique concurrent executions.

**Architecture:** `EditorBuildIsolationPathResolver` will retain the existing project-scoped platform root and every configured/stable-cache layout. Only the default temporary workspace branch will replace user-facing queue/execution segments with deterministic compact SHA-256 segments and a short workspace marker. The factory will continue generating a fresh execution id, so concurrent runs remain isolated without exposing long identifiers in native build paths.

**Tech Stack:** C#/.NET 9, xUnit, SHA-256, CMake/MSVC Windows path constraints

---

### Task 1: Capture the native path-budget regression

**Files:**
- Modify: `engine/helengine.editor.tests/managers/project/EditorBuildIsolationPathResolverTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphWorkspaceFactoryTests.cs`

- [ ] **Step 1: Add a failing default-path budget test**

Create a workspace through `EditorPlatformBuildGraphWorkspaceFactory` with representative Windows platform and queue identifiers. Append the known native CMake scratch/object suffix that exceeded `CMAKE_OBJECT_PATH_MAX`, and assert the resulting path remains below the 250-character CMake/MSVC limit with explicit safety headroom. Confirm the current layout fails.

- [ ] **Step 2: Preserve isolation contracts in tests**

Update default-layout assertions to require project/platform scoping without depending on literal queue identifiers. Add resolver assertions that repeated ids are deterministic, distinct queue or execution ids produce distinct roots, and generated-code invocation roots remain distinct.

### Task 2: Compact only default temporary invocation paths

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorBuildIsolationPathResolver.cs`

- [ ] **Step 1: Add compact deterministic invocation segments**

Hash sanitized queue and execution identifiers with SHA-256 and retain enough bytes to provide collision resistance for local build invocations. Encode them as lowercase hexadecimal fixed-width segments.

- [ ] **Step 2: Shorten the default workspace marker**

Use the compact segments beneath the existing project/platform root with a short stable marker. Do not shorten or change the existing project hash, deterministic stable-cache roots, or explicitly configured `HELENGINE_BUILD_WORKSPACE_ROOT` layout.

- [ ] **Step 3: Keep validation and concurrency behavior intact**

Continue rejecting blank platform, queue, and execution ids. Ensure different queue and execution ids cannot share mutable workspace roots and concurrent factory calls remain unique.

### Task 3: Verify the focused repair

**Files:**
- Modify: none

- [ ] **Step 1: Run resolver and workspace-factory tests**

Run `EditorBuildIsolationPathResolverTests` and `EditorPlatformBuildGraphWorkspaceFactoryTests`. Expected: all tests pass, including the native path-budget regression and concurrency checks.

- [ ] **Step 2: Check the patch**

Run `git diff --check` and inspect the exact production/test diff. Commit only the planned files with message `Shorten temporary platform build workspaces`.

### Task 4: Re-run the guarded Windows build

**Files:**
- Modify: none

- [ ] **Step 1: Run the committed Windows point-shadow build test**

Run `EditorPlatformBuildGraphRunnerTests.Execute_WhenBuildingCommittedPointShadowSceneForWindows_Succeeds` while monitoring `codegen.exe` and `WerFault`. If a codegen application-error window appears, terminate the launched process immediately and stop rather than retrying.

- [ ] **Step 2: Confirm native configuration passes the former failure point**

Inspect the test result and native configure log. Expected: no `CMAKE_OBJECT_PATH_MAX` warning, no MSVC C1041 PDB failure, and the Windows build test passes.
