#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_string.hpp"
#include "ShaderResourceType.hpp"

class MaterialLayoutBinding
{
public:
    std::string Name;

    std::string get_Name();

    ::ShaderResourceType ResourceType;

    ::ShaderResourceType get_ResourceType();

    int32_t Set;

    int32_t get_Set();

    int32_t Size;

    int32_t get_Size();

    int32_t Slot;

    int32_t get_Slot();

    MaterialLayoutBinding(std::string name, ::ShaderResourceType resourceType, int32_t set, int32_t slot, int32_t size);
};
