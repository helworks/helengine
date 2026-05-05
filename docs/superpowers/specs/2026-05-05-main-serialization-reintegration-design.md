# Main Reintegration Of Module Loading And Generic Serialization Design

## Goal

Bring `main` to the planned scripting and serialization architecture without reintroducing the current Windows build break. The engine must support module-based script projects, shared script type resolution, automatic editor serialization for eligible scripted components, and generated player deserializers before physics-specific outer asset schema changes are allowed to land.

## Context

The repository is currently split across three different realities:

- `main` still uses one generated script project under `assets`, one loaded script assembly, explicit handwritten component persistence descriptors, and strict runtime deserializers.
- `feature/demo-disc-dynamic-modules` contains real unmerged work for generated code output under `user_settings/generated_code`, per-module project generation, and root gameplay-module fallback when loose scripts exist outside folder-scoped module manifests.
- `physics-main-integration` and `physics-scene-polish` moved authored scene and material assets to outer HELE version `5` by adding `Physics3DSceneFeatureFlags` to `SceneAsset`, but they did not land the generic module/runtime/serialization system that the May 4 plans describe.

That split is the direct cause of the current failure:

- authored assets in `C:\dev\helprojs\city` were saved with outer asset version `5`
- `main` still only accepts outer asset version `4`
- the generic editor-reflection and generated player deserializer path was planned but not finished

The wrong fix is to widen `main` to accept version `5` immediately and carry the physics schema bump forward in isolation. That would preserve the branch mismatch while skipping the intended architecture.

## Existing Planned Direction

The intended architecture is already documented and should remain the source of truth:

- editor scripts are divided into authored code modules by `code.module.json`
- generated script projects live outside `assets`
- user types resolve through a shared module/type runtime instead of narrow `Type.GetType(...)`
- editor scene persistence may fall back to reflection and member names for eligible scripted components
- packaged player payloads must deserialize through generated compact ordinal readers rather than reflective runtime member-name lookup

The tagged editor-persistence design remains useful for built-in editor component payload resilience, but it is not the full answer for scripted components. The scripted-component path still needs the broader May 4 module and generated-runtime design.

## Decision

### Integration target

`main` is the only landing target.

Do not revive one existing worktree branch as the long-lived integration branch. Existing worktrees are useful as donors and references, but they represent partial, divergent snapshots. Reintegration should happen as a new staged effort on top of current `main`.

### Required merge order

The work must land in this order:

1. merge the real generated-code foundation from `feature/demo-disc-dynamic-modules`
2. complete the missing shared module runtime and type-resolution layer on top of `main`
3. complete automatic editor serialization fallback for eligible scripted components
4. complete generated compact player deserializers and runtime hookup
5. update demo-disc and other scripted persisted references to use the shared module/type model
6. only then reconcile physics-specific outer asset schema changes and regenerate affected authored assets

This order is mandatory because each stage supplies a dependency for the next one. Reversing the order recreates the current mismatch.

### Legacy policy

Do not add compatibility shims whose only purpose is to keep the old branch split alive.

Specifically:

- do not make `main` accept version `5` outer scene/material assets before the planned generic serialization path exists
- do not add special-case physics flags to core serialization as a substitute for the broader module/runtime/serialization integration
- do not silently downgrade or auto-heal authored assets at runtime

If an authored asset is incompatible with the landed architecture, the editor/build pipeline must fail clearly until the correct regeneration step is performed.

## Reintegration Scope

### Stage 1: Generated code foundation

Bring over the real unmerged code from `feature/demo-disc-dynamic-modules` for:

- `EditorProjectPaths` generated-code roots under `user_settings/generated_code`
- `EditorGeneratedCodeModuleProject`
- `EditorGeneratedCodeSolution`
- `EditorGeneratedCodeSolutionBuilder`
- `EditorGameSolutionService` per-module solution/project generation
- `EditorCodeModuleManifestService` root gameplay-module fallback when loose scripts exist outside folder-scoped modules
- the matching tests that verify generated project locations and module fallback behavior

This stage is a merge/reintegration stage, not a redesign. The donor branch already contains the expected direction and should be adapted onto `main`.

### Stage 2: Shared script module runtime

Implement the still-missing runtime and editor abstraction that the plans expected:

- one shared script assembly descriptor model
- one shared script type resolver contract in core
- one resolver implementation that understands full CLR type identity and loaded module assemblies
- editor assembly hosting that can load and reload more than one module output
- shared type resolution for menu providers and future scripted component restore paths

This stage replaces the current single-assembly assumption. `EditorGameScriptAssemblyHost` must stop being "one current assembly only" infrastructure.

### Stage 3: Automatic editor serialization fallback

Implement the generic editor path for scripted components that do not have explicit persistence descriptors.

Required behavior:

- explicit descriptors remain authoritative when present
- only eligible scripted component members participate
- member ordering is deterministic
- editor/authored payloads keep member names
- unsupported member shapes fail clearly
- using the fallback path emits an explicit warning

This stage is the editor-facing half of the intended generic system.

### Stage 4: Generated player deserializers

Implement the packaged-player half of the same system.

Required behavior:

