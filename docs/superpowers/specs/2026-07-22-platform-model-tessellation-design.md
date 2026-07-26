# Platform Model Tessellation Design

## Goal

Allow an imported model to be tessellated at import/cook time with independent settings for every target platform. The feature reduces the size of rendered triangles for targets such as PS2 and PSP that benefit from finer culling granularity, while leaving source files, scene components, and runtime code unchanged.

## Scope

This feature belongs to imported model assets, not `MeshComponent`.

- Each model import settings document exposes its existing platform tabs.
- Each platform can enable or disable tessellation independently.
- Each platform can select its own maximum local-space edge length.
- The active platform imports and caches one processed `ModelAsset`; every MeshComponent that references that model shares the same result.
- Procedural runtime models and source models without model import settings are unchanged.
- Tessellation runs before existing winding processing and before platform builders create target-specific packed assets.

## Settings and editor UI

`ModelAssetProcessorSettings` gains these properties:

- `Tessellate`: `bool`, default `false`.
- `TessellationMaxEdgeLength`: `double`, default `1.0`.

The asset import settings panel adds a `Tessellate` checkbox to the model processor section. When checked, it shows a `Tessellation Max Edge Length` numeric input. The input accepts only finite values greater than zero. The controls read and edit the selected platform's `ModelAssetProcessorSettings`, matching the current `Flip Winding` behavior.

The model import settings serializer advances from version 1 to version 2. Version 2 writes the existing winding flag followed by the tessellation flag and edge-length value for each platform. Version 1 remains readable and resolves to `Tessellate = false` and `TessellationMaxEdgeLength = 1.0`.

All cloning, equality, settings-section persistence, and cache-identity paths include both new values. The cache identity includes source checksum, importer id, active platform id, winding setting, tessellation setting, and the invariant-culture edge-length representation. Changing any of those values creates a distinct processed model variant.

## Processing architecture

`ModelAssetProcessor.Apply` remains the entry point for platform-specific model processing. It delegates tessellation to a dedicated tessellation service, then performs the existing winding flip.

The tessellation service receives one freshly imported `ModelAsset` and a validated maximum edge length. It returns a replacement mesh payload rather than changing the source import result. The processor assigns that payload to the platform-specific imported model instance.

The service owns only topology and vertex-data transformation. Model import settings, cache keys, UI binding, binary serialization, and target-specific PS2/PSP packing remain in their existing layers. Platform builders continue to receive an ordinary processed `ModelAsset` and require no tessellation-specific runtime support.

## Tessellation algorithm

The implementation uses conforming adaptive longest-edge bisection in model-local space.

1. Validate that the model has indexed triangle topology, a valid position array, index values in range, index count divisible by three, and optional normal/UV arrays either empty or position-aligned.
2. Build a topology map for geometric edges. Edge identity is based on endpoint positions so duplicated seam vertices follow the same geometric split pattern. Attribute-distinct seam vertices remain distinct in the output.
3. Queue every triangle with an edge longer than `TessellationMaxEdgeLength`.
4. Split the longest queued edge at its midpoint. Reuse an existing midpoint for shared attribute-compatible edges. Interpolate position and UV linearly; interpolate and normalize normals when normals are present.
5. Propagate each split to all incident triangles through the topology map. Continue refinement until every emitted edge is at or below the threshold. This prevents T-junctions and cracks, including across material boundaries and duplicated UV seams.
6. Preserve each emitted triangle's original material slot. After refinement, regroup triangles by original submesh order and rebuild `ModelSubmeshAsset` ranges without changing material-slot names.
7. Preserve the source index width when possible. Promote to 32-bit indices when the generated vertex count exceeds 16-bit capacity; target builders retain responsibility for rejecting index formats they cannot support.

The processor uses `double` for edge-length and interpolation math, converting generated positions, normals, and UVs back to their existing float-based asset representation.

## Limits and failures

The service fails the import/cook with an actionable exception when the threshold is non-finite or not greater than zero, the input model is malformed, or adaptive refinement would emit more than 1,000,000 triangles. The limit prevents a tiny threshold from exhausting editor memory; the error tells the user to increase the platform edge length or simplify the source model.

Tessellation is never silently skipped. Disabled tessellation returns the imported topology unchanged. Existing model-import settings that predate version 2 remain valid and disabled by default.

## Verification

Tests cover the following behavior:

- Version 1 settings deserialize to disabled tessellation and edge length `1.0`; version 2 settings round-trip both values for multiple platforms.
- UI platform selection edits only that platform's settings, toggles the numeric input visibility, rejects invalid edge lengths, and preserves values through Save.
- Processor output for a long single triangle contains smaller triangles whose every edge is within the configured limit.
- Adjacent triangles and duplicated UV seams receive matching geometric edge splits with no T-junctions.
- Position, UV, and normal interpolation is correct; normals remain normalized.
- Multiple submeshes retain their material-slot names and valid contiguous index ranges after tessellation.
- Disabled tessellation produces the original topology, while a changed platform toggle or edge length produces a different processed-model cache identity.
- Existing PS2 and PSP model builder tests consume the tessellated `ModelAsset` without a runtime renderer change.

## Acceptance criteria

An artist can open a model asset's import settings, choose the PS2 tab, enable tessellation, and set a smaller edge length without changing the Windows tab. A PS2 build then receives a packed model with conformingly tessellated topology; a Windows build of the same project keeps its original model when Windows tessellation remains disabled. No `MeshComponent` member, scene payload, or runtime platform code is added for this feature.
