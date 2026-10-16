# Editor Viewport Input-Capture Lifecycle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ensure stale editor UI blockers cannot disable viewport input after an editor session is replaced or disposed.

**Architecture:** The editor session is the owner of process-wide input-capture lifetime. It clears the static registry before registering a new session and during session teardown. A narrowly scoped source test asserts both lifecycle calls remain present.

**Tech Stack:** C#/.NET 9, xUnit.

---

### Task 1: Specify the session input-capture lifecycle

**Files:**
- Create: `engine/helengine.editor.tests/EditorSessionInputCaptureLifecycleSourceTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void Editor_session_source_resets_input_capture_at_startup_and_teardown() {
    string source = File.ReadAllText(GetEditorSessionSourcePath());

    Assert.Equal(2, CountOccurrences(source, "EditorInputCaptureService.Reset();"));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~EditorSessionInputCaptureLifecycleSourceTests`

Expected: FAIL because `EditorSession` has no input-capture reset calls.

### Task 2: Reset static capture state at session boundaries

**Files:**
- Modify: `engine/helengine.editor/EditorSession.cs:555`
- Modify: `engine/helengine.editor/EditorSession.cs:1475`

- [ ] **Step 1: Implement the minimal lifecycle calls**

```csharp
EditorKeyboardFocusService.Reset();
EditorInputCaptureService.Reset();
core.Initialize(render3D, render2D, input, CreateEditorPlatformInfo());
```

```csharp
public void Dispose() {
    EditorInputCaptureService.Reset();
    ClearSceneSelectionBeforeTeardown();
```

- [ ] **Step 2: Run regression test to verify it passes**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~EditorSessionInputCaptureLifecycleSourceTests`

Expected: PASS with one passing test.

- [ ] **Step 3: Run related viewport tests**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorViewportCameraControllerTests|FullyQualifiedName~EditorViewportPickerTests"`

Expected: PASS with no test failures.
