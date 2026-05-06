# Modal Content Root Contract Design

## Summary

The project `PlatformsDialog` currently violates a stronger modal ownership rule: dynamic checkbox rows are created during `Show(...)`, but they do not become immediately render-ready inside a dedicated modal-owned content subtree. In practice, that lets content appear only after later lifecycle events and makes it possible for dialog-created controls to behave like globally placed editor entities instead of owned modal content.

This change introduces a shared modal content-root contract in `EditorDialogBase` and migrates `PlatformsDialog` onto that contract first. The contract establishes that modal-specific dynamic content is always attached under a dedicated content root, and that `Show(...)` must leave the dialog fully positioned and laid out in the same call.

## Problem

Current modal behavior is inconsistent:

- Shared modal shell chrome lives in `EditorDialogBase`, but derived dialogs can attach dynamic content directly under arbitrary panel children.
- `PlatformsDialog.Show(...)` rebuilds dynamic checkbox rows, but it does not fully apply visible shell state and content layout during the same call.
- Because dynamic rows are created before a guaranteed show-time layout pass, controls can appear at default coordinates until some later lifecycle step occurs.
- This violates the desired architectural rule that modal-owned entities should behave like WinForms controls: owned by the dialog, attached to a stable container, and positioned immediately when shown.

The user expectation is explicit:

- every modal should have a dedicated content container
- dynamic modal content should never depend on implicit global placement
- no dialog-created entity should effectively end up with "global parent" behavior

## Goals

- Add a shared modal content-root contract to `EditorDialogBase`.
- Ensure modal `Show(...)` paths leave dialogs fully render-ready immediately.
- Ensure dynamic modal entities attach only under modal-owned content roots.
- Fix `PlatformsDialog` using the shared contract.
- Add regressions that prove immediate render readiness and owned hierarchy placement.

## Non-Goals

- Refactor every modal in the editor in this same patch.
- Redesign `PlatformsDialog` visuals, copy, or control choices.
- Introduce clipping, scrolling, or new layout behavior unrelated to ownership and show-time layout.
- Replace existing static modal chrome structure in `EditorDialogBase`.

## Architecture

### Base Modal Contract

`EditorDialogBase` will expose one dedicated dialog content root beneath the panel shell. Shared chrome such as backdrop, panel background, header, close button, and resize grips remain owned by the existing shell structure. Derived dialogs use the content root for dialog-specific content rather than attaching ad hoc children directly under the shell.

The base class also owns one explicit show-time visible-state application path:

- clamp and apply panel position
- apply backdrop and chrome layout
- invoke a derived-layout hook for dialog-owned content

This makes `Show(...)` sufficient to leave a modal render-ready without waiting for the next session `UpdateLayout(...)`.

### Derived Dialog Responsibilities

Derived dialogs are split into:

- static dialog content that can be created once and parented under the content root
- dynamic dialog content that is rebuilt under the content root and laid out immediately during `Show(...)`

`PlatformsDialog` will follow that pattern first. Its platform checkbox hosts and label hosts will be attached only under the modal content root, and its content layout routine will run as part of the immediate show-time path.

## Data Flow

### Show Lifecycle

The intended lifecycle for modal dialogs becomes:

1. `Show(...)` validates inputs and enables the dialog.
2. The dialog rebuilds any dynamic content under the dedicated content root.
3. The dialog applies visible shell state immediately.
4. The dialog applies content layout immediately.
5. The modal is fully render-ready before control returns from `Show(...)`.

This means a caller can open the dialog and expect all controls to already be in their correct positions without waiting for a later `EditorSession.UpdateLayout(...)`.

### Hide Lifecycle

`Hide(...)` keeps current shell behavior but dynamic cleanup remains local to the content root. Clearing rows or transient controls must detach from that owned subtree only.

## Ownership Rules

The new contract establishes these invariants:

- Every dialog-specific entity must have a modal-owned ancestor chain under the dialog content root.
- No dynamic modal row host may rely on default world origin placement between `Show(...)` and the next frame.
- Modal dynamic content must be laid out during the same call that makes the dialog visible.
- Row cleanup must only remove children from the dialog-owned content root used to create them.

## `PlatformsDialog` Changes

`PlatformsDialog` will be updated to:

- attach dynamic platform checkbox hosts and label hosts under the shared modal content root
- attach any static dialog content under the same content subtree where appropriate
- rebuild platform rows during `Show(...)`
- apply content layout immediately during `Show(...)`
- preserve current validation behavior for active-platform selection and empty selection

The dialog should open with all rows visible, positioned under the modal panel, and never appear to create controls on the editor form itself.

## Testing Strategy

Add regressions in `PlatformsDialogTests` that verify:

- immediately after `Show(...)`, platform checkbox rows already exist
- immediately after `Show(...)`, platform checkbox hosts and label hosts are positioned inside the modal content area without requiring a later `UpdateLayout(...)`
- dynamic row hosts are children of the modal-owned content root
- disabling one platform and saving still preserves the existing active-platform validation behavior

If practical, also add one focused base-level assertion through test inspection that the dialog exposes the dedicated content root, but the required behavior is primarily enforced through `PlatformsDialogTests`.

## Risks

- Introducing a new base content-root contract can shift parent hierarchies for derived dialogs, so the patch should avoid migrating unrelated modals in the same change.
- Some existing dialogs may already rely on direct `DialogPanelRoot` parenting. The base change must remain backward-compatible for dialogs not yet migrated.
- Show-time layout must not regress dialog drag or resize behavior. The new content hook should integrate with the existing visible-state and layout callbacks, not bypass them.

## Recommendation

Implement the shared content-root contract in `EditorDialogBase`, migrate `PlatformsDialog` onto it, and prove the rule with immediate-show regressions. This fixes the current bug at the architectural layer the user asked for and establishes the modal pattern future dialogs should follow.
