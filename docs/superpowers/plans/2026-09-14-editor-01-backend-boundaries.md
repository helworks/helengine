# Shared Editor Backend Boundaries Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task, subject to the user's orchestration approval and existing modernization worker constraints. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove concrete GPU and Windows graphics dependencies from the shared editor without changing current DirectX picking behavior.

**Architecture:** Keep selection policy and editor state in helengine.editor. Put native rendering/readback and Windows CLI composition in helengine.editor.windows; the Windows application injects capabilities. Reuse the existing renderer and shader contracts rather than introducing a second renderer hierarchy.

**Tech Stack:** C#/.NET 9, xUnit, existing Helengine renderer, authoring, workspace, and platform-builder contracts.

**Spec:** Accepted September 14 refactor scope; [editor modularization design](../specs/2026-08-26-editor-modularization-design.md), sections Editor Host and Lifetimes and Global Constraints.

## Execution constraints and baseline

- Work only in `C:\dev\helworks\helengine` or a deliberately created worktree of that repository. Never use `F:\dev\helengine`, and never cherry-pick its September refactor commits.
- Planning baseline: `main` at `118a0057`, plus the existing dirty working tree inspected on September 14. Before execution record `git status --short` and review the current diff. Do not stash, reset, stage, or overwrite unrelated edits.
- This document authorizes planning only. Obtain implementation authorization before executing it. Follow AGENTS.md and the existing modernization execution requirements; do not silently substitute a different worker model or launch an independent review.
- One class per file; substantive XML documentation on all members; PascalCase fields; no tuples, local functions, nullable annotations, or partial-class extraction.
- Preserve behavior and current persisted formats. No compatibility readers, generated-code rewriting, global service lookup, general DI framework, or silent fallback.
- Use explicit project/scene/operation ownership. A service must not accept the entire EditorSession merely to reach its dependencies.
- Put new test fixtures, logs, and artifacts under `C:\dev\helworks\builds\helengine\refactors\<task>`; do not introduce new uses of Path.GetTempPath. Existing fixtures that write there must be redirected before running those cases.
- Characterization tests should pass before extraction. A NEW boundary contract test should fail before implementation, then pass. Do not demand an artificial failing characterization test.
- Commit each verified task with explicit paths only. Do not merge, publish, update project engine pins, or deploy as part of these plans.

## Validation commands

From the correct repository, run the task's filter against:
```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "<task filter>" --logger "console;verbosity=minimal"
dotnet build helengine.ui/helengine.editor.app/helengine.editor.app.csproj --no-restore -v quiet
git diff --check
```
Replace the filter with the exact expression provided in each task. Record test counts and failures; a zero-test run is not a pass. Preserve the repository's current output conventions for existing projects. For new projects use explicit workspace build paths. Capture baseline failures and distinguish them from regressions; do not broaden this work to fix unrelated failures.

## Scope and decisions

Current shared project directly references helengine.directx11, helengine.vulkan and System.Drawing.Common. EditorViewportPicker owns SharpDX staging textures; ViewportWorkspacePanelController selects behavior with a DirectX type check. CLI runners construct DirectX renderers. Keep these implementations, relocate their ownership.

Use existing helengine.editor.windows for adapters: it may depend on helengine.editor and concrete backends; helengine.editor must not depend back on it. Do not move the renderer itself or delete any platform backend. Unsupported GPU picking remains an explicitly unavailable capability; this plan does not implement Vulkan GPU picking.

NintendoDsDebugFontFactory lives in the Windows application, not the neutral editor. Its lack of callers is a separate cleanup finding, not the justification for this refactor. Do not remove it in this plan.

## Proposed capability

Create each type in its own file under engine/helengine.editor/rendering. The shared picker retains entity-ID maps and translates results into editor selection; the backend receives render data, not EditorSession.
```csharp
public interface IEditorPickingBackend : IDisposable {
    void Render(CameraComponent camera, IReadOnlyDictionary<IDrawable3D, byte4> colors);
    bool TryReadPixel(int2 pixel, out byte4 color);
}
public interface IEditorPickingBackendFactory {
    bool IsSupported { get; }
    IEditorPickingBackend Create(CameraComponent camera);
}
```
TryReadPixel false means readback is not ready, never a swallowed device error. Preserve current frame timing, clear color, ID encoding, viewport transforms, and gizmo layer masks. Backend owns staging/native target resources; caller owns camera/entity lifetime. Document whether the camera's target is borrowed and clear it before releasing that target.

### Task 1: Extract DirectX picking resources

**Files**
- Modify: `engine/helengine.editor/components/EditorViewportPicker.cs`
- Create: `engine/helengine.editor/rendering/IEditorPickingBackend.cs`, `IEditorPickingBackendFactory.cs`
- Create: `engine/helengine.editor.windows/rendering/DirectX11EditorPickingBackend.cs`, `DirectX11EditorPickingBackendFactory.cs`
- Modify: `engine/helengine.editor.tests/EditorViewportPickerTests.cs`, `EditorViewportPicker2DSelectionTests.cs`
- Create: `engine/helengine.editor.tests/rendering/EditorPickingBackendContractTests.cs`

**Interface and ownership contract:** Use the interfaces above. Copy native pass/readback operations out of EditorViewportPicker; keep requests, ID lookup, selection priority and gizmo collection in the shared class.

**Regression cases:** Selection hit/miss, clear-color miss, 2D/3D priority, out-of-bounds coordinates, pending readback, resize while pending, repeated disposal, and no callback after disposal.

