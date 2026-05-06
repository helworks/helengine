# EditorDialogBase Modal Lifecycle Sweep Design

## Summary

This design standardizes the show-time and relayout behavior of all direct `EditorDialogBase` subclasses. The recent `PlatformsDialog` fix established the correct contract for modal-owned content parenting, immediate visual readiness after `Show(...)`, and base-driven relayout. This sweep applies that contract consistently across the remaining dialogs so modal behavior does not depend on a later `UpdateLayout(...)` pass to become visually or interactively correct.

The scope is limited to direct `EditorDialogBase` subclasses. Modal-like wrappers and other overlay types that are not direct subclasses, such as `AssetPickerModal` and `SaveFileDialog`, are explicitly out of scope for this pass.

## Problem Statement

Before the `PlatformsDialog` fix, several editor modals shared two architectural problems:

1. Dialog-owned controls could effectively behave like shell-attached or globally-parented UI instead of living under one dialog-owned content root.
2. `Show(...)` often stopped at `Enabled = true`, while dynamic row creation, combo-box item replacement, hierarchy picker population, or browser refresh happened before the dialog shell and content had been laid out for the current frame.

The first problem is now solved at the base-class level because `DialogPanelRoot` resolves to a dedicated modal content root inside `EditorDialogBase`.

The second problem still exists in multiple dialogs. Several `Show(...)` methods mutate visible content and only become correctly positioned after a later `UpdateLayout(...)` pass. That is the same failure mode that caused `PlatformsDialog` to create controls that appeared in the wrong place until the dialog closed or the next layout/frame event happened.

## Goals

- Ensure every direct `EditorDialogBase` modal reaches a visually valid state by the end of `Show(...)`.
- Ensure modal content relayout is driven by the base dialog shell lifecycle rather than duplicated ad-hoc shell calls.
- Preserve the dedicated modal content-root contract for all dialog-owned entities.
- Add regression coverage that proves dialog content is positioned immediately after `Show(...)` for representative dialog families.

## Non-Goals

- Refactoring non-`EditorDialogBase` overlays or modal wrappers.
- Reworking dialog visuals, copy, or interaction design beyond lifecycle correctness.
- Changing resize behavior except where required to keep the new lifecycle contract coherent.

## Modal Inventory

### Already aligned or mostly aligned

- `PlatformsDialog`
  - Already uses `ShowDialogImmediately()`.
  - Already routes relayout through `HandleDialogLayoutChanged()`.
- `BuildDialog`
  - Already overrides `HandleDialogLayoutChanged()` and has explicit shell-aware layout behavior.

### Requires lifecycle normalization

- `BuildSettingsDialog`
- `BuildDialogCopySettingsDialog`
- `ComponentAddDialog`
- `EditorPreferencesDialog`
- `OpenFileDialog`
- `ProfilesDialog`
- `ReparentEntityDialog`
- `RemoveComponentDialog`
- `UnsavedChangesDialog`

These dialogs currently use one of two problematic patterns:

- `Show(...)` enables the dialog and mutates content, but waits for a later `UpdateLayout(...)` call to position that content.
- `Show(...)` partially reproduces shell work manually, such as centering or chrome layout, but does not apply the full visible dialog state through one shared contract.

## Shared Lifecycle Contract

All direct `EditorDialogBase` subclasses should follow this lifecycle model:

1. `Show(...)` prepares dialog state and any dialog-specific data.
2. `Show(...)` enables the dialog.
3. `Show(...)` applies one immediate visible-state path so the shell, backdrop, panel position, chrome, and dialog content are valid before the next layout tick.
4. Ongoing position/size changes, including drag, resize, metric changes, and host relayout, flow through `UpdateDialogFrame(...)` and `HandleDialogLayoutChanged()`.

The contract must remain explicit. The base class should not silently infer that any `Enabled = true` transition means the dialog is ready to lay itself out. Each dialog must opt in at the correct point in `Show(...)`, after its visible state and content data are prepared.

## Architecture

### Base-class contract

`EditorDialogBase` remains the owner of:

- modal backdrop state
- shell position and centering
- chrome layout
- the dedicated dialog content root
- the post-shell layout callback via `HandleDialogLayoutChanged()`

`ShowDialogImmediately()` remains the standard path for fixed-size dialogs whose shell is already valid once their show-time data is prepared.

### Dialog-level relayout

Dialogs should stop duplicating shell work in `Show(...)` and `UpdateLayout(...)`. Instead:

- `Show(...)` should end with one immediate-show path.
- `UpdateLayout(...)` should call `UpdateDialogFrame(...)` and then rely on `HandleDialogLayoutChanged()` for content positioning.
- Content layout methods should be idempotent and safe to run multiple times.

### Custom immediate-show path for sized dialogs

`OpenFileDialog` is the known exception because it computes an initial size from the host window before applying the shell state. It should still follow the same semantic contract, but through an explicit dialog-level immediate-show method:

