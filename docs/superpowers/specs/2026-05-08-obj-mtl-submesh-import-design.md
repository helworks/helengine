# OBJ/MTL Submesh Import Design

## Summary

The engine currently treats one `MeshComponent` as one `RuntimeModel` plus one `RuntimeMaterial`. The existing import path flattens imported geometry into a single `ModelAsset` and discards mesh and material boundaries. Full OBJ support with multiple materials therefore requires an engine-wide submesh model, not only a new importer feature.

This design adds first-class submesh support across asset import, runtime model resources, mesh components, scene persistence, packaging, and rendering. OBJ import will preserve mesh and material boundaries, resolve the referenced `.mtl` file, and generate deterministic `.helmat` material assets that the existing material pipeline can load, edit, package, and render.

## Goals

- Import `.obj` files that reference `.mtl` files without losing material assignments.
- Preserve multiple submeshes from the authored source model.
- Allow one `MeshComponent` to bind one material per submesh.
- Generate normal authored `.helmat` assets from `.mtl` definitions so they participate in the existing material editor and packaging flow.
- Keep existing single-material models and scenes working without migration work from users.

## Non-Goals

- Full physically based translation of every MTL field on the first pass.
- Automatic scene restructuring that splits one OBJ into many entities.
- A second cached-only material format dedicated to imported OBJ materials.
- Per-submesh editor UI tooling beyond the minimum needed to store and reload material slots.

## Current Constraints

### Runtime drawing

`MeshComponent` currently exposes one `RuntimeModel` and one `RuntimeMaterial`. `IDrawable3D` mirrors that same shape, so the render path can only issue one materialized draw per component.

### Model assets

`ModelAsset` currently stores one flattened vertex/index payload with no submesh ranges, no material slot metadata, and no preserved mesh grouping from the source asset.

### Scene persistence

Editor and player scene persistence currently serialize one model reference and one material reference for `MeshComponent`. There is no persistent representation for material slots or submesh-level bindings.

### Import pipeline

The Assimp-backed importer currently returns only `ModelAsset` and the Assimp scene converter intentionally flattens mesh boundaries away. Material information from OBJ/MTL is not represented in imported output.

## Recommended Approach

The engine should adopt first-class submesh support and then update OBJ/MTL import to target that model.

The alternative approaches are weaker:

- Splitting imported OBJ files into many entities would leak importer behavior into scene structure and still would not give the engine a real submesh concept.
- Emitting cached pseudo-materials instead of `.helmat` assets would bypass the existing material tooling and packaging rules.

## Architecture

### 1. Model asset and runtime model changes

Add a new raw asset type to represent submesh layout inside `ModelAsset`.

Each submesh record should contain:

- `IndexStart`
- `IndexCount`
- `MaterialSlotIndex`
- `MaterialSlotName`

The index and vertex buffers remain shared across the full model. Submeshes describe draw ranges into those shared buffers.

`RuntimeModel` should expose submesh metadata to the renderers instead of remaining an empty marker base class. Backend resources such as `DirectX11ModelResource` and `VulkanModelResource` should preserve the imported submesh ranges after upload.

### 2. Mesh component and drawable contract changes

`MeshComponent` should move from one `Material` property to an ordered material-slot collection whose indices match the runtime model submesh list.

The minimal required behavior is:

- one model reference
- zero or more material slot bindings
- fallback behavior when a slot has no assigned material

`IDrawable3D` should expose the material collection or an equivalent slot-resolution API so render extraction can iterate submeshes and select the correct material for each draw.

### 3. Rendering changes

Both current renderers should issue one draw per visible submesh instead of one draw per mesh component.

For each submesh draw:

- bind the shared model buffers
- use the submesh index range
- resolve the slot material
- fall back to the existing missing material when the slot is unassigned

This keeps one scene component, one runtime model, and many draw submissions, which matches the authored shape without forcing scene duplication.

### 4. Scene persistence changes

Editor and runtime mesh serialization should be versioned forward to support:

- one model reference
- an ordered list of material references
- render order

Backward compatibility is required. Existing payloads with one material reference should deserialize into a single-slot binding. When a legacy scene loads a model that now exposes more than one submesh, slot zero should receive the authored material and the remaining slots should use fallback material behavior until explicitly assigned.

### 5. OBJ and MTL import changes

