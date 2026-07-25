# Desktop Input Frame Contract Design

## Goal

Remove keyboard and mouse state from non-desktop generated runtimes while
preserving the existing desktop input API and gamepad input on every platform.

## Context

`InputFrameState` currently contains keyboard, mouse, pointer, gamepad, and
text fields for every target. PS2 only populates gamepads, but its native input
backend must include the whole frame type. That causes generated core to emit
desktop keyboard and mouse dependencies in console builds.

The editor already owns the `DESKTOP_PLATFORM` preprocessor symbol. It applies
that symbol to Windows gameplay and generated-core codegen. Future desktop
platform definitions will use the same symbol.

## Selected Approach

Use `DESKTOP_PLATFORM` at the shared input-contract boundary:

- `InputFrameState.Keyboard` and `InputFrameState.Mouse` exist only for
  desktop compilation.
- Keyboard/mouse fields, initialization, capture, setters, queries, pointer
  wrapping, and background-input policy in `InputSystem` exist only for
  desktop compilation.
- `InputBackendWindows` remains the desktop implementation and keeps filling
  keyboard and mouse state.
- Non-desktop backends continue to return gamepads, pointer state, and text
  state without needing keyboard or mouse types.

The platform symbol remains the sole capability declaration. No input code
will special-case `windows`, `ps2`, or any other platform identifier.

## Rejected Approaches

Keeping empty keyboard and mouse fields on consoles would still force those
generated dependencies into the runtime. Creating unrelated desktop and
console frame types would split the backend API and require adapters in every
input consumer. Conditional members preserve one backend contract per target
while emitting only the supported state.

## Compatibility

Desktop source and behavior remain unchanged when `DESKTOP_PLATFORM` is
defined. Non-desktop authored gameplay that directly uses keyboard or mouse
APIs must be guarded by the same symbol, which makes unsupported input use a
compile-time error instead of silently supplying meaningless default values.

`InputFrameState` remains a platform-generated runtime type. It is not used
as a cross-platform serialized asset, so its target-specific layout is
intentional.

## Validation

Tests will prove that desktop codegen retains keyboard and mouse members and
that PS2 codegen omits both members and their dependent generated headers.
Input-system tests will compile each conditional surface under the applicable
symbol. A focused PS2 build will confirm `Ps2InputBackend.cpp` compiles
without `KeyboardState.hpp` or `MouseState.hpp`, then the existing B107
Colored Cubes build can resume.
