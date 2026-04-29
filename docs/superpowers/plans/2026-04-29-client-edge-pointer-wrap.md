# Client-Edge Pointer Wrap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow uninterrupted camera navigation and gizmo drags by wrapping the pointer across the full editor client area only during active navigation interactions.

**Architecture:** The platform mouse backend owns physical cursor teleport and reports wrap events to the shared input layer. The input layer resets mouse-delta continuity after a wrap, while the viewport camera controller and gizmo drag components only enable and disable wrapping for the lifetime of active interactions.

**Tech Stack:** C#, WinForms mouse backend, shared engine input manager, xUnit editor tests, Windows-targeted editor host build.

---

## File Map

### Core input and mouse backend
- Modify: `engine/helengine.core/managers/input/Mouse.cs`
- Modify: `engine/helengine.core/managers/input/InputManager.cs`
- Modify: `engine/helengine.core.windows/input/MouseWindows.cs`
- Modify: `engine/helengine.editor.tests/testing/TestMouse.cs`
- Modify: `engine/helengine.editor.tests/testing/TestInputManager.cs`
- Modify: `engine/helengine.editor.tests/InputManagerTests.cs`

### Camera navigation
- Modify: `engine/helengine.editor/components/EditorViewportCameraController.cs`
- Modify: `engine/helengine.editor.tests/EditorViewportCameraControllerTests.cs`

### Gizmo dragging
- Modify: `engine/helengine.editor/managers/gizmo/TransformTranslationGizmoDragComponent.cs`
- Modify: `engine/helengine.editor/managers/gizmo/TransformRotationGizmoDragComponent.cs`
- Modify: `engine/helengine.editor/managers/gizmo/TransformScaleGizmoDragComponent.cs`
- Create: `engine/helengine.editor.tests/managers/gizmo/TransformGizmoPointerWrapTests.cs`

### Final verification
- Build: `engine/helengine.editor/helengine.editor.csproj`
- Build: `helengine.ui/helengine.editor.app/helengine.editor.app.csproj`

### Worktree caution
- The worktree already contains unrelated dirty files:
  - `engine/helengine.editor/components/EditorViewportCameraController.cs`
  - `engine/helengine.editor.tests/EditorViewportCameraControllerTests.cs`
  - untracked unrelated spec/plan docs
- Preserve those changes and layer this work on top of them instead of reverting anything.

### Task 1: Add wrap-capable mouse state and input continuity

**Files:**
- Modify: `engine/helengine.core/managers/input/Mouse.cs`
- Modify: `engine/helengine.core/managers/input/InputManager.cs`
- Modify: `engine/helengine.core.windows/input/MouseWindows.cs`
- Modify: `engine/helengine.editor.tests/testing/TestMouse.cs`
- Modify: `engine/helengine.editor.tests/testing/TestInputManager.cs`
- Modify: `engine/helengine.editor.tests/InputManagerTests.cs`

- [ ] **Step 1: Write the failing input tests for wrap continuity**

Add focused tests in `engine/helengine.editor.tests/InputManagerTests.cs` for:
- wrap disabled at client edge keeps the raw pointer position
- wrap enabled teleports left/right/top/bottom across a configured client area
- corner wrap teleports both axes
- the first reported delta after a wrap does not include the teleport distance

- [ ] **Step 2: Run the focused input tests to confirm they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~InputManagerTests" -v minimal
```

Expected:
- new wrap tests fail because the current mouse abstraction has no wrap API and `InputManager.GetMouseDelta()` still uses raw previous and current positions

- [ ] **Step 3: Extend the shared mouse abstraction with wrap hooks**

Update `engine/helengine.core/managers/input/Mouse.cs` so the abstraction can:
- enable or disable client-edge wrapping
- receive client bounds for wrapping
- expose whether the most recent state query wrapped the pointer

Keep the API generic to input, not editor-specific.

- [ ] **Step 4: Implement wrap state and teleport in the Windows mouse backend**

Update `engine/helengine.core.windows/input/MouseWindows.cs` to:
- store whether wrapping is enabled
- resolve the current client rectangle from the attached window
- wrap to the opposite interior edge when the pointer reaches or crosses a client boundary
- mark that a wrap occurred during `GetState()`
- leave wrapping disabled when the window handle or client bounds are invalid

- [ ] **Step 5: Mirror the same capability in the test mouse**

Update `engine/helengine.editor.tests/testing/TestMouse.cs` and `engine/helengine.editor.tests/testing/TestInputManager.cs` so tests can:
- configure a client size
- enable and disable wrapping
- observe wrapped positions and wrap events deterministically

- [ ] **Step 6: Reset input delta continuity after a wrap**

Update `engine/helengine.core/managers/input/InputManager.cs` so a backend-reported wrap resets the previous-position basis for mouse delta calculations. The next `GetMouseDelta()` must reflect only real user movement, not the teleport distance.

- [ ] **Step 7: Re-run the focused input tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~InputManagerTests" -v minimal
```

