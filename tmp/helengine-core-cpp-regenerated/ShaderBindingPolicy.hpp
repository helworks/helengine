#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_exceptions.hpp"
#include "ShaderResourceType.hpp"

class ShaderBindingPolicy
{
public:
    virtual ~ShaderBindingPolicy() = default;

    int32_t ConstantBufferShift;

    int32_t get_ConstantBufferShift();

    int32_t DefaultSpace;

    int32_t get_DefaultSpace();

    int32_t SamplerShift;

    int32_t get_SamplerShift();

    int32_t StorageShift;

    int32_t get_StorageShift();

    int32_t TextureShift;

    int32_t get_TextureShift();

    int32_t GetSlot(::ShaderResourceType type, int32_t registerIndex);

    ShaderBindingPolicy(int32_t defaultSpace, int32_t constantBufferShift, int32_t textureShift, int32_t samplerShift, int32_t storageShift);
private:
    int32_t GetShift(::ShaderResourceType type);
};
