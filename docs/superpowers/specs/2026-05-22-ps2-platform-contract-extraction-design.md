# PS2 Platform Contract Extraction Design

## Summary

Helengine currently leaks PlayStation 2 runtime concepts into `helengine.core` and `helengine.editor`. The leak is not limited to build metadata. The main repository currently owns PS2 cooked asset payload classes, PS2 payload enums, and a PS2-packed model field on the generic `ModelAsset` type.

That ownership is wrong. The main repository should stay platform-generic. The external PS2 repository should own PS2 cooked runtime contracts and the PS2 player should load PS2-owned payload formats directly.

This design moves Helengine toward a metadata-only external platform plugin model:

- `helengine` owns only generic authoring, runtime, and build-graph concepts.
- external platform repositories expose only generic platform metadata and build entrypoints through a plugin-style manifest.
- `helengine-ps2` owns every PS2-specific cooked payload schema and native runtime loader.
- the editor never deserializes PS2 cooked runtime assets.

## Problem

The current implementation couples generic engine/editor code to PS2-only cooked payload details:

- `Ps2MaterialAsset` lives in `helengine.core`.
- `Ps2TextureAsset` lives in `helengine.core`.
- PS2 payload enums like `Ps2MaterialLightingMode`, `Ps2MaterialAlphaMode`, `Ps2RenderClass`, `Ps2TextureFormat`, and `Ps2TextureAlphaMode` live in `helengine.core`.
- `ModelAsset` exposes `Ps2PackedMeshBytes` even though that field is meaningless outside PS2.
- generic binary serialization layers in `helengine.core` and `helengine.files` know how to read and write PS2 cooked payload types.
- `helengine.editor` tests and package flows observe PS2 cooked asset classes directly.

This is a bad boundary because it means adding or changing PS2 runtime format details requires changes in the main generic engine repository.

## Goals

- Remove PS2 cooked asset ownership from the main Helengine repository.
- Keep the editor platform system generic.
- Let external platform repositories integrate through a plugin-style manifest without adding platform-specific engine concepts.
- Keep PS2 runtime payloads fully owned by `helengine-ps2`.
- Preserve current PS2 build capability while changing the ownership boundary.

## Non-Goals

- This design does not define a generalized plugin sandbox or marketplace system.
- This design does not redesign every existing external platform integration in one pass.
- This design does not change the authored project asset model unless required to remove PS2 leakage.
- This design does not require non-PS2 platforms to adopt PS2 runtime payload conventions.

## Recommended Approach

Use a metadata-only external platform plugin contract.

The external platform manifest should expose:

- platform identity
- build profiles
- graphics profile definitions
- generic material schema metadata
- asset requirement metadata
- generic build entrypoints

The external platform manifest should not expose:

- platform-specific asset payload classes
- platform-specific editor serializers
- platform-specific runtime-only enums that have no generic meaning
- platform-specific extensions to `helengine.core` asset objects

The editor should treat the external platform builder as a generic service that consumes source assets plus generic cook requests and produces opaque platform-owned outputs.

## Architecture

### Main Repository Responsibilities

The main `helengine` repository remains responsible for:

- generic engine asset types
- generic scene/runtime authoring
- generic build graph orchestration
- generic platform metadata consumption
- generic material schema UI and validation
- generic artifact packaging interfaces

The main repository must not own:

- PS2 cooked material payload classes
- PS2 cooked texture payload classes
- PS2-specific packed model fields on generic asset types
- PS2-only binary serializer branches

### PS2 Repository Responsibilities

`helengine-ps2` becomes responsible for:

- the `helengine.ps2` managed project
- the PS2 plugin manifest
- PS2 material cook logic
- PS2 texture cook logic
- PS2 model packing logic
- PS2 runtime/native payload readers
- PS2 disc layout and native build integration

The PS2 repository owns both sides of the PS2 cooked contract:

- managed cook-time payload writing
- native runtime payload reading

That keeps PS2 format changes local to the PS2 repository.

## Plugin Manifest Boundary

The plugin manifest must stay generic. It should describe what the editor can ask the platform to do, not how the platform stores its runtime payloads.

Required categories:

- platform id and display name
- supported build profiles
- supported graphics profiles
- generic platform setting definitions
- generic material schema definitions
- asset requirement declarations
- builder assembly entrypoint

Forbidden categories:

- declarations of runtime payload CLR types
- declarations of platform-owned binary layouts
- declarations of platform-specific serializer hooks into `helengine.core`
- declarations that mutate generic engine asset classes

## Runtime and Build Data Flow

### Before

Current PS2 flow:

- the editor and core understand PS2 cooked asset classes
- the PS2 builder writes those classes using shared engine serializers
- the PS2 runtime deserializes those classes from assets packaged by the main repository