- inspect the reflected scripted-component schema during build/cook time
- generate compact deserializer code using ordinal payload layout
- register the generated deserializer path in runtime component loading
- avoid reflective member-name lookup in the player runtime

This keeps editor resilience and player determinism intentionally separate.

### Stage 5: Scripted persisted reference reintegration

After the shared module runtime exists, rewire persisted scripted references to use it consistently:

- menu provider type persistence
- scripted scene component type persistence
- future module-owned user types that rely on assembly-qualified names

The demo-disc output should be treated as a consumer of the shared architecture, not as a one-off path.

### Stage 6: Physics schema reconciliation

Only after Stages 1 through 5 land should the physics-specific outer asset schema work be revisited.

At that point:

- decide whether `Physics3DSceneFeatureFlags` still belongs in the outer authored asset container
- if it does belong there, land it together with all required editor/build/runtime consumers on top of the unified architecture
- regenerate the affected `city` authored assets from the correct editor/build path
- keep authored asset versions synchronized with the engine branch that writes and reads them

If a better representation exists after the generic serialization system is in place, prefer that instead of carrying the current version-`5` branch shape forward unchanged.

## File-Level Expectations

### Expected donor files from `feature/demo-disc-dynamic-modules`

- `engine/helengine.editor/EditorProjectPaths.cs`
- `engine/helengine.editor/managers/project/EditorGeneratedCodeModuleProject.cs`
- `engine/helengine.editor/managers/project/EditorGeneratedCodeSolution.cs`
- `engine/helengine.editor/managers/project/EditorGeneratedCodeSolutionBuilder.cs`
- `engine/helengine.editor/managers/project/EditorGameSolutionService.cs`
- `engine/helengine.editor/managers/project/EditorCodeModuleManifestService.cs`
- `engine/helengine.editor.tests/EditorGameSolutionServiceTests.cs`
- `engine/helengine.editor.tests/managers/project/EditorCodeModuleManifestServiceRootFallbackTests.cs`

### Expected new implementation files on top of `main`

- shared script type resolution types under `engine/helengine.core/scripting`
- new or extended runtime component registration/deserializer support under `engine/helengine.core/scene/runtime`
- automatic scripted-component editor persistence types under `engine/helengine.editor/serialization/scene`
- generator/build integration for scripted player deserializers under `engine/helengine.editor/managers/project`
- focused tests for multi-module load/reload, type resolution, automatic fallback serialization, and generated runtime deserializers

### Expected files to defer until late integration

- `engine/helengine.core/assets/raw/scene/SceneAsset.cs`
- `engine/helengine.core/assets/EditorAssetBinarySerializer.cs`
- `engine/helengine.files/assets/EditorAssetBinarySerializer.cs`
- physics packaging/build files that currently assume `Physics3DSceneFeatureFlags`

These files are allowed to change only in the final physics-schema reconciliation stage.

## Error Handling

Failure behavior should stay explicit.

Expected failures:

- duplicate or invalid module boundaries: throw
- unresolved assembly-qualified scripted type: throw with module/type detail
- unsupported automatic-script-serialization member shape: throw
- malformed editor/scripted payload: throw
- generated player schema/runtime mismatch at package or runtime boundary: throw
- authored version-`5` physics assets opened by a branch that has not landed the final reconciled schema: throw

Non-failures:

- editor load of an eligible automatic-script payload that is merely missing future members should retain defaults where the scripted schema rules allow it

## Testing

Validation must follow the same staging model as the integration.

### Generated code and module tests

Verify:

- generated script projects live outside `assets`
- one generated project exists per code module
- loose scripts outside folder-scoped modules create the root `gameplay` module
- module globs exclude nested module folders correctly

### Shared module runtime tests

Verify:

- multiple module assemblies load and reload correctly
- full CLR type identity resolves through the shared resolver
- menu providers resolve through loaded module assemblies rather than direct `Type.GetType(...)`

### Automatic editor serialization tests

Verify:

- explicit descriptors still win
- eligible scripted components serialize through reflected member names
- warnings are emitted for fallback use
- unsupported member shapes fail clearly

### Generated player deserializer tests

Verify:

- reflected scripted schemas generate ordinal runtime readers
- packaged runtime payloads do not depend on editor member-name lookup
- generated deserializers round-trip supported scripted component payloads

### Final integration tests

Verify:

- demo-disc and other scripted references persist assembly-qualified names using the module model
- editor build/test flows still work from `main`
- physics scene/material assets can be regenerated from the unified architecture
- Windows build no longer fails because authored assets and engine schema drifted apart

## Non-Goals

- supporting every intermediate branch-only asset format forever
- carrying the current physics version-`5` outer asset schema forward without review
- replacing explicit persistence descriptors for built-in components where those descriptors are still the preferred path
- adding best-effort runtime fallbacks that hide branch drift

## Follow-Up

After reintegration lands on `main`, the branch policy should be tightened:

- authored asset writers and readers must evolve together
- outer HELE schema changes must not be committed without the matching consumer path
- generated-code, script-runtime, editor-persistence, and package/runtime changes should be reviewed as one architectural unit whenever they affect scripted component persistence
