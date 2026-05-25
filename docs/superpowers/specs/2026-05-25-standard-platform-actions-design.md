# Standard Platform Actions Design

## Summary

Projects currently poll raw `InputGamepadButton` values directly in gameplay and menu code. That makes shared UI flow brittle because the same intent means different buttons on different platforms. `city` already shows the failure mode: the menu and scene-return logic know about `South`, `East`, and `Select` directly, so DS and PS2 behavior drifts whenever a backend or project changes.

This design adds a tiny engine-owned standard action layer for platform-facing UI intent:

- `Accept`
- `Return`

The bindings for those actions live in project-shared `settings/platform.<platform-id>.json` metadata. Runtime code asks the engine whether the standard action fired. It no longer hardcodes button enums per platform.

## Problem

The current arrangement has three problems:

1. Gameplay and menu code poll raw gamepad buttons directly.
   `city` uses `InputGamepadButton.South`, `InputGamepadButton.East`, and `InputGamepadButton.Select` in `MenuComponent`, `DemoDiscReturnToMenuComponent`, and `NintendoDsReturnOverlayComponent`.

2. Platform policy is duplicated in gameplay code.
   The same menu intent must be rewritten per platform instead of configured once in project settings.

3. DS raw button semantics are already awkward.
   The DS backend currently publishes physical `A` as `South` and physical `B` as `East`. That is good enough for raw polling to function, but it makes the code read incorrectly when the actual design intent is "Accept" and "Return".

## Goals

- Provide an engine-owned, reusable way to query standard platform UI actions.
- Store platform-specific mappings in project settings under `settings/platform.<platform-id>.json`.
- Reuse the existing `InputSystem` logical action infrastructure instead of adding a parallel action stack.
- Let `city` switch from raw button polling to configured `Accept` and `Return` actions.
- Keep the first slice small enough to ship without editor UI work.

## Non-Goals

- This design does not introduce a full rebinding UI.
- This design does not replace the existing general-purpose input binding system.
- This design does not normalize every backend's raw button naming in the same change.
- This design does not define gameplay actions like `Jump`, `Shoot`, or `Pause`.

## Recommended Approach

Use the existing `InputSystem` action/binding pipeline and reserve a tiny engine-owned standard action set for cross-platform UI intent.

The engine should:

- define standard platform action identifiers for `Accept` and `Return`
- load one configured physical control per action from the selected platform settings document
- register those bindings into a reserved engine-owned input context during runtime startup
- expose a small runtime helper that callers use instead of raw `WasGamepadButtonPressed(...)`

This keeps platform policy in project metadata and keeps runtime callers on one stable API.

## Architecture

### Standard Action Surface

Add a tiny engine-owned action surface with two concepts:

- `StandardPlatformAction`
  - enum values: `Accept`, `Return`
- `StandardPlatformInput`
  - runtime-facing helper/service for polling the action state

`StandardPlatformInput` should wrap `InputSystem.WasActionPressed(...)`, `IsActionDown(...)`, and any similar action queries needed by UI code. The helper exists so gameplay code does not need to know the reserved `InputActionId` values or context details.

### Reserved Input Context

The engine should reserve one context for platform-standard actions. That context is always owned by the engine, not by the project.

Recommended shape:

- one reserved `InputContextId`
- one reserved `InputActionId` per standard action
- bindings registered during runtime initialization after the platform profile is resolved

This avoids per-project action-id coordination and keeps the feature deterministic across desktop, DS, and PS2.

### Project Settings Shape

Extend `EditorPlatformProfileSettingsDocument` with an `Input` section persisted in each `settings/platform.<platform-id>.json`.

Recommended JSON shape:

```json
{
  "platformId": "ds",
  "build": {},
  "graphics": {},
  "codegen": {},
  "input": {
    "standardActions": {
      "accept": {
        "deviceKind": "gamepad",
        "deviceIndex": 0,
        "controlKind": "button",
        "controlIndex": 0
      },
      "return": {
        "deviceKind": "gamepad",
        "deviceIndex": 0,
        "controlKind": "button",
        "controlIndex": 1
      }
    }
  }
}
```

