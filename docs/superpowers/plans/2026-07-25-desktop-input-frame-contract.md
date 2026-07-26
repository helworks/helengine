# Desktop Input Frame Contract Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Emit keyboard and mouse state only for desktop generated runtimes while preserving gamepad, pointer, and text input on every platform.

**Architecture:** `DESKTOP_PLATFORM` remains the platform-owned capability symbol. It conditionally exposes the keyboard/mouse fields in `InputFrameState`, their generated source types, and every corresponding `InputSystem` state/API surface. Console backends continue using the same backend interface but compile against the reduced frame layout. This must gate the complete keyboard/mouse source units because `helengine.core` links every `helengine.input` source file into code generation independently of its references from `InputFrameState`.

**Tech Stack:** C# 13, xUnit, the existing C#-to-C++ generated-core pipeline, PS2 native Docker toolchain.

---

### Task 1: Define the desktop-only generated contract

**Files:**
- Modify: `engine/helengine.input/InputFrameState.cs:6-36`
- Modify: `engine/helengine.input/InputSystem.cs:40-210, 480-860`
- Modify: `engine/helengine.input/KeyboardState.cs`
- Modify: `engine/helengine.input/MouseState.cs`
- Modify: `engine/helengine.input/Keys.cs`
- Modify: `engine/helengine.input/ButtonState.cs`
- Modify: `engine/helengine.input/TypeForwarders.cs`
- Modify: `engine/helengine.core/managers/input/KeyState.cs`
- Test: `engine/helengine.editor.tests/InputSystemTests.cs`

- [ ] **Step 1: Write a failing source-contract test**

Add an xUnit fact to `EditorGeneratedCoreRegenerationServiceTests.cs` that generates PS2 and Windows core output, then asserts:

```csharp
Assert.DoesNotContain("KeyboardState.hpp", ps2GeneratedInputFrameState);
Assert.DoesNotContain("MouseState.hpp", ps2GeneratedInputFrameState);
Assert.Contains("KeyboardState.hpp", windowsGeneratedInputFrameState);
Assert.Contains("MouseState.hpp", windowsGeneratedInputFrameState);
```

Use the test class's existing temporary generated-core helpers and XML-comment the new test.

- [ ] **Step 2: Run the test and confirm red**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests --nologo
```

Expected: the PS2 assertion fails because the current generated input frame includes keyboard and mouse state.

- [ ] **Step 3: Guard the frame members**

In `InputFrameState.cs`, place only the desktop fields inside this exact conditional region:

```csharp
#if DESKTOP_PLATFORM
/// <summary>
/// Gets or sets the captured keyboard state for the current frame.
/// </summary>
public KeyboardState Keyboard { get; set; }

/// <summary>
/// Gets or sets the captured mouse state for the current frame.
/// </summary>
public MouseState Mouse { get; set; }
#endif
```

Leave `Pointer`, `Gamepads`, `GamepadCount`, and `Text` unconditional.

- [ ] **Step 3a: Guard the desktop input source units**

Wrap the complete `KeyboardState.cs`, `MouseState.cs`, `Keys.cs`, `ButtonState.cs`, and core-owned `KeyState.cs` source units in `#if DESKTOP_PLATFORM`. Guard the associated `TypeForwardedTo` entries with the same symbol. This prevents non-desktop generated-core from receiving standalone headers for these desktop-only types.

- [ ] **Step 4: Guard every matching input-system surface**

In `InputSystem.cs`, wrap keyboard/mouse fields, constructor initialization, `SetKeyboardActive`, `SetKeyboardState`, `SetMouseState`, mouse pointer-wrapping APIs, keyboard and mouse query APIs, and the keyboard/mouse portions of `Update` in `#if DESKTOP_PLATFORM`. Preserve unconditional gamepad action resolution, pointer state, text state, backend capture, and `CurrentFrame` assignment.

- [ ] **Step 5: Run the focused generated-core test and confirm green**

Run the command from Step 2.

Expected: PASS; PS2 omits desktop state and Windows retains it.

### Task 2: Keep desktop backends and tests aligned

**Files:**
- Modify: `engine/helengine.core.windows/input/InputBackendWindows.cs:107-116`
- Modify: `engine/helengine.editor.tests/testing/TestInputBackend.cs:82-86, 122-154`
- Test: `engine/helengine.editor.tests/InputSystemTests.cs`

- [ ] **Step 1: Write a failing desktop compilation test**

Add a desktop-only test that creates `InputFrameState` with `Keyboard` and `Mouse`, injects it through `TestInputBackend`, calls `InputSystem.Update`, and asserts that `WasKeyPressed` and `GetMouseScrollWheelDelta` consume the supplied values.

- [ ] **Step 2: Run the focused input test and confirm red if a desktop assignment was accidentally removed**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~InputSystemTests --nologo
```

Expected: FAIL until Windows and `TestInputBackend` assignments are enclosed by the same desktop symbol.

- [ ] **Step 3: Apply matching desktop guards**

Keep `frame.Keyboard = CaptureKeyboardState();` and `frame.Mouse = CaptureMouseState();` in `InputBackendWindows` inside `#if DESKTOP_PLATFORM`. In `TestInputBackend`, guard keyboard/mouse state properties, capture assignments, and helper methods with the same symbol. Keep gamepad, pointer, and text assignments unconditional.

- [ ] **Step 4: Run the focused input test and confirm green**

Run the command from Step 2.

Expected: PASS.

### Task 3: Prove the PS2 native input boundary is desktop-free

**Files:**
- Modify: `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`

- [ ] **Step 1: Add the PS2 source assertion**

Add a fact that reads generated `InputFrameState.hpp`, `InputFrameState.cpp`, and `KeyboardState.hpp` paths from one PS2 generation result. Assert the first two do not refer to `KeyboardState`, `MouseState`, or their headers, and assert the generator did not create `KeyboardState.hpp` or `MouseState.hpp`.

- [ ] **Step 2: Run the generation test and confirm it fails on the existing pipeline**

Run the command from Task 1, Step 2 before the implementation is added.

- [ ] **Step 3: Run generation after Tasks 1 and 2**

Run the same command after the conditional code is in place.

Expected: PASS with no desktop input generated for PS2.

### Task 4: Verify the actual PS2 build path

**Files:**
- Use: `C:\dev\helworks\helengine-ps2\scripts\launch_in_emulator.ps1`

- [ ] **Step 1: Run the focused PS2 build**

Run the existing build-waiter command targeting the B107 Colored Cubes output and require `game.iso`.

Expected: `Ps2InputBackend.cpp` compiles without generated `KeyboardState.hpp` or `MouseState.hpp`; build-waiter reports the ISO.

- [ ] **Step 2: Launch through the PS2 launcher and OCR metrics**

Launch only with `scripts\launch_in_emulator.ps1`, then use the HelenUI OCR path against that launched window. Confirm B107 is visible before reporting metrics.

- [ ] **Step 3: Commit only owned files after verification**

```powershell
git -C C:\dev\helworks\helengine add -- engine/helengine.input/InputFrameState.cs engine/helengine.input/InputSystem.cs engine/helengine.core.windows/input/InputBackendWindows.cs engine/helengine.editor.tests/testing/TestInputBackend.cs engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs docs/superpowers/plans/2026-07-25-desktop-input-frame-contract.md
git -C C:\dev\helworks\helengine commit -m "feat(input): emit desktop state only on desktop platforms"
```
