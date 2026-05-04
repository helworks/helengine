#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_equatable.hpp"
#include "InputControlKind.hpp"
#include "InputDeviceKind.hpp"

class InputControlId : public IEquatable<::InputControlId>
{
public:
    virtual ~InputControlId() = default;

    InputControlId();

    int32_t ControlIndex;

    int32_t get_ControlIndex();

    ::InputControlKind ControlKind;

    ::InputControlKind get_ControlKind();

    int32_t DeviceIndex;

    int32_t get_DeviceIndex();

    ::InputDeviceKind DeviceKind;

    ::InputDeviceKind get_DeviceKind();

    bool Equals(::InputControlId other);

    bool Equals(void* obj);

    int32_t GetHashCode();

    InputControlId(::InputDeviceKind deviceKind, ::InputControlKind controlKind, int32_t deviceIndex, int32_t controlIndex);

    friend bool operator!=(::InputControlId left, ::InputControlId right);

    friend bool operator==(::InputControlId left, ::InputControlId right);
};
