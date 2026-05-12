# Core Update Delta Time Design

## Goal

Add a shared per-update delta-time API for engine components so gameplay and runtime systems can read the current frame step from `Core.Instance` instead of hardcoding fixed steps or sampling wall-clock time independently.

## Decision Summary

- `Core` owns timing state directly.
- Expose `DeltaTime` and `UnscaledDeltaTime` from `Core.Instance`.
- Both values are `float`.
- Both values are updated once at the start of `Core.Update()`.
- Delta values are computed from real elapsed wall-clock time between update calls.
- `TotalElapsedSeconds` remains `double`.
- On the first update after initialization, both delta values are `0f`.

## Why This Approach

This is the smallest change that introduces one authoritative update-step source without adding a second global timing singleton. It matches the engine's current ownership model, where cross-cutting runtime services already live on `Core.Instance`.

Using `float` for per-frame delta fits the expected usage better than `double`. The values are small, transient, and read frequently by gameplay code. Precision-sensitive long-running accumulation remains on `TotalElapsedSeconds`, which already uses `double`.

Adding both `DeltaTime` and `UnscaledDeltaTime` now avoids reshaping the public timing API later if time scaling or pause behavior is added.

## Public API

`Core` will expose:

- `public float DeltaTime { get; private set; }`
- `public float UnscaledDeltaTime { get; private set; }`

For this slice:

- `DeltaTime == UnscaledDeltaTime`
- no time-scale property is added yet
- no render delta is added

## Runtime Behavior

At the start of each `Core.Update()`:

1. Read the current wall-clock timestamp.
2. If this is the first update, set both delta values to `0f`.
3. Otherwise compute elapsed seconds since the previous update call.
4. Store the raw elapsed value into `UnscaledDeltaTime`.
5. Copy that same value into `DeltaTime`.
6. Advance any accumulated runtime clocks such as `TotalElapsedSeconds`.
7. Continue with the normal update pass.

This keeps every component in a given update observing the same delta value.

## Internal State

`Core` will maintain:

- one private timestamp for the previous update
- one private flag or equivalent sentinel that distinguishes the first update from later updates

The timing state stays internal to `Core` and is not exposed through a separate service in this slice.

## Consumer Guidance

New component logic that needs per-update time should read:

- `Core.Instance.DeltaTime` for gameplay movement and animations
- `Core.Instance.UnscaledDeltaTime` only when future time scaling should be ignored

Existing uses of:

- hardcoded `1f / 60f`
- direct `DateTime.Now` or `DateTime.UtcNow` update-step sampling inside components

should migrate to the shared delta API over time, but that migration is not required for the initial timing-source change.

## Error Handling

No new exceptions are needed for normal runtime use.

`Core.Update()` already requires a valid running `Core` instance. The delta properties are simple cached values and should remain readable whenever `Core.Instance` exists.

## Testing

Add focused tests that verify:

1. the first `Core.Update()` sets `DeltaTime` and `UnscaledDeltaTime` to `0f`
2. a later `Core.Update()` produces a positive elapsed delta
3. an `UpdateComponent` can read the current delta value during its `Update()` call
4. `DeltaTime` and `UnscaledDeltaTime` are equal for this slice

Tests should validate behavior, not implementation details like the exact timestamp mechanism.

## Out of Scope

This design does not include:

- a static `Time` class
- render delta time
- fixed-step simulation
- pause/time scale controls
- full migration of all existing timing consumers
- clamping or smoothing policies for unusually large frame gaps

## Files Expected To Change

- `engine/helengine.core/Core.cs`
- one or more core test files under `engine/helengine.editor.tests/`

## Acceptance Criteria

- Components can read `Core.Instance.DeltaTime`
- Components can read `Core.Instance.UnscaledDeltaTime`
- Both values update once per `Core.Update()`
- The first update reports `0f`
- Later updates report real elapsed wall-clock time
- `TotalElapsedSeconds` remains `double`
- Focused tests cover the new behavior
