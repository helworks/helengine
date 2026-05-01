#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class MaterialLayoutBinding;
class MaterialRenderState;

#include "runtime/native_exceptions.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/array.hpp"
#include "MaterialLayoutBinding.hpp"
#include "MaterialRenderState.hpp"

class MaterialLayout
{
public:
    static ::MaterialLayout* get_Empty();

    Array<::MaterialLayoutBinding*>* ConstantBufferBindings;

    Array<::MaterialLayoutBinding*>* get_ConstantBufferBindings();

    std::string PixelProgram;

    std::string get_PixelProgram();

    ::MaterialRenderState* RenderState;

    ::MaterialRenderState* get_RenderState();

    Array<::MaterialLayoutBinding*>* SamplerBindings;

    Array<::MaterialLayoutBinding*>* get_SamplerBindings();

    std::string ShaderAssetId;

    std::string get_ShaderAssetId();

    Array<::MaterialLayoutBinding*>* TextureBindings;

    Array<::MaterialLayoutBinding*>* get_TextureBindings();

    std::string Variant;

    std::string get_Variant();

    std::string VertexProgram;

    std::string get_VertexProgram();

    int32_t FindConstantBufferBindingIndex(std::string bindingName);

    int32_t FindSamplerBindingIndex(std::string bindingName);

    int32_t FindTextureBindingIndex(std::string bindingName);

    MaterialLayout(std::string shaderAssetId, std::string vertexProgram, std::string pixelProgram, std::string variant, ::MaterialRenderState* renderState, Array<::MaterialLayoutBinding*>* textureBindings, Array<::MaterialLayoutBinding*>* constantBufferBindings, Array<::MaterialLayoutBinding*>* samplerBindings);
private:
    static ::MaterialLayout* EmptyValue;

    static int32_t FindBindingIndex(Array<::MaterialLayoutBinding*>* bindings, std::string bindingName);
};
