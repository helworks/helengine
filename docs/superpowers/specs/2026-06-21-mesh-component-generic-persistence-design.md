# Mesh Component Generic Persistence Design

Date: 2026-06-21

## Goal

Remove the remaining mesh-specific scene persistence and runtime deserialization paths from shared engine/editor code by moving `MeshComponent` onto the same generic reflected persistence pipeline now used by camera and other generic components.

This migration is intentionally strict:

- `MeshComponent.Material` is removed entirely.
- `MeshComponent.Materials` becomes the only authored material API.
- No legacy scene payload compatibility is kept.
- Existing committed scene assets and tests are migrated to the new contract.

## Current Problem

`MeshComponent` is still a special case in three places:

1. Editor scene persistence uses `MeshComponentPersistenceDescriptor`.
2. Runtime scene loading uses `RuntimeMeshComponentDeserializer`.
3. Scene packaging contains mesh-specific payload rewriting because generic asset-reference handling only understands single asset-backed members, not asset-reference arrays.

That leaves mesh outside the generic engine-side persistence model and keeps shared code coupled to one component-specific serializer contract.

## Desired End State

`MeshComponent` persists through the automatic reflected component pipeline with this authored state only:

- `Model`
- `Materials`
- `RenderOrder3D`

The generic pipeline must support:

- one asset-backed member such as `RuntimeModel Model`
- one-dimensional arrays of asset-backed members such as `RuntimeMaterial[] Materials`
- packaging-time asset-reference rewriting for both single asset-backed members and asset-backed arrays

After the migration:

- mesh-specific editor persistence code is deleted
- mesh-specific runtime deserialization code is deleted
- mesh committed scene assets use the generic payload layout
- runtime generated native deserializers can treat mesh like other generic components if no explicit exclusion remains

## Component Contract Changes

### MeshComponent

`MeshComponent` becomes a straightforward reflected component shape:

- `public RuntimeModel Model { get; set; }`
- `public RuntimeMaterial[] Materials { get; set; }`
- `public byte RenderOrder3D { get; set; }`

`Material` is removed completely.

`Materials` must be a public readable/writable property so reflected persistence can serialize and restore it directly. Internally the component may still clone the assigned array and release prior ownership, but that logic belongs in the property setter instead of an external persistence descriptor.

`SetMaterials(...)` is no longer the authored persistence boundary. If it remains as a helper during transition, it must delegate to the `Materials` property and not define a separate persistence shape.

## Generic Persistence Changes

### Editor Automatic Descriptor

`AutomaticScriptComponentPersistenceDescriptor` already supports:

- leaf value encoding
- nested object/struct encoding
- arrays of generic value types
- single asset-backed members through save-state references

It must be extended so one-dimensional arrays of supported asset-backed member types are serialized through save-state reference arrays.

For `RuntimeMaterial[] Materials`, the descriptor should:

- serialize the array length from the live component value
- resolve each slot against save-state keys:
  - `Materials[0]`
  - `Materials[1]`
  - and so on
- write an optional-reference array payload through the generic descriptor path

On deserialize it should:

- read the optional-reference array
- resolve each reference through the normal asset resolver
- restore the `Materials` property with the resolved runtime array
- repopulate `EntitySaveComponent` reference slots using the same indexed key convention

### Runtime Automatic Deserializer

`AutomaticScriptComponentRuntimeDeserializer` must gain the same array-of-asset-reference support so runtime scene loading can materialize `RuntimeMaterial[]` and `RuntimeModel` from generic payloads without `RuntimeMeshComponentDeserializer`.

The runtime generic deserializer should use the same member-type test as the editor descriptor:

- supported single asset-backed member
- supported one-dimensional array of asset-backed member

## Save-State Naming Contract

The stable save-state naming contract for asset-backed arrays is:

- direct member: `<MemberName>`
- array slot: `<MemberName>[<Index>]`

Examples:

- `Model`
- `Materials[0]`
- `Materials[1]`

This naming rule should live in shared generic persistence helpers, not inside mesh-specific code, because other components may eventually need asset-backed arrays too.

## Scene Packaging Changes

`SceneComponentPackagingTransformService` currently contains a mesh-specific branch because packaging must rewrite authored asset references into packaged runtime references before runtime loading.

That service should be updated so generic reflected packaging can rewrite:

- single asset-backed members
- asset-backed arrays

For mesh this means:

- rewrite `Model` through the model packaging path
- rewrite each `Materials[n]` reference through the material packaging path
- serialize the resulting component back through the automatic descriptor/runtime payload path

Once that generic array-aware rewrite exists, the mesh-specific packaging branch can be removed from shared packaging code.

## Runtime Registry Changes

After generic runtime support exists:

- remove `RuntimeMeshComponentDeserializer`
- remove its registration from `RuntimeComponentRegistry`
- update any generator/source-audit tests that currently expect mesh to remain excluded from generic runtime generation

If generated native runtime deserializers still have exclusions tied to mesh-specific asset-array limitations, those exclusions should be removed as part of this work.

## Migration Strategy

No compatibility path is kept.

Migration is immediate:

1. Update `MeshComponent` public API.
2. Extend generic editor/runtime persistence for asset-backed arrays.
3. Extend generic packaging rewrite for asset-backed arrays.
4. Remove mesh-specific descriptor/runtime classes and registrations.
5. Update tests and committed scene assets to the new generic payload format.

Committed `.helen` scenes that contain mesh records must be rewritten in-repo, the same way camera scenes were migrated.

## Error Handling

The generic contract should remain strict.

- If a non-null runtime asset exists but the corresponding save-state reference key is missing, serialization throws.
- If an asset-backed array contains a non-null slot with no saved reference, serialization throws.
- If a payload declares an invalid negative array length, deserialization throws.
- If a reflected array member is multidimensional or not one-dimensional, automatic persistence does not support it.

No best-effort fallback or mesh-specific rescue path is added.

## Verification

Focused verification should cover:

1. `MeshComponent` generic editor persistence round-trip for:
   - model reference only
   - material array with multiple slots
   - generated references
   - filesystem references
2. Runtime generic deserialization of mesh payloads.
3. Scene packaging rewrite of generic mesh references.
4. Rendering/scene catalog tests for committed scenes containing mesh records.
5. Generator/runtime-registry tests proving mesh no longer depends on explicit runtime deserializers.

At minimum, the updated focused suite should include:

- `MeshComponentPersistenceDescriptorTests` replacement coverage in generic tests
- `SceneSaveServiceTests`
- `SceneFileLoadServiceTests`
- `RuntimeSceneLoadServiceTests`
- `EditorGeneratedCoreRegenerationServiceTests`
- any packager tests covering mesh payload rewriting
- any committed-scene tests exercising mesh scene content

## Non-Goals

This migration does not attempt to:

- preserve old mesh payload compatibility
- keep `Material` as an alias
- redesign render-time submesh behavior
- redesign model/material import or asset authoring workflows beyond what is needed for generic persistence

## Recommendation

Proceed with the full generic migration.

Keeping mesh-specific serializer or packaging branches would preserve the exact kind of shared-engine exception this cleanup is trying to remove. The correct long-term fix is to teach the generic persistence pipeline about asset-backed arrays and let mesh use that engine-side capability directly.
