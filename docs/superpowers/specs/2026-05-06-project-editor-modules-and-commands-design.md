# Project Editor Modules And Commands Design

## Summary

Add a general project-authored editor module system to the existing code-module pipeline by extending `code.module.json` with an explicit `moduleKind` declaration. Runtime modules remain the default and continue to drive packaging. Editor modules compile into editor-only assemblies that are loaded only by the editor process. Project-authored editor commands are discovered from those assemblies and can use a constrained editor context to perform tooling work. The first concrete command regenerates the demo-disc main menu scene so stale `DemoMenu*` scene payloads can be rewritten as current `Menu*` payloads through normal serialization.

## Goals

- Keep a single authored module model instead of inventing a second project system.
- Make editor-only behavior explicit through manifest metadata, not folder naming conventions.
- Allow editor modules to depend on runtime modules.
- Forbid runtime modules from depending on editor modules.
- Keep editor-only assemblies completely out of runtime packaging and native manifests.
- Add a project-authored editor command contract that can invoke editor-side utilities safely.
- Provide a client-side command that regenerates `DemoDiscMainMenu.helen` using the current menu serialization pipeline.

## Non-Goals

- Do not redesign the runtime packaging graph beyond filtering editor modules out of it.
- Do not add Unity-style folder magic such as a special `Editor` directory with implicit semantics.
- Do not add backward compatibility for old `helengine.DemoMenu*` serialized ids.
- Do not expose unrestricted editor internals directly to project assemblies.
- Do not require a UI surface for command invocation in this slice if service-level invocation is enough to prove the architecture.

## Current Context

The engine already supports authored code modules through `code.module.json`, generated script projects, and editor-side runtime script assembly loading. Menu scene generation already exists in engine code through `DemoMenuSceneBuildService` and `DemoMenuSceneAssetFactory`. The current build failure is caused by stale authored scene data in the client project, where `DemoDiscMainMenu.helen` still contains the removed `helengine.DemoMenu*` serialized component ids. The missing piece is a project-authored editor execution path that can regenerate that scene using the current `Menu*` descriptors.

## Design Overview

The existing module discovery system remains the single source of truth for project-authored code. Each module manifest gains an explicit `moduleKind` field:

- `runtime`
- `editor`

If `moduleKind` is omitted, the module is treated as `runtime`.

Runtime modules continue to participate in codegen, packaging, cooked manifests, and player loading. Editor modules participate in editor compilation and editor assembly loading only. The editor loads both runtime and editor project assemblies for tooling purposes, but platform packaging only consumes runtime modules.

Project-authored editor commands are discovered from loaded editor assemblies through a new editor command contract. Commands execute against a constrained editor context that exposes editor-safe services. The first command in the client project regenerates `DemoDiscMainMenu.helen` using the current menu definition provider and scene serialization path.

## Manifest Model

### `code.module.json`

Add a new optional field:

```json
{
  "moduleId": "gameplay.menu.tools",
  "dependencyModuleIds": ["gameplay", "gameplay.menu"],
  "loadScopes": ["always-loaded"],
  "moduleKind": "editor"
}
```

Rules:

- Missing `moduleKind` means `runtime`.
- Valid values are `runtime` and `editor`.
- Invalid values fail manifest loading with a clear error.
- Folder location does not imply module kind.

### Manifest Document Shape

The in-memory manifest entry model must retain the resolved module kind so later systems can filter, validate, and compile correctly.

## Dependency Rules

Dependency validation changes as follows:

- Runtime modules may depend on runtime modules.
- Editor modules may depend on runtime modules.
- Editor modules may depend on editor modules.
- Runtime modules may not depend on editor modules.

Validation must fail early with a targeted error that identifies both the offending runtime module and the editor-only dependency.

This rule preserves a one-way dependency flow:

- runtime is safe for player builds
- editor can build on top of runtime

## Discovery And Generated Projects

