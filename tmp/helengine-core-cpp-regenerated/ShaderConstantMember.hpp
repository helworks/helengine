#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_string.hpp"

class ShaderConstantMember
{
public:
    std::string Name;

    std::string get_Name();

    int32_t Offset;

    int32_t get_Offset();

    int32_t Size;

    int32_t get_Size();

    std::string Type;

    std::string get_Type();

    ShaderConstantMember(std::string name, std::string type, int32_t offset, int32_t size);
};
