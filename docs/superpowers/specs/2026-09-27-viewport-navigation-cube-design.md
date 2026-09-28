# Viewport navigation cube

Status: proposed for user review.

## Intent and scope

Add a navigation cube, inspired by the familiar DCC viewport control, to each
editor scene viewport. It selects standard viewing directions, rotates the view,
and switches between perspective and orthographic projection. It manipulates the
editor's viewing camera, not authored cameras, entities, or scene assets.

The user approved this interaction scope in conversation. This document specifies
the implementation boundaries and acceptance criteria; implementation awaits
review of this document.

## Interaction

- Place the cube inside the upper-right corner of the viewport's content area,
  below its title bar. Respect UI scaling and clip to that viewport.
- Show labeled visible faces: Front, Back, Left, Right, Top, Bottom. Use the
  engine's established world-axis convention and the existing front-facing camera
  basis; do not introduce a second coordinate convention.
- Clicking one of the six faces smoothly aligns the camera to that direction and
  selects orthographic projection. Top and bottom use deterministic camera-up
  vectors so the view never flips unpredictably near a pole.
- Clicking an edge or corner selects the corresponding diagonal direction and
  keeps the current projection. Support six faces, twelve edges, and eight corners.
- Dragging the cube orbits the camera about the controller's current pivot and
  keeps the current projection. Apply the existing selection-aware orbit target.
- Distinguish clicks from drags with a small screen-space movement threshold.
  Capture the pointer through release, including outside the cube. Cancel safely
  on focus loss, viewport closure, or capture loss.
- A visible `Persp / Ortho` control below the cube switches projection without
  changing orientation or the apparent scale at the orbit pivot.
- Highlight the hovered target. Cube orientation tracks all camera navigation,
  including navigation performed outside the cube.
- Standard-view transitions take approximately 200 ms and interpolate orientation
  along the shortest quaternion arc. New camera input cancels the transition and
  starts from the current displayed pose.
- Input consumed by the cube must not select scene objects, drag gizmos, pan the
  scene, or start the existing camera-look gesture. No scene undo entry is created.

## Current implementation constraints

`EditorViewportCameraController` already owns the orbit target and navigation.
`ViewportWorkspacePanelController` owns viewport-local integration and persisted
camera state. Existing editor overlay components provide the rendering and
lifetime conventions for the new control.

`ICamera` and `CameraComponent` currently expose perspective FOV and clip planes.
DirectX11 and Vulkan build perspective matrices through `CameraProjectionUtils`.
The pointer-ray builder, selection framing, and several gizmo/label calculations
also assume perspective, sometimes using a fixed 45-degree FOV. Changing only the
image projection would make scene interaction inaccurate.

## Projection contract

Introduce a small optional camera-projection interface in core with projection
mode and orthographic vertical span. The editor viewport camera implements it;
ordinary cameras retain perspective behavior without implementing it. Keep the
existing `ICamera` contract and perspective-only helper available so platform
builders and authored camera serialization do not require a coordinated migration.

Add a projection-aware helper alongside the perspective helper. Desktop renderers
use it for editor scene rendering and the object-picking pass. A camera without
the optional interface produces the same perspective projection as before.
Shadow-map cameras retain their existing projection rules.

Use an editor-owned camera component with an explicit `[RunInEditor]` opt-in for
viewport and picking cameras. Copy the projection state, clip planes, viewport,
and FOV together when synchronizing the picking camera.

Orthographic span is the full visible vertical world-space extent. Given pivot
distance `d` and vertical FOV `f`, switching to orthographic uses
`span = 2 * d * tan(f / 2)`. Switching back derives the perspective distance from
the current span and retains the pivot and orientation. Clamp degenerate spans
and distances to documented positive limits. Match existing depth conventions and
clip-plane behavior in both desktop backends.

Perspective zoom keeps its existing behavior. Orthographic wheel zoom changes
the visible span. Pan converts screen displacement into world displacement using
the current projection and viewport dimensions. Focus-selection computes a span
that fits the selection bounds, with padding and aspect-ratio compensation.

## Component responsibilities

- A viewport-owned navigation controller converts cube gestures into camera
  targets, projection changes, and interruptible transitions. It works through
  the existing camera controller so orbit, focus-selection, and navigation share
  one camera state.
- A pure geometry/hit-test helper projects the cube and resolves visible face,
  edge, and corner targets. Invisible back faces cannot steal input.
- An overlay view renders the cube, labels, highlights, and projection button.
  Use an isolated editor overlay mesh/render target and existing resource services;
  scene geometry, scene lighting, picking IDs, and scene depth must not affect it.
  Rendering and hit testing use the same cube orientation and geometry.
- The viewport owner constructs and disposes the control, manages input priority,
  handles resizing, and restores viewport state. New updating components explicitly
  declare `[RunInEditor]`.

Separate classes follow existing MVC conventions and resource ownership. No
global mutable cube/camera state and no new generated scene assets are needed.

## Selection, gizmos, and persistence

Provide projection-aware pointer rays: perspective rays share a camera origin;
orthographic rays have parallel directions and screen-dependent origins. Route
translation, rotation, and scale dragging through this common calculation.

Derive gizmo size and label size from world-units-per-pixel at the current
projection, preserving their screen-space size. Audit perspective assumptions in
camera overlays, viewport selection framing, and existing gizmo follow services.
Use the actual camera FOV wherever perspective math is required.

Persist projection mode, orthographic span, and orbit pivot with the existing
viewport workspace state. Continue storing camera pose using the existing fields.
Old workspace documents restore to perspective and derive a valid pivot from
their existing camera state. Persist only settled/current camera state, never an
in-progress animation. Scene files and authored camera payloads are unchanged.

## Validation and acceptance

Behavioral tests must cover:

1. Six face directions and twenty diagonal directions; stable top/bottom up axes.
2. Click/drag discrimination, visible-target hit testing, hover, input capture,
   interrupted transitions, focus loss, and disposal during a drag.
3. Round-trip projection switching preserves orientation, pivot, and apparent
   scale within numeric tolerance across aspect ratios and FOV values.
4. Perspective and orthographic matrix/ray agreement, including center and corner
   pixels, resized viewports, and non-default clip planes.
5. Object picking and all three transform tools agree with the rendered scene in
   both projections. Gizmo screen size remains stable while zooming.
6. Orthographic pan, zoom, and focus-selection work for point-like, large, and
   off-origin selections.
7. Multiple viewports keep independent navigation/projection state. Workspace
   restoration supports both old and new documents without changing scene data.
8. Ordinary runtime cameras retain their existing perspective projection and the
   editor control remains operational under default component suppression.
9. DirectX11 and Vulkan compile with the shared projection contract; exercise
   backend rendering where an available test harness supports it.

Run the smallest relevant behavioral suites, expanding only for changed paths or
failures. Tests assert runtime results and geometry, not production source text.
Manual visual verification should cover readable faces, hover, animation, input
priority, and viewport resizing. Screenshots require the user's explicit consent
under repository instructions; do not take them automatically.

## Deliberate boundaries

This iteration does not add an authored orthographic-camera asset format, change
console platform camera contracts, or implement a full DCC camera menu. Bookmarks,
custom home views, and additional keyboard shortcuts can be added separately.
