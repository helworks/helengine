# Dynamic Camera Properties Design

## Goal

Make the editor properties inspector support a metadata-driven default component inspector, starting with `CameraComponent`, so supported public properties are rendered dynamically and editor-only exclusions are controlled through explicit attributes instead of ad-hoc hardcoding.

## Scope

This change covers:

- a reusable default inspector path for component properties
- editor metadata attributes for property visibility, display name, and ordering
- migration of `CameraComponent` to the new metadata-driven path
- tests that verify reflected Camera properties render and update correctly

This change does not yet cover:

- nested custom editors for `CameraClearSettings`
- nested custom editors for `CameraRenderSettings`
- automatic support for every arbitrary CLR type
- a full custom-editor registry for all components

## Current Problem

`ComponentPropertiesView` already reflects component properties, but the rules are too loose for a stable editor contract:

- unsupported runtime-facing properties can leak into the inspector
- property names come straight from CLR names
- display order is not explicit
- there is no first-class property-level editor metadata

For `CameraComponent`, this means the editor cannot move toward a true dynamic inspector without risking exposure of engine/runtime properties that should stay hidden until custom editors exist.

## Desired Outcome

The editor should have a default reflected inspector with explicit metadata rules:

- supported public properties render automatically
- unwanted properties are hidden with attributes
- display labels and ordering are controlled explicitly
- unsupported complex property types are excluded by default

`CameraComponent` becomes the first component cleaned up under that contract.

## Architecture

### Metadata Ownership

Editor property metadata attributes should live with component types, so they belong in `helengine.core`.

The first attribute set should include:

- `EditorPropertyHiddenAttribute`
- `EditorPropertyDisplayNameAttribute`
- `EditorPropertyOrderAttribute`

These attributes define the editor contract directly on component properties without requiring the editor to guess intent from names or reflection order.

### Inspector Discovery Path

`helengine.editor` should own the reflected inspector discovery path.

`ComponentPropertiesView` remains the renderer for property rows, but property discovery/filtering should be expressed as a focused editor-side metadata path rather than scattered inline checks. The design should keep row rendering and property eligibility logic separable so later custom editors can plug in cleanly.

### Default Inspector Rules

The default reflected inspector should:

- inspect public instance properties only
- require `CanRead`
- skip indexers
- skip known non-user properties such as `Parent`
- skip properties marked with `EditorPropertyHiddenAttribute`
- sort properties by `EditorPropertyOrderAttribute` first, then by a stable fallback
- use `EditorPropertyDisplayNameAttribute` when present, otherwise fall back to the CLR property name

The default reflected inspector should render only properties whose types map to supported editor row kinds. For this first pass, unsupported property types should be excluded instead of being shown as noisy read-only strings.

That exclusion rule is intentional. It keeps the dynamic inspector clean now and leaves room for explicit custom editors later.

## CameraComponent First Pass

`CameraComponent` should use the new metadata contract to produce a clean default inspector.

The first pass should expose only direct supported properties that make sense in the current row system, such as:

- `CameraDrawOrder`
- `LayerMask`
- `NearPlaneDistance`
- `FarPlaneDistance`

Properties that are runtime-owned, nested, unsupported, or otherwise not ready for default editing should be hidden explicitly or naturally excluded by type support. This includes editor-unfriendly values like:

- `ClearSettings`
- `RenderSettings`
- `RenderQueue2D`
- `RenderQueue3D`
- `RenderTarget`

`Viewport` should remain out of scope for this first pass unless its type is given a supported dedicated row/editor. The goal is to land a clean Camera inspector, not to force incomplete type coverage.

## Extensibility

This design is intentionally split between default reflected behavior and future custom editors.

Later, when nested settings are ready:

- a custom editor/provider can take over `CameraClearSettings`
- a custom editor/provider can take over `CameraRenderSettings`
- additional supported property types can be added without changing the metadata contract

That means this first pass is not throwaway work. It establishes the default editor rules that custom editors will extend, not replace.

## Error Handling

The inspector should continue using the existing supported row behavior for editable types. Unsupported types should simply be omitted from the default inspector rather than rendered incorrectly or through a best-effort fallback.

If metadata is missing:

- display name falls back to the CLR property name
- ordering falls back to the stable default sort
- visibility defaults to visible, subject to the supported-type filter

## Testing

### Metadata Discovery Tests

Add tests that verify:

- hidden properties are excluded from the reflected inspector
- display-name metadata is used for row labels
- order metadata determines rendered order
- unsupported complex property types are excluded from the default inspector

### Camera Inspector Tests

Add tests that verify:

- `CameraComponent` renders the intended default-editable rows
- hidden/runtime Camera properties do not appear
- editing one reflected Camera scalar property writes back to the component

### Integration Level

Tests should stay at the `ComponentPropertiesView` and properties-panel rendering level so they verify the real behavior the editor uses, not just attribute parsing in isolation.

## File Boundaries

Expected areas of change:

- `engine/helengine.core/components/CameraComponent.cs`
- new editor-property attribute files under `engine/helengine.core`
- `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- editor tests covering reflected property rendering and Camera behavior

The implementation should avoid creating a second competing property-definition system. The new metadata and discovery path should sit directly on top of the current row-based inspector.

## Success Criteria

This work is complete when:

- `CameraComponent` properties shown in the inspector come from the dynamic reflected path
- hidden Camera properties stay out of the inspector through explicit metadata
- row labels and order are metadata-controlled
- unsupported nested/runtime properties are not rendered by default
- regression tests cover discovery, rendering, ordering, and write-back behavior
