#ifdef DrawText
#undef DrawText
#endif
#include "InputControlId.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_equatable.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_string.hpp"
#include "system/math.hpp"

InputControlId::InputControlId() : ControlIndex(0), ControlKind(), DeviceIndex(0), DeviceKind()
{
}

int32_t InputControlId::get_ControlIndex()
{
return this->ControlIndex;
}

::InputControlKind InputControlId::get_ControlKind()
{
return this->ControlKind;
}

int32_t InputControlId::get_DeviceIndex()
{
return this->DeviceIndex;
}

::InputDeviceKind InputControlId::get_DeviceKind()
{
return this->DeviceKind;
}

bool InputControlId::Equals(::InputControlId other)
{
return this->DeviceKind == other.get_DeviceKind() && this->ControlKind == other.get_ControlKind() && this->DeviceIndex == other.get_DeviceIndex() && this->ControlIndex == other.get_ControlIndex();}

bool InputControlId::Equals(void* obj)
{
    if (obj != nullptr)
    {
return this->Equals((*static_cast<InputControlId*>(obj)));    }
return false;}

int32_t InputControlId::GetHashCode()
{
{
int32_t hashCode = static_cast<int32_t>(this->DeviceKind);
hashCode = (hashCode * 397) ^ static_cast<int32_t>(this->ControlKind);
hashCode = (hashCode * 397) ^ this->DeviceIndex;
hashCode = (hashCode * 397) ^ this->ControlIndex;
return hashCode;}
}

InputControlId::InputControlId(::InputDeviceKind deviceKind, ::InputControlKind controlKind, int32_t deviceIndex, int32_t controlIndex) : ControlIndex(0), ControlKind(), DeviceIndex(0), DeviceKind()
{
this->DeviceKind = deviceKind;
this->ControlKind = controlKind;
this->DeviceIndex = deviceIndex;
this->ControlIndex = controlIndex;
}

bool operator!=(::InputControlId left, ::InputControlId right)
{
return !left.Equals(right);}

bool operator==(::InputControlId left, ::InputControlId right)
{
return left.Equals(right);}

