# Editor Platform Hardcode Removal Design

## Goal

Remove the remaining platform-specific production behavior from `helengine.editor` so the main engine repository no longer owns PS2, Nintendo DS, GameCube, or other external package-owned platform policy. The editor should orchestrate builds generically and leave platform-specific behavior to external platform plugins or project content.

## Current Problems

The editor still contains production branches that interpret concrete platform identities:

- `EditorPlatformBuildGraphRunner` hardcodes PS2, Nintendo DS, and GameCube repository-root environment overrides.
- `EditorPlatformBuildGraphRunner` emits a runtime graphics renderer manifest that still includes PS2-owned depth-handler behavior.
- `EditorBuildQueueItemDocument` expands scene lists specifically for Nintendo DS generated companion scenes.
- `EditorPlatformPreprocessorSymbolService` still emits a hardcoded external platform symbol.
- `AssetImportManager` still applies Nintendo DS-specific default texture import behavior.

These behaviors violate the repository boundary. They keep platform policy in the shared editor instead of the external platform repos.

## Design

### Editor Ownership Boundary

`helengine.editor` remains responsible for:

- loading installed platform descriptors
- collecting generic build settings
- invoking generic build/cook/export flows
- passing platform metadata through to platform builders

`helengine.editor` must not:

- branch on concrete platform ids for build behavior
- emit PS2-owned runtime manifest types
- mutate selected scene lists for one specific platform
- inject platform-specific preprocessor symbols
- invent platform-specific default import rules

### Build Graph Behavior

`EditorPlatformBuildGraphRunner` will stop owning special cases for:

- `HELENGINE_PS2_REPOSITORY_ROOT`
- `HELENGINE_DS_REPOSITORY_ROOT`
- `HELENGINE_GAMECUBE_REPOSITORY_ROOT`
- PS2 runtime graphics manifest generation

The runner becomes a generic orchestrator only. If an external platform builder still needs extra inputs, that requirement must move to plugin-owned behavior or a future generic plugin contract. This pass does not add that contract in `helengine.editor`; it removes the hardcoded behavior.

### Scene Selection Behavior

`EditorBuildQueueItemDocument` will stop expanding selected scenes for Nintendo DS companion files. The queued build scene set will be exactly the authored selected scene order.

If a platform still requires generated companion scenes, that responsibility must move outside `helengine.editor`, either into platform-owned build behavior or project-authored content.

### Import Defaults

`AssetImportManager` will stop returning Nintendo DS-specific default texture settings from shared editor code. Shared defaults become platform-agnostic only.

If a platform needs compact defaults, that must come from generic platform metadata or plugin-side behavior rather than editor-side id checks.

### Preprocessor Symbols

`EditorPlatformPreprocessorSymbolService` will stop hardcoding one named external platform symbol.

The editor may still generate generic symbols if they are derived from a platform descriptor contract rather than a hardcoded platform id branch. This pass removes the existing hardcoded external-package behavior and does not replace it with a new engine-owned compatibility layer.

### PS2 Manifest Types

`RuntimeGraphicsRendererManifest` and `Ps2DepthHandlerMode` do not belong in `helengine.editor`. They will be removed from this repo.

If a PS2 runtime still requires equivalent data, the data contract must be owned by the external PS2 repo rather than by the shared editor.

## Approach Options

### Option 1: Delete Hardcodes And Require Plugin Ownership

Remove all identified platform branches immediately and let external platform repos fail explicitly if they still depended on shared-editor behavior.

Pros:

- cleanest boundary
- no new abstraction layer in `helengine.editor`
- makes hidden platform coupling visible

Cons:

- may require follow-up fixes in external platform repos

This is the recommended approach.

### Option 2: Replace Hardcodes With Generic Hooks First

Introduce new plugin contracts before removing the old behavior.

Pros:

- lower short-term integration risk

Cons:

- keeps the engine repo responsible for designing and carrying new platform behavior seams now
- risks preserving the same policy under more abstract names

This is not recommended for this pass.

## Testing Strategy

Run only focused validation for the touched editor slices:

- focused editor tests for `EditorBuildQueueItemDocument`
- focused editor tests for `EditorPlatformBuildGraphRunner`
- focused editor tests for `EditorPlatformPreprocessorSymbolService`
- focused editor tests for `AssetImportManager`

Tests that exist only to assert old platform-specific behavior should be deleted rather than migrated.

## Risks

### External Platform Breakage

External platform repos may currently depend on:

- repository-root environment overrides
- PS2 manifest generation from the editor
- DS scene expansion
- hardcoded external platform symbol injection
- DS import defaults

This pass intentionally allows those dependencies to break so they can be moved to the correct owners.

### Hidden Test Coupling

Some editor tests may still encode platform-specific assumptions. Those tests should be removed when they are validating the old bad boundary instead of a generic editor rule.

## Success Criteria

The pass is complete when:

- `helengine.editor` production code no longer branches on `ps2`, `ds`, `gamecube`, or any one named external package-owned platform
- `RuntimeGraphicsRendererManifest` and `Ps2DepthHandlerMode` are gone from the main repo
- `EditorBuildQueueItemDocument` preserves selected scene order without DS companion expansion
- `EditorPlatformPreprocessorSymbolService` no longer hardcodes external package behavior
- `AssetImportManager` no longer has DS-specific default texture import logic
- focused editor validation passes
