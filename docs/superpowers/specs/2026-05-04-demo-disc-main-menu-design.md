# Demo Disc Menu, Dynamic Code Modules, And Automated Script Serialization Design

## Summary

Build the demo-disc main menu as a normal consumer of a broader scripted-runtime architecture:

- authored code is divided into modules by `code.module.json` boundaries
- each module builds into its own dynamically loaded DLL
- persisted scripted references keep full CLR type identity
- scripted components can use automated editor serialization when no explicit descriptor exists
- player builds generate lighter deserializers from the reflected schema

The demo-disc menu remains a concrete deliverable in the `city` project, but the engine work must fix the underlying module, type-resolution, and serialization model instead of adding menu-only workarounds.

## Problem

The first menu implementation exposed a larger architecture mismatch.

Current gaps:

- the editor script solution generator assumes one project-wide script assembly
- generated `*.csproj` files live under `assets`, which is not acceptable
- menu-provider persistence assumed a guessed assembly name instead of the actual dynamically loaded module assembly
- runtime type resolution is too narrow for dynamically loaded user code
- user components still depend on explicit handwritten serialization infrastructure

Those assumptions conflict with the intended scripting model:

- projects can contain multiple code modules
- module boundaries are defined by `code.module.json`
- scripts without a folder-scoped module manifest belong to the main project module
- each module should build a DLL that the engine loads dynamically and can later unload
- persisted scripted references should use full type identity
- editor serialization can fall back to reflection
- player packages should use generated lightweight deserializers

If the menu is implemented without correcting those boundaries, later scripted menus and scripted components will continue to depend on brittle special cases.

## Goals

- Keep the demo-disc menu as one reusable multi-panel menu scene.
- Treat code-module id as the CLR assembly name for dynamically built user modules.
- Generate one script project per module outside `assets`.
- Continue assigning scripts without a local manifest to the main project module.
- Load and unload user assemblies dynamically through a shared module runtime.
- Persist menu providers and scripted component references using full CLR type identity.
- Add automated editor serialization for scripted components that lack explicit persistence descriptors.
- Emit a warning when the automated editor serializer path is used.
- Generate lighter player deserializers from the reflected editor schema, using ordinal storage instead of member names.
- Keep explicit persistence descriptors as the preferred and authoritative path.

## Non-Goals

- No visual editor for authoring module manifests in this slice.
- No best-effort fallback when a type, module, or scene target is invalid.
- No hidden alias layer between module id and assembly name.
- No reflective per-member-name lookup in packaged player runtime.
- No requirement that the first pass auto-migrate incompatible component schema changes silently.

## Module Model

### Authored Module Ownership

Scripts are owned by the nearest enclosing `code.module.json` boundary beneath `assets`.

- if a script is inside a folder with `code.module.json`, it belongs to that module
- if it is not inside any folder-scoped module boundary, it belongs to the main project module
- nested module folders remain excluded from parent module compile globs

This preserves the existing authored-boundary concept while making it the source of truth for runtime assembly identity.

### Assembly Identity

The module id is the CLR assembly name verbatim.

Examples:

- module id `gameplay` builds `gameplay.dll`
- module id `gameplay.ui` builds `gameplay.ui.dll`
- the main project module continues using the engine's main-module id when no folder-scoped manifest is present

Persisted scripted references must use those real assembly names directly.

There is no aliasing layer from module id to a different CLR assembly name.

### Generated Script Projects

The editor should generate one `*.csproj` per code module and place those generated projects outside `assets`.

Required properties:

- source files still come from the authored module folders under `assets`
- generated solution files can include all generated module projects
- module projects produce one DLL per module
- output paths should stay outside authored asset folders

This keeps authored content clean while preserving IDE support.

## Dynamic Module Runtime

### Shared Module Registry

Introduce a shared runtime concept for user code modules that knows:

- discovered module ids
- resolved dependency order
- currently loaded assemblies
- load context ownership
- which modules are safe to unload

Both editor and player runtime code should ask this registry for user-code type resolution instead of assuming `Type.GetType(...)` against the default runtime context is sufficient.

### Editor Behavior

The editor should:

1. build the generated module projects
2. load each resulting DLL dynamically
3. register loaded assemblies with the shared user-type resolver
4. unload and reload modules cleanly when scripts are rebuilt

This runtime path should support menu-provider resolution, scripted component discovery, and future scene/runtime systems uniformly.

### Player Behavior

The player should:

1. load packaged module DLLs dynamically according to load scope and dependency rules
2. resolve scripted component types and menu providers through the shared user-type resolver
3. unload modules only when dependency and load-scope rules allow it

The packaged player should not depend on editor-only reflection helpers or editor load contexts.

## Type Persistence

### Full CLR Type Identity

Persist scripted references as full CLR type names, including assembly name.

Examples:

- `city.menu.DemoDiscMenuDefinitionProvider, gameplay`
- `city.ui.InventoryPanelComponent, gameplay.ui`

This applies to:

- menu-definition providers
- scripted scene components
- any other persisted user-side type reference introduced by this architecture

### Resolution Rules

Type resolution should:

