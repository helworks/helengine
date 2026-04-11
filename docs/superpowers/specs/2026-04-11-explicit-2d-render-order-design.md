# Explicit 2D Render Order Design

## Summary

This document replaces the current bucket-style 2D render-order helper with explicit render-order values. The immediate bug is that the `Add` menu can render behind docked panels like `Scene Hierarchy`, but the root cause is broader: editor UI currently relies on a small set of shared helper buckets from `GetRenderOrderForLayer2D(...)`, which creates ties between unrelated UI surfaces and makes final draw order depend on registration order.

The fix is to stop treating 2D order as a small number of coarse layers and instead define explicit 2D render-order constants with clear semantics. The title bar menu strip and all of its children must live in a dedicated overlay band that always renders in front of docked panels and panel content. The same policy should be reusable for context menus, modal dialogs, viewport overlays, and similar editor UI.

## Goals

- Remove the bucket-based `GetRenderOrderForLayer2D(...)` pattern from 2D UI rendering.
- Stop using `RenderOrderLayers2D` as a convenience abstraction for 2D draw ordering.
- Make the title bar menu strip and every child it spawns render in front of all docked panels.
- Make 2D UI ordering deterministic even when two entities are registered in different orders.
- Centralize 2D render-order values so editor UI stops scattering ordering decisions across many files.
- Keep input hit testing aligned with the same visual front-to-back policy.

## Non-Goals

- No change to 3D render ordering in this slice.
- No change to update-order helper APIs in this slice.
- No redesign of docking behavior beyond the 2D order values it uses.
- No attempt to solve arbitrary future z-sorting policies for game UI outside the editor.
- No best-effort compatibility layer that preserves the old 2D helper as a synonym.

## Current Problem

The editor currently assigns many unrelated 2D surfaces through four coarse helper buckets:

- panel backgrounds
- panel text
- title bar text and buttons
- overlay text or input

That causes several problems:

1. Different systems reuse the same bucket even when they need a strict front-to-back guarantee.
2. When two drawables share the same `RenderOrder2D`, final visual order falls back to insertion order in `RenderList2D`.
3. The title bar menus are built independently, so a small inconsistency like `AddMenu` using title-bar orders instead of overlay orders is enough to put it behind a docked panel.
4. Input ordering follows the same render-order values, so ambiguous draw order also creates ambiguous hover and click behavior.

This is not a one-off `AddMenu` bug. It is a structural weakness in the 2D ordering model.

## Proposed Architecture

### 1. Replace Bucket Helpers With Explicit 2D Render Orders

2D UI should no longer request a coarse layer index and let `ObjectManager` map it into `0..255`. Instead, the codebase should define explicit byte values for the 2D ordering bands it actually needs.

Recommended type:

- `RenderOrder2D`

Recommended shape:

- one central static type
- named constants only
- values chosen explicitly in ascending front-to-back policy

Illustrative policy:

- `PanelBackground`
- `PanelSurface`
- `PanelText`
- `FloatingPanelBias`
- `OverlayBackground`
- `OverlayForeground`
- `OverlayInput`
- `ModalBackground`
- `ModalForeground`
- `ModalInput`

The exact numbers can be finalized during implementation, but they should leave headroom between bands so future additions do not require renumbering the whole table.

### 2. Remove `GetRenderOrderForLayer2D(...)` From 2D UI Paths

The editor and shared 2D components should stop asking `ObjectManager` for a derived 2D render order.

That means migrating current usages such as:

- docked panel backgrounds and text
- title bar background, buttons, menu strip, and menus
- context menus
- asset browser menus
- viewport toolbar and camera-angle overlay
- modal dialogs like save/open/unsaved changes
- shared 2D interactable visuals like buttons, combo boxes, and text boxes where they participate in editor UI

After the migration, 2D render order becomes explicit at the call site, but the values come from one central table instead of being hard-coded ad hoc.

### 3. Remove `RenderOrderLayers2D` From Initialization

`RenderOrderLayers2D` currently exists only to support bucket mapping. Once 2D UI ordering becomes explicit, this setting stops carrying meaningful behavior for 2D rendering.

This slice should remove:

