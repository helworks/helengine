# Camera Clear Settings Editor Design

## Goal

Extend the dynamic component inspector so complex property types can provide custom editors, starting with `CameraClearSettings`, and render `CameraComponent.ClearSettings` as a collapsible nested section inside the Camera inspector.

## Scope

This change covers:

- a reusable editor-side custom property editor provider contract
- type-based custom editor resolution layered on top of the reflected inspector
- a `CameraClearSettings` custom editor
- tests for provider matching, nested rendering, collapse state, and struct write-back

This change does not yet cover:

- `CameraRenderSettings`
- arbitrary recursive object inspection
- a visual color picker widget
- a general-purpose nested inspector for all structs

## Current Problem

The new dynamic inspector handles supported primitive/editor-friendly property types through the reflected descriptor builder, but unsupported complex types are excluded.

That is the correct default behavior, but it means `CameraComponent.ClearSettings` cannot yet be edited even though it is a high-value authored camera setting.

The next step is not to weaken the default inspector and let arbitrary complex types leak through. The next step is to add a clean custom-editor path that can take over specific property types intentionally.

## Desired Outcome

The dynamic inspector should:

- keep the reflected default path as the baseline
- allow custom editors to claim specific property types
- render `Clear Settings` inside the Camera component section as a collapsible nested section
- write nested edits back through `CameraComponent.ClearSettings` by rebuilding the struct value

This should become the pattern used later for `CameraRenderSettings` and other complex authored types.

## Architecture

### Provider Layer

Add an editor-side custom property editor provider contract, owned by `helengine.editor`.

The provider contract should:

- inspect a reflected property
- decide whether it can handle the property type
- produce a custom property editor descriptor/definition when it matches

The reflected inspector keeps working as it does now for properties that no provider handles.

This is an extension of the dynamic system, not a replacement for it.

### Matching Model

For this first pass, provider matching should be by property type.

`CameraClearSettings` should be handled by a dedicated provider such as:

- `CameraClearSettingsPropertyEditorProvider`

This keeps the contract simple and explicit. If later editors need richer matching rules, the provider layer can evolve without changing the Camera clear-settings UI contract.

### Integration with the Current Inspector

`ReflectedComponentPropertyDescriptorBuilder` should check custom providers before the default primitive row mapping.

If a provider matches:

- the property is emitted as a custom editor descriptor
- the default row-kind mapping does not run for that property

If no provider matches:

- the existing metadata-driven reflected row rules continue unchanged

## Camera Clear Settings Editor

### Presentation

`CameraComponent.ClearSettings` should render as one collapsible nested section labeled `Clear Settings`.

That nested section lives inside the Camera component section and behaves like a property-owned subsection, not a top-level component section.

When expanded, it shows sub-controls for:

- `Clear Color Enabled`
- `Clear Color`
- `Clear Depth Enabled`
- `Clear Depth`
- `Clear Stencil Enabled`
- `Clear Stencil`

When collapsed, those sub-controls are hidden and the nested section preserves its current values and collapsed state.

### Editor Controls

The nested editor should reuse existing row behaviors where possible:

- booleans reuse checkbox rows
- `ClearDepth` and `ClearStencil` reuse scalar rows
- `ClearColor` uses a dedicated `float4` editor path with four scalar fields for `R`, `G`, `B`, and `A`

This is intentionally practical. A color picker is not required for this first pass.

## State and Write-Back

### Collapse State

`ComponentPropertiesView` should own nested custom-editor expansion state in the same spirit as existing component-section collapse state.

That state should be keyed by a stable property identity, such as:

- target component instance + property name

The goal is to preserve nested expansion/collapse across relayouts and refreshes while the inspected component remains the same.

### Struct Updates

`CameraClearSettings` is a value type, so edits cannot mutate one nested field in place.

Every committed nested edit must:

1. read the current `CameraClearSettings` value from `CameraComponent.ClearSettings`
2. replace the edited field in a new struct value
3. assign the rebuilt struct back through the owning property

This is a strict requirement. The implementation should not try to fake mutable nested state or rely on best-effort field mutation.

### Mutation Notifications

Each committed nested edit should mark the scene mutated exactly once, matching the existing behavior for scalar and boolean property rows.

## Extensibility

This design should establish a general custom-editor path for complex property types.

After `CameraClearSettings`, the same provider contract can support:

- `CameraRenderSettings`
- other authored struct/value-object properties
- richer editor surfaces for types that should not appear as primitive rows

The default reflected inspector remains the fallback path for simple properties, while custom providers opt in for complex ones.

## Testing

### Provider Resolution Tests

Add tests that verify:

- `CameraClearSettings` is claimed by the custom provider
- provider-backed properties are included even though the default primitive mapper would exclude them
- unsupported complex properties without a provider remain excluded

### Rendering Tests

Add tests that verify:

- Camera inspector shows `Clear Settings` as a nested collapsible section
- expanding it renders the expected six sub-controls
- collapsing it hides those sub-controls without removing the parent Camera section

### Write-Back Tests

Add tests that verify:

- toggling `Clear Color Enabled` updates `CameraComponent.ClearSettings.ClearColorEnabled`
- editing `Clear Depth` updates `CameraComponent.ClearSettings.ClearDepth`
- editing one `Clear Color` channel updates the `float4` inside the reassigned `CameraClearSettings` struct

### Regression Tests

Add tests that verify:

- existing primitive Camera rows still render in metadata order
- `Clear Settings` no longer disappears from the Camera inspector
- unrelated unsupported complex properties still remain excluded by default

## File Boundaries

Expected areas of change:

- `engine/helengine.editor/components/ui/ReflectedComponentPropertyDescriptorBuilder.cs`
- new editor-side custom property editor provider files under `engine/helengine.editor/components/ui`
- `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- possibly `engine/helengine.editor/components/ui/ComponentPropertyRowKind.cs` if a dedicated nested/custom row kind is needed
- editor tests covering dynamic inspector custom-editor behavior

The implementation should preserve the current separation:

- core component metadata stays in `helengine.core`
- custom editor resolution and rendering stay in `helengine.editor`

## Success Criteria

This work is complete when:

- the dynamic inspector resolves `CameraClearSettings` through a type-matched custom editor
- `CameraComponent.ClearSettings` renders as a collapsible nested section labeled `Clear Settings`
- nested edits rebuild and assign a full `CameraClearSettings` struct back to the owning camera
- scene mutation notifications fire for committed nested edits
- focused regressions cover provider matching, nested rendering, collapse state, and write-back behavior
