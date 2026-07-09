# Blueprint Asset Design

**Goal:** Add first-class `Blueprint` assets to Helengine as reusable authored entity hierarchies that can be embedded into scenes with full platform support matching scene content, while keeping v1 scene instances as pure references with only instance-root transform overrides.

**Non-Goals For V1:**
- No nested blueprints.
- No runtime code-driven blueprint instantiation API.
- No per-instance editable child/component overrides in scenes yet.
- No multi-root blueprints.

**Core Decision:** A `Blueprint` is a standalone asset file that behaves like an embeddable single-root scene subtree. Scenes reference blueprints through a dedicated instance component, and the build pipeline expands blueprint content into packaged scene entity data after applying target-platform overrides.

## Requirements

The feature must satisfy these user-approved constraints:
- Asset name is `Blueprint`, not `Prefab`.
- A blueprint is stored as its own asset file on disk.
- A blueprint contains exactly one root entity.
- Scene instances are pure references in v1, with only position, rotation, and scale overrides on the placed instance root.
- Nested blueprints are not allowed in v1.
- Blueprints support the same platform authoring model scenes support now:
  - entity existence overrides
  - transform overrides
  - component overrides
- Blueprint changes propagate to all referencing scenes on reload/rebuild.
- Expanded blueprint children in scenes are visible but read-only.
- The internal data model must be override-ready so future scene-local overrides can be added without redesigning the asset format.

## Asset Model

`Blueprint` should be a new top-level asset container parallel to `SceneAsset`, but narrower in scope.

Scene today:
- many root entities
- scene settings
- scene-owned asset references

Blueprint:
- exactly one root `SceneEntityAsset`
- no scene settings
- blueprint-owned asset references
- same component payload serialization model as scenes
- same per-platform entity/component override payloads as scenes

The blueprint payload should reuse the existing entity/component persistence machinery wherever possible instead of creating a second persistence system. The design target is “scene subtree as asset,” not “new special object graph format.”

### Proposed Container Shape

Add a dedicated `BlueprintAsset` raw asset type:
- `RootEntity`
- `AssetReferences`
- stable file extension specific to blueprints

It should intentionally reuse:
- `SceneEntityAsset`
- `SceneComponentAssetRecord`
- `SceneAssetReference`
- existing automatic script/component serialization rules

This keeps blueprint content aligned with scenes and gives platform packaging one uniform subtree representation to work with.

## Instance Model

A scene should embed a blueprint through a dedicated instance root entity that owns a `BlueprintInstanceComponent`.

That instance root is scene-owned and stores:
- blueprint asset reference/path
- local position override
- local rotation override
- local scale override
- future-ready override payload storage, empty in v1

The blueprint-authored children are not serialized into the scene as ordinary scene-owned entities. Instead, the editor materializes them as an expanded inherited subtree under the instance root.

### Why The Instance Root Exists

The instance root gives the scene a stable placement object that:
- carries the asset reference
- owns the legal transform override
- provides a clear hierarchy node for selection and refresh
- gives future override data a home

Without an explicit instance root, later support for per-instance edits becomes much harder.

## Future Override Readiness

Even though v1 instances are read-only, the data model must preserve stable source identity so later overrides can target specific inherited items.

The system should preserve stable blueprint-authored ids for:
- inherited entities
- inherited components

These ids must remain distinct from scene instance identity and runtime scene entity ids. Future override records should be able to say:
- blueprint child entity X has an overridden transform in scene instance Y
- blueprint component Z on entity X has property override P in scene instance Y

### Override Conflict Rule

Future per-property overrides must win over source blueprint changes for the overridden property only. Non-overridden properties should continue flowing from the source blueprint.

That rule should shape the internal identity model now, even though v1 exposes no override UI.

## Editor Behavior

The editor should treat a blueprint instance as a special scene node with expanded inherited children.

### Scene Authoring

When a user places a blueprint into a scene:
- create one scene-owned instance root entity
- attach `BlueprintInstanceComponent`
- set the blueprint asset reference
- expand the blueprint root and descendants beneath that node for visualization

### Read-Only Rules For V1

Users may:
- select the instance root
- edit the instance root transform
- change the referenced blueprint asset
- inspect inherited children and components

Users may not:
- move inherited children
- edit inherited component values
- add/remove inherited children
- detach inherited children from the instance
- save inherited children back as scene-owned content

### Visual Treatment

Inherited nodes should be clearly marked as blueprint-owned/read-only in the hierarchy and properties UX. The exact visual language can be decided during implementation, but the distinction must be obvious.

### Editing The Source

Opening the blueprint asset itself should permit normal editing of its internal hierarchy, because inside the blueprint editor that hierarchy is the source of truth rather than inherited content.

## Load, Refresh, And Save Behavior

### Blueprint File Load/Save

Blueprint files should use editor services parallel to scene save/load:
- load one blueprint asset file into editable editor entities
- save one editable blueprint root entity back into a blueprint asset file
- use the same component persistence registry and asset reference inference rules as scenes

### Scene Editor Load

When the editor loads a scene containing blueprint instances:
- load the scene-owned instance root entity
- resolve the referenced blueprint asset
- expand the blueprint subtree beneath the instance root
- mark expanded content read-only

### Scene Save

