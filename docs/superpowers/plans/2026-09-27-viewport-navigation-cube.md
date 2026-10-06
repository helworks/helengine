# Viewport Navigation Cube Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Execution is proposed inline, without subagents or an independent review unless the user requests them.

**Goal:** Add a per-viewport navigation cube with standard views, orbit dragging, and correct perspective/orthographic interaction.

**Architecture:** An optional core projection contract extends editor cameras without changing existing `ICamera` implementations. Desktop rendering, picking, and editor geometry consume common projection math. A viewport-owned controller and overlay share cube geometry and integrate with existing camera navigation, input capture, and workspace persistence.

**Tech Stack:** C#, .NET 9, helengine core/editor, DirectX11, Vulkan, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-27-viewport-navigation-cube-design.md` (approved in conversation).

## Global Constraints

- Six faces, twelve edges, and eight corners; face clicks select orthographic, diagonal clicks and dragging preserve projection.
- Face labels: `Front`, `Back`, `Left`, `Right`, `Top`, `Bottom`. Projection control: `Persp / Ortho`.
- Standard-view transitions take approximately 200 ms and use the shortest quaternion arc.
- Preserve apparent scale at the orbit pivot when switching projection.
- No changes to scene serialization, authored camera payloads, or console platform camera contracts.
- No global mutable cube/camera state and no new generated scene assets.
- New updating components explicitly declare `[RunInEditor]`; the attribute is not inherited.
- Tests assert runtime results and geometry, not production source text.
- Screenshots require explicit user consent. No independent review or agent orchestration is authorized.
- Preserve existing dirty changes, including the preceding editor execution-policy implementation. Record the baseline before editing; stage only task-owned changes when committing.
- One class per file, substantive XML documentation, PascalCase fields, no tuples or local helper functions.
- Build outputs and logs go under `C:/dev/helworks/builds/viewport-navigation-cube`.

## Review Focus

1. Zero-height/minimized viewports must produce no invalid matrix or pointer ray; restore correctly after resizing (tasks 1, 2, 5).
2. Top/bottom views and repeated interrupted transitions must preserve a deterministic up direction (task 3).
3. Pointer release outside the cube, focus loss, or panel disposal must release input ownership (tasks 4, 5).
4. Picking and gizmo cameras must copy projection state as well as FOV and clip planes (tasks 1, 2, 5).
5. Old workspace state and two simultaneous viewports must not share pivot, span, or transition state (task 5).

## File and interface conventions

New core files live beside `CameraProjectionUtils`, `ICamera`, and the camera model types. New editor navigation services live in `engine/helengine.editor/managers/viewport/`; create that directory. Each named type below has its own `.cs` file.

Use these numeric limits consistently: minimum orthographic span and orbit distance `0.001`, transition duration `0.2` seconds, drag threshold `4` logical UI pixels. Viewport pixel dimensions must be positive before producing projection-dependent geometry. Use double math and convert only at engine float boundaries.

