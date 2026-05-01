#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ShaderConstantMember;

#include "runtime/native_list.hpp"
#include "ShaderConstantMember.hpp"
#include "runtime/native_string.hpp"
#include "ShaderResourceType.hpp"
#include "runtime/array.hpp"

class ShaderBinding
{
public:
    List<::ShaderConstantMember*>* get_Members();

    std::string Name;

    std::string get_Name();

    int32_t Set;

    int32_t get_Set();

    int32_t Size;

    int32_t get_Size();

    int32_t Slot;

    int32_t get_Slot();

    ::ShaderResourceType Type;

    ::ShaderResourceType get_Type();

    ShaderBinding(std::string name, ::ShaderResourceType type, int32_t set, int32_t slot, int32_t size, Array<::ShaderConstantMember*>* members);
private:
    Array<::ShaderConstantMember*>* members;
};