The existing module discovery flow continues to scan `assets` recursively for `code.module.json` files. No separate root or second discovery pipeline is introduced.

Generated script solution/project creation expands to include both runtime and editor modules:

- Runtime modules remain normal generated game assemblies.
- Editor modules generate separate editor assemblies.
- The generated solution includes both kinds so the client can author and compile them together.

Project templates should create a conventional location for editor code, such as `assets/codebase/editor`, but this is only a starter layout. Behavior is driven by manifest metadata, not by path.

## Editor Assembly Loading

The editor gets a parallel authored assembly loading path for editor modules.

Behavior:

- The editor loads runtime project assemblies as it does today for script-backed content and providers.
- The editor also loads editor module assemblies into the editor process.
- Editor module assemblies are never included in runtime package manifests, runtime code-module manifests, or native embedding outputs.

This requires the editor script assembly host and related generated-solution/build logic to distinguish module kinds and surface both assembly sets to the editor.

## Editor Command Model

Add a project-authored editor command contract, for example:

- `IEditorCommand`

Required metadata and behavior:

- stable command id
- display name
- execute entrypoint

Commands are discovered from loaded editor assemblies by a small editor command registry. The registry is editor-only and does not participate in runtime builds.

### Editor Command Context

Commands receive a constrained context/service surface rather than unrestricted access to editor internals. The initial context should cover the needs of deterministic tooling and scene generation:

- project root path
- asset path helpers
- scene asset read/write helpers
- access to the editor script type resolver when needed
- access to menu scene build services or other explicit editor services

The context should be capability-based. New capabilities can be added later without forcing commands to depend on all editor subsystems.

## Demo Scene Regeneration Command

The first concrete project-authored command regenerates `DemoDiscMainMenu.helen`.

Flow:

1. Resolve the project-authored menu definition provider.
2. Build a fresh scene asset through `DemoMenuSceneBuildService`.
3. Write the rebuilt scene asset to the project scene path using the normal asset serializer.
4. Replace the old scene file atomically.

Expected outcome:

- The saved scene contains `helengine.MenuComponent`.
- The saved scene contains `helengine.MenuPanelComponent`.
- The saved scene contains `helengine.MenuItemComponent`.
- The saved scene contains `helengine.MenuSelectedDescriptionComponent`.
- The old `helengine.DemoMenu*` ids are removed from authored scene data.

This command is the first proof that client-authored editor code can safely drive editor utilities and content regeneration without polluting runtime builds.

## Error Handling

Failure behavior must be explicit:

- Unknown `moduleKind` values fail manifest loading.
- Runtime-to-editor dependency edges fail validation.
- Missing editor assemblies or command implementations fail command discovery clearly.
- Command execution failures surface a direct error and do not silently write partial scene output.
- Scene regeneration writes complete files only after successful generation.

## Testing

Add coverage for:

- manifest loading defaults missing `moduleKind` to `runtime`
- manifest loading accepts explicit `editor`
- manifest loading rejects unknown `moduleKind`
- dependency validation allows editor -> runtime
- dependency validation allows editor -> editor
- dependency validation rejects runtime -> editor
- generated project/solution flow includes editor modules
- runtime build graph excludes editor modules
- editor assembly host loads editor modules for discovery
- command registry discovers project-authored editor commands from editor assemblies
- demo-disc regeneration command rewrites the scene with `helengine.Menu*` ids
- regression check proves regenerated output no longer contains `helengine.DemoMenuBuildComponent`

## Implementation Notes

This work should reuse existing systems where possible:

- extend the current code-module manifest types instead of creating parallel editor-manifest types
- extend existing generated project and assembly-host infrastructure instead of inventing separate editor project plumbing
- reuse `DemoMenuSceneBuildService` for scene generation
- reuse the existing scene asset serializer for output writing

The demo scene regeneration command should be implemented as the first consumer of the general editor module and command architecture, not as a special-case built-in editor feature.
