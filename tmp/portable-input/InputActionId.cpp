#ifdef DrawText
#undef DrawText
#endif
#include "InputActionId.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_equatable.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_string.hpp"
#include "system/math.hpp"

InputActionId::InputActionId() : Value(0)
{
}

int32_t InputActionId::get_Value()
{
return this->Value;
}

bool InputActionId::Equals(::InputActionId other)
{
return this->Value == other.get_Value();}

bool InputActionId::Equals(void* obj)
{
    if (obj != nullptr)
    {
return this->Equals((*static_cast<InputActionId*>(obj)));    }
return false;}

int32_t InputActionId::GetHashCode()
{
return this->Value;}

InputActionId::InputActionId(int32_t value) : Value(0)
{
this->Value = value;
}

bool operator!=(::InputActionId left, ::InputActionId right)
{
return !left.Equals(right);}

bool operator==(::InputActionId left, ::InputActionId right)
{
return left.Equals(right);}

