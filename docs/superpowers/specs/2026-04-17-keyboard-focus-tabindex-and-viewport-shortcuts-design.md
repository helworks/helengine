# Keyboard Focus, TabIndex, And Viewport Shortcuts Design

## Summary

This document defines the first keyboard-navigation foundation for the editor. The editor gains one shared focus system, explicit `TabIndex` ordering scoped per parent container like WinForms, a visible active-outline treatment for every dock and keyboard-reachable control, and viewport-local `W` / `R` / `S` gizmo shortcuts that only work when the viewport content is active and the right mouse button is not pressed.

The design is intentionally a foundation pass. It centralizes focus ownership, keeps mouse and keyboard state synchronized, and makes the most important editor controls reachable without inventing separate keyboard rules inside every widget.

## Goals

- Add one central keyboard-focus model for the editor.
- Add explicit `TabIndex` support scoped per parent container like WinForms.
- Let `Tab` and `Shift+Tab` move through controls inside the active dock by `TabIndex`.
- Let `Ctrl+Tab` and `Ctrl+Shift+Tab` move between top-level dock groups.
- Give every dock and every focusable control the same thin active outline treatment.
- Keep mouse clicks and keyboard focus synchronized.
- Allow `Enter` and `Space` to activate focused controls in the first pass.
- Add viewport-local `W`, `R`, and `S` shortcuts for translate, rotate, and scale gizmos.
- Suppress viewport gizmo shortcuts while the right mouse button is currently pressed.

## Non-Goals

- No full keyboard-only interaction for every complex editor widget in this phase.
- No arrow-key navigation standard for every list, tree, or combo-box popup in this phase.
- No generalized accessibility framework beyond the focus and activation model defined here.
- No OS-level accessibility integration in this phase.
- No attempt to make arbitrary scene-canvas interactions fully keyboard-driven in this pass.

## Current Problem

The editor already has many clickable controls, but it does not have one shared notion of keyboard focus.

Current gaps:

- Most controls are mouse-only state machines with local hover and press logic.
- `TextBoxComponent` is the only control that owns a real focus concept, and it does so independently.
- There is no editor-wide `TabIndex` concept.
- There is no visible active/focused-outline treatment shared by docks and controls.
- There is no central traversal model for moving through docks and controls by keyboard.
- Viewport gizmo mode selection currently depends on toolbar clicks rather than keyboard shortcuts scoped to an active viewport.

Without a shared focus system, adding shortcuts control-by-control would create inconsistent behavior, duplicate traversal rules, and make future keyboard support harder instead of easier.

## Proposed Architecture

### 1. Central Keyboard Focus Service

The editor will introduce one shared `EditorKeyboardFocusService` that owns:

- the active top-level dock group
- the currently focused subgroup inside that dock, when applicable
- the currently focused control target
- top-level dock traversal
- in-dock control traversal
- keyboard activation dispatch

This service becomes the single source of truth for editor keyboard focus. Controls and docks do not compute `Tab`, `Shift+Tab`, or `Ctrl+Tab` transitions on their own.

### 2. Focus Groups And Targets

The model has two layers:

- top-level focus groups
- focus targets

Top-level focus groups are dock panels.

Some dock panels may expose nested internal groups when they have clearly distinct keyboard regions. The viewport is the primary example in this first pass:

- viewport toolbar subgroup
- viewport content subgroup

Nested groups exist to organize traversal and active-state visuals inside a dock. They do not replace top-level dock grouping for `Ctrl+Tab`.

Focus targets are concrete keyboard-reachable controls inside a group, such as:

- buttons
- toolbar buttons
- dock tabs
- text boxes
- combo-box main controls
- scene hierarchy rows
- asset browser rows
- viewport content

### 3. WinForms-Style TabIndex

Each focus target exposes an explicit `TabIndex`.

Rules:

- `TabIndex` is scoped per parent container, not globally.
- Traversal order inside a group is determined by `TabIndex`, not inferred from geometry.
- Ties are resolved by stable registration order inside the parent container.
- Disabled, hidden, or non-focusable targets are skipped.

This keeps traversal intentional and predictable, especially as dock layouts and toolbar compositions change.

### 4. Traversal Rules

The editor uses two traversal modes.

In-dock traversal:

- `Tab` moves to the next focus target inside the active dock.
- `Shift+Tab` moves to the previous focus target inside the active dock.
- Nested subgroups inside the active dock are traversed as part of the dock-local chain.
- When a dock has no focused target yet, `Tab` resolves the first valid target inside that dock.

Top-level dock traversal:

- `Ctrl+Tab` moves to the next visible dock group.
- `Ctrl+Shift+Tab` moves to the previous visible dock group.
- When dock traversal lands on a new dock, focus moves to that dock's default target.

Default-target rule:

- If a dock exposes an explicit preferred target, use it.
- Otherwise, use the first valid target by subgroup order and `TabIndex`.
- If the dock has no valid target, the dock container remains active until one becomes available.

Initial-entry rule:

- If no target is focused and no dock is active, the first visible dock in docking-layout order becomes active.
- If a dock is already active from mouse interaction, keyboard traversal starts from that dock.

### 5. Mouse And Keyboard Synchronization

