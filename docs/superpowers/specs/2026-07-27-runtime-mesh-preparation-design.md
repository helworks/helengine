# Runtime Mesh Preparation Design

## Goal

Let each platform override choose whether enabled mesh tessellation and scale baking execute while packaging or while the owning scene loads. The choice must have identical semantics on every supported runtime, including PSP.

## Settings

`MeshComponentTessellationSettings` will expose these booleans:

- `TessellateAtCookTime`, default `true`.
- `BakeScaleAtCookTime`, default `true`.

The values are serialized in the existing platform override alongside `MeshTessellate`, `MeshTessellationMaxEdgeLength`, and `MeshBakeScale`. Existing scenes that lack the new members resolve both values to `true`, retaining their current package-time behavior.

## Packaging

The scene packaging transform applies an operation only when that operation is enabled and its corresponding `AtCookTime` value is `true`.

For enabled load-time operations, packaging keeps the source model reference and writes the requested operation settings into the packaged MeshComponent. It does not create a package-time model variant or apply a compensating transform.

The variant identity includes both execution-time booleans so a package-time variant cannot be reused for a load-time component.

## Runtime Execution

An engine-owned runtime mesh-preparation service runs while a scene is loading, before its MeshComponents are made available to rendering. For each component with an enabled load-time request, it:

1. Resolves the component's model asset.
2. Creates a private model copy for that component.
3. Applies bake scale first when requested, using the component's resolved world scale.
4. Applies tessellation second when requested, measuring with unit scale after baking and with world scale otherwise.
5. Replaces the component's model reference with the private prepared model.
6. Rebuilds or invalidates that component's runtime render model so the renderer consumes the prepared geometry.

The service must never mutate the shared asset-manager model. A model can be referenced by multiple entities whose scales and load-time settings differ.

## Failure Behavior

An enabled load-time operation requires a valid model and finite non-zero scale where scale is used. Invalid data fails scene loading with a precise exception. The system does not silently skip or substitute a default model.

## Validation

Tests cover setting serialization defaults, packaging decisions for all four booleans, shared-model isolation, operation ordering, and renderer-model invalidation. PSP source tests verify the renderer uses the prepared model without applying non-uniform scale again.
