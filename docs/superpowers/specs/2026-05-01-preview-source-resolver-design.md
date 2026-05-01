# Preview Source Resolver Design

## Summary

This document defines a dedicated preview-source pipeline for the editor `Preview` panel.

The current panel is texture-only. The new design turns it into a generic host that can display any preview source that can produce a runtime texture, including:

- imported textures,
- live camera previews,
- future model previews,
- and other previewable editor assets or scene selections.

The first implementation slice includes two preview sources:

- `TexturePreviewSource`
- `CameraPreviewSource`

The resolver chooses the highest-priority valid source from the current editor selection state, so a selected camera replaces an existing texture preview automatically.

## Goals

- Add a dedicated preview-source abstraction instead of hard-coding preview cases into `PreviewPanel`.
- Keep `PreviewPanel` focused on hosting, layout, and display.
- Show a live preview when a scene camera is selected.
- Replace an existing texture preview when a camera preview becomes available.
- Preserve the authored camera state for selected scene cameras that are suppressed in the editor.
- Leave room for future preview types without redesigning the panel again.
- Keep preview lifecycle management explicit so render targets and hidden entities are disposed correctly.

## Non-Goals

- No model preview implementation in this first slice.
- No material preview implementation in this first slice.
- No preview-specific scene navigation or orbit controls yet.
- No changes to the existing main viewport camera controller.
- No reuse of the editor's scene-picking hidden camera.
- No automatic preview creation for unsupported selections.

## Current Problem

`PreviewPanel` currently knows only how to show imported textures. That makes it hard to extend and ties preview behavior directly to the panel implementation.

The current flow also has a structural gap:

- asset selection can show a texture preview,
- entity selection updates the properties panel,
- but selected cameras do not flow into the preview UI at all.

That creates two problems:

- there is no live camera preview,
- and any future preview type would force more branching into the panel and session code.

The editor already has the pieces needed for a camera preview:

- `CameraComponent` can render into a `RenderTarget`,
- the renderer already supports offscreen camera targets,
- and the editor already stores the authored camera state for suppressed scene cameras in `EditorSceneCameraSuppressionComponent`.

What is missing is a clean selection-to-preview boundary.

## Proposed Architecture

### 1. Preview Panel Becomes A Host

`PreviewPanel` stays responsible for panel chrome and display layout, but not for deciding what should be shown.

It will:

- own the active preview source,
- forward resize changes to the active source,
- forward update ticks to the active source,
- display the source's runtime texture,
- dispose the previous source when a new one is assigned,
- clear the preview when no source is available.

This keeps the panel focused on presentation and avoids preview-specific branching in the UI layer.

### 2. Dedicated Preview Source Interface

Introduce one small interface for preview sources.

The source owns its own lifecycle and produces the renderable output consumed by `PreviewPanel`.

Responsibilities:

- build or load the preview output,
- keep the output up to date,
- react to size changes,
- dispose any owned render targets or helper entities.

The first version should cover sources that can provide a `RuntimeTexture`, which is enough for the current texture and camera cases and still leaves the door open for model previews rendered to texture later.

### 3. Dedicated Preview Source Resolver

Add a resolver service that translates the current editor selection snapshot into a preview source.

The resolver should be the only place that knows the selection precedence rules.

Recommended precedence:

1. selected scene camera preview,
2. selected texture asset preview,
3. no preview.

That ordering ensures a selected camera overrides an existing asset preview, while still allowing a texture preview to remain available when the current entity selection is not previewable.

### 4. Editor Session Owns The Current Selection Snapshot

`EditorSession` already receives both:

- asset browser selection events,
- scene selection events.

It should keep the latest asset selection and the latest entity selection, then ask the resolver to recompute the preview source whenever either changes.

That keeps the resolution deterministic and prevents preview logic from leaking into unrelated UI classes.

### 5. Texture Preview Source

`TexturePreviewSource` wraps the existing imported-texture flow.

Responsibilities:

- load or build the runtime texture for the selected asset,
- expose that runtime texture to the panel,
- dispose the runtime texture if it owns one,
- do nothing per-frame after construction unless future texture preview cases need it.

This source can keep using the existing 2D render manager path that builds a runtime texture from the imported `TextureAsset`.

### 6. Camera Preview Source

`CameraPreviewSource` provides a live camera view for a selected entity with a camera component.

Responsibilities:

