# Mesh Component Tessellation Design

## Goal

Add an editor-only, per-platform MeshComponent tessellation override that cooks scale-aware model variants for static scene instances. This complements the existing per-platform model import tessellation: imported assets can establish shared base geometry, while selected scaled components can receive additional refinement without changing runtime rendering code.

## Settings

MeshComponent exposes these editor-only per-platform settings through the existing platform override system:

- `Tessellate: bool = false`
- `TessellationMaxEdgeLength: double = 1.0d`

The edge length is expressed in final world-space units. The settings are serialized, cloned, compared, and displayed independently for every target platform. They do not become runtime MeshComponent fields.

## Cook pipeline

1. Model import applies the selected platform's model-import tessellation setting once to its shared source `ModelAsset`.
2. Scene cooking resolves each MeshComponent's final static world scale, including every parent scale.
3. For a component whose `Tessellate` setting is enabled, scene cooking requests a derived cooked model variant from that platform-processed source model.
4. The tessellator measures every candidate edge after multiplying its local-space delta by the resolved world-scale vector. It keeps positions, UVs, normals, and indices in local space, so the existing runtime transform path remains correct.
5. The cooked scene changes only that component's model reference to the variant. Components with tessellation disabled retain the source model reference.

Runtime or animated scale is intentionally out of scope. The variant reflects the authored static hierarchy at cook time.

## Variant identity and reuse

Variants are cached within the scene/package cook using an invariant-culture identity containing:

- Platform identifier.
- Platform-processed source model asset identifier.
- Component tessellation enabled state.
- World-space maximum edge length.
- Resolved world scale X, Y, and Z.

Components with equal identities reuse exactly one variant. Different scales or thresholds create separate variants. The scene cooker must give each generated variant a stable generated asset identifier and include it in normal packaged-model output.

## Geometry and failure behavior

The existing conforming edge-aware subdivision algorithm is reused with scale-aware distance calculation. It preserves seams, material submeshes, winding, local-space attributes, index-width promotion, and the 1,000,000-triangle cap.

Cooking fails clearly for non-finite or zero world-scale components, invalid thresholds, missing source models, malformed geometry, and output over the triangle cap. It never silently disables component tessellation or substitutes a default model.

## Editor UI

The MeshComponent inspector presents the toggle and, when enabled, the maximum-edge-length field under each platform override. Invalid numeric input is rejected before it becomes pending component settings. The model import inspector remains unchanged except for its already designed model-level controls.

## Validation

Automated coverage must prove:

- Per-platform component settings round-trip, clone, compare, and display correctly.
- Static local and parent scale are composed into world-scale edge measurement.
- Non-uniform scale generates enough local-space subdivision for the largest scaled edge.
- Disabled components preserve their original model reference.
- Equal component requests share a generated variant; changed model, scale, platform, or threshold does not.
- Model-import tessellation runs before component refinement.
- UV seams, normals, winding, material submeshes, and index representation remain valid.
- PS2 and PSP builders consume generated variants through their existing model paths with no renderer changes.
