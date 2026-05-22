# HelEngine Shader Runtime Boundary

## Goal

Create a dedicated `helengine.shader` shared library for shader-runtime concepts so `helengine.core` no longer owns built-in material or shader conventions that only apply to shader-capable platforms.

## Problem

`BuiltInMaterialIds` currently lives in `helengine.core`, but its consumers are shader-aware systems:

- `helengine.directx11`
- `helengine.vulkan`
- `helengine.editor`
- `helengine.editor.fbximporter`

This is the wrong ownership boundary. Non-shader platforms should not carry shader-runtime conventions through core, and core should not define fallback shader-material assumptions.

## Design

### New project

Add a new `engine/helengine.shader` project.

This project becomes the shared home for shader-runtime concepts that:

- are meaningful for shader-capable platforms
- are shared across multiple shader-aware runtime or authoring layers
- do not belong to one specific renderer backend

### Ownership rules

After the migration:

- `helengine.core`
  - contains only platform-agnostic runtime concepts
  - does not define built-in shader/material ids
  - does not define shader fallback conventions
- `helengine.shader`
  - owns built-in shader/material identifiers
  - owns shared shader-runtime convention helpers such as standard-mesh-transform matching
  - becomes the long-term home for future cross-renderer shader runtime concepts
- `helengine.directx11`
  - depends on `helengine.shader`
  - keeps backend-specific fallback material construction and backend implementation details
- `helengine.vulkan`
  - depends on `helengine.shader`
  - keeps backend-specific fallback material construction and backend implementation details
- `helengine.editor`
  - depends on `helengine.shader` when editor flows need shader-runtime identities
- `helengine.editor.fbximporter`
  - depends on `helengine.shader` for built-in shader/material identifiers used during import

### First migration slice

The first migration moves `BuiltInMaterialIds` out of:

- `engine/helengine.core/assets/material/BuiltInMaterialIds.cs`

and into the new `helengine.shader` project as the first shared shader-runtime convention type.

The initial move preserves behavior. This is a boundary correction, not a redesign of material identity semantics.

### Scope of `helengine.shader`

`helengine.shader` is intended to become the home for all shared shader runtime concepts going forward, but this implementation slice stays narrow:

- create the project
- move `BuiltInMaterialIds`
- retarget current consumers
- move or update focused tests

No speculative shader-domain taxonomy is required yet. The project can begin with a minimal folder structure and grow only when additional shared shader-runtime concepts appear.

## Data and dependency flow

1. Shader-aware import/editor flows request shared built-in material ids from `helengine.shader`.
2. Shader renderers request shared built-in material ids and matching helpers from `helengine.shader`.
3. Backend-specific runtime fallback construction remains inside each renderer backend.
4. `helengine.core` no longer sits in the dependency path for shader-only conventions.

## Testing

Focused validation should cover:

- compile/reference correctness after the new project is added
- existing built-in material identity behavior through the new `helengine.shader` owner
- the shared `UsesStandardMeshTransform` behavior still returning the same answers
- at least one smallest practical shader-aware build/test path that proves the new project wiring works

## Non-goals

- redesigning built-in material ids
- introducing renderer-independent fallback runtime objects
- moving backend-specific shader compilation or runtime binding code into `helengine.shader`
- preemptively moving unrelated shader concepts without a concrete current consumer
