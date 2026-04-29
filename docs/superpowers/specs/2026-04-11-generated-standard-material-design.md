# Generated Standard Material Design

## Summary

This document defines the first generated material shipped by the engine asset system. New primitive scene objects created by the editor should no longer spawn with only a generated model. They should also receive a generated default material so they render visibly without requiring immediate manual setup.

The first generated material is `Engine/Materials/Standard`. It is a virtual engine asset, appears in browse and pick flows, resolves to one cached runtime material instance, and can be persisted through `.helen` scene save/load using the same generated-asset reference model already used for generated models.

## Goals

- Make new primitive scene objects render with a default material automatically.
- Expose the default material as a generated engine asset in the asset browser.
- Allow generated materials to be selected from material-picking flows.
- Persist generated material references in `.helen` scene files.
- Load generated material references back into runtime materials during scene open.
- Reuse existing built-in shader infrastructure instead of introducing a new shader for v1.

## Non-Goals

- No retroactive fix-up pass for existing scene entities that already have missing materials.
- No automatic default material injection for arbitrary `MeshComponent` construction outside the editor primitive-creation flow.
- No new user-authored `.helmat` template defaults in this slice.
- No second generated material family yet beyond `Engine/Materials/Standard`.
- No generalized generated-texture or generated-shader provider work in this slice.

## Current Problem

Primitive creation in `EditorSceneCreationService` currently assigns only a generated runtime model. `Cube` and `Plane` therefore depend on whatever fallback behavior the renderer has for missing materials, which is not the behavior we want in the editor.

At the same time, generated assets are currently asymmetric:

- generated models can be browsed and picked
- generated materials do not exist as engine assets yet
- generated material references cannot be resolved by scene load
- material pickers only work with filesystem `.helmat` assets

That leaves primitive creation incomplete and makes generated material workflows impossible to persist cleanly.

## Proposed Architecture

### 1. Generated Material As A First-Class Engine Asset

The engine asset provider system should treat materials the same way it already treats generated models.

The first generated material is:

- `Engine/Materials/Standard`

This entry is virtual, browseable, and provider-backed. It should appear under the same `Engine` virtual root that already hosts generated models.

Updated engine virtual tree:

- `Engine`
- `Engine/Models`
- `Engine/Models/Cube`
- `Engine/Models/Plane`
- `Engine/Materials`
- `Engine/Materials/Standard`

This keeps generated materials in the same conceptual asset system instead of hiding them behind editor-only special cases.

### 2. One Cached Runtime Material For `Standard`

The engine should build `Standard` once and reuse it afterward through a dedicated cache, mirroring the pattern already used by generated models.

Recommended type:

- `EngineGeneratedMaterialCache`

Responsibilities:

- expose a stable generated material asset id for `Standard`
- lazily build the runtime material the first time it is requested
- reuse the same runtime material instance on subsequent requests

`Standard` should be built from the existing built-in shader [EditorDefaultMesh.hlsl](/F:/dev/helengine/engine/helengine.editor/shaders/builtin/EditorDefaultMesh.hlsl). No new shader asset is required for v1.

The generated `MaterialAsset` used for runtime construction should map to that built-in shader in the same way the editor already constructs built-in runtime materials elsewhere in `EditorSession`.

### 3. Generated Asset Provider Contract Extends To Materials

The generated asset provider interface currently resolves only runtime models. That is now too narrow for the asset system the editor is growing toward.

`IGeneratedAssetProvider` should gain one material-resolution method alongside its existing model-resolution method.

Recommended addition:

- `bool TryResolveRuntimeMaterial(AssetBrowserEntry entry, out RuntimeMaterial runtimeMaterial)`

`GeneratedAssetProviderRegistry` should gain a parallel `ResolveRuntimeMaterial(...)` method with the same validation style it already uses for models.

This keeps material resolution provider-driven and avoids introducing a second, ad hoc generated-material path somewhere else in the editor.

### 4. Engine Provider Publishes `Materials/Standard`

`EngineGeneratedAssetProvider` should publish the new virtual `Materials` directory and the `Standard` material entry.

Expected browse behavior:

- empty relative path returns `Engine`
- `Engine` returns `Models` and `Materials`
- `Engine/Models` returns `Cube` and `Plane`
- `Engine/Materials` returns `Standard`

Expected resolution behavior:

- generated model entries continue to resolve through `EngineGeneratedModelCache`
- generated material entries resolve through `EngineGeneratedMaterialCache`

The stable provider id remains `engine`.

### 5. Primitive Creation Uses Both Generated References

`EditorSceneCreationService` should assign both model and material when creating primitives.