Mouse interaction immediately updates keyboard focus state.

Rules:

- Left click anywhere inside a control focuses that control and activates its containing dock.
- Right click anywhere inside a control also activates its containing dock.
- Clicking viewport content or right-clicking viewport content activates the viewport content target.
- Clicking a dock tab focuses that tab target and activates the owning dock.
- Clicking a row control focuses that row target before running its normal action.

This keeps keyboard users and mouse users in one consistent focus universe instead of two separate ones.

### 6. Shared Active Outline Treatment

Every dock and every focus target uses the same thin accent outline language.

Rules:

- The active dock always shows a thin accent outline.
- The focused target inside the active dock also shows the same accent outline treatment.
- Controls may still keep their existing hover and pressed fill-color behavior.
- Hover does not replace focus. Focus remains visible after pointer movement stops.

This makes keyboard position obvious without requiring heavy color changes or layout shifts.

### 7. Keyboard Activation

The first pass standardizes activation behavior for controls that already act like discrete widgets.

Rules:

- `Enter` activates the focused target when the target supports activation.
- `Space` activates the focused target when the target supports activation.
- Row-based controls use their existing primary action as the activation behavior.
- Text-entry controls keep text editing semantics and do not treat `Space` as button activation.

### 8. Viewport Shortcut Behavior

Viewport gizmo shortcuts become focus-aware instead of global.

Rules:

- `W` selects translation mode.
- `R` selects rotation mode.
- `S` selects scale mode.
- These shortcuts only work when the viewport content target is focused.
- These shortcuts do not fire while the right mouse button is currently pressed.
- Toolbar buttons remain normal focus targets and continue to reflect the selected tool mode visually.

This keeps viewport shortcuts available for efficient editing without stealing key presses from unrelated controls or camera-navigation gestures.

## Control Integration Plan

### 1. Shared Control Primitives

The following shared controls should expose focus-target support directly:

- `ButtonComponent`
- `TextBoxComponent`
- `ComboBoxComponent` main control

Each should gain:

- `TabIndex`
- focusable/enabled checks
- focus gained / lost callbacks
- outline visual support
- optional keyboard activation callback

### 2. Dock-Level Controls

The following editor-specific surfaces should register focus targets through their owning dock:

- `DockableEntity` as a top-level dock group
- `DockTabStrip` visible tabs
- `EditorViewport` toolbar buttons and viewport content
- `SceneHierarchyPanel` rows
- `AssetBrowserView` toolbar button and visible rows

These controls do not need to invent a second focus service. They only need to expose targets, `TabIndex`, and activation hooks to the shared service.

### 3. Nested Group Use

Only introduce nested groups where the boundary is useful and stable.

First-pass nested groups:

- viewport toolbar
- viewport content

Other docks can remain single-group containers until they demonstrate a clear need for internal group segmentation.

## Failure Handling And Dynamic UI

The focus service must be resilient to editor UI changing at runtime.

Rules:

- Hidden or disabled targets are skipped automatically.
- Empty docks are skipped by `Ctrl+Tab` traversal when they have no focusable targets.
- If the focused target disappears, focus falls back to the next valid target in the same dock.
- If no target remains in that dock, the dock stays active without a focused target.
- If the active dock disappears, the service activates the next valid visible dock.
- Mouse clicks always win immediately and resynchronize active dock and focused target state.

## Testing Requirements

The implementation must include coverage for:

1. `Tab` moving forward inside the active dock by explicit `TabIndex`.
2. `Shift+Tab` moving backward inside the active dock by explicit `TabIndex`.
3. `Ctrl+Tab` moving to the next visible dock.
4. `Ctrl+Shift+Tab` moving to the previous visible dock.
5. Mouse left click activating a dock and focusing the clicked target.
6. Mouse right click activating a dock and focusing the clicked target when the target accepts focus.
7. Active-outline state appearing on the active dock and the focused target.
8. `ButtonComponent` activating from `Enter` and `Space`.
9. `DockTabStrip` participating in focus traversal and keyboard activation.
10. `SceneHierarchyPanel` rows participating in focus traversal and keyboard activation.
11. `AssetBrowserView` toolbar button and rows participating in focus traversal and keyboard activation.
12. `EditorViewport` content accepting focus and gating `W`, `R`, and `S`.
13. Viewport shortcut suppression while the right mouse button is pressed.
14. Focus fallback when a focused control is hidden, disabled, or removed.

## Deferred Follow-Ups

These items are intentionally deferred to later specs:

- universal arrow-key navigation for all list and tree surfaces
- combo-box popup keyboard navigation standardization
- full keyboard control of scene-canvas interactions
- editor-wide mnemonic support
- accessibility metadata beyond the editor's own focus visuals and traversal model

## Recommendation

Implement one central keyboard-focus foundation now instead of adding shortcuts separately to each control.

That gives the editor:

- predictable WinForms-style `TabIndex` behavior
- visible active state on docks and controls
- one place to extend future keyboard navigation
- viewport-local transform shortcuts without global key conflicts

This is the smallest coherent slice that solves the immediate viewport-shortcut request while also creating a real keyboard-navigation foundation for the rest of the editor.
