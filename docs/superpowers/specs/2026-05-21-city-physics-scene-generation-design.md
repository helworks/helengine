# City Physics Scene Generation Design

## Purpose

Move physics demo scene generation ownership from `helengine.editor` into the city project at `C:\dev\helprojs\city`. The editor should still provide the command host, project context, scene serialization, packaging, and runtime component support, but city should own the generated demo scene catalog, layouts, support assets, and menu command behavior.

## Current State

The city project contributes an editor menu item named `Generate Physics Scenes` through `assets/codebase/menu.tools`. That command calls `city.physics.tools.PhysicsSceneGenerator`, but the generator immediately delegates to `helengine.editor.PhysicsValidationSceneFactory`. The actual scene ids, scene layouts, physics component payloads, support shader, and support materials are therefore still editor-owned.

The city project currently has generated rendering scenes under `assets/scenes/rendering`, but no generated physics scenes under `assets/scenes/physics`.

## Target Ownership

City should own the physics demo source of truth:

- `assets/codebase/physics.tools` contains the physics scene catalog and factory.
- `GeneratePhysicsScenesCommand` calls only city-owned generation code.
- Generated scene assets are written under `assets/scenes/physics`.
- Shared physics demo shader and material assets are written under city `assets/Shaders/physics` and `assets/Materials/physics`.

The editor should no longer be the production home for city physics demo layouts. Any editor-side physics validation coverage should either use editor/test-local fixtures or cover generic editor packaging/runtime behavior without depending on city project scene content.

## Architecture

Add city-owned equivalents of the current editor catalog and factory:

- `city.physics.tools.PhysicsSceneCatalog`
  - Exposes the stable ordered list of generated physics scene ids.
  - Keeps paths compatible with existing generated ids, such as `scenes/physics/test_scene_dynamic_stack_boxes.helen`.

- `city.physics.tools.PhysicsSceneFactory`
  - Creates `SceneAsset` instances for each catalog id.
  - Writes every catalog scene and support asset into the provided project root.
  - Reuses engine/editor asset serialization APIs exactly as the rendering scene generator already does.

- `city.physics.tools.PhysicsSceneGenerator`
  - Validates the project root path.
  - Calls the city-owned factory.

Existing scene layouts can be copied from the editor factory first to preserve behavior. Follow-up visual or gameplay changes can then happen inside city without touching editor product code.

## Data Flow

1. The city menu provider contributes `Generate Physics Scenes`.
2. The editor command host invokes `GeneratePhysicsScenesCommand`.
3. The command passes `context.ProjectRootPath` to `PhysicsSceneGenerator`.
4. The generator asks `PhysicsSceneFactory` to write support assets and every catalog scene.
5. The normal editor asset catalog sees the generated `.helen`, shader, and material assets as project files.

## Error Handling

Generation should fail clearly when the project root path is null, empty, or whitespace. Unknown scene ids should throw an `InvalidOperationException`. File and serialization failures should propagate so the editor reports the real cause instead of silently producing partial content.

## Testing

City generation tests should verify:

- The city catalog returns the expected stable ordered scene list.
- `WriteScenes` writes every generated physics scene under `assets/scenes/physics`.
- Representative scenes still contain required camera, scenario, render, rigid-body, collider, character controller, and kinematic-motion records.
- Support shader and material assets are emitted.

Editor tests should be adjusted so they no longer require editor-owned city demo factories. Packaging and runtime simulation coverage can move to city integration tests if the project test harness supports compiling city editor modules; otherwise, keep narrowly scoped editor fixtures for generic packaging/runtime assertions.

## Migration Notes

This change moves ownership, not behavior. The first implementation should preserve the generated scene ids and layouts so existing demo menu/build flows do not change unexpectedly. After ownership is moved, scene content updates can happen in the city project without changing editor source.
