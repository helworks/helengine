# Model Preview Bounding-Box Dimensions Design

## Goal

Show the model bounds width, height, and depth directly on the model preview bounding box while its box display mode is active.

## Behavior

- The existing Preview-panel bounds button continues to cycle `none`, `box`, and `sphere`.
- In `box` mode, three labels display the bounds size along the X, Y, and Z axes.
- Each label is centered on a positive-facing edge: X on the positive-X horizontal edge, Y on the positive-Y vertical edge, and Z on the positive-Z depth edge.
- Labels hide when the preview uses `sphere` or `none` bounds mode.
- Values derive from `BoundsMax - BoundsMin` and are formatted consistently by one dedicated preview-bounds formatter.

## Rendering

The labels reuse the transform-gizmo axis-label mesh factory and material factory. This preserves the gizmo font atlas, outline treatment, and camera-facing billboard behavior without introducing a second text-rendering path.

The preview source owns the three label entities alongside its box and sphere overlays. The source updates their enabled state whenever its bounds display mode changes. Their positions remain fixed relative to the centered preview model.

## Validation

Tests will verify that three dimension label entities are created with the expected X/Y/Z sizes, use the transform-gizmo label material path, and are enabled only in box mode. Preview-panel tests will continue to verify mode cycling and persistence across model source changes.
