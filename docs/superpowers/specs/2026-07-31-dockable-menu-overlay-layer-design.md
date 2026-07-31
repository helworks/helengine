# Dockable Menu Overlay Layer

## Goal

Render a dockable panel's `...` menu above all docked and floating panel content while keeping it below actual modal UI.

## Root Cause

Dockable menus currently use the shared editor UI layer, whose camera draws before panel-content cameras. Render-order values cannot overtake a later camera draw order, so the menu can appear beneath panel content.

## Design

Construct the dockable panel menu on `EditorLayerMasks.EditorModalUi`. The existing modal UI camera renders after panel content, so the menu appears above every dockable. The menu keeps its non-modal overlay render orders (`OverlayBackground` and `OverlayForeground`), which remain below modal dialog surfaces and controls that use the modal render-order bands.

## Verification

Tests will assert that dockable panel menus use the modal UI layer, retain their non-modal overlay render orders, and remain below modal background render order values.
