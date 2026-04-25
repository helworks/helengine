# Full Height Title Bar Buttons Design

## Summary

This change makes every interactive button in the editor title bar span the full title-bar height with no top or bottom gap.

The affected buttons are:

- `File`
- `Add`
- `-`
- `Max`
- `X`

The horizontal layout stays the same. Only the vertical sizing and placement change.

## Goals

- Make all title-bar buttons touch the top edge of the title bar.
- Make all title-bar buttons touch the bottom edge of the title bar.
- Keep the existing button order, widths, and horizontal spacing.
- Keep `EditorTitleBar` as the single place that owns title-bar button layout.
- Add regression coverage for the new vertical layout.

## Non-Goals

- No redesign of button colors, labels, or hover behavior.
- No change to title-bar height.
- No change to context-menu behavior.
- No change to title text positioning unless required by button-height alignment.
- No host-window platform changes.

## Current Problem

`EditorTitleBar` currently hard-codes a top inset and a shorter button height:

- `ButtonTop = 6`
- `ButtonHeight = 24`
- `HeightPixels = 36`

That creates visible gaps above and below every title-bar button. The buttons do not align with the full title-bar surface, which makes the chrome look inset instead of edge-aligned.

## Proposed Design

### 1. Full-Height Button Layout

All title-bar buttons will use:

- `Y = 0`
- `Height = EditorTitleBar.HeightPixels`

This removes all vertical gaps and makes the button surfaces flush with the title-bar bounds.

### 2. Preserve Horizontal Layout

The following values stay unchanged:

- left and right edge padding,
- inter-button spacing,
- computed button widths,
- title text width calculation,
- drag-region horizontal bounds.

This keeps the visual change narrow and predictable.

### 3. Keep Button Construction Centralized

`EditorTitleBar.CreateTitleBarButton` remains the single construction path for both left-side menu buttons and right-side window-control buttons.

The implementation should update the shared title-bar button constants rather than introducing separate button sizing rules for different button groups.

### 4. Regression Coverage

Tests should verify that:

- title-bar buttons are positioned at the top edge,
- title-bar button surfaces use the full title-bar height,
- the rule applies to both left-side menu buttons and right-side window controls.

## Testing

- Add or update editor title-bar tests to assert full-height button layout.
- Run the most targeted available verification for `EditorTitleBar` layout behavior in this workspace.
