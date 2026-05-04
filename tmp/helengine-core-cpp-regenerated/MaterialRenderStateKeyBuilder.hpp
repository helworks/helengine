#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class MaterialRenderState;

#include "runtime/native_exceptions.hpp"
#include "MaterialRenderState.hpp"
#include "MaterialRenderState.hpp"

class MaterialRenderStateKeyBuilder
{
public:
    virtual ~MaterialRenderStateKeyBuilder() = default;

    static int32_t Build(::MaterialRenderState* renderState);
};
