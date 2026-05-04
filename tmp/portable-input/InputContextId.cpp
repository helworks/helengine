#ifdef DrawText
#undef DrawText
#endif
#include "InputContextId.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_equatable.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_string.hpp"
#include "system/math.hpp"

InputContextId::InputContextId() : Value(0)
{
}

int32_t InputContextId::get_Value()
{
return this->Value;
}

bool InputContextId::Equals(::InputContextId other)
{
return this->Value == other.get_Value();}

bool InputContextId::Equals(void* obj)
{
    if (obj != nullptr)
    {
return this->Equals((*static_cast<InputContextId*>(obj)));    }
return false;}

int32_t InputContextId::GetHashCode()
{
return this->Value;}

InputContextId::InputContextId(int32_t value) : Value(0)
{
this->Value = value;
}

bool operator!=(::InputContextId left, ::InputContextId right)
{
return !left.Equals(right);}

bool operator==(::InputContextId left, ::InputContextId right)
{
return left.Equals(right);}