For each test cycle, use this command with the task's filter:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --artifacts-path C:/dev/helworks/builds/viewport-navigation-cube/artifacts --filter '<FILTER>' --results-directory C:/dev/helworks/builds/viewport-navigation-cube/results --logger 'trx;LogFileName=<TASK>-<red-or-green>.trx'
```

Run red before implementation, confirm the intended missing behavior, then run green. Existing unrelated failures must be reported and distinguished from regressions. Commit only explicitly reviewed task paths; never `git add .` in this workspace.

### Task 1: Optional orthographic projection and desktop rendering

**Files:**
- Create `engine/helengine.core/model/CameraProjectionMode.cs`.
- Create `engine/helengine.core/model/interfaces/ICameraProjectionSettings.cs`.
- Modify `engine/helengine.core/utils/CameraProjectionUtils.cs`.
- Create `engine/helengine.editor/components/EditorViewportCameraComponent.cs`.
- Modify `engine/helengine.directx11/DirectX11Renderer3D.cs` and `engine/helengine.vulkan/VulkanRenderer3D.cs`.
- Extend `engine/helengine.editor.tests/rendering/CameraProjectionUtilsTests.cs`.

**Interfaces:** `CameraProjectionMode` has `Perspective = 0` and `Orthographic = 1`. `ICameraProjectionSettings` exposes get/set `ProjectionMode` and float `OrthographicVerticalSpan`. `EditorViewportCameraComponent : CameraComponent, ICameraProjectionSettings` explicitly opts into editor execution. Add `CameraProjectionUtils.CreateProjection(ICamera camera, float aspectRatio)` and `GetWorldUnitsPerPixel(ICamera camera, double distance, double viewportHeight)`; keep both existing perspective overloads.

- [ ] Add tests `CreateProjection_OrdinaryCamera_MatchesExistingPerspective`, `CreateProjection_Orthographic_MapsVisibleExtentsAndClipPlanes`, and `WorldUnitsPerPixel_Orthographic_IsIndependentOfDistance`. Assert matrix equivalence for ordinary cameras, visible extent Â±span/2, correct near/far depth, and a span of 20 over 1000 pixels yielding 0.02 world units/pixel.
- [ ] Add invalid-dimension/span cases and actual-FOV cases; run filter `FullyQualifiedName~CameraProjectionUtilsTests` and confirm red.
- [ ] Implement the optional contract and helpers using existing depth conventions. New editor camera defaults to perspective; opt-in properties are editor-only and ignored by scene persistence. Reject non-finite/invalid direct API input; workspace migration handles absent legacy values explicitly.
- [ ] Route desktop scene and ID-picking projection creation through `CreateProjection`. Leave shadow projection code unchanged. Run tests green and build both desktop backend projects using the same artifacts root.
- [ ] Review and commit only this task's files: `feat(editor): support optional orthographic viewport projection`.

### Task 2: Projection-aware rays, camera motion, framing, and gizmos

**Files:**
- Modify `engine/helengine.editor/managers/gizmo/EditorViewportPointerRayBuilder.cs`.
- Modify `engine/helengine.editor/components/EditorViewportCameraController.cs` and `EditorViewportPicker.cs`.
- Modify `engine/helengine.editor/managers/scene/EditorViewportSelectionFramingService.cs`.
- Modify translation/rotation/scale drag and follow components in `engine/helengine.editor/managers/gizmo/`, plus `TransformScaleGizmoScaleResolver.cs` where its scale formula is selected.
- Modify `engine/helengine.editor/components/ui/EditorViewportCameraAngleOverlayComponent.cs`.
- Extend matching tests: `EditorViewportPointerRayBuilderTests`, `EditorViewportCameraControllerTests`, `EditorViewportSelectionFramingServiceTests`, gizmo drag/follow/scale tests, and `EditorViewportCameraAngleOverlayComponentTests`.

**Interfaces:** Add `TryBuildCameraRay(CameraComponent camera, int2 pointer, out float3 origin, out float3 direction)` using existing viewport coordinate conventions. Keep the existing explicitly perspective-named API compatible for its callers. Add `EditorViewportCameraController.SetViewPose(float3 pivot, float4 orientation, double distance)` to atomically synchronize pose, pivot, distance, and cached yaw/pitch. Add `SetProjectionMode(CameraProjectionMode mode)` using task 1's optional settings. Expose the existing controller's navigation activity through a read-only boolean for transition cancellation.

- [x] Add tests for parallel orthographic rays with different origins, perspective rays with a common origin, off-center viewports, zero viewport dimensions, and non-default FOV. Run the pointer-ray filter red.
- [x] Add tests for projection round trips preserving pivot/orientation and projected scale; orthographic pan, wheel zoom, and focus-selection with portrait/wide viewports. Include tiny and off-origin selections. Run controller/framing filters red.
- [x] Implement span/distance conversion from the spec, orthographic zoom and pan, and projection-aware framing. Reject navigation on zero-size viewports. Use camera FOV instead of fixed 45-degree assumptions.
- [x] Route all transform tools through the projection-aware ray builder and use task 1's world-units-per-pixel calculation for gizmo and label scaling. Preserve frozen drag-time scale behavior.
- [x] Copy optional projection settings in picker synchronization. Add behavioral tests that dragging in orthographic view maps pixels to expected world displacement and that gizmo/label size stays constant on screen. Run `FullyQualifiedName~ViewportPointerRay|FullyQualifiedName~ViewportCameraController|FullyQualifiedName~SelectionFraming|FullyQualifiedName~Gizmo|FullyQualifiedName~CameraAngleOverlay` green.
- [x] Review and preserve the isolated task diff for parent integration. Do not commit: the worktree inherits a preexisting dirty baseline whose overlapping modifications are excluded by the baseline-to-final tree diff.

### Task 3: Navigation targets and interruptible camera transitions

**Files:**
- Create `EditorViewportNavigationTarget.cs`, `EditorViewportNavigationPose.cs`, and `EditorViewportNavigationController.cs` in `engine/helengine.editor/managers/viewport/`.
- Create corresponding controller tests in `engine/helengine.editor.tests/managers/viewport/`.

**Interfaces:** `EditorViewportNavigationTarget` stores integer `X`, `Y`, `Z`, each in [-1,1], excluding all-zero. One nonzero axis is a face, two an edge, three a corner. `EditorViewportNavigationPose` holds `float3 Pivot`, `float4 Orientation`, and `double Distance`. Controller constructor takes `EditorViewportCameraController`; methods: `SelectTarget(EditorViewportNavigationTarget target)`, `ToggleProjection()`, `Orbit(float2 deltaRadians)`, `Advance(double elapsedSeconds)`, `CancelTransition()`; property `bool IsTransitioning`.

- [x] Add parameterized tests for all 26 targets, face-only orthographic switching, and unchanged projection for diagonals and orbit. Verify target direction against the engine's established front/up basis.
- [x] Add transition tests: exactly completed at 0.2 seconds, shortest-arc interpolation, stable top/bottom orientation, a second click starts from the displayed pose, and external navigation cancels a transition. Run `FullyQualifiedName~EditorViewportNavigationControllerTests` red.
- [x] Implement immutable target/pose data and transition state owned by the controller instance. Capture the current orbit pivot for each action and apply poses through task 2's atomic method. Derive deterministic top/bottom up vectors rather than crossing parallel axes.
- [x] Run controller tests green, including two controllers acting independently. Review and preserve the isolated task diff for parent integration; no commit because of the inherited dirty baseline.

### Task 4: Cube geometry, target hit testing, and overlay interaction

**Files:**
- Create `EditorViewportNavigationCubeGeometry.cs` and `EditorViewportNavigationCubeHit.cs` in `engine/helengine.editor/managers/viewport/`.
- Create `engine/helengine.editor/components/ui/EditorViewportNavigationCube.cs` and `EditorViewportNavigationCubeUpdateComponent.cs`.
- Create geometry/hit-test and input tests in `engine/helengine.editor.tests/managers/viewport/` and `components/ui/`.

**Interfaces:** Geometry exposes `TryHit(float2 localPointer, float4 cameraOrientation, float size, out EditorViewportNavigationCubeHit hit)` and supplies the same cube vertices to rendering. Hit data includes `EditorViewportNavigationTarget Target` and visible-face depth for deterministic resolution. Overlay exposes `Resize(int2 contentSize, float uiScale)`, `CancelInteraction()`, and `Dispose()`. It consumes the task 3 controller and session-owned renderer/input/font resources.

- [x] Add tests that face centers, visible edge regions, and corner regions resolve all reachable targets while occluded faces cannot win. Test rotated cubes and scaled UI; run the geometry filter red.
- [x] Define a logical 96-pixel cube area with an 8-pixel content margin; derive device bounds from UI scale. Reserve a 24-pixel projection-control row beneath it. Clip to the viewport and suppress the control when content cannot accommodate usable bounds.
- [x] Implement isolated overlay rendering using editor resource services and a dedicated render target. Cube geometry, face labels, and hover highlights must not participate in scene picking/depth. Keep rendering orientation and target hit geometry identical.
- [x] Add input tests: movement below four logical pixels clicks; movement at/above four pixels begins orbit; release outside the bounds ends capture; focus/capture loss cancels; no scene selection or gizmo movement occurs during cube interaction. Run input tests red, implement through existing session input-ownership APIs, then green.
- [x] Mark the updater `[RunInEditor]`. Release render target, materials, subscriptions, and capture on disposal. Add an allocation/disposal test and commit the task: `feat(editor): render interactive viewport navigation cube`.

### Task 5: Viewport ownership, projection synchronization, and workspace state

**Files:**
- Modify `engine/helengine.editor/managers/workspace/ViewportWorkspacePanelController.cs` and `ViewportWorkspacePanelStateDocument.cs`.
- Modify `engine/helengine.editor/components/ui/EditorViewport.cs` at the existing viewport overlay/input boundary.
- Create `engine/helengine.editor.tests/managers/workspace/ViewportNavigationCubeIntegrationTests.cs` and `ViewportNavigationStateTests.cs`.

**Interfaces:** Workspace document adds `CameraProjectionMode ProjectionMode`, float `OrthographicVerticalSpan`, boolean `HasOrbitPivot`, and float `OrbitPivotX/Y/Z`. `HasOrbitPivot` distinguishes a valid zero pivot from a legacy document. The viewport controller owns one navigation controller and overlay; scene, gizmo, and picker cameras use task 1's editor camera type.

- [x] Add tests loading an old document without new fields, restoring a new orthographic document, and capturing current pose during an interrupted transition. Old documents default to perspective and resolve a pivot using existing camera-controller initialization; missing span is derived from that pivot/FOV.
- [x] Add tests for two viewports with different projection/pivot state, resize/minimize/restore, pointer capture during panel closure, and identical scene/gizmo/picking projection matrices. Run integration/state filters red.
- [x] Create and dispose navigation resources with the viewport. Synchronize projection changes to gizmo and picker cameras, not only the scene camera. Put the cube after existing content overlays without overlapping title-bar controls, and route capture before scene/gizmo interactions.
- [x] Implement workspace capture/restore with finite-value validation and explicit legacy defaults. Preserve the existing serialized scene and authored camera formats. Run task tests green and relevant viewport/keyboard/input-capture regressions.
- [ ] Review and commit: `feat(editor): integrate and persist viewport navigation cube`.

### Task 6: Final regression evidence and user documentation

**Files:**
- Create `docs/editor-viewport-navigation.md`.
- Record results under the workspace build directory; update this plan's checkboxes with verified results.

- [x] Run the combined changed-area filters: `FullyQualifiedName~CameraProjection|FullyQualifiedName~Viewport|FullyQualifiedName~Gizmo|FullyQualifiedName~SelectionFraming|FullyQualifiedName~Keyboard|FullyQualifiedName~InputCapture|FullyQualifiedName~EditorUpdateComponentExecutionPolicy`. Require zero failures; classify any environment skips explicitly.
- [x] Build DirectX11 and Vulkan desktop projects and exercise backend projection tests where supported. Do not claim GPU validation from compilation alone. The shared projection and camera-stack matrix behavior was exercised; no GPU-backed render harness was available.
- [x] Verify a real viewport manually if an interactive editor is available: all faces, diagonal clicks, drag, Persp/Ortho framing, selection, all transform tools, resize, and two viewport panels. The current environment exposes no interactive desktop surface, so manual UI verification was not available; no screenshots were taken.
- [x] Document controls, non-inherited `[RunInEditor]` requirements for the new editor camera/updater, workspace-only persistence, and actual validation coverage. Run `git diff --check`; review changed paths against the recorded baseline.
- [x] Preserve the task-owned documentation and verified feature-only baseline delta, then report implementation status and test evidence. No commit is created because the worktree carries overlapping inherited dirty changes. Do not start an independent review without asking the user.

## Execution handoff

Recommended method: the current assistant implements the tasks sequentially in this session. Projection, navigation, and viewport ownership depend closely on each other's interfaces, so a sequential workflow avoids parallel editing conflicts and extra agent compute. Await the user's review of this plan and selection of execution method before production edits.
