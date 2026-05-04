#ifdef DrawText
#undef DrawText
#endif
#include "InputFrameState.hpp"
#include "runtime/array.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_string.hpp"

InputFrameState::InputFrameState() : GamepadCount(0), Gamepads(), Keyboard(), Mouse(), Pointer(), Text()
{
}

int32_t InputFrameState::get_GamepadCount()
{
return this->GamepadCount;
}

void InputFrameState::set_GamepadCount(int32_t value)
{
this->GamepadCount = value;
}

Array<::InputGamepadState>* InputFrameState::get_Gamepads()
{
return this->Gamepads;
}

void InputFrameState::set_Gamepads(Array<::InputGamepadState>* value)
{
this->Gamepads = value;
}

::KeyboardState InputFrameState::get_Keyboard()
{
return this->Keyboard;
}

void InputFrameState::set_Keyboard(::KeyboardState value)
{
this->Keyboard = value;
}

::MouseState InputFrameState::get_Mouse()
{
return this->Mouse;
}

void InputFrameState::set_Mouse(::MouseState value)
{
this->Mouse = value;
}

::InputPointerState InputFrameState::get_Pointer()
{
return this->Pointer;
}

void InputFrameState::set_Pointer(::InputPointerState value)
{
this->Pointer = value;
}

::InputTextState InputFrameState::get_Text()
{
return this->Text;
}

void InputFrameState::set_Text(::InputTextState value)
{
this->Text = value;
}

