# Normalize Camera Viewport In Core

## Goal

Make `CameraComponent.Viewport` represent normalized target bounds in core instead of pixel dimensions.

Fullscreen cameras must use `0,0,1,1`.

This removes the current ambiguity where some authored scenes store `1280x720` in camera viewports even though the actual runtime target can be `640x480`, `1280x720`, render-target sized, or anything else.

## Problem

The current runtime mixes two different concepts:

- camera viewport bounds
- canvas or layout resolution

That leak is now visible on PS2:

- the startup menu camera is loaded with a viewport of `0,0,1280,720`
- the PS2 runtime target is not `1280x720`
- renderers and layout systems interpret viewport data inconsistently

This causes incorrect fullscreen behavior and makes menu scaling depend on authored desktop dimensions instead of the actual runtime target.

## Decision

`CameraComponent.Viewport` is a normalized rectangle in target space.

Rules:

- `X` and `Y` are normalized target offsets
- `Z` and `W` are normalized target size
- `0,0,1,1` means fullscreen over the active target
- renderers convert normalized viewport values into pixel rectangles using the active target size
- canvas sizing and menu layout continue to use dedicated canvas/layout data, not camera viewport pixels

## Non-Goals

- This does not redefine scene canvas profiles
- This does not remove explicit layout sizing from menu or UI systems
- This does not add dual viewport modes
- This does not keep pixel-based camera viewports as a compatibility fallback

## Architecture

### Core Contract

`CameraComponent.Viewport` remains a `float4`, but its meaning changes to normalized bounds.

Core code must treat camera viewport values as percentages of the active render target, not physical pixels.

### Renderer Responsibility

Each renderer resolves the normalized viewport against the active target size:

- main backbuffer or window size for on-screen rendering
- render-target size for offscreen rendering

The resolved pixel viewport is then used for rasterization, projection, and screen-space conversions.

### Authoring and Packaging Responsibility

Authored scenes and generated scenes must stop serializing runtime camera viewports as `1280x720` pixel rectangles when they mean fullscreen.

Fullscreen runtime cameras should serialize `0,0,1,1`.

Any build-time scene transform that rewrites camera payloads must preserve the normalized contract and must not reintroduce pixel-based fullscreen bounds.

### Menu/Layout Responsibility

Menu layout and other 2D UI systems that need a logical canvas continue using their existing layout or canvas data.

They must not infer layout resolution from camera viewport pixel dimensions.

## Expected Code Changes

### Core

- update `CameraComponent` documentation to define normalized viewport semantics
- update any runtime math in core that assumes viewport width and height are already pixels

### Renderers

- update DirectX11, Vulkan, and PS2 renderers to resolve normalized viewport bounds to physical pixels before drawing
- ensure offscreen render targets use their own dimensions when resolving fullscreen cameras

### Scene Generation and Packaging

- normalize generated fullscreen camera records from `1280x720` to `0,0,1,1`
- keep non-fullscreen authored sub-rectangles expressible as normalized values

### Tests

- add a core-facing regression that locks the normalized viewport contract
- add renderer-facing tests for fullscreen viewport resolution from normalized values
- update existing scene-generator tests that currently expect `1280x720`
- rebuild the PS2 ISO and verify the menu camera resolves to the PS2 target correctly

## Migration Rules

Fullscreen cameras:

- old authored value: `0,0,1280,720`
- new authored/runtime value: `0,0,1,1`

Sub-rectangles:

- must be stored as normalized fractions of the intended target

No compatibility mode will be kept in core.

Any code still writing fullscreen pixel dimensions after this change is a bug.

## Risks

### Renderer Regression

If a renderer continues treating normalized values as pixels, it will render to a tiny corner or nothing at all.

Mitigation:

- update all active renderers in the same change
- verify with targeted renderer tests and the PS2 runtime diagnostic build

### Scene/Editor Test Churn

Some existing tests currently assert pixel viewport values such as `1280x720`.

Mitigation:

- update those tests to assert normalized fullscreen bounds where appropriate
- only retain pixel expectations in code that is explicitly about resolved render-target dimensions, not camera storage

### Hidden Canvas Coupling

Some UI code may have been implicitly relying on viewport pixel dimensions as a canvas size.

Mitigation:

- keep diagnostics focused on menu/layout behavior
- move any discovered dependency back onto explicit canvas/layout data

## Verification

The change is complete when all of the following are true:

- fullscreen runtime cameras serialize as `0,0,1,1`
- all active renderers resolve fullscreen cameras to the actual target size
- PS2 startup menu no longer uses `1280x720` as its runtime camera viewport
- the PS2 menu renders against the PS2 target instead of a desktop-authored viewport
- directional and menu scenes still load without runtime scene-manager regressions
