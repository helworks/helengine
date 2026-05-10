# Properties Platform Override Indicators Design

## Summary

The properties inspector already supports platform tabs for both component overrides and entity transform overrides. The next missing piece is visibility and control: the user needs to see which values differ from `common`, and they need a direct way to revert a platform override back to inherited common behavior.

This design adds:

- row-level override indicators for transform rows and component property rows
- component-header override indicators for component existence overrides
- a `Revert` action wherever an override is active

The behavior must preserve the existing sparse override model. Override state stays editor-authored and platform-specific. Reverting an override removes platform-authored data instead of copying the common value into the platform override.

## Goals

- Make platform overrides visible in the inspector without opening serialized data or guessing from value differences.
- Let the user revert one transform row, one component property row, or one component existence override back to `common`.
- Preserve sparse override semantics so future edits to `common` flow into reverted values automatically.
- Keep the UI behavior consistent between transform rows and component rows.

## Non-Goals

- No per-axis or per-subfield override indicators. The user explicitly wants row-level indicators.
- No new override authoring model. This is layered on top of the existing transform override service and component platform override service.
- No runtime-facing changes. This is editor UX and editor-authored metadata behavior only.

## Terminology

- `common`: shared default authored state
- `platform override`: platform-specific authored state layered on top of `common`
- `component existence override`: whether a component exists or does not exist on a platform, regardless of its property values
- `property override`: whether a single editable property differs from `common`

## UX Behavior

### Transform Rows

The transform block contains three editable rows:

- `Position`
- `Rotation`
- `Scale`

On a non-`common` platform tab:

- a row is considered overridden if any underlying channel for that row is overridden on the active platform
- the whole row shows a subtle override border
- the row shows a compact `Revert` button

Clicking `Revert`:

- removes the platform override state for that whole row
- reapplies the effective inherited common value to the live entity
- updates the inspector immediately

On the `common` tab:

- transform rows do not show override borders
- transform rows do not show `Revert`

### Component Property Rows

For component properties shown inside `ComponentPropertiesView`:

- a row is considered overridden when the active platform value differs from the effective common value for that property
- the whole row shows a subtle override border
- the row shows a compact `Revert` button

Clicking `Revert`:

- removes the platform-authored value override for that property
- rebuilds the effective editable platform component from `common` plus any remaining platform overrides
- updates the visible row value immediately

### Component Section Headers

Component existence override is shown at the component section header level, not the row level.

Examples:

- component exists only on `ps2`
- component exists in `common` but is removed on `ps2`

When component existence differs from `common` on the active platform:

- the component section header shows the same override border treatment
- the header shows a compact `Revert` button

Clicking `Revert`:

- removes the component existence override for the active platform
- restores exact `common` existence behavior

That means:

- for a platform-only component, `Revert` removes it from the active platform
- for a common component removed on a platform, `Revert` restores it to match `common`

## Visual Design

The indicator styling should be consistent with the editor theme and remain subtle.

Rules:

- use an existing theme accent color
- do not introduce a warning or error style
- keep borders thin and readable
- only show the `Revert` button when an override is actually active

The design should use one visual pattern for:

- transform rows
- component rows
- component section headers

with only size/layout differences as needed.

## Data Model Semantics

### Transform Overrides

Transform storage remains sparse by channel in `SceneEntityPlatformTransformOverrideAsset`.

Row-level detection rules:

- `Position` row overridden if any of `HasLocalPositionOverride` channels are effectively platform-authored
- `Rotation` row overridden if any orientation override is platform-authored
- `Scale` row overridden if any of `HasLocalScaleOverride` channels are effectively platform-authored

Row-level revert rules:

- `Position` revert clears the position override for the active platform
- `Rotation` revert clears the orientation override for the active platform
- `Scale` revert clears the scale override for the active platform

Clearing the row override means the active platform inherits that row from `common`.

### Component Overrides

Component platform editing remains based on `ComponentPlatformEditingService` and serialized override payloads.

Property-level override detection must answer:

- does this property have a platform-authored value different from `common`?

Component existence override detection must answer:

- is the existence of this component on the active platform different from `common`?

Revert behavior must mutate persisted override state first, then refresh the view. The UI must never only patch detached in-memory editor objects while leaving serialized override metadata stale.

## Architecture

### Transform Override Responsibilities

`EntityPlatformTransformEditingService` will be extended with:

- row-level override state queries
- row-level revert operations

Expected responsibilities:

- determine whether `Position`, `Rotation`, or `Scale` is overridden for the active platform
- clear the override for one transform row
- reapply the effective common value to the live entity after revert

`PropertiesPanel` remains responsible only for:

- rendering row chrome
- wiring the `Revert` button input
- syncing visible fields after state changes

### Component Override Responsibilities

`ComponentPlatformEditingService` will be extended with:

- component existence override queries
- property override queries
- property revert operations
- component existence revert operations

`ComponentPropertiesView` remains responsible only for:

- rendering row/header override chrome
- wiring `Revert` button input
- rebuilding visible sections and rows after state changes

### Shared UI Pattern

The implementation should avoid separate ad hoc override styling for:

- transform rows
- component property rows
- component headers

Instead, use one reusable inspector override chrome pattern with:

- border on active override
- optional `Revert` action surface

That may be implemented as a reusable helper, view fragment, or shared row/header setup path, as long as the styling and behavior stay consistent.

## Interaction Rules

- Override indicators and `Revert` are only shown on non-`common` platform tabs.
- Revert immediately updates the inspector without requiring tab switching.
- Revert removes override metadata instead of copying common values into override storage.
- After revert, later edits on `common` propagate into that reverted property or component naturally.

## Error Handling

- If revert is requested without a selected entity or component context, do nothing.
- If the active platform is `common`, no revert action should be available.
- If an expected override entry is missing during revert, the operation should behave as a no-op and refresh to the effective common state.

## Testing

Add focused tests for:

### Transform Rows

- active platform transform row shows override state when platform-authored
- reverting one transform row clears the override and restores common value
- after revert, changing `common` and switching back reflects the updated common value

### Component Property Rows

- active platform component property row shows override state when platform-authored
- reverting one component property row clears the property override and restores common value
- after revert, changing `common` and refreshing reflects the updated common value

### Component Existence

- platform-only component section header shows override state
- reverting a platform-only component removes the section from the active platform
- platform-removed common component section header shows override state
- reverting a platform removal restores the component section to match `common`

### Inspector Presentation

- no override borders or revert buttons appear on the `common` tab
- row-level indicators remain row-level even when the underlying storage is sparse by channel

## Rollout Notes

This feature depends on the existing transform override projection behavior already being in place. No migration is required beyond continuing to read existing scenes that do not contain any transform override metadata.
