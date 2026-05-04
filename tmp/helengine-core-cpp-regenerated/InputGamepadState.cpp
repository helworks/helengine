#ifdef DrawText
#undef DrawText
#endif
#include "InputGamepadState.hpp"
#include "runtime/array.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_string.hpp"

InputGamepadState::InputGamepadState() : Buttons(), Connected(), LeftStickX(), LeftStickY(), LeftTrigger(), RightStickX(), RightStickY(), RightTrigger()
{
}

uint64_t InputGamepadState::get_Buttons()
{
return this->Buttons;
}

void InputGamepadState::set_Buttons(uint64_t value)
{
this->Buttons = value;
}

bool InputGamepadState::get_Connected()
{
return this->Connected;
}

void InputGamepadState::set_Connected(bool value)
{
this->Connected = value;
}

int16_t InputGamepadState::get_LeftStickX()
{
return this->LeftStickX;
}

void InputGamepadState::set_LeftStickX(int16_t value)
{
this->LeftStickX = value;
}

int16_t InputGamepadState::get_LeftStickY()
{
return this->LeftStickY;
}

void InputGamepadState::set_LeftStickY(int16_t value)
{
this->LeftStickY = value;
}

int16_t InputGamepadState::get_LeftTrigger()
{
return this->LeftTrigger;
}

void InputGamepadState::set_LeftTrigger(int16_t value)
{
this->LeftTrigger = value;
}

int16_t InputGamepadState::get_RightStickX()
{
return this->RightStickX;
}

void InputGamepadState::set_RightStickX(int16_t value)
{
this->RightStickX = value;
}

int16_t InputGamepadState::get_RightStickY()
{
return this->RightStickY;
}

void InputGamepadState::set_RightStickY(int16_t value)
{
this->RightStickY = value;
}

int16_t InputGamepadState::get_RightTrigger()
{
return this->RightTrigger;
}

void InputGamepadState::set_RightTrigger(int16_t value)
{
this->RightTrigger = value;
}

bool InputGamepadState::IsButtonDown(::InputGamepadButton button)
{
return (this->Buttons & (1UL << static_cast<int32_t>(button))) != 0;}

void InputGamepadState::SetButtonDown(::InputGamepadButton button, bool isDown)
{
const uint64_t mask = 1UL << static_cast<int32_t>(button);
    if (isDown)
    {
this->set_Buttons(this->Buttons | mask);
    }
else {
this->set_Buttons(this->Buttons & ~mask);
}
}