### After

Target PS2 flow:

1. the editor loads generic platform metadata from the external manifest
2. the editor emits generic build and cook requests
3. the PS2 builder consumes generic requests and source assets
4. the PS2 builder writes PS2-owned cooked outputs
5. the PS2 runtime loads those cooked outputs directly using PS2-owned code

The key rule is:

- `helengine` must never deserialize PS2 cooked runtime payloads

## Contract Changes

### PS2 Material and Texture Payloads

`Ps2MaterialAsset` and `Ps2TextureAsset` leave `helengine.core`.

They move into the new `helengine.ps2` managed project inside the PS2 repository, or they are replaced by PS2-owned opaque file formats produced there. Either way, the main repository no longer defines them.

### PS2 Model Packing

`ModelAsset.Ps2PackedMeshBytes` must be removed from the generic model type.

The recommended replacement is a PS2-owned sidecar cooked model payload produced during PS2 build staging. The PS2 runtime should load that sidecar instead of reading PS2-packed bytes from a generic `ModelAsset`.

This is the cleanest way to remove the strongest PS2 leak from `helengine.core`.

### Serialization

`EditorAssetBinaryValueKind`, `EditorAssetBinarySerializer`, and `helengine.files` serialization code must lose PS2-specific value kinds and read/write branches.

If PS2 still needs managed serialization in its own repo, that serializer belongs there and should operate on PS2-owned payloads without expanding the generic engine serializer surface.

## Error Handling

The editor should fail at the plugin boundary, not inside generic asset deserialization.

Examples:

- missing PS2 plugin manifest should fail platform discovery
- invalid PS2 plugin metadata should fail platform registration
- PS2 build output contract violations should fail the build stage that consumes those outputs
- missing PS2 cooked payload files should fail in the PS2 runtime loader

The editor should not try to recover from PS2 payload mismatches by fabricating generic assets.

## Migration Strategy

### Phase 1: External Plugin Boundary

- formalize the metadata-only plugin manifest shape
- load PS2 platform metadata from the external repo through that shape
- ensure the editor depends only on generic platform metadata and build entrypoints

### Phase 2: PS2 Managed Contract Ownership

- create `helengine.ps2` inside `helengine-ps2`
- move PS2 cooked material and texture contract code into that project
- move PS2 cook-time serializer code into the PS2 repository if still needed

### Phase 3: Remove Generic Asset Leaks

- remove `Ps2MaterialAsset` from the main repository
- remove `Ps2TextureAsset` from the main repository
- remove PS2 payload enums from the main repository
- remove PS2 serializer branches from generic serializers

### Phase 4: Remove `Ps2PackedMeshBytes`

- replace `ModelAsset.Ps2PackedMeshBytes` with a PS2-owned sidecar cooked model payload
- update the PS2 builder and runtime to use that sidecar
- remove the PS2 field from `ModelAsset`

### Phase 5: Verification and Cleanup

- update tests in both repositories to enforce the new ownership boundary
- confirm PS2 builds still produce valid staged assets and bootable output
- confirm the main repository no longer contains PS2-specific cooked runtime contracts

## Testing Strategy

Main repository verification should focus on:

- plugin manifest discovery and validation
- generic build-graph execution against external platform metadata
- absence of PS2-specific serializer branches and asset types
- generic material schema behavior remaining intact

PS2 repository verification should focus on:

- PS2 cook outputs for materials, textures, and model sidecars
- PS2 runtime loading of cooked payloads
- staged disc layout correctness
- native boot and scene load behavior

## Tradeoffs

### Benefits

- PS2 format evolution stops polluting the generic engine repository.
- The editor remains generic and does not grow hidden platform-type seams.
- PS2 runtime payload changes become local to the PS2 repository.
- Future external platforms can follow the same boundary.

### Costs

- The platform plugin contract needs to become more formal.
- PS2 build integration becomes stricter because the main repo can no longer inspect PS2 payload internals.
- `Ps2PackedMeshBytes` removal requires a real sidecar model-payload migration rather than a small rename.

## Open Questions Resolved By This Design

- Should external platforms contribute platform-specific editor/runtime types into `helengine`?
  - No.
- Should the editor remain generic and consume only metadata plus build services?
  - Yes.
- Should PS2 cooked payload ownership stay in the PS2 repository?
  - Yes.
- Should `ModelAsset` keep a PS2-specific packed-mesh field?
  - No.

## Outcome

After this migration, the main Helengine repository no longer contains PS2-specific cooked runtime asset contracts. The editor remains generic. The PS2 repository owns PS2 runtime payloads end to end, and the PS2 player loads PS2-owned data instead of generic engine assets with embedded PS2 details.
