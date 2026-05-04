#ifdef DrawText
#undef DrawText
#endif
#include "InputPointerState.hpp"
#include "runtime/array.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_string.hpp"

InputPointerState::InputPointerState() : Buttons(), Connected(), DeltaX(0), DeltaY(0), ScrollDelta(0), X(0), Y(0)
{
}

uint64_t InputPointerState::get_Buttons()
{
return this->Buttons;
}

void InputPointerState::set_Buttons(uint64_t value)
{
this->Buttons = value;
}

bool InputPointerState::get_Connected()
{
return this->Connected;
}

void InputPointerState::set_Connected(bool value)
{
this->Connected = value;
}

int32_t InputPointerState::get_DeltaX()
{
return this->DeltaX;
}

void InputPointerState::set_DeltaX(int32_t value)
{
this->DeltaX = value;
}

int32_t InputPointerState::get_DeltaY()
{
return this->DeltaY;
}

void InputPointerState::set_DeltaY(int32_t value)
{
this->DeltaY = value;
}

int32_t InputPointerState::get_ScrollDelta()
{
return this->ScrollDelta;
}

void InputPointerState::set_ScrollDelta(int32_t value)
{
this->ScrollDelta = value;
}

int32_t InputPointerState::get_X()
{
return this->X;
}

void InputPointerState::set_X(int32_t value)
{
this->X = value;
}

int32_t InputPointerState::get_Y()
{
return this->Y;
}

void InputPointerState::set_Y(int32_t value)
{
this->Y = value;
}

bool InputPointerState::IsButtonDown(::InputPointerButton button)
{
return (this->Buttons & (1UL << static_cast<int32_t>(button))) != 0;}

void InputPointerState::SetButtonDown(::InputPointerButton button, bool isDown)
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

