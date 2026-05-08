# Component Platform Tab Overrides Design

## Summary

The editor component inspector should always show a platform tab row directly under the selected entity name. That row is the primary entry point for per-platform component authoring. The inspector should switch the entire component editor context when the active platform changes, while keeping the vertical layout lightweight and avoiding the attached lower-panel chrome used by the build dialog and asset processor.

The default authoring model is shared/common state. Every component starts as one common baseline that applies to every platform. When a user edits a component while a specific platform tab is active, that platform gets an independent component override copied from the common baseline. After that point, the platform-specific component state is edited independently.

This first pass standardizes the inspector platform tabs and lands the component-scoped override model. Property-level copy actions such as `Send to -> PS2` are explicitly separate one-shot copy tools and should not create inheritance or synchronization.

## Goals

- Always show platform tabs under the selected entity name in the component inspector.
- Reuse the shared `PlatformTabStripView` mechanics for overflow, arrows, and keyboard reveal.
- Keep the inspector layout compact by avoiding an attached chrome panel below the tabs.
- Switch the entire component editor context when the active platform changes.
- Use a common baseline by default until a platform diverges through editing.
- Create independent per-platform component overrides only when the user actually edits under a platform tab.
- Persist and reload component-level platform overrides cleanly.

## Non-Goals

- Do not add mixed common/platform field rendering in the first pass.
- Do not add linked inheritance indicators per property.
- Do not introduce synchronization between platforms after divergence.
- Do not implement property-level `Send to ->` in this pass.
- Do not redesign the shared tab visuals again as part of this work.

## User Experience

### Inspector Layout

The component inspector layout should be:

1. Selected entity name.
2. Platform tab row directly below the entity name.
3. Component editors directly below the tab row with normal inspector spacing.

The tab row should use the same shared behavior already introduced elsewhere:

- horizontal overflow
- left/right arrows
- keyboard auto-reveal
- same tab visual language

Unlike the build dialog and asset processor, the component inspector should not add a bordered attached panel under the tabs. The component editors should continue using the normal inspector surface.

### Editing Model

The inspector exposes two conceptual editing contexts:

- `Common`
- one tab per supported platform, such as `Windows`, `PS2`, `GameCube`, or `Wii`

Behavior:

- Every component initially exists only in `Common`.
- Every platform tab initially renders the effective values from `Common`.
- If the user edits a component while a non-common platform tab is active, the editor creates a component override for that platform by copying the current common state.
- After that copy, edits on that platform affect only that platform override.
- The override is independent from the common baseline and from other platforms.

This makes the default path simple: everything is shared until the user intentionally changes a platform.

## Data Model

The first pass should use component-scoped overrides rather than property-scoped overrides.

For each logical component attached to an entity:

- one common/base authored state exists
- zero or more platform-specific override states may exist

Effective state resolution:

1. If the selected platform has an override for the component, render and edit that override.
2. Otherwise, render the common/base component state.

Override creation:

1. User activates a non-common platform tab.
2. User edits any field for a component.
3. Editor checks whether an override exists for that component on the active platform.
4. If not, it creates one full copy of the component from the current effective baseline.
5. The edit is applied to that platform override.

This model keeps the first version coherent and avoids a half-inherited, half-overridden field mix inside one component editor.

## Copy Semantics

Property copy is a separate future utility, not part of inheritance.

`Send to -> <platform>` should mean:

- copy the current property value into the specified target platform override
- no persistent relationship is created
- after the copy, source and target may diverge freely

That future action should layer on top of the component-scoped override model rather than replacing it.

## Architecture

### Shared Tab Strip

The inspector should reuse the editor-owned `PlatformTabStripView` rather than implementing another tab row.

Responsibilities of the shared strip remain:

- tab creation
- overflow arrows
- auto-reveal
- keyboard navigation
- selection change callbacks

The inspector host remains responsible for:

- placing the strip under the selected entity name
- supplying platform ids and the selected tab
- reacting to tab changes
- swapping the component editor context

### Inspector Host

The component inspector host should:

- always build the tab row when an entity is selected
- include a `Common` tab plus one tab for each supported platform
- keep track of the active inspector platform context
- rebuild or refresh the component editors when the active tab changes
- route component edits through an override-aware authoring layer

### Override-Aware Authoring Layer

The component editing path should gain an override-aware layer that can:

- resolve the effective component state for a selected platform
- create a platform override from common when the first edit occurs
- update an existing override when already diverged
- persist the resulting common and platform-specific component state

This should be implemented in the editor authoring/model layer, not as UI-only patch logic.

## Persistence

Persistence must support:

- one common component state
- zero or more per-platform component override states

The persisted representation should allow the editor to reconstruct:

- the common baseline component
- which platforms have overrides
- the component payload for each override

Loading rules:

- if no override exists for the selected platform, the editor materializes the common component values in the inspector
- if an override exists, the editor materializes that platform-specific component values instead

The persistence layer should fail loudly for invalid or incomplete override data rather than silently fabricating defaults.

## Error Handling

- Missing supported platform list should fail immediately.
- Invalid platform ids should fail immediately.
- Editing a component on a platform tab without a valid common baseline should fail instead of inventing defaults.
- Invalid persisted override payloads should fail loudly during load.

## Implementation Scope

### In Scope

- shared platform tab row under the selected entity name
- inspector context switching by platform tab
- common baseline plus component-level per-platform overrides
- first-edit override creation
- persistence and reload for component-level overrides

### Out of Scope

- property-level `Send to ->`
- per-property override badges
- explicit `Customize Platform` gating
- mixed common/platform field display
- synchronization between platform copies

## Verification

This should be validated primarily through focused editor tests around:

- tab-row presence and selection behavior in the inspector
- effective-state resolution from common versus platform override
- first-edit override creation
- persistence and reload of component overrides

Manual verification should confirm:

- the tab row always appears under the selected entity name
- switching tabs swaps the entire component editor context
- untouched platforms read from common
- first edit on a platform creates divergence
- later edits remain independent on that platform