The Assimp importer should preserve imported mesh boundaries and material assignments when converting to `ModelAsset`.

The OBJ import flow should:

- load the OBJ through Assimp
- preserve submesh ordering from the imported scene
- map each imported mesh or face group to one submesh record
- resolve the referenced `.mtl` file
- generate deterministic `.helmat` assets for the materials declared in that `.mtl`

The first-pass MTL-to-material mapping should support:

- material name to `.helmat` file name
- `map_Kd` to `DiffuseTextureAssetId`
- default shader/material compatibility fields needed by current DirectX11 and Vulkan renderers

Additional MTL fields such as normal-map conventions, emissive mappings, opacity, or illumination model translation can be layered on later without changing the structural design.

### 6. Generated material asset rules

Generated `.helmat` assets should be written as normal file-backed assets adjacent to the source model in a deterministic folder or naming scheme.

Required properties:

- stable regenerated file path per source material name
- deterministic material asset id
- normal material-sidecar settings support
- idempotent regeneration when reimporting the OBJ

The generated asset rule must not create duplicate materials on every import. Reimport should update the same generated `.helmat` asset path.

### 7. Packaging and runtime asset resolution

Build packaging must include every material reference used by mesh slots, not only one top-level mesh material reference.

The packaging transforms that currently rewrite one `MeshComponent` material reference must be extended to rewrite and include:

- the model reference
- every slot material reference

Runtime scene asset resolution remains file-backed for both model and material assets, but mesh deserialization now needs to resolve a list of material references instead of only one.

## Data Model

### ModelAsset

`ModelAsset` will continue to own:

- positions
- normals
- texture coordinates
- 16-bit or 32-bit index buffers

It will additionally own:

- ordered submesh records

### MeshComponent

`MeshComponent` will continue to own:

- one `RuntimeModel`
- render order

It will change to own:

- ordered submesh material bindings

### Scene payload compatibility

Legacy mesh payloads:

- deserialize as one material slot

New mesh payloads:

- deserialize as explicit material slot arrays

## Error Handling

- OBJ import should fail when the source contains no meshes or invalid face topology after Assimp processing.
- Missing referenced `.mtl` files should be a surfaced import error, not silently ignored, when the OBJ declares one.
- Missing texture files referenced by `.mtl` should not block generating the `.helmat` asset itself; the generated material can omit that texture binding while logging the missing source texture explicitly.
- Material-slot count mismatches between a model and serialized scene data should not crash scene load. Extra scene bindings should be ignored after logging. Missing bindings should fall back to the missing material.

## Testing Strategy

### Import tests

- OBJ with one material still imports correctly.
- OBJ with multiple materials produces one `ModelAsset` with multiple submeshes.
- OBJ with multiple meshes that share materials preserves the expected submesh ordering and slot mapping.
- OBJ referencing `.mtl` generates deterministic `.helmat` assets and reimport updates them in place.

### Runtime and renderer tests

- Runtime model resources preserve submesh ranges after upload.
- A mesh component with multiple material slots produces one draw per submesh.
- Missing slot materials fall back to the current missing-material behavior.

### Scene persistence tests

- New-format mesh components round-trip material slot references.
- Old-format mesh component payloads still deserialize.

### Packaging tests

- Build packaging rewrites and includes every material referenced by mesh slots.
- Runtime startup can load packaged scenes containing multi-submesh models and multiple file-backed materials.

## Rollout Order

1. Add submesh structures to raw and runtime model types.
2. Update renderers and drawable contracts to render per submesh.
3. Update `MeshComponent` and scene persistence to store material slots.
4. Update packaging and runtime scene resolution for multi-slot material references.
5. Update the Assimp OBJ pipeline to preserve submeshes and generate `.helmat` assets from `.mtl`.
6. Add regression coverage for import, scene load/save, and packaging.

## Risks

- This change crosses the importer, editor, runtime, and renderer layers, so partial implementation will leave the engine in an inconsistent state.
- Scene backward compatibility must be handled deliberately because current mesh payloads assume a single material.
- Renderer draw-loop changes may expose assumptions elsewhere in culling, render ordering, or debug tooling that currently treat one mesh component as one draw.

## Recommendation

Proceed with the full-stack change. Any attempt to support multi-material OBJ files without first-class submeshes will either distort authored scene structure or create a parallel material/import path that the rest of the engine does not understand.
