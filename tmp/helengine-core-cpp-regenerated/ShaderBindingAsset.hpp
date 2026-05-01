#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ShaderBinding;
class ShaderConstantMemberAsset;
class ShaderConstantMember;

#include "runtime/array.hpp"
#include "ShaderBinding.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/array.hpp"
#include "runtime/native_list.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "ShaderConstantMemberAsset.hpp"
#include "ShaderResourceType.hpp"
#include "ShaderBinding.hpp"
#include "runtime/array.hpp"
#include "ShaderConstantMember.hpp"

class ShaderBindingAsset
{
public:
    ShaderBindingAsset();

    Array<::ShaderConstantMemberAsset*>* Members;

    std::string Name;

    int32_t Set;

    int32_t Size;

    int32_t Slot;

    ::ShaderResourceType Type;

    static ::ShaderBindingAsset* FromBinding(::ShaderBinding* binding);

    ::ShaderBinding* ToBinding();
private:
    static Array<::ShaderConstantMemberAsset*>* BuildMemberAssets(::ShaderBinding* binding);

    Array<::ShaderConstantMember*>* BuildMembers();

    void Validate();
};
