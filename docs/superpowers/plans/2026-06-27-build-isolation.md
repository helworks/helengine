# Automatic Build Isolation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add automatic per-project per-platform build isolation so concurrent platform builds do not share editor, generated-code, or build-graph output roots.

**Architecture:** Introduce one editor-owned isolation path resolver that computes stable roots from the project path and platform id. Use that resolver for generated script outputs and build workspaces, and mirror the same root layout from the top-level build script so the editor app itself runs with isolated `bin/obj` trees.

**Tech Stack:** C#, xUnit, PowerShell, .NET/MSBuild

---

### Task 1: Add isolation path tests

**Files:**
- Create: `engine/helengine.editor.tests/managers/project/EditorBuildIsolationPathResolverTests.cs`
- Modify: `engine/helengine.editor.tests/EditorGameSolutionServiceTests.cs`

- [ ] **Step 1: Write failing resolver tests for stable per-project per-platform roots**
- [ ] **Step 2: Run the targeted tests and verify they fail for the missing behavior**
- [ ] **Step 3: Add one solution-service test that asserts generated code output can be redirected to an isolated root**

### Task 2: Implement the editor isolation resolver

**Files:**
- Create: `engine/helengine.editor/managers/project/EditorBuildIsolationPathResolver.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphWorkspaceFactory.cs`
- Modify: `engine/helengine.editor/managers/project/EditorGeneratedCodeSolutionBuilder.cs`
- Modify: `engine/helengine.editor/managers/project/EditorGameSolutionService.cs`
- Modify: `engine/helengine.editor/EditorCliBuildRunner.cs`

- [ ] **Step 1: Implement one central resolver with project-hash and platform-scoped roots**
- [ ] **Step 2: Route workspace creation through the resolver**
- [ ] **Step 3: Route generated script project output roots through the resolver for headless builds**
- [ ] **Step 4: Re-run targeted tests and verify the new behavior passes**

### Task 3: Isolate the editor app build entrypoint

**Files:**
- Modify: `artifacts/build-platform.ps1`

- [ ] **Step 1: Compute the same project/platform isolation root in PowerShell**
- [ ] **Step 2: Pass isolated `BaseOutputPath` and `BaseIntermediateOutputPath` into `dotnet run`**
- [ ] **Step 3: Run targeted verification commands and inspect the outputs before reporting completion**
