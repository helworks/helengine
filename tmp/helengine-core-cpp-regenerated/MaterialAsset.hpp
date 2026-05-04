#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class Asset;
class MaterialConstantBufferAsset;
class MaterialRenderState;

#include "Asset.hpp"
#include "runtime/array.hpp"
#include "MaterialConstantBufferAsset.hpp"
#include "runtime/native_string.hpp"
#include "MaterialRenderState.hpp"

class MaterialAsset : public Asset
{
public:
    virtual ~MaterialAsset() = default;

    Array<::MaterialConstantBufferAsset*>* ConstantBuffers;

    std::string PixelProgram;

    ::MaterialRenderState* RenderState;

    std::string ShaderAssetId;

    std::string Variant;

    std::string VertexProgram;

    MaterialAsset();

    std::string get_Id();

    void set_Id(std::string value);
};
