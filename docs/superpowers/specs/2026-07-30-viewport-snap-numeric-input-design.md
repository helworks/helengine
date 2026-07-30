# Viewport snap numeric input design

## Scope

Replace the two read-only snap values in the viewport toolbar with editable numeric textboxes. The controls represent the Ctrl and Shift snap slots for the active transform tool mode: translate, rotate, or scale.

The existing snap increase and decrease buttons remain unchanged. No other toolbar controls change.

## Interaction

Each snap textbox displays the current formatted value obtained from `TransformGizmoSnapSettingsService` for the active tool mode and its assigned slot. A valid finite number commits when Enter is pressed or focus leaves the field. The commit writes through the same snap settings service used by the existing increase and decrease buttons.

Invalid, incomplete, or non-finite input does not change the snap setting. The field restores the current formatted service value. Changing the active tool mode, or using either existing snap adjustment button, refreshes both textboxes from their active snap values.

## Architecture

`EditorViewport` owns one `TextBoxComponent` per snap slot in place of the existing background and text readout components. Each textbox submit handler captures its slot index, resolves the current `ToolMode`, parses invariant-culture numeric text, validates that it is finite, and updates the slot through `TransformGizmoSnapSettingsService`.

The existing toolbar synchronization method remains responsible for formatting values from the service. This makes the service the only source of truth and preserves viewport-local snap state, tool-specific values, and persistence behavior.

## Verification

Tests will prove that a toolbar pointer press focuses the snap textbox; valid typed values update only the active tool mode and selected slot; invalid values restore the formatted service value; and existing button-driven synchronization still updates the textbox. Existing viewport toolbar and snap service tests remain green.
