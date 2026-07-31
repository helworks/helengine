# Model Preview Bounds Overlay Design

## Purpose

Add a second model-preview toolbar button that cycles the selected model's bounds overlay through wireframe bounding box, wireframe bounding sphere, and no overlay.

## Behavior

- The control appears beside the existing model-grid button and is visible only for `ModelPreviewSource` instances.
- A newly created Preview panel starts in the no-overlay mode.
- Each activation advances in this fixed order: no overlay, bounding box, bounding sphere, no overlay.
- The selected mode belongs to the Preview panel and remains active as the user changes model assets during the editor session.
- The existing grid visibility control remains independent from the bounds overlay mode.

## Rendering

`ModelPreviewSource` owns both overlays because it owns the cached bounds and offscreen scene. It creates reusable line-based models for a box and a sphere, centers them with the previewed model, and enables only the entity represented by the requested mode. The box follows all cached bounds dimensions. The sphere encloses the same bounds using the preview source's existing bounds-radius calculation.

## UI and Input

`PreviewPanel` owns the current mode, toolbar button visuals, focus target, and pointer handling. The bounds button uses the shared compact toolbar layout, reserves no additional vertical area, and absorbs pointer input so its clicks and drags cannot orbit or pan the model preview.

## Tests

- Model-preview tests cover line mesh creation, cached-bounds placement, and mutually exclusive mode visibility.
- Preview-panel tests cover the three-state cycle, default no-overlay state, persistence across model-source replacement, and toolbar-only visibility.
