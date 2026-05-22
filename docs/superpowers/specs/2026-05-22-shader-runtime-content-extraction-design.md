# Shader Runtime Content Extraction Design

## Summary

`helengine.core` still owns shader-specific runtime content concepts that should live in `helengine.shader`. This design extracts shader package contracts, shader metadata, shader processor identifiers, and shader package registration logic out of core while preserving generic content loading in `helengine.core`.

After this change:

- `helengine.core` owns only generic content loading primitives and generic content registration APIs.
- `helengine.shader` owns shader runtime asset contracts, shader metadata, shader package readers, shader processor identifiers, and shader-specific content registration helpers.
- `helengine.directx11`, `helengine.vulkan`, and shader-aware editor flows depend on `helengine.shader`.
- PS2 and GameCube remain outside `helengine.shader` unless they truly consume the shared modern shader-runtime metadata types.

## Problem

The previous `helengine.shader` split moved material-layout conventions and built-in material identifiers, but `helengine.core` still contains a second shader-specific surface area:

- shader package content registration in `RuntimeContentManagerConfiguration`
- shader processor identifiers in `RuntimeContentProcessorIds`
- shader asset and reflection metadata types under `engine/helengine.core/shaders`
- shader-target decisions that should not be owned by the generic runtime host

This leaves `helengine.core` carrying modern shader-runtime semantics even though non-shader platforms should not depend on them.

## Goals

- Remove shader-runtime-specific asset contracts and registration details from `helengine.core`.
- Preserve generic content loading primitives in `helengine.core`.
- Make `helengine.shader` the home for all shared shader runtime concepts.
- Keep platform-specific fixed-function or fixed-format render configurations out of `helengine.shader`.
- Avoid changing authored project semantics beyond namespace and ownership relocation.

## Non-Goals

- Reworking PS2 or GameCube render configuration formats into the shader runtime model.
- Redesigning the generic content pipeline.
- Changing shader authoring workflows beyond the ownership move required by this extraction.
- Refactoring unrelated renderer behavior.

## Ownership Boundary

### `helengine.core`

`helengine.core` keeps:

- `ContentManager`
- generic `AssetContentProcessor<T>`
- generic runtime content registration APIs
- generic asset loading and serialization infrastructure

`helengine.core` loses:

- shader package extension constants
- shader processor ids
- shader asset/package runtime contracts
- shader reflection metadata
- shader package readers
- modern shader target selection decisions

### `helengine.shader`

`helengine.shader` owns:

- shader asset/package contracts
- shader reflection metadata
- compiled shader binary descriptors
- shader module/package readers
- shader processor ids and package extension constants
- shader-specific content registration/bootstrap helpers

This includes the current `engine/helengine.core/shaders/` tree and related content registration pieces now embedded in core.

### Platform Projects

`helengine.directx11` and `helengine.vulkan` depend on `helengine.shader` for shared shader runtime concepts.

PS2 and GameCube remain platform-owned for their fixed render configs. If those platforms do not consume shared shader-runtime metadata, they must not depend on `helengine.shader`.

## Design

### 1. Move shader runtime contracts into `helengine.shader`

Move the current core shader runtime types into `helengine.shader`, including:

- shader assets
- shader module definitions
- shader program definitions
- shader program binaries
- shader bindings
- shader constant members
- shader vertex elements
- shader variants
- shader stage/resource enums
- shader package readers and package wrapper types

The namespace becomes `helengine.shader` or nested namespaces rooted under it.

### 2. Move shader-specific content registration out of core

`RuntimeContentManagerConfiguration` and `RuntimeContentProcessorIds` in `helengine.core` currently encode shader-specific package registration. That logic moves to `helengine.shader`.

`helengine.shader` will provide a shader-specific registration entrypoint that installs shader asset processors into a generic `ContentManager` or generic runtime registration surface.

Core keeps only the registration mechanism, not the shader registrations themselves.

### 3. Remove shader-target decisions from generic runtime bootstrap

Any shader-target or shader-backend selection currently anchored in `Core.cs` moves out to shader-aware layers. Generic runtime bootstrap should not decide DirectX11/Vulkan shader targets.

Shader-aware backends or shader-layer helpers should own those choices.

### 4. Preserve generic content loading

No new special-case asset loader should be introduced in core. The generic content system remains the same:

- generic processor registration
- generic load APIs
- generic serialized asset loading

The only change is which assembly provides the shader asset contracts and shader registration helper.

## Data Flow

After the cut:

1. Generic runtime code creates or receives a generic content manager.
2. Shader-aware code calls into `helengine.shader` registration/bootstrap helpers.
3. Shader asset packages load through generic content APIs, but deserialize into types owned by `helengine.shader`.
4. Shader-aware renderers consume shader metadata and binaries from `helengine.shader`.
5. Non-shader platforms never need to know these types exist.

## Risks

### Project reference fallout

Moving the shader metadata tree out of core will affect:

- renderer projects
- editor projects
- tests
- any content bootstrap code that assumes shader registration comes from core

This is expected and should be repaired systematically through compile-site updates.

### Namespace drift

If partial compatibility aliases are left behind in core, the boundary remains muddy. The move should be explicit and ownership should become singular in `helengine.shader`.

### Platform confusion

PS2 or GameCube code may use the word "shader" informally. Those concepts should only move if they actually consume the shared shader-runtime metadata model. Fixed-format platform configs stay platform-owned.

## Testing Strategy

Focused validation should cover:

- `helengine.shader` builds with the moved runtime content types
- `helengine.core` builds without shader-specific registrations or metadata ownership
- shader-aware editor/runtime tests still load shader packages through generic content registration
- DirectX11/Vulkan compile after the reference move

Validation should stay focused on compile/build coverage and the smallest shader-aware content-loading tests needed to prove the ownership cut.

## Implementation Notes

- Prefer a single ownership move rather than compatibility wrappers.
- Keep `helengine.core` generic by policy, not by naming alone.
- If a type exists because of compiled shader metadata, reflection, or shader-package loading, it belongs in `helengine.shader`.