Expected:
- all `InputManagerTests` pass, including the new wrap tests

- [ ] **Step 8: Commit the mouse/input slice**

```bash
git add engine/helengine.core/managers/input/Mouse.cs engine/helengine.core/managers/input/InputManager.cs engine/helengine.core.windows/input/MouseWindows.cs engine/helengine.editor.tests/testing/TestMouse.cs engine/helengine.editor.tests/testing/TestInputManager.cs engine/helengine.editor.tests/InputManagerTests.cs
git commit -m "Add client-edge pointer wrap input support"
```

### Task 2: Enable wrapping during viewport camera navigation

**Files:**
- Modify: `engine/helengine.editor/components/EditorViewportCameraController.cs`
- Modify: `engine/helengine.editor.tests/EditorViewportCameraControllerTests.cs`

- [ ] **Step 1: Write the failing camera-controller tests**

Add focused tests in `engine/helengine.editor.tests/EditorViewportCameraControllerTests.cs` for:
- RMB freelook keeps rotating after the pointer crosses a client edge
- MMB pan keeps moving after the pointer crosses a client edge
- `Alt + MMB` orbit keeps rotating after the pointer crosses a client edge
- camera navigation enables wrap only after a qualifying press begins inside the viewport and disables it when the interaction ends

- [ ] **Step 2: Run the focused camera tests to confirm they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorViewportCameraControllerTests" -v minimal
```

Expected:
- new wrap-focused camera tests fail because the controller never enables wrapping during navigation

- [ ] **Step 3: Wire wrap enablement into viewport camera navigation**

Update `engine/helengine.editor/components/EditorViewportCameraController.cs` so:
- RMB freelook enables wrapping for the drag lifetime
- MMB pan enables wrapping for the drag lifetime
- `Alt + MMB` orbit enables wrapping for the drag lifetime
- all three disable wrapping immediately on release or cancellation

Do not add wrap math here; only call the shared mouse/input API.

- [ ] **Step 4: Re-run the focused camera tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorViewportCameraControllerTests" -v minimal
```

Expected:
- all camera-controller tests pass, including the new continuity cases

- [ ] **Step 5: Commit the camera slice**

```bash
git add engine/helengine.editor/components/EditorViewportCameraController.cs engine/helengine.editor.tests/EditorViewportCameraControllerTests.cs
git commit -m "Enable pointer wrap during camera navigation"
```

### Task 3: Enable wrapping during gizmo drags

**Files:**
- Modify: `engine/helengine.editor/managers/gizmo/TransformTranslationGizmoDragComponent.cs`
- Modify: `engine/helengine.editor/managers/gizmo/TransformRotationGizmoDragComponent.cs`
- Modify: `engine/helengine.editor/managers/gizmo/TransformScaleGizmoDragComponent.cs`
- Create: `engine/helengine.editor.tests/managers/gizmo/TransformGizmoPointerWrapTests.cs`

- [ ] **Step 1: Write the failing gizmo wrap tests**

Create `engine/helengine.editor.tests/managers/gizmo/TransformGizmoPointerWrapTests.cs` with focused tests that prove:
- translate drag enables wrapping at drag start and disables it at drag end
- rotate drag enables wrapping at drag start and disables it at drag end
- scale drag enables wrapping at drag start and disables it at drag end
- wrapped pointer movement still changes the dragged entity rather than stalling

