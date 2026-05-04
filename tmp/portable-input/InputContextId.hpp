#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_equatable.hpp"

class InputContextId : public IEquatable<::InputContextId>
{
public:
    virtual ~InputContextId() = default;

    InputContextId();

    int32_t Value;

    int32_t get_Value();

    bool Equals(::InputContextId other);

    bool Equals(void* obj);

    int32_t GetHashCode();

    InputContextId(int32_t value);

    friend bool operator!=(::InputContextId left, ::InputContextId right);

    friend bool operator==(::InputContextId left, ::InputContextId right);
};
