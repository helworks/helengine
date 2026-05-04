#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ShaderConstantMember;

#include "ShaderConstantMember.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "ShaderConstantMember.hpp"

class ShaderConstantMemberAsset
{
public:
    virtual ~ShaderConstantMemberAsset() = default;

    ShaderConstantMemberAsset();

    std::string Name;

    int32_t Offset;

    int32_t Size;

    std::string Type;

    static ::ShaderConstantMemberAsset* FromMember(::ShaderConstantMember* member);

    ::ShaderConstantMember* ToMember();
private:
    void Validate();
};
