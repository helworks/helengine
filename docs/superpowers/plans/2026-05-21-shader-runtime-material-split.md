# Shader Runtime Material Split Implementation Plan

## Objective

Keep `RuntimeMaterial` as the cross-platform runtime material shell while moving shader-only layout and property behavior into `helengine.shader` through a new `ShaderRuntimeMaterial` type.

## Task 1: Add `ShaderRuntimeMaterial` and move shader-only material types

Create `ShaderRuntimeMaterial` in `helengine.shader` and move the shader-only material layout/property types out of `helengine.core`.

Types to move:
- `MaterialLayout`
- `MaterialLayoutBinding`
- `MaterialLayoutBuilder`
- `MaterialPropertyBlock`

Implementation notes:
- Preserve existing behavior for shader-backed materials.
- Keep XML comments and project style intact.
- Do not move unrelated material asset or render-state types in this task.

Validation:
- focused build of `helengine.shader`
- focused build of one shader renderer project

## Task 2: Reduce `RuntimeMaterial` to the cross-platform shell

Refactor `RuntimeMaterial` so it no longer owns shader-only layout/property state.

Implementation notes:
- Keep only behavior that is genuinely cross-platform.
- Preserve the public surface needed by DS/PSP/PS2 and generic runtime systems.
- Move shader-layout/property synchronization behavior into `ShaderRuntimeMaterial`.

Validation:
- focused build of `helengine.core`
- focused tests around generic runtime material behavior

## Task 3: Repoint shader renderers to `ShaderRuntimeMaterial`

Update shader backend material resources and shader-aware code paths to use the new shader-specific material type.

Primary areas:
- `helengine.directx11`
- `helengine.vulkan`
- shader-aware editor material preview/build paths

Implementation notes:
- `DirectX11MaterialResource` and `VulkanMaterialResource` should derive from `ShaderRuntimeMaterial`.
- Avoid introducing optional shader-state lookups on the hot path.

Validation:
- focused builds for `helengine.directx11`, `helengine.vulkan`, and relevant editor projects

## Task 4: Update focused tests for the new ownership boundary

Repoint or adjust tests that currently assume shader layout/property types live in `helengine.core`.

Likely focus areas:
- `MaterialLayoutBuilderTests`
- `MaterialPropertyBlockTests`
- `RuntimeMaterialTests`
- renderer material-binding tests

Implementation notes:
- Keep test intent the same where behavior is preserved.
- Add only the smallest new coverage needed for the `ShaderRuntimeMaterial` split.

Validation:
- focused `dotnet test` runs covering material layout/property/runtime behavior

## Task 5: Verify the boundary cut

Confirm shader-only material layout/property concepts now live in `helengine.shader` and that `RuntimeMaterial` remains the generic cross-platform type.

Validation:
- search for defining files under `helengine.shader`
- search that `RuntimeMaterial` in core no longer owns shader-only layout/property types
- final focused builds/tests used during the migration