- create its own hidden offscreen camera entity,
- create its own render target through `RenderManager3D.CreateRenderTarget`,
- size the target to the preview panel's usable content area,
- mirror the selected camera's authored state into the preview camera,
- keep the target and preview camera synchronized while the source is active,
- dispose the render target and hidden entity when replaced or cleared.

The source must not reuse the editor's existing hidden scene-picking camera. That camera is tied to picking behavior, uses a fixed size, and has a separate responsibility.

The camera preview should mirror the authored camera state, not the editor-suppressed runtime state. When the selected entity has an `EditorSceneCameraSuppressionComponent`, the preview source should read the stored authored values from that component and apply them to the preview camera.

At minimum, the camera preview source should mirror:

- camera draw order,
- layer mask,
- viewport size,
- clear settings,
- world transform from the selected entity.

### 7. Size Handling

The preview panel already computes a content area inside the title bar and padding.

The active preview source should receive that usable size whenever the panel changes size.

For camera previews:

- the source should resize its render target to match the panel content area,
- it should rebuild the render target when the backend requires a new resource,
- and it should update the preview camera viewport accordingly.

For texture previews:

- the source can keep the asset's native size,
- and the panel continues to scale it to fit the available area.

### 8. Selection Flow

When the user selects an asset or a scene entity:

1. `EditorSession` records the new selection snapshot.
2. `EditorSession` asks `PreviewSourceResolver` for the best source.
3. The resolver returns a camera source, texture source, or no source.
4. `PreviewPanel` replaces the active source if needed.
5. The panel updates layout and display state.

Selection changes that do not produce a preview source should not disturb a valid lower-priority source unless the selection snapshot no longer supports it.

## Data Flow

### Texture Selection

1. User selects a texture asset in the asset browser.
2. `EditorSession` records the asset selection.
3. The resolver creates a `TexturePreviewSource`.
4. `PreviewPanel` binds the source and shows its runtime texture.

### Camera Selection

1. User selects a scene entity that owns a camera component.
2. `EditorSession` records the entity selection.
3. The resolver creates a `CameraPreviewSource`.
4. `PreviewPanel` disposes the previous source and binds the new camera source.
5. The camera source mirrors the selected camera state and keeps its render target sized to the panel.
6. The preview panel shows the live camera render.

### Clearing Selection

1. The active selection changes to nothing previewable.
2. `EditorSession` recomputes the preview source.
3. The resolver returns no source.
4. `PreviewPanel` clears the current source and empties the preview.

## Error Handling

The preview path should fail clearly and leave the editor selection unchanged.

Rules:

- If a texture preview cannot be built, the resolver should log the failure and return no source.
- If a camera preview cannot create a render target, the resolver should log the failure and return no source.
- If a preview source throws during update, the panel should dispose that source, clear the preview, and log the error.
- If the selected entity does not have a camera component, the resolver should not invent a preview source.
- If the selected camera has no authored suppression state, the source should mirror the live camera component directly.

The preview UI is supplemental. A preview failure must not block scene selection, asset selection, or editor interaction.

## Testing Requirements

The implementation must include coverage for:

1. `PreviewSourceResolver` returning a texture source for a texture asset selection.
2. `PreviewSourceResolver` returning a camera source for a selected entity with a camera component.
3. `PreviewSourceResolver` preferring the camera source over an existing texture preview when both are available.
4. `PreviewPanel` replacing the active source instead of layering multiple preview sources.
5. `CameraPreviewSource` mirroring authored camera state from `EditorSceneCameraSuppressionComponent` when present.
6. `CameraPreviewSource` resizing its render target when the panel content area changes.
7. `EditorSession` recomputing preview state from both asset and entity selection changes.
8. `EditorSession` clearing the preview when the current selection snapshot no longer resolves to any previewable source.

Test support will likely need one renderer test double update:

- `TestRenderManager3D` should override `CreateRenderTarget` so camera preview sources can be exercised in tests.

## Open Follow-Ups

These items are intentionally deferred:

- model preview rendering,
- material preview rendering,
- animated preview timelines,
- source-specific camera controls inside the preview panel,
- preview source caching across repeated selections,
- richer preview metadata such as overlay labels or loading states.

## Recommendation

Implement the preview feature as a resolver-driven source pipeline now.

That gives the editor a single preview boundary, lets camera previews replace texture previews cleanly, and keeps future preview types out of `PreviewPanel` and `EditorSession`.
