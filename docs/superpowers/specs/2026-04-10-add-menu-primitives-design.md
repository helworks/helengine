# Add Menu Primitives Design

## Summary

This document defines a new `Add` menu in the editor title bar. The menu sits immediately to the right of `File` and creates scene entities directly into the current editor scene.

The first slice includes three commands:

- `Empty`
- `Cube`
- `Plane`

All created entities are root scene entities, start at the world origin, and become the current editor selection immediately after creation.

## Goals

- Add a top-level `Add` menu beside `File` in the editor title bar.
- Create user-authored scene entities directly from title-bar commands.
- Support `Empty`, `Cube`, and `Plane` in the first version.
- Keep scene-creation logic out of `EditorTitleBar`.
- Ensure created primitives are compatible with the current scene save architecture.
- Refresh the scene hierarchy and selection automatically after creation.

## Non-Goals

- No nested `Add` submenus in this phase.
- No dynamic population from the full generated-asset registry.
- No transform placement heuristics beyond spawning at `0,0,0`.
- No drag-to-place or viewport placement flow.
- No generalized "create from any generated asset" workflow in this slice.

## Current Problem

The editor title bar already exposes `File` commands, but there is no equivalent top-level creation menu for common scene objects.

Without an explicit creation flow:

- Users cannot create `Empty`, `Cube`, or `Plane` from the main editor chrome.
- Scene creation logic would be easy to scatter into UI classes if added ad hoc.
- `Cube` and `Plane` cannot be treated as pure UI shortcuts because persisted meshes require stable scene asset references for generated models.

The last point matters because scene saving already serializes `MeshComponent` through `SceneAssetReference` metadata stored on the entity's hidden `EntitySaveComponent`. Primitive creation must populate that metadata up front instead of relying on later repair.

## Proposed Architecture

### 1. Second Top-Level Title Bar Menu

`EditorTitleBar` will gain a second title-bar button named `Add`, positioned immediately to the right of `File`.

Behavior:

- `File` keeps its current commands.
- `Add` opens its own `ContextMenu`.
- Opening `Add` hides `File` if it is visible.
- Opening `File` hides `Add` if it is visible.
- Both menus use the same render and input layering rules already required for title-bar menus.

This keeps the title bar model simple: each top-level menu owns one trigger button and one context menu.

### 2. Explicit Command Events From The Title Bar

`EditorTitleBar` remains a presentation and input-wiring class only.

It should expose explicit events for:

- `AddEmptyRequested`
- `AddCubeRequested`
- `AddPlaneRequested`

The title bar does not create entities itself. It only translates menu clicks into high-level editor commands, matching the same pattern already used by `New Map`, `Save Map`, and `Save Map As...`.

### 3. Scene Creation Owned By An Editor Service

Entity creation should live in a dedicated editor-side service instead of being assembled inline in `EditorSession`.

Recommended shape:

- `EditorSceneCreationService`

Responsibilities:

- Create new root scene entities for editor commands.
- Apply the standard scene-entity defaults.
- Attach any required runtime components.
- Populate persistence metadata on `EntitySaveComponent` when runtime components need stable asset references.

`EditorSession` becomes the coordinator:

- subscribe to title-bar events,
- call the scene-creation service,
- refresh hierarchy,
- update selection.

This keeps UI concerns, orchestration, and scene-object construction separate.

### 4. Empty Entity Creation

`Add > Empty` creates one root `EditorEntity` with:

- `Name = "Empty"`
- `LayerMask = EditorLayerMasks.SceneObjects`
- `LocalPosition = float3.Zero`
- `LocalOrientation = float4.Identity`
- `LocalScale = float3.One`

No visual components are attached.

The entity already carries `EntitySaveComponent` through the normal `EditorEntity` constructor, so no extra persistence work is needed for `Empty`.

### 5. Primitive Entity Creation

`Add > Cube` and `Add > Plane` create one root `EditorEntity` with the same base scene defaults as `Empty`, plus one `MeshComponent`.

Primitive names:

- `Cube`
- `Plane`

Model assignment:

- `Cube` uses `EngineGeneratedModelCache.CubeAssetId`
- `Plane` uses `EngineGeneratedModelCache.PlaneAssetId`

Persistence metadata:

- Primitive creation must write the generated model reference into the entity's `EntitySaveComponent`
- The stored reference uses `SceneAssetReference`
- `SourceKind` identifies a generated asset
- `ProviderId` uses the engine generated-asset provider id
- `AssetId` uses the stable generated model id

This guarantees that `Save Map` works immediately for newly created primitives without waiting for a properties-panel picker flow to populate the metadata later.

### 6. Material Behavior In V1

The first `Add` menu slice will not introduce a new generated material provider.

For `Cube` and `Plane`:

- the primitive mesh gets a generated model reference,
- no explicit material reference is attached in this phase.

This keeps the scope aligned with the request and with the current generated-asset implementation, which already exposes engine-generated models but not engine-generated materials.

This choice is valid with the current scene save contract because `MeshComponent` already treats model and material references as independently optional. If a future renderer-independent primitive material is needed, the follow-up should be a generated material provider plus a stable material reference, not an ad hoc runtime-only material assignment.

### 7. Editor Session Flow

When the user clicks one of the `Add` commands:

1. `EditorTitleBar` raises the corresponding event.
2. `EditorSession` handles the event and calls the scene-creation service.
3. The service creates and returns the new root entity.
4. `EditorSession` refreshes the scene hierarchy.
5. `EditorSession` selects the created entity through `EditorSelectionService`.

Selection after creation is part of the contract, not a best-effort UI extra.

## Data Flow

### Add Empty

1. User opens `Add`.
2. User clicks `Empty`.
3. Title bar raises `AddEmptyRequested`.
4. Editor session asks the creation service for a new empty scene entity.
5. The entity is already registered as a root entity by normal construction.
6. The session refreshes hierarchy and selects the entity.

### Add Cube Or Plane

1. User opens `Add`.
2. User clicks `Cube` or `Plane`.
3. Title bar raises the matching primitive event.
4. Editor session asks the creation service to build the primitive entity.
5. The service:
   creates the root `EditorEntity`,
   adds `MeshComponent`,
   assigns the generated runtime model,
   writes the stable model `SceneAssetReference` into `EntitySaveComponent`.
6. The session refreshes hierarchy and selects the new entity.

## Error Handling

The add flow must fail clearly instead of producing half-created state.

Rules:

- If a generated runtime model cannot be resolved, the command fails and no partially configured primitive remains in the scene.
- If the entity save metadata component cannot be found on the new editor entity, primitive creation fails explicitly.
- If title-bar menu state is inconsistent, opening one top-level menu should still hide the other before showing its own menu.
- On failure, the editor logs a clear error and leaves the current selection unchanged.

## Testing Requirements

The implementation must include coverage for:

1. `EditorTitleBar` layout placing `Add` immediately to the right of `File`.
2. `EditorTitleBar` opening `Add` with `Empty`, `Cube`, and `Plane`.
3. `EditorTitleBar` hiding `File` when `Add` opens, and hiding `Add` when `File` opens.
4. `EditorSession` handling `AddEmptyRequested` by creating a root scene entity at the origin and selecting it.
5. `EditorSession` handling `AddCubeRequested` by creating a root scene entity with a `MeshComponent`, generated runtime model, and generated model save reference.
6. `EditorSession` handling `AddPlaneRequested` with the same guarantees as `Cube`.
7. Newly created primitives saving successfully through the existing scene save flow because their model reference metadata is already attached.

## Open Follow-Ups

These items are intentionally deferred:

- Additional `Add` entries such as lights, cameras, sprites, or more primitives.
- Dynamic menu generation from a broader creation registry.
- Nested menu categories.
- Generated default material references for primitives.
- Viewport-aware placement and drag-to-place workflows.

## Recommendation

Implement `Add` as a second fixed title-bar menu and route creation through a dedicated editor scene-creation service.

That keeps the UI simple, keeps creation logic out of `EditorTitleBar`, and makes `Cube` and `Plane` compatible with the current scene-save architecture from the moment they are created.
