# City Rendering Motion Component Ownership Design

## Goal

Move `DirectionalShadowCameraOrbitComponent`, `DirectionalShadowOrbitComponent`, `DirectionalShadowSunSweepComponent`, `DirectionalShadowTowerSpinComponent`, and `RotateComponent` fully out of `helengine` and make them plain city-owned script components.

## Problem

The engine still owns several demo-scene motion components that are specific to city-authored rendering scenes. That is the same ownership mistake that previously existed with the demo-disc menu stack. The current arrangement is worse than just misplaced classes because the engine also carries special serialization, deserialization, and packaging rewrite logic for those components.

That means `helengine` still has hard knowledge of city scene behavior:

- engine-owned component classes under `helengine.core`
- manual scene payload serializer helpers for directional-shadow motion components
- runtime component deserializer registrations for those engine-owned ids
- editor packaging transforms that rewrite city or gameplay ids into engine-owned serialized ids

This is the wrong boundary. These behaviors are authored by city scenes and should travel through the ordinary script-component path instead of a custom engine-owned scene format.

## Scope

In scope:

- Move ownership of the five motion components to the city project
- Remove the engine-owned classes
- Remove engine serializer and deserializer support dedicated to those components
- Remove editor packaging rewrite logic dedicated to those components
- Regenerate affected city scenes so they reference only city-owned component types
- Validate through a Windows build of the city project

Out of scope:

- General changes to the automatic script-component serialization system
- New compatibility shims or aliases
- PS2-specific validation in this pass
- Unrelated rendering or scene-authoring refactors

## Target Architecture

After the migration:

- `city` is the only owner of the five motion components
- `helengine` does not contain classes, serializers, deserializers, or packaging rules for those component types
- affected city scenes reference the city-owned component types directly
- scene persistence uses the existing automatic script-component path rather than a dedicated engine component schema

The key rule is simple: these five types must behave like ordinary project script components, not engine-defined scene component formats.

## Ownership Rules

### City owns

- runtime component classes for the five motion behaviors
- scene-factory usage of those components
- any authored scene content that references them

### Engine owns

- generic component lifecycle
- generic script-component loading and scene serialization infrastructure
- no city-specific knowledge of these five types

## Migration Strategy

1. Ensure the city project contains the five component classes in the intended namespaces used by authored scenes and generators.
2. Update city scene-generation code to reference the city-owned classes consistently.
3. Remove engine-owned copies of those component classes.
4. Remove manual serializer helpers and runtime deserializer registrations in `helengine.core` that exist only for the directional-shadow motion components.
5. Remove packaging rewrite branches in `helengine.editor` that special-case those motion component ids.
6. Update or remove tests that assert engine-owned serialized ids or rewrite behavior for those components.
7. Regenerate affected city scenes and validate with a Windows export.

## Data and Serialization Rules

There should be no dedicated serialized type ids for these components in engine code after the migration.

Expected behavior:

- authored scenes persist the components through the normal automatic script-component representation
- runtime loading resolves them through the ordinary script assembly/type resolution path
- no custom binary payload serializer or dedicated runtime component deserializer remains for these types

## Risks

### Authored scene id drift

Older scene assets or tests may still reference `gameplay.rendering.*`, `city.rendering.*`, or old engine-owned names. The migration should normalize the city project to one city-owned identity and then regenerate the affected scenes.

### Hidden non-city usage of `RotateComponent`

`RotateComponent` looks generic enough that it may have spread beyond city content. The implementation must search for all references and move callers to a city-owned equivalent rather than leaving the engine type behind.

### Packaging assumptions in tests

Several tests likely assert rewrite behavior that should no longer exist. Those tests need to be rewritten or deleted rather than preserving obsolete compatibility paths.

## Validation

Minimum validation for this migration:

- regenerate affected city scenes
- build the Windows city export successfully
- confirm the packaged build no longer depends on engine-owned directional-shadow or rotate component handling

## Non-Goals

- preserving old engine-owned ids
- keeping bridge deserializers for legacy scene records
- maintaining special packager transforms for these component types

The migration should be a clean ownership break.
