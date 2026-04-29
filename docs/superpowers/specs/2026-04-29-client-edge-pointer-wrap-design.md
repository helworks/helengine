# Client-Edge Pointer Wrap Design

## Goal

Allow uninterrupted editor camera navigation and gizmo dragging by teleporting the mouse cursor to the opposite side of the editor client area when it reaches a client edge during an active interaction.

This behavior must not affect normal editor UI hover, selection, or non-navigation clicks.

## Scope

Pointer wrapping applies only while one of these interactions is active:

- viewport RMB freelook
- viewport MMB pan
- viewport `Alt + MMB` orbit
- active translate gizmo drag
- active rotate gizmo drag
- active scale gizmo drag

Pointer wrapping does not apply to:

- normal editor hover
- asset picking or asset browser navigation
- text input
- scene hierarchy interaction
- generic UI dragging unless explicitly added later

## Behavioral Rules

Wrapping uses the full editor client area, not the viewport rectangle and not the physical monitor edges.

When wrapping is enabled:

- `x <= 0` teleports to the right interior edge
- `x >= clientWidth - 1` teleports to the left interior edge
- `y <= 0` teleports to the bottom interior edge
- `y >= clientHeight - 1` teleports to the top interior edge
- if both axes cross during the same update, both axes wrap

The wrapped position must remain inside the client area by one pixel so the next update does not immediately re-trigger wrapping from the same edge.

## Architectural Boundary

The Windows mouse backend owns the physical cursor teleport because it already converts between screen-space and client-space coordinates and already exposes cursor positioning.

Editor interaction systems own enablement only:

- camera navigation enables wrapping when a qualifying drag starts
- camera navigation disables wrapping when that drag ends
- gizmo drag components enable wrapping when drag begins
- gizmo drag components disable wrapping when drag ends

This keeps viewport and gizmo code responsible for intent while the platform mouse layer remains responsible for native cursor movement.

## Input Continuity

Teleporting the OS cursor cannot be treated as ordinary motion because the next frame would otherwise report a large false delta equal to the wrap distance.

The input stack must therefore treat a wrap as a position-basis reset:

- when the mouse backend teleports the cursor, it marks that a wrap occurred
- the input layer updates its previous-position basis so the next delta continues from the wrapped location instead of the pre-wrap location

This requirement is mandatory for all supported wrapped interactions. A wrap that keeps the drag active but injects a large reverse jump is still considered broken.

## Implementation Shape

### Mouse Backend

Add client-wrap support to the Windows mouse implementation:

- disabled by default
- configurable with the active client bounds
- performs wrap checks when queried for state
- teleports the OS cursor with the existing native cursor-position API
- records whether a wrap happened during the latest update

The core mouse abstraction should expose only the behavior needed by the input layer and editor interaction code. Avoid editor-specific policy in the mouse type itself.

### Input Layer

Extend input capture so a backend-reported wrap resets mouse delta continuity for the current frame boundary.

This should be solved in the input system, not by adding ad hoc correction logic to individual camera or gizmo components.

### Camera Controller

`EditorViewportCameraController` enables wrapping only while:

- RMB freelook is active
- MMB pan is active
- `Alt + MMB` orbit is active

It disables wrapping immediately when the corresponding interaction ends.

### Gizmo Drag Components

Each gizmo drag component enables wrapping during its active drag lifetime and disables it during teardown:

- `TransformTranslationGizmoDragComponent`
- `TransformRotationGizmoDragComponent`
- `TransformScaleGizmoDragComponent`

These components should not implement wrap math themselves.

## Error Handling

If the window handle or client size is not valid, wrapping must remain disabled rather than guessing invalid bounds.

If the cursor cannot be teleported through the native API, input should continue without wrapping rather than corrupting delta state.

## Testing

Add focused tests for:

### Mouse/Input

- wrap disabled does not teleport at client edges
- wrap enabled teleports across each edge
- corner crossing wraps both axes
- the first delta after a wrap does not include the full teleport distance

### Camera

- RMB freelook continues rotating after client-edge wrap
- MMB pan continues moving after client-edge wrap
- `Alt + MMB` orbit continues rotating after client-edge wrap

### Gizmos

- translate drag continues after client-edge wrap
- rotate drag continues after client-edge wrap
- scale drag continues after client-edge wrap

## Non-Goals

- wrapping at physical screen or monitor edges
- wrapping for ordinary editor UI
- generalized pointer-capture changes outside camera navigation and gizmo drags
- platform implementations beyond the current Windows host in this change
