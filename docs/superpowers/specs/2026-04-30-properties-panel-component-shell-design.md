# Properties Panel Component Shell Design

## Overview

The editor needs a first-class component shell inside the `Properties` panel so components can be visually grouped, collapsed, and removed without turning the panel into a Unity-like inspector clone. This slice introduces a shared component header with a fixed remove action, collapse/expand behavior, and a confirmation modal before removal.

This design only covers existing component presentation and removal in the editor UI. It does not yet add the ability to create new components.

## Goals

- Give every component shown in the `Properties` panel a clear title bar.
- Let users collapse a component so only its title bar remains visible.
- Let users remove a component through a confirmation modal.
- Keep component-specific field rendering separate from generic panel chrome.
- Preserve the current selected entity after removal.

## Non-Goals

- Adding new components to entities.
- Persisting component collapsed state into scene files.
- Introducing component restrictions beyond leaving a seam for future rules.
- Redesigning the overall Properties panel layout.

## UX

Each component section becomes a component card with:

- A title bar at the top.
- The component name aligned on the left.
- A fixed `X` aligned on the right.
- A visually distinct header strip.

Behavior:

- Clicking the title bar toggles collapse and expand.
- Clicking the `X` does not toggle collapse.
- Collapsed components hide the full body and show only the title bar.
- Expanded components show the existing component fields exactly as they do today, inside the component body.

The remove confirmation modal should use the existing editor dialog style and present:

- Title: `Remove Component`
- Message: `Remove <Component Name> from <Entity Name>?`
- Actions:
  - `Remove`
  - `Cancel`

## Architecture

### Component Shell

Add a reusable component-section shell in the editor UI layer that owns:

- Header rendering
- Collapse state
- Remove button wiring

This shell wraps existing component-specific field rendering instead of replacing it.

### Component-Specific Rendering

`ComponentPropertiesView` stays responsible for component-specific fields and value editors. It should not own shared card chrome, shared collapse behavior, or the confirmation flow.

### Properties Panel Coordination

`PropertiesPanel` or a closely related editor-side helper should own:

- Per-entity collapsed-state tracking for component sections
- Opening the remove confirmation dialog
- Refreshing the selected entity’s displayed properties after removal

## State Model

Collapsed state is editor-local UI state only.

- Default state: expanded
- Scope: per component section for the currently selected entity
- Persistence: none for now

The design should leave one future seam for removal restrictions, such as a `CanRemoveComponent(...)` check, but all current scene-authoring components should be removable in this slice.

## Removal Flow

1. User clicks the `X` on a component title bar.
2. Editor opens the remove confirmation modal.
3. If the user confirms:
   - Remove the component from the selected entity.
   - Refresh the properties panel.
   - Keep the entity selected.
4. If the user cancels:
   - Close the modal.
   - Leave everything unchanged.

## Testing

Add focused tests for:

- Clicking a component header collapses the component body.
- Clicking the header again expands it.
- Collapsed state hides the body rows completely.
- Clicking the `X` opens the confirmation modal.
- Confirming removal deletes the component from the selected entity.
- Cancelling removal leaves the component intact.
- The properties panel refreshes after removal and keeps the same entity selected.
- Multiple components keep independent collapsed state.

## Implementation Notes

- Follow the editor’s existing dialog and chrome patterns.
- Do not introduce Unity-style inspector summaries or dense inline foldouts.
- Keep the generic shell behavior reusable so add-component UI can later build on the same visual language.
