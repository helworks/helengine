#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ShaderVariant;

#include "ShaderVariant.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/array.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/array.hpp"
#include "ShaderVariant.hpp"

class ShaderVariantAsset
{
public:
    ShaderVariantAsset();

    Array<std::string>* Defines;

    std::string Name;

    static ::ShaderVariantAsset* FromVariant(::ShaderVariant* variant);

    ::ShaderVariant* ToVariant();
private:
    static Array<std::string>* BuildDefines(::ShaderVariant* variant);

    void Validate();
};
