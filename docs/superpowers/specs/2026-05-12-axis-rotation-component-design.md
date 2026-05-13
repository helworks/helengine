# Axis Rotation Component Design

## Goal

Replace the stale `DirectionalShadowTowerSpinComponent` path used by `cube_test` with a reusable city-owned gameplay component named `AxisRotationComponent`.

The new component must:

- live in the `city` project, not the engine
- support reuse by other scenes
- rotate in local space only
- advance using `DeltaTime`
- preserve stable angular speed regardless of frame rate

This slice also removes the stale packaging dependency on `helengine.DirectionalShadowTowerSpinComponent` for `cube_test`.

## Current Problem

`cube_test` currently authors a gameplay component, but the packaging path rewrites it into the engine component `helengine.DirectionalShadowTowerSpinComponent`.

That is wrong for two reasons:

1. The name is scene-specific and no longer matches the intended use.
2. The runtime behavior is supposed to live in `city`, not in an engine-specific compatibility path.

The current Windows cooked `cube_test` scene confirms that the authored gameplay component is still being rewritten into the old engine component.

## Recommended Approach

Use a reusable gameplay component:

- Type: `gameplay.rendering.AxisRotationComponent`
- Ownership: `city`
- Behavior: local-space incremental rotation
- Timing source: `Core.Instance.DeltaTime`

`cube_test` will attach this component directly with:

- `Axis = new float3(0f, 1f, 0f)`
- `AngularSpeedRadiansPerSecond = (float)(Math.PI / 2.0)`

The packaging path will stop rewriting this behavior into an engine runtime component. Instead, the cooked scene will preserve the gameplay script component so the player resolves and runs it through the normal gameplay component runtime path.

## Alternatives Considered

### 1. Keep `DirectionalShadowTowerSpinComponent`

Rejected.

The name is misleading and leaks old demo-specific history into a reusable scene behavior.

### 2. Add a new engine `AxisRotationComponent`

Rejected.

The user explicitly wants this behavior to remain in the `city` project. Moving it into the engine would repeat the same architectural mistake as the current rewrite path.

### 3. Use Euler-axis booleans or per-axis speed fields

Rejected.

A single axis vector plus angular speed is the cleanest reusable contract and is easier to serialize, reason about, and reuse across generated scenes.

## Architecture

### City Gameplay Component

Add `gameplay.rendering.AxisRotationComponent` in `city`.

Responsibilities:

- validate or normalize the authored local axis before use
- maintain the current local orientation state
- apply an incremental local rotation each update using `DeltaTime`

Public authored fields:

- `float3 Axis`
- `float AngularSpeedRadiansPerSecond`

The component will rotate its parent in local space only.

### Cube Test Scene Authoring

Update `CubeTestSceneFactory` to attach `AxisRotationComponent` instead of `DirectionalShadowTowerSpinComponent`.

No engine-specific component knowledge should remain in the factory.

### Packaging

Update `SceneComponentPackagingTransformService` so `AxisRotationComponent` is not rewritten into `helengine.DirectionalShadowTowerSpinComponent`.

`cube_test` should package as a normal gameplay script component, not as an engine compatibility component.

If the old directional-shadow tower-spin rewrite remains for older authored scenes, that compatibility path must not affect `cube_test` after the scene generator switches to `AxisRotationComponent`.

### Player Build Requirement

Because `AxisRotationComponent` lives in `city`, the Windows player build must include the `gameplay` code module.

Without `gameplay`, the player cannot instantiate or execute the packaged project component even if the cooked scene is otherwise correct.

## Data Flow

1. `CubeTestSceneFactory` authors `AxisRotationComponent` on `CubeTestCube`.
2. Scene save writes the gameplay component as a normal authored script component.
3. Packaging preserves that gameplay component in the cooked scene.
4. Player runtime loads the gameplay component through the generated runtime deserializer path.
5. The component applies local-axis incremental rotation each frame using `DeltaTime`.

## Behavior Details

`AxisRotationComponent` should produce frame-rate-independent motion by advancing from per-frame `DeltaTime`.

Expected runtime behavior:

- zero-length axis is invalid input and should fail clearly rather than silently inventing a default
- the axis is treated as local-space rotation axis
- angular speed is radians per second
- orientation advances incrementally each frame from the current local orientation

The component should not expose world-space behavior in this slice.

## Testing

### City/Editor Tests

Add or update tests to verify:

- `cube_test.helen` contains `AxisRotationComponent`
- `cube_test.helen` no longer contains `DirectionalShadowTowerSpinComponent`
- the regenerated scene remains readable through the live-authoring save path

### Packaging Tests

Add or update tests to verify:

- `cube_test` packaging preserves the gameplay `AxisRotationComponent`
- the old tower-spin engine rewrite is not used for `cube_test`

### Runtime/Behavior Tests

Add tests to verify:

- `AxisRotationComponent` advances orientation using `DeltaTime`
- equal simulated elapsed time produces equivalent rotation independent of frame count

### Build Configuration Verification

Verify that the Windows build configuration used for player testing includes the `gameplay` module before claiming the player rotation issue is fixed.

## Risks

### Empty Windows Code Module Selection

The current `city/user_settings/build_config.json` shows `selectedCodeModuleIds: []` for the Windows platform.

That is incompatible with a city-owned runtime component and must be treated as a real blocker for player validation.

### Partial Compatibility Cleanup

If old rewrite logic remains too broad, new authored `AxisRotationComponent` scenes could still be transformed incorrectly during packaging.

The implementation must verify the exact cooked component type for `cube_test`.

## Scope

Included:

- add `AxisRotationComponent` in `city`
- switch `cube_test` to use it
- stop `cube_test` from depending on `helengine.DirectionalShadowTowerSpinComponent`
- add focused serialization, packaging, and runtime behavior tests
- verify the Windows gameplay-module requirement

Not included:

- migrating all other city scenes in the same change
- adding world-space rotation support
- removing every legacy directional-shadow compatibility path for unrelated scenes unless required by this slice