Scene save must serialize only:
- the instance root
- the `BlueprintInstanceComponent`
- the instance root transform
- any future instance override payloads

Scene save must not serialize the expanded inherited subtree as ordinary child entities. If it does, blueprint instances will silently degrade into copied scene content.

### Refresh

When a referenced blueprint changes and the scene reloads or rebuilds:
- all scene instances should pick up the new source content automatically
- instance root transforms must remain intact
- future override scaffolding must remain aligned by stable blueprint ids

## Build And Packaging

Blueprint support must follow the same cross-platform correctness bar as scenes.

### Key Architectural Decision

Blueprints should be expanded during editor/build processing, not resolved as a special runtime asset class in v1.

That means:
- scene packaging resolves referenced blueprints
- target-platform blueprint overrides are applied before expansion
- the packaged scene contains ordinary platform-resolved entities
- runtime scene loading stays simple and continues using `RuntimeSceneLoadService`

### Why Build-Time Expansion

Build-time expansion:
- fits naturally into the existing scene packaging pipeline
- gives Windows/DS/other targets identical semantics
- keeps runtime lean
- avoids adding a second runtime asset-loader path
- simplifies ownership, unloading, and diagnostics

### Expansion Order

For v1:
1. load scene asset
2. find blueprint instance references
3. resolve referenced blueprint asset
4. apply target-platform blueprint overrides
5. expand blueprint subtree under the scene instance root
6. emit final packaged scene entity tree

Because nested blueprints are disallowed in v1, no recursive expansion stack is needed beyond defensive cycle validation.

## Platform Support

Blueprints must support the same platform concepts scenes already support:
- per-platform entity existence overrides
- per-platform transform overrides
- per-platform component existence/value overrides
- target-platform packaging and rewrite behavior

This must hold in both places:
- inside the blueprint asset itself
- in the consuming scene build pipeline when blueprint content is expanded

The correct mental model is: a blueprint is authored content, not a convenience macro. It must be treated as first-class authored data all the way through the platform pipeline.

## Runtime Behavior

At runtime in v1, no dedicated blueprint asset resolution path is required.

Packaged scene runtime should:
- load ordinary expanded entities through `RuntimeSceneLoadService`
- preserve enough blueprint-origin metadata for diagnostics and future override support

If a runtime marker is useful, it should be lightweight and diagnostic-oriented, not a hard dependency for behavior in v1.

## Validation Rules

The editor/build system must enforce these rules:
- blueprint file must contain exactly one root entity
- nested blueprint instances are invalid in v1
- referenced blueprint asset must resolve
- cycles are invalid, even if only theoretically reachable
- expanded inherited children in scenes must not be persisted as ordinary scene-owned children
- blueprint instance root transform overrides are allowed
- inherited child transforms/components are not locally editable in v1

Validation should fail loudly rather than silently flattening or partially preserving illegal states.

## Testing Strategy

The feature needs coverage at four layers.

### Raw Asset And Serialization Tests

Add tests that verify:
- blueprint asset serializer round-trips one-root entity trees
- blueprint asset serializer round-trips asset references
- blueprint asset serializer round-trips per-platform override payloads
- invalid multi-root or nested-blueprint cases fail clearly

### Editor Authoring Tests

Add tests that verify:
- editor can save and load blueprint files
- scene load expands blueprint instances into visible inherited subtrees
- inherited subtree is marked read-only
- scene save stores only the instance reference, not duplicated inherited children
- source blueprint edits propagate after reload

### Build/Packaging Tests

Add tests that verify:
- packager resolves blueprint references
- packager applies target-platform blueprint overrides before expansion
- packaged scene contains ordinary expanded entity data
- Windows and DS packaging both preserve expected entity/component outcomes

### Runtime Tests

Add tests that verify:
- packaged scene runtime load produces expected entity hierarchies
- blueprint-origin identity scaffolding survives into runtime when required
- no dedicated blueprint loader is required for v1 packaged scenes

## Recommended Implementation Phases

### Phase 1: Asset Type And Persistence

Add the raw blueprint asset type, serializer support, editor save/load services, and validation for one-root assets.

### Phase 2: Scene Instance Component And Editor Expansion

Add `BlueprintInstanceComponent`, scene embedding rules, expanded inherited read-only hierarchy behavior, and scene-save filtering so inherited children are not serialized as scene-owned content.

### Phase 3: Packaging Integration

Teach the build pipeline to resolve blueprint references, apply platform overrides, and expand blueprint content into packaged scenes across supported targets.

### Phase 4: Runtime Identity And Diagnostics

Preserve blueprint-origin ids/markers needed for diagnostics and future override work without adding a heavy runtime blueprint system.

### Phase 5: Future Override Scaffolding

Add internal storage and id plumbing for future per-instance property overrides, while leaving editing UI disabled in v1.

## Recommendation Summary

The recommended v1 architecture is:
- standalone `Blueprint` asset files
- exactly one root entity per blueprint
- no nested blueprints
- scene-owned instance root with `BlueprintInstanceComponent`
- visible but read-only inherited children in editor
- build-time expansion into packaged scene content
- full platform override support inside blueprint assets
- stable blueprint entity/component ids preserved for future per-instance overrides

This gives Helengine an embeddable authored-content system that is scene-grade, platform-correct, and future-proof for instance overrides without forcing that complexity into the initial user-facing workflow.