- `CoreInitializationOptions.RenderOrderLayers2D`
- `ObjectManager.RenderOrderLayers2D`
- `ObjectManager.GetRenderOrderForLayer2D(...)`
- the internal helper path that maps layer indices into byte values for 2D rendering

This keeps the API honest: if 2D no longer uses buckets, the engine should stop exposing a bucket-count property for it.

### 4. Make Editor Menus A Guaranteed Overlay Band

The title bar menu strip and every child it owns must render in a band that is strictly above docked panels and panel text.

This includes:

- the title bar itself
- `File` and `Add` button visuals
- their transparent input surfaces
- `ContextMenu` background
- context-menu rows
- context-menu labels
- transparent blockers used to absorb hover or click in menu padding and gaps

The policy must guarantee that all of these render above dockables, regardless of creation order.

### 5. Keep Input Ordering Coupled To Explicit Visual Ordering

Input currently resolves overlapping interactables using the highest drawable `RenderOrder2D`. That rule can stay, but it must now rely on explicit orders rather than derived buckets.

The important requirement is:

- if something is meant to visually cover another element, it must also own a strictly higher explicit render order for hit testing

That means menu blockers, menu rows, title bar input surfaces, and modal blockers all need to participate in the same explicit ordering table.

### 6. Preserve Floating Dockable Bias Without Bucket Math

`DockableEntity` currently boosts undocked panels by adding the top bucket value to the baseline order. That behavior should become explicit too.

Recommended approach:

- docked panel visuals keep their baseline explicit panel orders
- floating panel visuals add a dedicated explicit bias constant
- the bias is defined in the same central 2D render-order policy instead of being derived from bucket count

This keeps floating windows above docked windows without reintroducing helper buckets.

### 7. Prefer One Shared Policy For Editor Overlays And Dialogs

The current code computes “top layer” values in several places by referencing the last bucket index. That should be replaced with explicit modal and overlay constants.

Expected result:

- title bar menus use overlay orders
- asset browser context menus use overlay orders
- viewport overlays use overlay orders
- open/save dialogs use modal orders
- unsaved-changes guard uses modal orders

This gives the editor a readable visual stack instead of many files independently guessing what “top” means.

## Data Flow

### Title Bar Menu

1. User clicks `File` or `Add`.
2. `EditorTitleBar` shows a `ContextMenu`.
3. The menu background, rows, labels, and blockers all use explicit overlay orders.
4. Docked panel content remains on lower explicit panel orders.
5. The menu renders on top and also wins hover and click hit testing.

### Floating Dockable

1. A panel becomes undocked.
2. `DockableEntity` reapplies its explicit floating bias.
3. The panel renders above docked panels but below overlay menus and modal dialogs.

### Modal Dialog

1. The editor opens `OpenFileDialog`, `SaveFileDialog`, or `UnsavedChangesDialog`.
2. Its panel, text, and input surfaces use explicit modal orders.
3. The dialog renders above normal overlays and owns input priority while visible.

## Error Handling

Rules:

- No 2D component should silently fall back to an old bucket helper after this migration.
- If a migrated class needs a render order, it must choose an explicit constant from the shared table.
- Floating-panel bias must clamp safely within `byte` range instead of overflowing.
- Tests should catch any accidental equal-order overlap between menus and docked panels where strict precedence is required.

## Testing Requirements

Implementation must include coverage for:

1. `Add` menu renders above `SceneHierarchyPanel`.
2. `File` menu continues to render above docked panels.
3. Menu rows and menu gap blockers still win hover and click over panel content behind them.
4. Floating dockables render above docked dockables after the explicit-bias migration.
5. Modal dialogs render above overlay menus.
6. `ObjectManager` no longer exposes `GetRenderOrderForLayer2D(...)` or `RenderOrderLayers2D`.
7. Existing editor tests that assert old bucket helper values are updated to assert the new explicit constants instead.

## Recommendation

Implement a centralized explicit 2D render-order table and migrate the editor to it now. The immediate `Add` menu bug then becomes a natural consequence of the new policy instead of another special-case patch.

That gives the editor a readable, deterministic visual stack:

- docked panels below overlays
- overlays below modals
- input precedence matching visible precedence

This is the right place to remove the bucket model for 2D before more editor UI depends on it.
