# Input System Foundation Design

## Summary

This design replaces the current ad hoc input shape with a layered system that can serve three different needs without changing the core model:

- modern editor and runtime input
- deterministic tests and replays
- conversion to low-level targets with limited hardware, including PS1-style constraints

The target is not "more features than Unity or Unreal." The target is a clearer core that can express action mapping, device state, and frame snapshots without becoming dependent on platform APIs or editor behavior.

## Problem Statement

The current input layer already has useful pieces:

- keyboard and mouse state snapshots
- frame-by-frame delta capture
- pointer wrap support
- routing for UI and gizmo interactions

But those pieces are mixed together inside the same surface. That makes the system harder to evolve in three directions at once:

- gameplay-style input mapping
- editor interaction input
- low-level portability for older hardware or C# to C++ conversion

The result is a system that works, but does not yet have a strong boundary between:

- raw device capture
- logical input interpretation
- high-level interaction routing

That boundary is the main thing this design introduces.

## Goals

- Preserve deterministic, frame-based input capture.
- Support action-style input similar to Unity's new input system and Unreal's action/axis mapping model.
- Treat keyboard, mouse, and gamepad as optional device families instead of permanent assumptions.
- Keep the runtime core compact enough to convert to C++ without depending on heavy runtime features.
- Keep platform-specific code isolated to backends.
- Keep editor-only behavior out of the portable core.
- Support replay, test injection, and headless simulation.
- Keep the API understandable from the engine side, not from an input-framework side.

## Non-Goals

- Build a full rebinding UI in the core.
- Make the input layer depend on reflection, attributes, or runtime discovery.
- Treat text entry as identical to gameplay input.
- Add platform-specific special cases directly into the shared core.
- Preserve the current routing model if it prevents the cleaner core shape.

## Design Principles

### 1. Separate Raw Input From Logical Input

The system should first capture what the hardware reported, then resolve that into actions and gameplay/editor queries.

### 2. Favor Frame Snapshots

Input should be captured once per frame into immutable or effectively immutable snapshots. Consumers should query the snapshot, not the backend.

### 3. Keep The Core Portable

The shared core should prefer:

- enums
- bitmasks
- small structs
- fixed-size arrays where practical
- explicit IDs instead of string lookups in hot paths

The shared core should avoid:

- per-frame allocations
- reflection
- runtime type discovery
- dynamic binding graphs that depend on editor-only metadata

### 4. Make Platform Adapters Thin

Windows, web, mobile, console, and future retro targets should translate native device state into a shared input description. They should not own input policy.

### 5. Treat Text, Pointer, And Gameplay As Different Channels

Keyboard and gamepad state drive actions.
Pointer state drives UI and editor workflows.
Text input is a separate stream because it has different semantics and different portability requirements.

### 6. Make Device Families Optional

Keyboard and mouse should not be hard requirements for the core.
They are just one possible set of device families, and they should disappear cleanly from builds that do not support them.

That means:

- the portable input model must not require keyboard or mouse fields to exist
- platform backends may compile without keyboard and mouse code entirely
- gameplay and editor systems must depend on `InputSystem`, not on `Keyboard` or `Mouse`
- direct `Keyboard` and `Mouse` references should remain inside backend and test-adapter code only

## Proposed Model

### 1. Raw Device Layer

Each backend reports a compact `InputFrameState` for the current frame.

The raw frame should be able to represent:

- keyboard buttons
- mouse buttons
- pointer position
- pointer delta
- scroll wheel
- gamepad buttons
- gamepad axes
- touch contacts when available
- text input characters when available

For portability, the portable core should treat devices as optional. A target with no mouse or keyboard support should still build and run by exposing only the devices it actually has. A target with gamepads only should be equally valid.

Gamepads should be exposed through an abstract family interface rather than a one-off special case. The input core should be able to consume:

- zero or more connected pads
- a fixed maximum pad count for constrained targets
- per-pad button and axis state
- per-pad device identity or player slot when available

The core should not assume a pad is physically a console controller, a PC gamepad, or an emulated device. It only needs a normalized state stream.

### 2. Action Layer

An action is a logical intent, not a device.

Examples:

- `Jump`
- `Confirm`
- `Cancel`
- `PrimaryFire`
- `Move`
- `Look`
- `Pan`
- `TextCommit`

An action can be driven by one or more bindings:

- a button
- a key
- a gamepad button
- an analog axis
- a pointer gesture
- a chord of inputs

The portable runtime should resolve actions from data, not from per-action code branches.

### 3. Context Layer

Contexts determine which bindings are active.

Examples:

- gameplay
- UI
- text entry
- editor viewport
- editor gizmo manipulation

Contexts can be prioritized and stacked. The active context set decides which input consumes precedence when multiple systems want the same control.

The core rule is simple:

- the system resolves priority first
- consumers query the final resolved actions second

### 4. Query Layer

Consumers do not read raw backend state directly. They query a stable API such as:

- `IsDown`
- `WasPressed`
- `WasReleased`
- `GetValue1D`
- `GetValue2D`
- `GetPointerPosition`
- `GetPointerDelta`
- `GetScrollDelta`

That keeps the gameplay/editor code independent from the device backend.

## Suggested Core Shape

The following is the intended shape, not a final file list:

