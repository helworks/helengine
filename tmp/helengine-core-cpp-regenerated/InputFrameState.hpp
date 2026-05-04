#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class InputGamepadState;
class KeyboardState;
class MouseState;
class InputPointerState;
class InputTextState;

#include "runtime/array.hpp"
#include "InputGamepadState.hpp"
#include "KeyboardState.hpp"
#include "MouseState.hpp"
#include "InputPointerState.hpp"
#include "InputTextState.hpp"

class InputFrameState
{
public:
    virtual ~InputFrameState() = default;

    InputFrameState();

    int32_t GamepadCount;

    int32_t get_GamepadCount();
    void set_GamepadCount(int32_t value);

    Array<::InputGamepadState>* Gamepads;

    Array<::InputGamepadState>* get_Gamepads();
    void set_Gamepads(Array<::InputGamepadState>* value);

    ::KeyboardState Keyboard;

    ::KeyboardState get_Keyboard();
    void set_Keyboard(::KeyboardState value);

    ::MouseState Mouse;

    ::MouseState get_Mouse();
    void set_Mouse(::MouseState value);

    ::InputPointerState Pointer;

    ::InputPointerState get_Pointer();
    void set_Pointer(::InputPointerState value);

    ::InputTextState Text;

    ::InputTextState get_Text();
    void set_Text(::InputTextState value);
};
