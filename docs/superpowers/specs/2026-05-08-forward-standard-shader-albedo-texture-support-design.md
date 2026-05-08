# Forward Standard Shader Albedo Texture Support

## Overview

This change adds `albedo-only` texture support to the built-in `ForwardStandardShader` used by the main standard material path.

The goal is narrow:

- imported textured models rendered through the standard material path should use their diffuse/albedo texture
- existing untextured standard-material content should keep working
- point, spot, and directional lighting behavior should remain unchanged apart from now shading textured albedo instead of a hardcoded flat color
- shadow logic should remain unchanged in this pass

This is explicitly not a broader material-system redesign. It does not add normal-map support, emissive support, or shader-variant authoring changes beyond what is required for one albedo texture.

## Current State

The current built-in forward shader hardcodes a neutral surface color in the pixel shader. At the same time, the runtime material system and the DirectX11 renderer already support a first texture binding:

- `MaterialLayout` supports texture bindings
- `MaterialPropertyBlock` supports textures by binding name
- `DirectX11Renderer3D` already binds the first texture resource for materials whose layout contains a texture binding

So the renderer already has most of the plumbing needed. The missing piece is the main standard shader contract and the standard generated material layout.

## Approach Options

### Option 1: Single albedo texture on the standard shader

Add one `DiffuseTexture` binding to `ForwardStandardShader`, sample it in the pixel shader, and use it as the albedo in the existing lighting path.

Pros:

- smallest real renderer upgrade
- matches the runtime path already present in DX11
- does not widen scope into a material-system redesign

Cons:

- only solves albedo texturing for now

### Option 2: Albedo plus flat-color mode in the same shader

Add texture support and also an explicit mode/fallback branch in the shader for flat-color materials.

Pros:

- more explicit control for textured vs non-textured materials

Cons:

- adds more shader contract surface than needed
- unnecessary complexity for this pass

### Option 3: Material-system-first redesign

Rework the standard material path first, then add texture support on top of it.

Pros:

- clean long-term architecture

Cons:

- too broad for the current goal
- slows down getting textured models rendering correctly

## Chosen Approach

Use **Option 1**.

The engine already has the material layout, property block, and DX11 texture-binding path required for one texture. The standard shader should be upgraded to use that existing path instead of inventing a second mechanism.

## Design

### Shader Contract

`ForwardStandardShader` will expose:

- `Texture2D DiffuseTexture : register(t0)`
- a matching sampler for the diffuse texture path

The vertex shader will pass UVs through to the pixel shader.

The pixel shader will sample `DiffuseTexture` and use the sampled RGB as `surfaceColor`.

The rest of the lighting structure stays intact:

- ambient term remains
- forward light evaluation remains
- specular remains
- shadow logic remains

### Generated Standard Material

The generated built-in standard material and its layout will be updated so the shader contract now includes one `DiffuseTexture` binding.

This keeps the built-in standard material aligned with the shader metadata and with the DX11 runtime material binding path that already exists.

### Runtime Behavior

Textured standard materials should render using the sampled albedo texture.

Untextured content must remain valid. The runtime path should preserve sane behavior for standard materials that do not provide a bound texture. This pass should not introduce renderer crashes or black-material failures for untextured primitives and existing generated assets.

### Non-Goals

This pass does not include:

- normal-map support
- emissive-texture support
- PBR authoring expansion
- multi-texture standard materials
- material-inspector redesign
- platform-specific custom texture behavior beyond the existing main standard path

## Verification

Verification should stay focused on the standard shader contract and the existing runtime path:

- update built-in shader tests so `ForwardStandardShader` now reports one `DiffuseTexture` texture binding
- verify the generated standard material still builds successfully through the existing asset pipeline
- verify the DirectX11 standard-material path can sample the bound texture through the existing binding flow

If shader contract and generated material layout drift apart, tests should fail loudly.

## Risks

The main risks are:

- shader contract drift between the built-in shader and generated material layout
- breaking untextured standard-material content if the shader assumes a texture is always bound
- changing visual output more broadly than intended if the sampled texture path unintentionally affects non-textured content

These should be controlled by keeping the change narrow and validating the shader contract explicitly.