Use the existing test input harness and the same full-client-area rule as the camera tests.

- [ ] **Step 2: Run the focused gizmo tests to confirm they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~TransformGizmoPointerWrapTests" -v minimal
```

Expected:
- new gizmo wrap tests fail because drag components never enable wrapping

- [ ] **Step 3: Wire wrap enablement into each gizmo drag component**

Update:
- `engine/helengine.editor/managers/gizmo/TransformTranslationGizmoDragComponent.cs`
- `engine/helengine.editor/managers/gizmo/TransformRotationGizmoDragComponent.cs`
- `engine/helengine.editor/managers/gizmo/TransformScaleGizmoDragComponent.cs`

Behavior:
- enable wrapping immediately after drag start succeeds
- disable wrapping in all drag teardown paths, including explicit end, selection loss, and component removal

- [ ] **Step 4: Re-run the focused gizmo tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~TransformGizmoPointerWrapTests" -v minimal
```

Expected:
- all new gizmo wrap tests pass

- [ ] **Step 5: Commit the gizmo slice**

```bash
git add engine/helengine.editor/managers/gizmo/TransformTranslationGizmoDragComponent.cs engine/helengine.editor/managers/gizmo/TransformRotationGizmoDragComponent.cs engine/helengine.editor/managers/gizmo/TransformScaleGizmoDragComponent.cs engine/helengine.editor.tests/managers/gizmo/TransformGizmoPointerWrapTests.cs
git commit -m "Enable pointer wrap during gizmo drags"
```

### Task 4: Run the final verification pass

**Files:**
- Modify: none
- Test: `engine/helengine.editor.tests/InputManagerTests.cs`
- Test: `engine/helengine.editor.tests/EditorViewportCameraControllerTests.cs`
- Test: `engine/helengine.editor.tests/managers/gizmo/TransformGizmoPointerWrapTests.cs`

- [ ] **Step 1: Run the focused regression suite**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~InputManagerTests|FullyQualifiedName~EditorViewportCameraControllerTests|FullyQualifiedName~TransformGizmoPointerWrapTests" -v minimal
```

Expected:
- all wrap-related tests pass

- [ ] **Step 2: Run the existing gizmo and pointer-ray regressions**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorViewportPointerRayBuilderTests|FullyQualifiedName~TransformTranslationGizmoFollowComponentTests|FullyQualifiedName~TransformRotationGizmoFollowComponentTests|FullyQualifiedName~TransformScaleGizmoFollowComponentTests" -v minimal
```

Expected:
- existing viewport/gizmo regressions still pass

- [ ] **Step 3: Build the editor core**

Run:

```bash
rtk dotnet build engine/helengine.editor/helengine.editor.csproj -v minimal
```

Expected:
- build succeeds with `0 errors`

- [ ] **Step 4: Build the Windows editor host**

Run:

```bash
rtk dotnet build helengine.ui/helengine.editor.app/helengine.editor.app.csproj -p:EnableWindowsTargeting=true -v minimal
```

Expected:
- build succeeds with `0 errors`

- [ ] **Step 5: Commit the verification-safe final state**

```bash
git status --short
git add engine/helengine.core/managers/input/Mouse.cs engine/helengine.core/managers/input/InputManager.cs engine/helengine.core.windows/input/MouseWindows.cs engine/helengine.editor.tests/testing/TestMouse.cs engine/helengine.editor.tests/testing/TestInputManager.cs engine/helengine.editor.tests/InputManagerTests.cs engine/helengine.editor/components/EditorViewportCameraController.cs engine/helengine.editor.tests/EditorViewportCameraControllerTests.cs engine/helengine.editor/managers/gizmo/TransformTranslationGizmoDragComponent.cs engine/helengine.editor/managers/gizmo/TransformRotationGizmoDragComponent.cs engine/helengine.editor/managers/gizmo/TransformScaleGizmoDragComponent.cs engine/helengine.editor.tests/managers/gizmo/TransformGizmoPointerWrapTests.cs
git commit -m "Wrap pointer during camera and gizmo navigation"
```

