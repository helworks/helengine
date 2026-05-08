## Summary

Add editor-only per-entity platform overrides for scenes using per-platform sidecar scene files.

Each authored scene keeps:

- one base scene file: `scene.helen`
- zero or more platform sidecars: `scene.windows.helen`, `scene.ps2.helen`

The base file owns the shared entity tree, names, and default entity state. Sidecars own only per-platform entity-local overrides keyed by stable entity id. The Properties panel exposes `Base` plus one tab per supported project platform. The entity name stays shared and editable only on `Base`. Everything else about that entity may vary by platform:

- enabled or disabled
- transform values
- component membership
- component settings

The editor loads the base scene plus every discovered sidecar when a scene opens. Packaging resolves `base + target platform sidecar` into one flattened runtime scene. Runtime players do not load or understand sidecar metadata.

## Goals

- Reduce git conflicts by separating shared scene edits from platform-specific edits.
- Let one base scene author different entity behavior and composition per target platform.
- Keep shared edits flowing from the base scene unless a platform explicitly diverges.
- Keep hierarchy ownership local to each entity instead of letting parents remove children per platform.
- Keep the system editor-only and packaging-resolved so runtime players stay simple.
- Add a `Copy From Platform...` workflow for fast override authoring.

## Non-Goals

- No runtime system for interpreting platform overrides.
- No full independent per-platform scene copies.
- No parent-level child removal or child list mutation by platform.
- No per-platform entity renaming.
- No packaging of disabled platform entities as dormant runtime records.

## User Model

Users author one base scene and optional sidecars.

Base:

- `scene.helen`
- owns the shared entity tree
- owns shared entity names
- owns the default entity transforms, components, and values

Platform sidecars:

- `scene.windows.helen`
- `scene.ps2.helen`
- store only entity-local overrides keyed by stable base entity id

Users edit the selected entity through platform tabs in the Properties panel:

- `Base` edits shared scene state from `scene.helen`
- `Windows`, `PS2`, and other supported platforms edit that entity's sidecar override state

If a platform tab has no explicit override yet, the entity inherits from `Base`. The first platform-specific change materializes a sidecar override for that entity and platform.

The entity name is always shared:

- editable on `Base`
- read-only on platform tabs

To remove a child entity on one platform, the user selects that child and disables it on that platform. The parent entity never owns per-platform child removal.

## File Model

### Base Scene File

`scene.helen` persists:

- scene id
- scene settings
- shared root entity tree
- stable entity ids
- shared component payloads
- shared asset references

### Platform Sidecar Files

`scene.<platform>.helen` persists:

- target platform id
- overrides keyed by stable entity id from the base scene

Each sidecar override contains only entity-local platform state:

- optional enabled-state override
- optional local transform override
- component removals
- component additions
- full component payload overrides

Sidecars must not own:

- entity names
- parent-child hierarchy
- independent root entity trees

## Data Model

### Live Editor Model

Each `EditorEntity` already owns an `EntitySaveComponent`. That hidden editor-only component is the correct live seam for platform override metadata.

The live editor entity remains the base/shared entity state.

`EntitySaveComponent` should gain editor-only platform override storage keyed by platform id.

### Serialized Base Scene Model

`SceneEntityAsset` stays focused on the shared entity definition:

- `Id`
- `Name`
- local transform
- base components
- children

The base scene file should not embed platform override data.

### Serialized Sidecar Model

Introduce a dedicated platform-sidecar asset model that stores:

- sidecar platform id
- entity override records keyed by entity id

Each entity override record contains:

- entity id
- optional enabled override
- optional local transform override
- component removals
- component additions
- full component payload overrides

The first slice stores full component payload overrides, not fine-grained per-property diffs.

## Resolution Model

To resolve one entity for target platform `P`:

1. Start from the base entity state in `scene.helen`.
2. Look up override record `P` for that entity id in `scene.P.helen`.
3. Apply entity enabled override if present.
4. Apply local transform override if present.
5. Apply component removals.
6. Apply component additions.
7. Apply full component payload overrides to the resulting component set.

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
- transform rows
- component list
- add component
- remove component
- component property editors

Platform tab behavior:

- inherited values are shown when no explicit sidecar override exists
- the first edit materializes the sidecar override for that entity and platform
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

## Scene Open And Save Behavior

### Open

Opening `scene.helen` should:

1. load the base scene
2. discover every matching `scene.<platform>.helen` sidecar
3. load all valid sidecars immediately
4. attach their override data to the loaded editor entities by stable entity id

The editor should not require reopening the scene when switching tabs.

### Save

Saving should be split by edit scope:

- editing `Base` writes only `scene.helen`
- editing `Windows` writes only `scene.windows.helen`
- editing `PS2` writes only `scene.ps2.helen`

Base save must not rewrite unrelated sidecars except where stable entity id maintenance requires cleanup.

If a base entity is removed, orphaned sidecar overrides for that entity id should be removed or flagged clearly on the next save.

## Packaging Behavior

Platform overrides are resolved entirely in the editor build pipeline.

For target platform `P`:

- load `scene.helen`
- load `scene.P.helen` when present
- resolve each entity against `P`
- omit entities disabled for `P`
- emit enabled entities with their resolved final transform, component set, and component payloads
- write one normal packaged runtime scene with no sidecar metadata

This applies to every packaged target, including native players.

## Runtime Behavior

Runtime players stay unchanged conceptually:

- they load one normal packaged scene
- they do not know sidecars exist
- they never evaluate platform tabs or inheritance rules

This keeps runtime memory, code complexity, and native codegen scope low.

## Editor Integration

### Serialization

Scene authoring serialization must preserve:

- base scene in `scene.helen`
- per-platform override sidecars in `scene.<platform>.helen`

Packaged scene serialization must preserve only:

- the resolved target-platform entity tree

### Entity Editing

The properties system needs a platform-aware entity editing context so transform edits, component add/remove, and component property edits know whether they are editing:

- base state
- one platform sidecar override

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
- Sidecar files must never become full scene copies.

## Testing

Add coverage for at least:

- opening a base scene discovers and loads all matching platform sidecars
- saving a platform tab writes only the matching sidecar file
- base-only entity resolves identically for multiple platforms
- platform-disabled entity is omitted from packaged target scene
- platform transform override changes only that platform
- platform component removal omits only that component on that platform
- platform component addition appears only on that platform
- platform component payload override replaces the inherited component settings
- child entities resolve independently from parents
- entity name remains shared and base-only editable
- `Copy From Platform...` produces the same resolved entity state as the source
- Properties panel shows `Base` plus supported platform tabs and enforces base-only rename behavior

## Recommended First Slice

1. Add sidecar asset types and sidecar discovery for `scene.<platform>.helen`.
2. Add editor-side entity override storage on `EntitySaveComponent`.
3. Persist and load all platform sidecars alongside the base scene.
4. Add platform tabs to entity properties.
5. Support per-platform entity enabled state and transforms.
6. Support per-platform component add/remove and full component payload overrides.
7. Resolve sidecars in the scene packager and omit disabled entities.
8. Add `Copy From Platform...` for entity overrides.

This sequence gets the sidecar authoring model in place without dragging runtime into the feature.