- [ ] Add the specified characterization cases using existing test fixtures, with fixture output redirected to the workspace build directory. Run the filter below and record the baseline.
- [ ] Add boundary assertions for the new contract and verify they fail for the intended missing behavior before implementing it.
- [ ] Move ReadbackTexture, dimensions/format, staging allocation, target casts and pixel mapping into DirectX11EditorPickingBackend. Keep the existing numeric conversion/readback semantics unchanged.
- [ ] Replace PickerRenderer with IEditorPickingBackend; constructor rejects null. Do not dispose caller-owned camera or renderer.
- [ ] Use a fake backend returning a chosen byte4 to test shared selection independently of SharpDX. Keep real-device tests separate and report when no device is available.
- [ ] Run `FullyQualifiedName~EditorViewportPicker|FullyQualifiedName~EditorPickingBackendContractTests` through the test command above. Run the editor-host build after changing composition or project references.
- [ ] Inspect the diff for duplicate old implementations, lifetime changes, and accidental fixture/output files. Commit only the listed task paths with message `refactor: isolate editor picking backend`.

### Task 2: Inject host capabilities and shader target selection

**Files**
- Modify: `engine/helengine.editor/managers/workspace/ViewportWorkspacePanelController.cs`
- Modify: `engine/helengine.editor/EditorSessionRendererResources.cs`
- Modify: `engine/helengine.editor/managers/gizmo/TransformGizmoRotationPreviewMaterialFactory.cs`, `TransformGizmoPlaneMaterialFactory.cs`
- Modify: `helengine.ui/helengine.editor.app/MainForm.cs`
- Create: `engine/helengine.editor/rendering/EditorRenderCapabilities.cs`
- Create: `engine/helengine.editor.tests/rendering/EditorRenderCapabilitiesTests.cs`

**Interface and ownership contract:** EditorRenderCapabilities has constructor (ShaderCompileTarget shaderTarget, IEditorPickingBackendFactory pickingFactory) and read-only ShaderTarget/PickingFactory properties. Host owns the factory; each viewport owns its created backend.

**Regression cases:** Two viewports get independent backends; closing one does not invalidate another; unavailable picker does not allocate a native target; shader target comes from configuration rather than runtime type.

- [ ] Add the specified characterization cases using existing test fixtures, with fixture output redirected to the workspace build directory. Run the filter below and record the baseline.
- [ ] Add boundary assertions for the new contract and verify they fail for the intended missing behavior before implementing it.
- [ ] Add required capabilities to the existing renderer resource constructor and migrate every constructor call in the same task.
- [ ] Replace the controller's render3D is DirectX11Renderer3D branch with PickingFactory.IsSupported; create backend only for supported configurations.
- [ ] Replace concrete renderer tests in gizmo factories with the supplied ShaderTarget. Search all shared source for remaining backend casts; enumerate and migrate each before removing references.
- [ ] Run `FullyQualifiedName~EditorRenderCapabilitiesTests|FullyQualifiedName~EditorSessionWorkspaceTests|FullyQualifiedName~EditorViewportPicker` through the test command above. Run the editor-host build after changing composition or project references.
- [ ] Inspect the diff for duplicate old implementations, lifetime changes, and accidental fixture/output files. Commit only the listed task paths with message `refactor: inject editor rendering capabilities`.

### Task 3: Move CLI composition and enforce project boundary

**Files**
- Modify: `engine/helengine.editor/EditorCliBuildRunner.cs`, `EditorCliCommandRunner.cs`
- Create: `engine/helengine.editor.windows/hosting/EditorWindowsCliComposition.cs`
- Modify: `engine/helengine.editor/helengine.editor.csproj`, `engine/helengine.editor.windows/helengine.editor.windows.csproj`
- Modify: `helengine.ui/helengine.editor.app/helengine.editor.app.csproj`
- Create: `engine/helengine.editor.tests/rendering/SharedEditorDependencyTests.cs`

**Interface and ownership contract:** Shared runners consume existing renderer/importer/shader registries through constructors. EditorWindowsCliComposition creates and owns the concrete graph. Preserve public CLI arguments and existing command results; fail clearly when a command requires an unavailable capability.

**Regression cases:** Headless authoring command does not construct a GPU; Windows build still registers its shader backend; startup failure releases constructed resources; shared assembly references exclude helengine.directx11, helengine.vulkan, SharpDX and System.Drawing.Common.

- [ ] Add the specified characterization cases using existing test fixtures, with fixture output redirected to the workspace build directory. Run the filter below and record the baseline.
- [ ] Add boundary assertions for the new contract and verify they fail for the intended missing behavior before implementing it.
- [ ] Move new DirectX11Renderer3D and concrete shader backend registration from shared CLI runners into Windows composition; trace runner callers before changing constructors.
- [ ] Inventory all System.Drawing usages in shared source. Move graphics implementations to Windows adapters with the same current typed inputs/outputs; retain neutral asset types.
- [ ] Remove concrete ProjectReference/PackageReference entries only after shared source compiles without them. Add a dependency assertion using typeof(EditorSession).Assembly.GetReferencedAssemblies().
- [ ] Run the Windows CLI's existing smoke fixture plus desktop selection/gizmo smoke on a GPU. Do not claim platform parity from fake-backend tests.
- [ ] Run `FullyQualifiedName~SharedEditorDependencyTests|FullyQualifiedName~EditorCli` through the test command above. Run the editor-host build after changing composition or project references.
- [ ] Inspect the diff for duplicate old implementations, lifetime changes, and accidental fixture/output files. Commit only the listed task paths with message `refactor: enforce shared editor backend boundary`.

## Completion
Shared editor builds without concrete backend/Windows graphics references; DirectX selection and CLI behavior remain intact. No renderer implementation or platform support is removed. Land before changing session constructor ownership in plan 02.
