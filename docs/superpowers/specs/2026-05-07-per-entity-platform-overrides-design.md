## Summary

Add editor-only per-entity platform overrides for scenes.

Each entity keeps one shared base definition and may optionally define sparse overrides for specific supported platforms. The Properties panel exposes `Base` plus one tab per supported project platform. The entity name stays shared and editable only on `Base`. All other entity-local state may vary by platform:

- enabled or disabled
- component membership
- component settings

Overrides are resolved during packaging. Runtime players receive only the final flattened target-platform scene and do not load or understand platform-override metadata.

## Goals

- Let one scene author different entity behavior and composition per target platform.
- Keep shared edits flowing from a base entity definition unless a platform explicitly diverges.
- Keep hierarchy ownership local to each entity instead of letting parents remove children per platform.
- Make the system editor-only and packaging-resolved so runtime players stay simple.
- Add a `Copy From Platform...` workflow for fast override authoring.

## Non-Goals

- No runtime system for interpreting platform overrides.
- No parent-level child removal or child list mutation by platform.
- No per-property diff encoding in the first slice.
- No per-platform entity renaming.
- No packaging of disabled platform entities as dormant runtime records.

## User Model

Each entity has:

- one shared base state
- zero or more platform override records keyed by platform id

Users edit the selected entity through platform tabs in the Properties panel:

- `Base` edits shared state
- `Windows`, `PS2`, and other supported platforms edit that entity's override for that platform

If a platform tab has no explicit override yet, the entity inherits from `Base`. The first platform-specific change materializes an override for that entity.

The entity name is always shared:

- editable on `Base`
- read-only on platform tabs

To remove a child entity on one platform, the user selects that child and disables it on that platform. The parent entity never owns per-platform child removal.

## Data Model

### Scene-Level Storage

Scene files persist:

- base scene entity tree
- editor-only per-platform override metadata for entities

Platform override metadata is authoring data only. It must not be emitted into packaged runtime scenes.

### Entity-Level Storage

Each entity gains editor-side platform override storage with this conceptual shape:

- `PlatformOverrides`
  - keyed by stable platform id
  - value is one entity override record

Each entity override record contains only entity-local state:

- optional enabled-state override
- platform component removals
- platform component additions
- platform component payload overrides

### Component Override Shape

The first slice stores full component payload overrides, not fine-grained per-property deltas.

That means:

- if a component is overridden on one platform, the override stores one full serialized component payload for that platform
- if a component remains inherited, no platform payload is stored

This keeps serialization, copying, and packaging much simpler than per-property patch records.

## Resolution Model

To resolve one entity for target platform `P`:

1. Start from the base entity state.
2. If override `P` exists, apply the entity enabled-state override.
3. Apply component removals from override `P`.
4. Apply component additions from override `P`.
5. Apply full component payload overrides from override `P` to the resulting component set.

The resolved entity for packaging is therefore one normal flattened entity definition.

Important rules:

- parent overrides do not remove children
- each child entity resolves independently against the same target platform
- disabled resolved entities are omitted entirely from the packaged scene

## Properties Panel UX

The Properties panel gains platform tabs when an entity is selected:

- `Base`
- one tab per supported project platform

The name row is always present, but:

- editable only on `Base`
- read-only on platform tabs

After the name row, all entity-local properties become platform-sensitive:

- enabled toggle
- component list
- add component
- remove component
- component property editors

Platform tab behavior:

- inherited values are shown when no explicit platform override exists
- the first edit materializes the override for that entity and platform
- removed components disappear from that platform tab
- platform-added components appear only on that platform tab

### Copy Workflow

Platform tabs expose `Copy From Platform...`.

Rules:

- available only on platform tabs, not `Base`
- source choices include `Base` and other platform tabs
- copy uses the fully resolved source entity state
- copy replaces the current target platform override for that entity
- copy is explicit and coarse, not field-by-field merge magic

This gives users a fast way to start from another resolved platform shape and then refine.

## Packaging Behavior

Platform overrides are resolved entirely in the editor build pipeline.

For target platform `P`:

- the scene packager resolves each entity against `P`
- entities disabled for `P` are omitted from the packaged scene
- enabled entities are emitted with their resolved final component set and final component payloads
- runtime output contains no platform override metadata

This applies to every packaged target, including native players.

## Runtime Behavior

Runtime players stay unchanged conceptually:

- they load one normal packaged scene
- they do not know platform overrides exist
- they never evaluate platform tabs or inheritance rules

This keeps runtime memory, code complexity, and native codegen scope low.

## Editor Integration

### Serialization

Scene authoring serialization must preserve:

- base entity definition
- editor-only per-platform entity override records

Packaged scene serialization must preserve only:

- the resolved target-platform entity tree

### Entity Editing

The properties system needs a platform-aware entity editing context so component add/remove/property edit commands know whether they are editing:

- base state
- one platform override record

This context should stay in editor-side services or models, not inside runtime entity/component classes.

### Project Platforms

The visible platform tabs should follow the project's supported platform list. If no supported platforms are configured, only `Base` should appear.

## Safety Rules

- Entity names are shared and cannot diverge by platform.
- Parent entities cannot remove children by platform.
- Platform-disabled entities are not packaged for that target.
- Platform-added components exist only on that entity for that platform.
- Base edits should continue to affect platforms that still inherit.
- Copying from another platform replaces the target override instead of silently merging ambiguous state.

## Testing

Add coverage for at least:

- base-only entity resolves identically for multiple platforms
- platform-disabled entity is omitted from packaged target scene
- platform component removal omits only that component on that platform
- platform component addition appears only on that platform
- platform component payload override replaces the inherited component settings
- child entities resolve independently from parents
- entity name remains shared and base-only editable
- `Copy From Platform...` produces the same resolved entity state as the source
- properties panel shows `Base` plus supported platform tabs and enforces base-only rename behavior

## Recommended First Slice

1. Add editor-side scene/entity override data structures and serialization.
2. Add platform tabs to entity properties.
3. Support per-platform entity enabled state.
4. Support per-platform component add/remove.
5. Support full component payload overrides per platform.
6. Resolve overrides in the scene packager and omit disabled entities.
7. Add `Copy From Platform...` for entity overrides.

This sequence gets the full authoring model in place without dragging runtime into the feature.