`CreateCube()` and `CreatePlane()` should:

- resolve the generated runtime model
- resolve the generated runtime material
- create `MeshComponent` with both fields populated
- persist both generated references into `EntitySaveComponent`

`Empty` remains unchanged and should not grow a mesh or material.

This keeps primitive creation explicit and ensures new objects are immediately visible in the viewport without extra editor actions.

### 6. Scene Persistence Supports Generated Materials

The scene save/load system already persists material references structurally through `MeshComponentPersistenceDescriptor`. The missing piece is resolution of generated material references during load.

`EditorSceneAssetReferenceResolver` should stop rejecting generated materials and instead route them through `GeneratedAssetProviderRegistry.ResolveRuntimeMaterial(...)`.

Generated material references should use the same stable shape already established for generated models:

- `SourceKind = Generated`
- `ProviderId = "engine"`
- `RelativePath = "Engine/Materials/Standard"`
- `AssetId = <stable standard material id>`

That allows `.helen` round-tripping without inventing a second persistence format for materials.

### 7. Material Picking Accepts Generated Assets

Material selection should not remain filesystem-only once the engine exposes generated materials as real browseable assets.

`ComponentPropertiesView` should accept generated material entries from the picker when the entry kind is `Material`.

Material picker behavior after the change:

- filesystem `.helmat` entries still load as before
- generated material entries resolve through `GeneratedAssetProviderRegistry.ResolveRuntimeMaterial(...)`
- when a generated material is chosen, the same `SceneAssetReference` persistence metadata is written into `EntitySaveComponent`

This keeps the picker behavior aligned with the asset browser contract: if the asset can be browsed, it can be picked.

## Data Flow

### Primitive Creation

1. User selects `Add > Cube` or `Add > Plane`.
2. `EditorSession` routes the command to `EditorSceneCreationService`.
3. The service resolves the generated runtime model.
4. The service resolves the generated `Standard` runtime material.
5. The service creates the entity and attaches `MeshComponent` with both references.
6. The service records generated model and material references in `EntitySaveComponent`.
7. The session refreshes the hierarchy and selects the new entity.

### Material Pick

1. User opens the material picker for a `MeshComponent`.
2. The picker browses assets, including `Engine/Materials/Standard`.
3. User selects `Standard`.
4. `ComponentPropertiesView` resolves the generated runtime material.
5. The row updates the live `MeshComponent.Material`.
6. The generated material reference is stored in the entity save metadata.
7. The scene is marked dirty.

### Save And Load

1. Scene save serializes the `MeshComponent` reference metadata already stored in the hidden save component.
2. `.helen` contains the generated material reference record.
3. Scene load deserializes the `MeshComponent` payload.
4. `EditorSceneAssetReferenceResolver` resolves the generated material through the generated asset registry.
5. The loaded `MeshComponent` is rebuilt with the cached runtime material.

## Error Handling

Rules:

- If the generated material provider is missing, material resolution should fail clearly instead of silently constructing a fallback.
- If the `Standard` material cannot be built from the built-in shader, primitive creation should fail and clean up the partially created entity, matching the existing primitive-creation failure behavior.
- If a generated material reference is encountered during scene load but cannot be resolved, scene load should fail through the existing scene-load error path.
- Material picker should report generated material resolution failures the same way it reports filesystem material load failures.

This keeps failure behavior explicit and consistent with the rest of the editor.

## Testing Requirements

Implementation must include coverage for:

1. `EngineGeneratedAssetProvider` exposes `Engine/Materials` and `Engine/Materials/Standard`.
2. `GeneratedAssetProviderRegistry` resolves generated runtime materials.
3. `EditorSceneCreationService` creates `Cube` and `Plane` with both generated model and generated standard material references.
4. `EditorSceneCreationService` keeps `Empty` unchanged.
5. `ComponentPropertiesView` accepts a generated material pick and persists the generated reference.
6. `EditorSceneAssetReferenceResolver` resolves generated materials during scene load.
7. `.helen` round-trip preserves a generated material reference on `MeshComponent`.
8. The existing editor-session primitive-creation flow still yields visible, selectable new primitives.

## Recommendation

Implement `Engine/Materials/Standard` as the first generated material asset, backed by one cached runtime material built from [EditorDefaultMesh.hlsl](/F:/dev/helengine/engine/helengine.editor/shaders/builtin/EditorDefaultMesh.hlsl).

That solves the immediate visibility problem for new primitives and extends the generated asset system in the right direction: generated materials become browseable, pickable, and persistable first-class assets instead of editor-only hidden defaults.
