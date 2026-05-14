# Remove Core Default Font Design

## Goal

Remove the `DefaultFontAsset` concept from runtime `Core`, keep any editor UI font ownership on editor-only types, and make `FPSComponent` behave correctly when no font is assigned.

## Scope

This change covers:

- removing `DefaultFontAsset` from `helengine.core.Core`
- removing all `FPSComponent` fallback behavior that implicitly resolves a font from core state
- allowing `FPSComponent` to serialize and deserialize without a font reference
- making `FPSComponent` inert when no font is available instead of throwing
- moving editor-only font ownership and special-case editor font resolution away from `Core`

This change does not require:

- inventing a new runtime default-font system
- forcing all existing text-based components to become font-optional
- changing the packaged font asset format

## Desired Behavior

### Core

`Core` no longer exposes or stores `DefaultFontAsset`. Runtime engine behavior must not depend on a process-wide implicit default font.

### Editor

If the editor needs a UI font, that font is owned by `EditorCore` or other editor-only services. Editor UI font ownership must not leak back into generic runtime component behavior.

### FPSComponent

`FPSComponent` requires a concrete `Font` to render text, but a missing font is not an error state for scene persistence.

Expected behavior:

- creating an `FPSComponent` without assigning `Font` is valid
- serializing an `FPSComponent` with no `Font` is valid and writes no font reference
- deserializing an `FPSComponent` with no font reference is valid and produces `Font == null`
- attaching an `FPSComponent` with `Font == null` does not throw and does not build its overlay
- an attached `FPSComponent` begins rendering only after a non-null `Font` is assigned
- clearing `Font` after attach tears down the overlay and stops FPS sampling

The component remains logically present in the scene even when it is visually inactive.

## Architecture

### Remove runtime-global default font state

Delete `Core.DefaultFontAsset` and its backing field. No core initialization or runtime scene path should reference a global default font.

### Keep editor font ownership editor-only

Introduce or reuse editor-only storage for the editor UI font on `EditorCore`. Code that previously checked `Core.Instance.DefaultFontAsset` to identify the editor font must instead check editor-only state.

This keeps the `ui-font` reference behavior available for editor workflows without making it part of the runtime core contract.

### Make FPSComponent font-optional for lifecycle, font-required for rendering

`FPSComponent` should separate existence from renderability.

The component lifecycle changes as follows:

- constructor does not resolve any implicit font
- attach succeeds even when `Font == null`
- overlay entities and text components are only created when a font exists
- losing the font removes the overlay and unregisters the component from active frame sampling
- regaining a font after attach recreates the overlay and resumes sampling

This preserves scene validity while making the rendering dependency explicit.

## Component Lifecycle Details

### Attach

When `ComponentAdded` runs:

- validate the parent entity as usual
- do not throw when `Font` is null
- if no font is assigned, do not allocate overlay entities or text components
- if a font is assigned, create the overlay hierarchy and register the component as active

### Runtime activation

The `Font` property becomes responsible for runtime activation changes.

When `Font` changes:

- assigning a non-null font to an attached inactive component creates the overlay and resets sampling
- assigning a different non-null font to an active component updates text rows and layout
- assigning null to an attached active component removes the overlay and unregisters sampling

### Update and frame sampling

An inactive `FPSComponent` must not throw during update. It should simply do nothing while no overlay exists.

Global frame sampling methods should count only active components that currently own an initialized overlay and remain hierarchy-enabled.

## Serialization and Deserialization

### Editor persistence

`FPSComponentPersistenceDescriptor` must treat the font reference as optional.

Rules:

- save a font reference when one is explicitly present or inferable through editor-only font ownership rules
- omit the font reference when no font exists
- deserialize successfully when the record contains no font reference
- leave `fpsComponent.Font` null when no reference is available

### Runtime packaged scene loading

Runtime FPS component deserialization must also accept an absent packaged font reference. The loaded component should remain valid and inactive until some runtime code assigns a font.

## Editor Font Reference Rules

Editor-only reference inference for `ui-font` should stay intact, but it must no longer depend on `Core`.

Affected editor paths include:

- scene asset reference inference
- scene persistence descriptors for text-bearing components
- editor scene asset reference resolution

Those paths should consult `EditorCore` state, or another explicit editor-owned dependency, when they need to recognize or resolve the special editor UI font reference.

## Error Handling

The change deliberately removes failure behavior for missing FPS fonts during attach and deserialization.

Failure should still occur when:

- code that requires a concrete non-null font receives null in APIs that are explicitly font-required
- an editor-only `ui-font` reference is requested outside editor context
- a serialized font reference exists but cannot be resolved

Failure should not occur merely because an `FPSComponent` has no font.

## Testing

Update and extend tests to cover:

- `FPSComponent` can be constructed and attached without a font
- `FPSComponent` without a font does not create overlay entities
- `FPSComponent` without a font does not throw during update
- assigning a font after attach creates the overlay
- clearing the font after attach removes the overlay and stops active sampling
- changing fonts after attach updates overlay text components correctly
- editor FPS persistence can deserialize records with no font reference
- runtime FPS deserialization can deserialize records with no packaged font reference
- editor font-reference inference still works through editor-only font ownership, not `Core`

Existing tests that assert constructor fallback to `Core.DefaultFontAsset` or deserialization fallback to `Core.DefaultFontAsset` must be removed or rewritten.

## Risks

The main risk is incomplete removal of `Core.DefaultFontAsset` references across editor serialization and packaging paths. If any of those remain, the engine will compile inconsistently or preserve the old implicit behavior through hidden side channels.

The second risk is leaving `FPSComponent` half-initialized when font assignment changes after attach. The implementation should centralize overlay creation and teardown so activation and deactivation are symmetric.

## Implementation Notes

- keep overlay creation and teardown in explicit private instance methods rather than scattered conditional blocks
- do not reintroduce a runtime fallback font through helper methods
- preserve one-class-per-file and XML comment requirements
- update tests first before production code changes