- compute or preserve the intended panel size
- apply the shell state for the current host
- apply browser, status, and footer layout immediately

This must not fall back to ad-hoc shell calls such as separately calling `CenterDialogIfNeeded()`, `UpdateDialogChromeLayout()`, or content layout methods without also applying backdrop/input-blocker state.

## Per-Dialog Design

### BuildSettingsDialog

- Move show-time completion onto `ShowDialogImmediately()`.
- Route row/header/status/button layout through `HandleDialogLayoutChanged()`.
- Keep dynamic platform-row rebuilding unchanged except for the moment layout is applied.

### BuildDialogCopySettingsDialog

- Replace the manual sequence of `CenterDialogIfNeeded()`, `UpdateDialogChromeLayout()`, and direct `LayoutContent()` with the shared immediate-show contract.
- Route ongoing content layout through `HandleDialogLayoutChanged()`.

### ComponentAddDialog

- Apply immediate visible state after search reset, list rebuild, and initial selection/focus state are prepared.
- Route search/list/footer layout through `HandleDialogLayoutChanged()`.
- Preserve current scroll/list virtualization behavior.

### EditorPreferencesDialog

- Apply immediate visible state after combo-box selections and enabled-state toggles are prepared.
- Route all control positioning through `HandleDialogLayoutChanged()`.

### OpenFileDialog

- Add a dialog-specific immediate-show path that computes the initial panel size and then applies shell/content state in one coherent flow.
- Keep current dynamic sizing behavior and browser refresh logic.
- Avoid reintroducing duplicated shell behavior or partial backdrop application.

### ProfilesDialog

- Apply immediate visible state after platform combo-box items, selection-model resolution, tab selection, and active-section data are prepared.
- Route selector, tab, section, status, and footer layout through `HandleDialogLayoutChanged()`.
- Preserve current tab/content state semantics.

### ReparentEntityDialog

- Apply immediate visible state after target text, available parent state, and hierarchy picker content are prepared.
- Route target, hierarchy, status, and footer layout through `HandleDialogLayoutChanged()`.

### RemoveComponentDialog

- Apply immediate visible state after message text is set.
- Route message/footer layout through `HandleDialogLayoutChanged()`.

### UnsavedChangesDialog

- Apply immediate visible state in `Show()`.
- Route message/footer layout through `HandleDialogLayoutChanged()`.
- Do not bundle resize-behavior changes into this sweep.

## Testing Strategy

Tests should focus on behavior, not implementation trivia. The core regression is: after `Show(...)`, dialog-owned visible content must already be positioned under the modal content root and must not depend on a later `UpdateLayout(...)` call to leave default origin coordinates.

### Required regression coverage

- `BuildSettingsDialogTests`
  - Show-time platform rows are positioned immediately.
- `ProfilesDialogTests`
  - Show-time combo box, tabs, and active content hosts are positioned immediately.
- `ComponentAddDialogTests`
  - Show-time search/list/footer content is positioned immediately after the initial list rebuild.
- `ReparentEntityDialogTests`
  - Show-time hierarchy content and footer are positioned immediately.
- `OpenFileDialogTests`
  - Show-time browser/status/footer placement is valid immediately after `Show(...)`, using the custom immediate-show path.

### Secondary coverage

- `EditorPreferencesDialogTests`
- `RemoveComponentDialogTests`
- `UnsavedChangesDialogTests`
- `BuildDialogCopySettingsDialogTests`

These should verify that simple/static dialogs also honor the new contract, but they do not need excessive new assertions if existing tests already cover the relevant hosts and positions.

### Regression philosophy

- Prefer one or two show-time assertions per dialog family over broad fixture duplication.
- Use existing private/protected field access helpers where test suites already use them.
- Do not rewrite unrelated stale tests as part of this work, except when a test must be updated to reflect the new lifecycle contract.

## Error Handling and Safety

- Keep all existing validation behavior unchanged.
- Do not add fallback behavior that hides missing host-size or invalid show-time state.
- If a dialog requires host-size-dependent behavior at show time, it should use an explicit path that makes that dependency clear.

## Risks

- `OpenFileDialog` may require more careful handling because its initial size is host-dependent.
- `ProfilesDialog` and `ComponentAddDialog` have the most complicated show-time content state, so regressions there are more likely if lifecycle and content rebuild ordering are changed carelessly.
- `UnsavedChangesDialogTests` already contain unrelated stale resize expectations; that suite should not be used as proof that this lifecycle sweep broke resize behavior unless the failures are directly attributable to the new changes.

## Success Criteria

- All direct `EditorDialogBase` subclasses use one coherent immediate-show lifecycle.
- Dialog-owned content is always under the dedicated modal content root.
- Representative dialog tests prove content is positioned immediately after `Show(...)`.
- No dialog relies on a later `UpdateLayout(...)` pass to become visually correct after opening.
