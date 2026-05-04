#ifdef DrawText
#undef DrawText
#endif
#include "MaterialLayout.hpp"
#include "MaterialLayout.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_string.hpp"
#include "MaterialRenderState.hpp"
#include "MaterialLayoutBinding.hpp"
#include "runtime/array.hpp"
#include "runtime/native_cast.hpp"
#include "runtime/native_datetime.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_disposable.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_event.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_nullable.hpp"
#include "runtime/native_span.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/app_context.hpp"
#include "system/bit_converter.hpp"
#include "system/diagnostics/debug.hpp"
#include "system/guid.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"

::MaterialLayout* MaterialLayout::get_Empty()
{
return EmptyValue;
}

Array<::MaterialLayoutBinding*>* MaterialLayout::get_ConstantBufferBindings()
{
return this->ConstantBufferBindings;
}

std::string MaterialLayout::get_PixelProgram()
{
return this->PixelProgram;
}

::MaterialRenderState* MaterialLayout::get_RenderState()
{
return this->RenderState;
}

Array<::MaterialLayoutBinding*>* MaterialLayout::get_SamplerBindings()
{
return this->SamplerBindings;
}

std::string MaterialLayout::get_ShaderAssetId()
{
return this->ShaderAssetId;
}

Array<::MaterialLayoutBinding*>* MaterialLayout::get_TextureBindings()
{
return this->TextureBindings;
}

std::string MaterialLayout::get_Variant()
{
return this->Variant;
}

std::string MaterialLayout::get_VertexProgram()
{
return this->VertexProgram;
}

int32_t MaterialLayout::FindConstantBufferBindingIndex(std::string bindingName)
{
return this->FindBindingIndex(this->ConstantBufferBindings, bindingName);}

int32_t MaterialLayout::FindSamplerBindingIndex(std::string bindingName)
{
return this->FindBindingIndex(this->SamplerBindings, bindingName);}

int32_t MaterialLayout::FindTextureBindingIndex(std::string bindingName)
{
return this->FindBindingIndex(this->TextureBindings, bindingName);}

MaterialLayout::MaterialLayout(std::string shaderAssetId, std::string vertexProgram, std::string pixelProgram, std::string variant, ::MaterialRenderState* renderState, Array<::MaterialLayoutBinding*>* textureBindings, Array<::MaterialLayoutBinding*>* constantBufferBindings, Array<::MaterialLayoutBinding*>* samplerBindings) : ConstantBufferBindings(), PixelProgram(), RenderState(), SamplerBindings(), ShaderAssetId(), TextureBindings(), Variant(), VertexProgram()
{
this->ShaderAssetId = shaderAssetId;
this->VertexProgram = vertexProgram;
this->PixelProgram = pixelProgram;
this->Variant = variant;
this->RenderState = (renderState != nullptr ? renderState : throw new ArgumentNullException("renderState"));
this->TextureBindings = (textureBindings != nullptr ? textureBindings : throw new ArgumentNullException("textureBindings"));
this->ConstantBufferBindings = (constantBufferBindings != nullptr ? constantBufferBindings : throw new ArgumentNullException("constantBufferBindings"));
this->SamplerBindings = (samplerBindings != nullptr ? samplerBindings : throw new ArgumentNullException("samplerBindings"));
}

::MaterialLayout* MaterialLayout::EmptyValue = CreateEmptyValue();

::MaterialLayout* MaterialLayout::CreateEmptyValue()
{
return ([&]() {
auto __ctor_arg_00000095 = String::Empty;
auto __ctor_arg_00000096 = String::Empty;
auto __ctor_arg_00000097 = String::Empty;
auto __ctor_arg_00000098 = String::Empty;
auto __ctor_arg_00000099 = new ::MaterialRenderState();
auto __ctor_arg_0000009A = Array<MaterialLayoutBinding*>::Empty();
auto __ctor_arg_0000009B = Array<MaterialLayoutBinding*>::Empty();
auto __ctor_arg_0000009C = Array<MaterialLayoutBinding*>::Empty();
return new ::MaterialLayout(__ctor_arg_00000095, __ctor_arg_00000096, __ctor_arg_00000097, __ctor_arg_00000098, __ctor_arg_00000099, __ctor_arg_0000009A, __ctor_arg_0000009B, __ctor_arg_0000009C);
})();}

int32_t MaterialLayout::FindBindingIndex(Array<::MaterialLayoutBinding*>* bindings, std::string bindingName)
{
    if (bindings == nullptr)
    {
throw new ArgumentNullException("bindings");
    }
    if (String::IsNullOrWhiteSpace(bindingName))
    {
throw ([&]() {
auto __ctor_arg_0000009D = "Binding name must be provided.";
auto __ctor_arg_0000009E = "bindingName";
return new ArgumentException(__ctor_arg_0000009D, __ctor_arg_0000009E);
})();
    }
for (int32_t bindingIndex = 0; bindingIndex < bindings->Length; bindingIndex++) {
::MaterialLayoutBinding *binding = (*bindings)[bindingIndex];
    if (binding == nullptr)
    {
continue;
    }
    if (String::Equals(binding->get_Name(), bindingName, StringComparison::Ordinal))
    {
return bindingIndex;    }
}
return -1;}