1. check the shared dynamic module/type registry
2. fall back to normal runtime assemblies when appropriate
3. fail fast with a clear error when the type or assembly is unavailable

Errors should include:

- requested full type name
- available loaded module ids or assemblies when helpful
- whether the requested module was absent or the type was missing within a loaded module

## Automated Script Serialization

### Descriptor Priority

Explicit persistence descriptors remain the preferred path.

Resolution order:

1. explicit descriptor exists: use it
2. explicit descriptor does not exist and the component is eligible: use automated script serialization
3. unsupported shape: throw

The automated path is a fallback, not a replacement for the explicit descriptor system.

### Editor Serialization Path

When a scripted component lacks an explicit descriptor, the editor should serialize it through reflection.

Requirements:

- deterministic member ordering
- strict member-type support rules
- persisted member names in editor/authored scene data
- schema information sufficient to detect incompatible structural changes

The editor should log a warning when the automated serializer is used, naming the component type and making it clear that the fallback path serialized it.

That warning is intentional and should not be suppressed silently.

### Player Serialization Path

Packaged players should not deserialize automated scripted components through reflective member-name lookup.

Instead, the build pipeline should:

1. inspect the reflected editor schema
2. generate a compact deserializer class for the packaged player format
3. serialize player payloads in ordinal order without member names

This is valid because packaged code and schema are built together and are expected to match exactly at runtime.

### Change Handling

Editor scenes keep member names so structural changes remain diagnosable and mappable.

Expected behavior:

- editor/authored scenes can identify which stored members map to which current members
- packaged player payloads assume code and generated deserializer are in sync
- incompatible schema changes fail clearly during editor load or package-generation time rather than being hidden

## Demo Disc Menu Integration

### Runtime Menu Framework

Keep the reusable menu framework introduced for the demo-disc scene:

- one host scene
- switchable panels
- keyboard, mouse, and gamepad navigation
- curated scene selection
- polished shell `Options` panel

This menu work remains valid, but all menu-provider resolution should now flow through the dynamic module runtime instead of narrow type lookup.

### Generated City Menu Code

The menu generator should place generated menu source files into a chosen authored folder under `assets`.

Provider assembly identity is derived from module ownership:

- if that folder is inside a module boundary, the provider uses that module id as its assembly name
- otherwise, the provider uses the main project module assembly name

The generator must not infer the assembly name from the project display name.

### Generated Menu Scene

The generated menu scene should persist the provider type exactly as the runtime will load it.

Example:

- generated code in the root fallback module: `city.menu.DemoDiscMenuDefinitionProvider, gameplay`
- generated code in a manifest-defined module `gameplay.menu`: `city.menu.DemoDiscMenuDefinitionProvider, gameplay.menu`

This keeps the menu scene compatible with the same type-resolution rules used by other scripted systems.

## Validation Rules

Fail fast on:

- duplicate module ids
- duplicate folder boundaries
- unresolved module dependencies
- module assembly names that do not match module ids
- persisted scripted types that target unloaded or unknown assemblies
- unsupported automated-serialization member types
- invalid generated menu provider module ownership
- invalid curated menu scene targets

Do not add silent defaulting, guessed replacement assemblies, or best-effort schema skipping.

## Testing

Add focused coverage for the new architecture.

### Module Discovery And Project Generation

- nested module boundaries are discovered correctly
- scripts without a local manifest belong to the main project module
- generated `*.csproj` files are written outside `assets`
- generated project source globs point back into authored module folders
- generated assembly names equal module ids

### Dynamic Loading And Type Resolution

- editor module reload registers fresh assemblies
- stale assemblies are not used after reload
- menu providers resolve through the shared user-type resolver
- scripted component types resolve through the same registry
- unload behavior respects dependency rules

### Automated Serialization

- explicit descriptors override automated serialization
- automated editor serialization logs the expected warning
- supported reflected members round-trip in authored scene data
- unsupported reflected members fail clearly
- generated player deserializers round-trip compact ordinal payloads

### Demo Disc Menu Integration

- generated city menu code lands in the intended authored folder
- generated menu scene stores the expected full CLR provider type
- root fallback-module generation persists the main-module assembly name
- manifest-owned generation persists the manifest module assembly name
- menu provider resolution succeeds after loading the owning module

## Implementation Notes

- Reuse the existing module-manifest discovery model rather than inventing a parallel menu-specific config.
- Move script-project generation toward a per-module solution model instead of a single project-wide script assembly.
- Centralize dynamic user-type resolution so menu providers and scripted component systems share the same lookup path.
- Keep editor and player serialization formats intentionally different where needed:
  editor keeps names for diagnostics and structure-aware mapping
  player keeps ordered compact payloads for runtime efficiency
- Preserve warnings for automated serialization so teams know when they are depending on fallback behavior.

## Migration Direction

The prior menu-only design is superseded by this broader design.

Implementation should proceed in layers:

1. correct generated script project and module assembly identity
2. add shared dynamic type resolution and module loading
3. add automated editor serialization fallback and warning path
4. add generated compact player deserializers
5. rewire the demo-disc menu generator and menu-provider resolution onto that foundation
