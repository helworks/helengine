#ifdef DrawText
#undef DrawText
#endif
#include "InputActionState.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_string.hpp"

InputActionState::InputActionState() : IsDown(), Value(), WasPressed(), WasReleased()
{
}

bool InputActionState::get_IsDown()
{
return this->IsDown;
}

void InputActionState::set_IsDown(bool value)
{
this->IsDown = value;
}

float InputActionState::get_Value()
{
return this->Value;
}

void InputActionState::set_Value(float value)
{
this->Value = value;
}

bool InputActionState::get_WasPressed()
{
return this->WasPressed;
}

void InputActionState::set_WasPressed(bool value)
{
this->WasPressed = value;
}

bool InputActionState::get_WasReleased()
{
return this->WasReleased;
}

void InputActionState::set_WasReleased(bool value)
{
this->WasReleased = value;
}

