# Model Preview Toolbar Design

## Purpose

Give the Preview panel a compact, in-canvas toolbar for model previews. Its first control toggles the preview grid without affecting other preview types or the editor viewport grid.

## Behavior

- The toolbar is visible only while the active preview source is a `ModelPreviewSource`.
- Its first button represents preview-grid visibility and starts enabled for a newly created Preview panel.
- Activating the button immediately shows or hides the grid in the active model preview.
- The visibility choice belongs to the Preview panel, so it survives switching between model assets during the current editor session.
- Texture and camera previews show no model-preview toolbar and retain their current presentation and input behavior.

## Rendering

`ModelPreviewSource` owns the preview-grid entities and rendering because it owns the offscreen model-preview scene and cached model bounds. The grid is centered beneath the model and scales from the model's horizontal bounding-box footprint, with a minimum 5-unit span.

## UI and Input

`PreviewPanel` owns the toolbar state, layout, button visual state, focus target, and pointer capture. The toolbar reserves its own portion of the panel content area so toolbar clicks cannot orbit, pan, or zoom the model. It follows the compact in-canvas toolbar conventions already used by `EditorViewport`.

## Tests

- Model-preview tests cover the generated grid's model-bounds sizing and visibility changes.
- Preview-panel tests cover toolbar visibility for model sources only, immediate grid toggling, persistence across source replacement, and toolbar input isolation.
