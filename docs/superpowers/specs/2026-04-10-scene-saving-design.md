# Scene Saving Design

## Summary

This document defines the first scene-saving slice for the editor. Scenes become first-class project assets saved as `.helen` files under `assets`. The editor will save only user-authored scene content, while editor-only cameras, gizmos, docking UI, overlays, and other internal entities continue to be recreated automatically by editor bootstrap.

The scene format integrates with the engine's existing HELE binary asset serialization flow. Component persistence is explicit and extensible: built-in engine components describe their own save payloads through a persistence contract instead of reflection-driven field dumping.

## Goals

- Save editor scenes as `.helen` assets inside the project `assets` folder.
- Route `Save Map` and `Save Map As...` through an editor-owned save flow.
- Persist only user-authored scene entities, never editor infrastructure.
- Keep scene serialization inside the existing HELE asset pipeline.
- Allow built-in engine components to define their own persistence payloads.
- Persist runtime-backed component assignments through stable asset references instead of raw runtime objects.
- Provide an editor-owned save dialog for choosing the destination path inside `assets`.

## Non-Goals

- No reflective serializer for arbitrary components.
- No attempt to persist editor UI state, gizmos, docking layout, or editor cameras as part of scene files.
- No native operating-system file dialog integration in this phase.
- No full load workflow in the same implementation slice unless it is required to verify round-trip correctness for the save design.
- No export-time inclusion of editor-only save metadata components in final game builds.

## Current Problem

The editor already exposes `New Map`, `Save Map`, and `Save Map As...` in the title bar, but there is no actual scene asset type or save pipeline behind those commands.

Current blockers:

- `AssetSerializer` and `EditorAssetBinarySerializer` do not understand scene assets.
- The asset browser has no scene entry kind and no `.helen` classification.
- `EditorSession` does not track a current scene path.
- User-authored entities are currently just live entities in the object manager with no project-backed persistence model.
- `MeshComponent` stores `RuntimeModel` and `RuntimeMaterial`, which cannot be written directly to disk and reconstructed safely without stable asset references.

## Proposed Architecture

### 1. SceneAsset In The HELE Pipeline

Scene files will be represented by a new `SceneAsset` type serialized through the existing HELE asset format. `.helen` is the user-facing file extension, but the binary payload still uses the standard engine header and `EditorAssetBinarySerializer` flow.

This keeps scene persistence aligned with the engine's current asset architecture instead of creating a second serializer stack just for scenes.

`SceneAsset` contains:

- Scene version metadata handled by the standard HELE header.
- A collection of serialized root entities.

Each serialized entity contains:

- Name.
- Local transform.
- Child entities.
- Serialized component records.

### 2. Save Only User Scene Content

The scene save service collects only user-authored scene entities.

Excluded content:

- Entities marked `InternalEntity`.
- Editor cameras.
- Gizmo entities.
- Dock panels.
- Modal UI.
- Any editor-only runtime helper entities.

This rule matches current editor behavior where internal/editor entities are already separated from normal scene content.

### 3. Hidden EntitySaveComponent

Each user-authored entity will carry a hidden editor-time save component, referred to here as `EntitySaveComponent`.

Purpose:

- Store persistence metadata for the entity in editor time.
- Expose a stable place where component persistence records can live.
- Keep persistence state out of gameplay-facing runtime components such as `MeshComponent`.

Rules:

- It is attached automatically to user-authored entities.
- It is hidden from normal editor component presentation by default.
- It is not exported into final game builds.
- Engine/editor tools may query it during editor time when they need persistence metadata.

This avoids polluting gameplay runtime components with editor-only asset reference state while still making persistence metadata entity-local and accessible.

### 4. Explicit Component Persistence Contracts

Scene persistence will not inspect arbitrary component fields through reflection.

Instead, the engine will introduce a component persistence contract and registry:

- Each persistible component type registers a descriptor.
- The descriptor owns write and read behavior for that component type.
- The descriptor emits one binary payload per component instance.
- The save system uses a `ComponentTypeId` and stable entity-local component identity to route payloads correctly.

This keeps serialization versionable, debuggable, and safe for components with runtime-only state.

### 5. MeshComponent Persistence Through Stable Asset References

`MeshComponent` is the first built-in component supported by the scene persistence system.

Its save payload does not serialize `RuntimeModel` or `RuntimeMaterial`. Instead it serializes stable asset references for those assignments.

