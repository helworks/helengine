# HelEngine Shader Runtime Boundary Implementation Plan

## Objective

Create `helengine.shader` as the shared home for shader-runtime conventions, move `BuiltInMaterialIds` out of `helengine.core`, and retarget current shader-aware consumers without changing behavior.

## Task 1: Create the `helengine.shader` project

Add a new project under `engine/helengine.shader` and wire it into the solution and dependent project references.

Implementation notes:
- Match the existing project conventions used by other engine libraries.
- Start with a minimal namespace and folder layout.
- Do not move speculative shader concepts in this task.

Validation:
- `dotnet build` on the new project
- `dotnet build` on one immediate dependent project after references are added

## Task 2: Move `BuiltInMaterialIds` into `helengine.shader`

Relocate `BuiltInMaterialIds` from `helengine.core` into `helengine.shader` and preserve its current behavior.

Implementation notes:
- Keep the public API behavior stable for the first move.
- Delete the old core file after consumers are repointed.
- Keep XML comments and class/member formatting aligned with repository rules.

Validation:
- focused build of `helengine.shader`
- focused tests covering the moved helper behavior

## Task 3: Retarget shader-aware consumers

Update current consumers to depend on `helengine.shader` rather than `helengine.core` for shader-runtime material conventions.

Primary consumer areas:
- `helengine.directx11`
- `helengine.vulkan`
- `helengine.editor`
- `helengine.editor.fbximporter`
- relevant tests

Implementation notes:
- Keep backend-specific fallback material construction in each renderer.
- Do not move backend-specific implementation logic into `helengine.shader`.

Validation:
- smallest practical builds for affected projects
- focused tests for built-in material behavior

## Task 4: Verify editor and renderer integration still works

Run the smallest validation that proves the new shared shader project is wired correctly through editor and renderer paths.

Implementation notes:
- Prefer focused test coverage over broad suite execution.
- If one existing build test already covers the shader-aware path, use that rather than creating redundant coverage.

Validation:
- focused `BuiltInMaterialIds` behavior test
- one focused editor/import or build-packaging test that exercises the moved dependency path

## Task 5: Record the architectural cut

Confirm the repository no longer keeps shader-runtime built-in material conventions in `helengine.core` and that `helengine.shader` is now the owner.

Validation:
- search for `BuiltInMaterialIds` references and confirm the defining file is only in `helengine.shader`
- final targeted `dotnet test` or `dotnet build` commands used during the migration