```csharp
public sealed class InputSystem {
    public void SetBackend(IInputBackend backend);
    public void BeginFrame();
    public void EndFrame();
    public void PushContext(InputContextId contextId);
    public void PopContext(InputContextId contextId);
    public bool IsActionDown(InputActionId actionId);
    public bool WasActionPressed(InputActionId actionId);
    public bool WasActionReleased(InputActionId actionId);
    public int2 GetPointerPosition();
    public int2 GetPointerDelta();
}
```

```csharp
public interface IInputBackend {
    InputFrameState CaptureFrame();
}
```

```csharp
public struct InputFrameState {
    public KeyboardState Keyboard;
    public MouseState Mouse;
    public GamepadState[] Gamepads;
    public TextInputState Text;
}
```

The exact names can change. The important part is the boundary:

- the backend captures hardware
- the core resolves logic
- consumers query the resolved state

## Conditional Device Compilation

The core should support build-time stripping of unused device families.

Recommended approach:

- use compile symbols or platform feature flags to include or exclude keyboard, mouse, and gamepad code paths
- keep device-family-specific files separate so removal is mechanical
- avoid spreading direct `Keyboard` or `Mouse` calls through gameplay or editor systems
- use `InputSystem` as the only stable entry point for consumer code

This is important for retro or console targets because conditional removal is not just a cleanup detail. It is how the core stays small enough to translate cleanly.

Examples of reasonable feature gates:

- `HELENGINE_INPUT_KEYBOARD`
- `HELENGINE_INPUT_MOUSE`
- `HELENGINE_INPUT_GAMEPAD`
- `HELENGINE_INPUT_TEXT`

The exact symbol names may change, but the rule does not:

- if a target does not support a device family, that family should compile out of the source graph
- code that depends on that family should also compile out or move behind an equivalent abstraction

## Current Codebase Mapping

The existing engine already contains the right starting pieces for this model:

- `KeyboardState` and `MouseState` already represent frame snapshots.
- `Keyboard` and `Mouse` already act as platform-specific capture surfaces.
- `InputManager` already owns frame-to-frame state capture and query logic.
- `TestInputManager` already proves that deterministic injection is valuable for tests.
- `InputManagerWindows` already demonstrates that platform backends can stay thin.

The migration should preserve those strengths while changing the ownership model:

- `KeyboardState` and `MouseState` remain the low-level snapshot types.
- `InputManager` stops being the place where all interaction policy lives.
- action mapping and context priority move into a higher-level portable layer.
- platform backends continue to provide raw device state only.

That means the safest next step is not a rewrite. It is a split:

1. keep the current snapshot capture working
2. introduce a logical action layer above it
3. route gameplay and editor code through actions instead of raw device checks where practical

## Hardware Conversion Constraints

This is the part that matters for PS1-style conversion.

The core should be designed around constraints that survive older hardware:

- fixed maximum device counts
- fixed maximum player counts
- bit-packed button state
- small integer axis storage
- no reliance on OS cursor APIs in the shared core
- no requirement for runtime memory churn
- no requirement for arbitrary Unicode text input in gameplay code

Recommended portable storage choices:

- buttons: bitfields
- axes: signed 16-bit or 32-bit integers in the core, normalized only at the edge
- pointer position: integer screen or client coordinates
- deltas: integer deltas
- action IDs: small integers or enums

The system should be able to compile out unsupported devices per target profile. A PS1-class target should not need to pretend it has a mouse, a wheel, or a Unicode text stream.

## Platform Adapter Rules

Platform code owns native collection and translation only.

Examples:

- Windows backend reads the OS keyboard, mouse, and cursor
- editor backend can feed synthetic test states
- console backend reads controller ports
- retro target backend reads controller packets or an emulated input bus

Platform adapters should not:

- resolve gameplay actions
- decide context priority
- implement editor-specific routing
- bake in UI behavior

## Text Input

Text input must stay separate from gameplay buttons and editor navigation.

Reason:

- text composition is platform-specific
- some targets will not support it
- text can be buffered or absent without breaking gameplay

Text input belongs in its own stream so gameplay input remains usable even when text services are minimal or unavailable.

## Pointer And Mouse Policy

Mouse/pointer support should be treated as an optional device family.

The core should expose pointer state when available, but should not assume:

- a visible OS cursor
- screen-space wrapping
- a multi-button mouse
- a wheel

This keeps the model usable for non-PC targets.

## Suggested Migration Plan

### Phase 1: Split Capture From Query

Move the current `InputManager` toward a model where capture happens once and query happens against a stable snapshot.
Move gameplay and editor code off direct keyboard/mouse access and onto `InputSystem` queries.

### Phase 2: Introduce Action Maps

Add a compact action mapping layer for gameplay and editor use.
Add gamepad bindings in the same layer, not as a separate system.

### Phase 3: Add Context Priorities

Allow UI, gameplay, text, and editor modes to coexist without every consumer manually checking device state first.

### Phase 4: Move Platform Backends Behind Thin Adapters

Keep OS-specific code in platform assemblies.

### Phase 5: Prune Old Direct Queries

Replace code that reaches directly for raw device details when the code really wants a logical action.
Remove `Keyboard` and `Mouse` references from gameplay/editor codepaths that do not need backend access.

## Success Criteria

The design works when all of these are true:

- runtime code can ask for a logical action without caring which device produced it
- editor code can keep pointer and keyboard behavior without special-casing the backend
- tests can inject frame state directly
- platform adapters remain small
- the core can be translated to a lower-level target without pulling in editor assumptions
- a retro-target implementation can support the same action model using fewer devices

In practical terms, the shared core should be small enough that a future C++ backend can keep the same model and only replace the hardware adapters.