The serialized contract should stay generic and use `InputControlId` fields rather than platform names like `Cross`, `Circle`, `A`, or `B`. That keeps the persisted format portable across backends.

### Default Seeding

`EditorProfileSettingsService` should seed default standard-action bindings whenever it creates or normalizes a platform settings document.

First-slice defaults should come from platform id:

- `ds`
  - `Accept` maps to the DS physical accept control used by the current backend
  - `Return` maps to the DS physical return control used by the current backend
- `ps2`
  - `Accept` maps to PS2 south / `X`
  - `Return` maps to PS2 north / `Triangle`
- other platforms
  - leave the standard-action map empty unless there is already a clear engine convention

The DS note matters: the backend currently reports physical `A` and `B` through existing gamepad button indices that do not read positionally in source. The initial settings seed should honor the backend as it exists today so the project behavior is correct immediately. If backend positional normalization happens later, only the seeded DS mapping needs to change.

### Runtime Bootstrap

At runtime startup, once the active project platform settings are resolved, the engine should:

1. read the active platform's standard-action mapping
2. clear any previous bindings for the reserved standard-action context
3. register one `InputBinding` per configured standard action
4. push the reserved context so the actions resolve every frame

The binding registration must happen in generic engine bootstrap, not in `city`, so every project gets the same behavior and runtime API.

### Runtime Query API

Callers should use a small helper instead of touching raw action ids:

- `WasStandardPlatformActionPressed(StandardPlatformAction action)`
- `IsStandardPlatformActionDown(StandardPlatformAction action)`

That helper can live on a small service class or on `InputSystem` itself through convenience methods. The key requirement is that callers should not build `InputActionId` values manually.

### City Call-Site Migration

`city` should stop polling raw `InputGamepadButton` values for platform-intent UI flow.

Initial migration scope:

- `MenuComponent`
  - replace raw accept/return button checks with standard actions
- `DemoDiscReturnToMenuComponent`
  - use the standard `Return` action
- `NintendoDsReturnOverlayComponent`
  - use the standard `Return` action

Directional navigation can stay on raw D-pad buttons for this slice because the user request is specifically about `Accept` and `Return`.

## Error Handling

- Missing `input` section:
  - treat as unconfigured and register no standard-action bindings
- missing individual action entry:
  - skip only that action
- malformed persisted control values:
  - preserve current editor validation behavior and fail during settings load/normalization instead of inventing fallback controls

The engine should not silently synthesize a runtime default when a configured standard action is required. The authoritative default belongs in settings seeding, not in per-frame runtime polling.

## Testing

### Engine Tests

- profile-settings tests
  - loading a platform file without `input` should seed default standard-action mappings for supported platforms
  - saving and reloading should preserve the configured control ids

- runtime bootstrap tests
  - the selected platform settings should register the reserved standard-action bindings into `InputSystem`
  - missing mappings should leave the corresponding action unbound

- runtime input tests
  - `WasStandardPlatformActionPressed(Accept)` should resolve from the configured control
  - `WasStandardPlatformActionPressed(Return)` should resolve from the configured control

### Project Tests

- `city` source audit or focused unit tests should verify the DS menu and return flow use standard platform actions instead of raw `South` and `East` button checks.

## Rollout

1. Add the settings document/model changes in `helengine.editor`.
2. Add the reserved standard-action ids, context, and runtime helper in generic engine/input code.
3. Register bindings during runtime bootstrap from the active platform settings.
4. Seed DS and PS2 defaults in project platform settings normalization.
5. Migrate `city` menu and return call sites.

## Open Follow-Up

The DS backend currently reports physical face buttons through raw gamepad button names that do not match the positional language the project uses when talking about DS controls. This design intentionally avoids changing that backend contract in the same slice. If the team wants positional button names to match every backend literally, that should be handled in a separate backend-normalization change after `Accept` and `Return` are already project-configured.
