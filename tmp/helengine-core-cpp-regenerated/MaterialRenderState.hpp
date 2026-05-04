#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "MaterialBlendMode.hpp"
#include "MaterialCullMode.hpp"

class MaterialRenderState
{
public:
    virtual ~MaterialRenderState() = default;

    ::MaterialBlendMode BlendMode;

    ::MaterialBlendMode get_BlendMode();
    void set_BlendMode(::MaterialBlendMode value);

    ::MaterialCullMode CullMode;

    ::MaterialCullMode get_CullMode();
    void set_CullMode(::MaterialCullMode value);

    bool DepthTestEnabled;

    bool get_DepthTestEnabled();
    void set_DepthTestEnabled(bool value);

    bool DepthWriteEnabled;

    bool get_DepthWriteEnabled();
    void set_DepthWriteEnabled(bool value);

    ::MaterialRenderState* Clone();

    MaterialRenderState();
};
