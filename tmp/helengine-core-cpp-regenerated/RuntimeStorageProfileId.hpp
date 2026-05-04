#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_string.hpp"

class RuntimeStorageProfileId
{
public:
    virtual ~RuntimeStorageProfileId() = default;

    std::string Value;

    std::string get_Value();

    RuntimeStorageProfileId(std::string value);

    std::string ToString();
};
