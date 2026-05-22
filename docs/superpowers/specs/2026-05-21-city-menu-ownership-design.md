# City Menu Ownership Design

## Goal

Move the entire demo-disc menu system out of `helengine` and into the `city` project. After this migration, `helengine` must not own or special-case any menu-specific runtime, editor, serialization, or build behavior that exists only to support the city demo disc.

## Problem Statement

The current menu implementation is split across three ownership layers:

- `helengine.core` owns baked menu runtime components, menu definitions, menu layout helpers, and runtime deserializers.
- `helengine.editor` owns menu persistence descriptors and menu-scene build helpers.
- `city` owns the actual demo-disc menu content and some menu generation code.

That split is incorrect for this project. The “reusable” menu stack is actually city-specific product code. The leak is large enough that engine systems such as `FPSComponent` currently inspect `MenuComponent`, which creates unwanted behavior and invalid engine dependencies.

## Design Decision

The migration will preserve the current baked-scene architecture, but all menu-specific ownership moves into `city`.

### Engine Responsibility After Migration

`helengine` keeps only generic systems:

- entity/component lifecycle
- generic scene loading and saving infrastructure
- generic rendering primitives
- generic UI primitives such as `TextComponent`, `SpriteComponent`, `RoundedRectComponent`, `ScrollComponent`, `ClipRectComponent`, `ViewportComponent`, and `ReferenceCanvasFitComponent`
- generic project/build pipeline hooks that are not specific to menu content

`helengine` must not contain menu-specific runtime components, menu definitions, menu-scene factories, menu persistence descriptors, menu runtime deserializers, or menu-aware gameplay logic.

### City Responsibility After Migration

`city` owns the full demo-disc menu stack:

- menu definition models
- menu provider interfaces and provider resolution
- menu layout constants and helpers
- baked menu runtime components
- baked menu runtime deserializers
- menu persistence descriptors for generated authored scenes
- demo-disc menu scene generation
- menu-specific return-to-menu runtime behavior
- platform-info overlay behavior

## Ownership Boundary

If a type exists to support the city demo-disc menu and its name or behavior is menu-specific, it belongs in `city`, not `helengine`.

That includes the current engine-owned menu abstractions even if they were originally designed to look generic:

- `MenuDefinition`
- `MenuPanelDefinition`
- `MenuItemDefinition`
- `MenuActionDefinition`
- `MenuActionKind`
- `MenuOverlayImageDefinition`
- `MenuPlatformInfoDefinition`
- `IMenuDefinitionProvider`
- `MenuDefinitionProviderResolver`
- `DemoMenuLayout`
- `MenuComponent`
- `MenuPanelComponent`
- `MenuItemComponent`
- `MenuSelectedDescriptionComponent`
- `MenuHostItemRuntime`
- `MenuHostPanelRuntime`
- `MenuItemRuntime`
- `MenuPanelRuntime`
- all menu runtime deserializers
- all menu persistence descriptors
- editor menu-scene build services and scene factories that exist only for the demo-disc menu flow

## Serialized Identity Decision

The migrated components and related serialized records will change from `helengine.*` menu type IDs to `city.*` menu type IDs in the same refactor.

The city menu scenes will be regenerated immediately after the migration so the packaged content no longer depends on legacy engine menu identities. This is a clean ownership cut, not a long compatibility bridge.

## Runtime Behavior Changes

### FPS Behavior

`FPSComponent` must stop inspecting menu components entirely. No replacement pause abstraction will be introduced in this refactor because the pause behavior was never requested and menu-aware engine logic is explicitly unwanted.

### Player Runtime Deserialization

Player builds currently require explicit runtime deserializers when runtime reflection is disabled. After migration, the city-owned menu components must be deserialized entirely through city-owned registrations rather than engine-owned menu registrations.

### Scene Loading

Packaged menu scenes must load through city-owned component type IDs and city-owned runtime deserializers. The engine runtime component registry should no longer register built-in menu deserializers.

## Editor and Generation Changes

The city generation pipeline already owns significant parts of authored demo-disc scene generation. This migration completes that ownership by moving the remaining editor-side menu persistence and build helpers out of `helengine.editor`.

The city project will own:

- persistence descriptors used by generated authored menu scenes
- menu scene asset or authoring factories
- provider resolution for menu definitions
- regeneration helpers needed by city menu generation

Any remaining engine-side helper that exists only to build the city menu should be removed or replaced by a city-owned equivalent.

## Migration Plan

1. Add city-owned equivalents for every engine-owned menu model, runtime component, runtime helper, deserializer, and persistence descriptor.
2. Repoint city menu generation and scene writing to the city-owned types.
3. Repoint player runtime deserializer registration to city-owned menu deserializers.
4. Remove engine gameplay logic that inspects menu components, including the `FPSComponent` special case.
5. Regenerate the city menu scenes so serialized component type IDs switch to `city.*`.
6. Delete old engine and editor menu classes once the city-owned path is validated.

## Testing Strategy

Use focused validation rather than broad full-repo sweeps.

Required checks:

- targeted tests for menu scene serialization/deserialization after the type-id migration
- targeted tests for runtime component registration and scene loading in player-style reflection-disabled paths
- targeted tests proving `FPSComponent` no longer branches on menu presence
- city menu generation or source-level regression tests that confirm the generated menu scene uses city-owned component type IDs
- PS2 demo-disc rebuild and boot verification after the migration

## Risks

### Serialized Scene Breakage

Changing menu component type IDs from `helengine.*` to `city.*` will break stale generated scenes until they are regenerated. The migration must regenerate the menu scenes as part of validation.

### Player Runtime Registration Gaps

If any migrated menu component lacks a city-owned runtime deserializer registration, player builds will fail during scene load. Focused runtime scene-load tests must cover this explicitly.

### Editor Save/Load Drift

Menu generated-scene persistence currently spans both editor and runtime formats. The city-owned persistence descriptors must preserve current payload semantics during the move so existing authored behavior does not silently drift.

## Non-Goals

This refactor does not redesign the menu UX, replace the baked-scene architecture, or generalize a new engine-level UI/menu framework. It only fixes ownership and removes menu-specific engine leakage while keeping current city behavior intact.
