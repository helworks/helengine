# Shader Runtime Material Split

## Goal

Keep `RuntimeMaterial` as the cross-platform runtime material contract while moving shader-only material layout and property behavior out of `helengine.core` into `helengine.shader`.

## Problem

`RuntimeMaterial` is used by both shader-capable renderers and cooked platform-owned material paths such as DS, external package-owned platforms, and PS2. However, the current core runtime material implementation also owns shader-only concepts:

- `MaterialLayout`
- `MaterialLayoutBinding`
- `MaterialLayoutBuilder`
- `MaterialPropertyBlock`
- shader binding inheritance and synchronization behavior

These concepts only make sense for `RawShaderBacked` material resolution and should not live in `helengine.core`.

## Existing platform split

The repository already exposes two material resolution modes:

- `RawShaderBacked`
- `CookedPlatformOwned`

This means the right architectural split is not moving all runtime material behavior out of core. The split is separating:

- generic runtime material identity and ownership
- shader-specific layout and binding state

## Design

### Core ownership

`helengine.core` keeps `RuntimeMaterial` as the generic runtime material shell.

`RuntimeMaterial` remains the type used by:

- DS
- External package-owned platform
- PS2
- other cooked platform-owned material paths
- generic scene/runtime systems that only need runtime material identity

`RuntimeMaterial` should only keep behavior that is genuinely cross-platform.

### Shader ownership

`helengine.shader` becomes the owner of shader-runtime material state.

Add:

- `ShaderRuntimeMaterial : RuntimeMaterial`

Move into `helengine.shader`:

- `MaterialLayout`
- `MaterialLayoutBinding`
- `MaterialLayoutBuilder`
- `MaterialPropertyBlock`

`ShaderRuntimeMaterial` owns:

- resolved shader material layout
- shader binding property storage
- shader-specific material inheritance and synchronization behavior

### Renderer ownership

Shader renderers move to the shader-specific type:

- `DirectX11MaterialResource : ShaderRuntimeMaterial`
- `VulkanMaterialResource : ShaderRuntimeMaterial`

These renderer backends continue using layout/property-block logic through `helengine.shader`, not `helengine.core`.

### Parenting behavior

The recommended split is:

- keep generic parent/child material ownership concepts in `RuntimeMaterial` if they are still meaningful outside shader renderers
- move layout/property synchronization into `ShaderRuntimeMaterial`

This preserves material-instance usability without forcing non-shader platforms to carry shader binding data.

## Resulting boundary

After the migration:

- `helengine.core`
  - owns `RuntimeMaterial`
  - does not own shader layout or binding containers
- `helengine.shader`
  - owns shader-runtime material state and layout/property systems
- `helengine.directx11`
  - depends on `helengine.shader`
  - material resources derive from `ShaderRuntimeMaterial`
- `helengine.vulkan`
  - depends on `helengine.shader`
  - material resources derive from `ShaderRuntimeMaterial`
- cooked platform-owned renderers
  - continue using `RuntimeMaterial` without shader layout baggage

## Performance and usability rationale

Using a shader-specific subclass is preferred over storing optional shader state on every `RuntimeMaterial`.

Benefits:

- no per-material optional attachment lookup on shader render paths
- stronger typing for shader-aware code
- no fake shader layout state on cooked platform-owned materials
- clearer API boundaries for both engine users and backend implementations

## Testing

Focused validation should cover:

- `RuntimeMaterial` still works as the generic core material type
- `ShaderRuntimeMaterial` preserves current shader-layout behavior
- DirectX11 and Vulkan shader material resources still compile and bind through the moved shader types
- existing editor or renderer tests around material layouts and property blocks still pass after the move

## Non-goals

- redesigning authored material asset schemas
- changing DS/external package-owned platform/PS2 cooked material behavior
- merging shader and cooked platform-owned material models
- moving unrelated shader systems unless they are directly required by this split