The recommended reference structure is a dedicated value model such as `SceneAssetReference` containing:

- `SourceKind`
- `RelativePath`
- `ProviderId`
- `AssetId`

This supports:

- File-backed assets under `assets`.
- Generated assets exposed through provider ids and generated asset ids.

On load, the persistence descriptor resolves those references back into runtime resources.

### 6. Save Dialog Owned By The Editor

`Save Map As...` will use an editor-owned `SaveFileDialog`, built with the same modal-style UI system as `AssetPickerModal`.

Behavior:

- Rooted inside project `assets`.
- Allows folder navigation.
- Allows entering a file name.
- Forces `.helen` extension.
- Suggests `assets/Scenes` as the initial destination when appropriate.
- Rejects invalid or empty names with explicit UI feedback.

This dialog is project-aware and does not depend on operating-system dialogs.

### 7. Current Scene Tracking In EditorSession

`EditorSession` will track the current scene asset path for the open editor scene.

Behavior:

- New empty scene starts without a scene path.
- `Save Map` saves directly when a current scene path exists.
- `Save Map` falls back to the `Save Map As...` flow when no current scene path exists.
- `Save Map As...` always opens the save dialog.
- Once the user confirms a path, the session records it as the current scene path for future saves.

## Data Flow

### Save Map

1. User activates `Save Map`.
2. `EditorSession` checks whether a current `.helen` path is already assigned.
3. If yes, the session gathers user scene entities and writes a `SceneAsset`.
4. If no, the session opens the editor `SaveFileDialog`.

### Save Map As

1. User activates `Save Map As...`.
2. The editor opens the `SaveFileDialog`.
3. The user chooses a path under `assets`.
4. The editor forces the `.helen` extension.
5. The session writes the `SceneAsset`.
6. The chosen path becomes the current scene path.

### Scene Serialization

1. The scene save service enumerates user-authored root entities.
2. Each entity contributes name, local transform, child hierarchy, and component persistence records.
3. The component persistence registry resolves descriptors for each save-supported component.
4. Each descriptor writes its own payload into the entity persistence record.
5. The final `SceneAsset` is written through `AssetSerializer`.

## Error Handling

The system must fail clearly and avoid partial silent persistence.

Rules:

- If the selected save path is invalid, the dialog stays open and shows an explicit error.
- If a required directory does not exist under `assets`, the editor creates it as part of a valid save flow.
- If a component exists on an entity but no persistence descriptor is registered for that component type, the save fails with a clear error instead of silently dropping it.
- If a persistence descriptor cannot resolve an asset reference while writing or validating save state, the save fails explicitly.
- The current scene path is only updated after a successful write.

## Browser Integration

The asset browser will classify `.helen` files as scene assets so they display consistently in `assets`.

This first design only requires browse-and-recognize behavior for saved scenes. Rich scene-specific inspectors can remain a later follow-up.

## Testing Requirements

The implementation must include coverage for:

1. `SceneAsset` round-trip serialization through the HELE asset pipeline.
2. Scene value-kind registration inside `EditorAssetBinarySerializer`.
3. `.helen` classification in the asset browser.
4. `EntitySaveComponent` default attachment and hidden-editor behavior.
5. Component persistence registry lookup and failure behavior.
6. `MeshComponent` persistence payload round-trip for filesystem-backed model and material references.
7. `MeshComponent` persistence payload round-trip for generated model references.
8. `EditorSession` routing `Save Map` to `Save As` when there is no current scene path.
9. `SaveFileDialog` filename validation and `.helen` extension enforcement.
10. Save flow writing only user-authored entities and excluding editor/internal entities.

## Open Follow-Ups

These are intentionally deferred:

- Full scene load command UX.
- Undo/dirty-state integration for scene changes.
- Scene thumbnails or previews in the asset browser.
- Persistence descriptors for additional built-in components beyond `MeshComponent`.
- User-visible management UI for hidden persistence metadata.
- Export pipeline rules beyond excluding editor-only persistence metadata from game builds.

## Recommendation

Implement scene saving as a proper `SceneAsset` in the existing HELE pipeline, backed by explicit component persistence descriptors and a hidden per-entity save component.

That gives the editor a clean save architecture now, keeps runtime gameplay components free of editor-only persistence fields, and creates a controlled extension point for future built-in and custom component persistence.
