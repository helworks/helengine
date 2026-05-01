#ifdef DrawText
#undef DrawText
#endif
#include "MaterialLayoutBuilder.hpp"
#include "runtime/native_exceptions.hpp"
#include "ShaderProgramAsset.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_dictionary.hpp"
#include "system/string_comparer.hpp"
#include "MaterialRenderState.hpp"
#include "MaterialLayout.hpp"
#include "runtime/native_string.hpp"
#include "ShaderStage.hpp"
#include "MaterialLayoutBinding.hpp"
#include "ShaderResourceType.hpp"
#include "ShaderBindingAsset.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
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

::MaterialLayout* MaterialLayoutBuilder::Build(::MaterialAsset* materialAsset, ::ShaderAsset* shaderAsset)
{
    if (materialAsset == nullptr)
    {
throw new ArgumentNullException("materialAsset");
    }
    if (shaderAsset == nullptr)
    {
throw new ArgumentNullException("shaderAsset");
    }
::ShaderProgramAsset *vertexProgram = FindProgram(shaderAsset, materialAsset->VertexProgram, ShaderStage::Vertex);
::ShaderProgramAsset *pixelProgram = FindProgram(shaderAsset, materialAsset->PixelProgram, ShaderStage::Pixel);
List<::MaterialLayoutBinding*> *textureBindings = new List<::MaterialLayoutBinding*>();
List<::MaterialLayoutBinding*> *constantBufferBindings = new List<::MaterialLayoutBinding*>();
List<::MaterialLayoutBinding*> *samplerBindings = new List<::MaterialLayoutBinding*>();
Dictionary<std::string, ::MaterialLayoutBinding*> *bindingByKey = new Dictionary<std::string, ::MaterialLayoutBinding*>(StringComparer::Ordinal);
AddBindings(vertexProgram, textureBindings, constantBufferBindings, samplerBindings, bindingByKey);
AddBindings(pixelProgram, textureBindings, constantBufferBindings, samplerBindings, bindingByKey);
::MaterialRenderState *renderState = (materialAsset->RenderState != nullptr ? materialAsset->RenderState : throw new InvalidOperationException("Material render state must be provided."));
return ([&]() {
auto __ctor_arg_4f5e5ee5 = materialAsset->ShaderAssetId;
auto __ctor_arg_c97bd707 = materialAsset->VertexProgram;
auto __ctor_arg_b032ab82 = materialAsset->PixelProgram;
auto __ctor_arg_1b3390de = materialAsset->Variant;
auto __ctor_arg_bd0d4bf7 = renderState->Clone();
auto __ctor_arg_f828b1b9 = textureBindings->ToArray();
auto __ctor_arg_ff623637 = constantBufferBindings->ToArray();
auto __ctor_arg_454b8ee6 = samplerBindings->ToArray();
return new ::MaterialLayout(__ctor_arg_4f5e5ee5, __ctor_arg_c97bd707, __ctor_arg_b032ab82, __ctor_arg_1b3390de, __ctor_arg_bd0d4bf7, __ctor_arg_f828b1b9, __ctor_arg_ff623637, __ctor_arg_454b8ee6);
})();}

std::string MaterialLayoutBuilder::TransformBufferName = "TransformBuffer";

void MaterialLayoutBuilder::AddBindingToCategory(::MaterialLayoutBinding* binding, List<::MaterialLayoutBinding*>* textureBindings, List<::MaterialLayoutBinding*>* constantBufferBindings, List<::MaterialLayoutBinding*>* samplerBindings)
{
    if (binding == nullptr)
    {
throw new ArgumentNullException("binding");
    }
    if (binding->get_ResourceType() == ShaderResourceType::Texture2D || binding->get_ResourceType() == ShaderResourceType::TextureCube)
    {
textureBindings->Add(binding);
return;    }
else     if (binding->get_ResourceType() == ShaderResourceType::ConstantBuffer)
    {
constantBufferBindings->Add(binding);
return;    }
else     if (binding->get_ResourceType() == ShaderResourceType::Sampler)
    {
samplerBindings->Add(binding);
return;    }
throw new InvalidOperationException(std::string("Shader resource type '") + std::to_string(static_cast<int32_t>(binding->get_ResourceType())) + std::string("' is not supported by the material layout builder."));
}

void MaterialLayoutBuilder::AddBindings(::ShaderProgramAsset* program, List<::MaterialLayoutBinding*>* textureBindings, List<::MaterialLayoutBinding*>* constantBufferBindings, List<::MaterialLayoutBinding*>* samplerBindings, Dictionary<std::string, ::MaterialLayoutBinding*>* bindingByKey)
{
    if (program == nullptr)
    {
throw new ArgumentNullException("program");
    }
    if (program->Bindings == nullptr)
    {
throw new InvalidOperationException("Shader program bindings must be provided.");
    }
for (int32_t bindingIndex = 0; bindingIndex < program->Bindings->Length; bindingIndex++) {
::ShaderBindingAsset *binding = (*program->Bindings)[bindingIndex];
    if (binding == nullptr)
    {
throw new InvalidOperationException("Shader program bindings contain a null entry.");
    }
    if (IsEngineManagedBinding(binding))
    {
continue;
    }
::MaterialLayoutBinding *layoutBinding = new ::MaterialLayoutBinding(binding->Name, binding->Type, binding->Set, binding->Slot, binding->Size);
const std::string bindingKey = BuildBindingKey(layoutBinding);
::MaterialLayoutBinding* existingBinding;
    if (bindingByKey->TryGetValue(bindingKey, existingBinding))
    {
ValidateMatchingBinding(existingBinding, layoutBinding);
continue;
    }
(*bindingByKey)[bindingKey] = layoutBinding;
AddBindingToCategory(layoutBinding, textureBindings, constantBufferBindings, samplerBindings);
}
}

std::string MaterialLayoutBuilder::BuildBindingKey(::MaterialLayoutBinding* binding)
{
    if (binding == nullptr)
    {
throw new ArgumentNullException("binding");
    }
return String::Concat(std::to_string((static_cast<int32_t>(binding->get_ResourceType()))), "|", binding->get_Name());}

::ShaderProgramAsset* MaterialLayoutBuilder::FindProgram(::ShaderAsset* shaderAsset, std::string programName, ::ShaderStage stage)
{
    if (shaderAsset->Programs == nullptr)
    {
throw new InvalidOperationException("Shader asset programs must be provided.");
    }
for (int32_t programIndex = 0; programIndex < shaderAsset->Programs->Length; programIndex++) {
::ShaderProgramAsset *program = (*shaderAsset->Programs)[programIndex];
    if (program == nullptr)
    {
continue;
    }
    if (!String::Equals(program->Name, programName, StringComparison::Ordinal))
    {
continue;
    }
else     if (program->Stage != stage)
    {
continue;
    }
else {
return program;}
}
throw new InvalidOperationException(std::string("Shader program '") + programName + std::string("' with stage '") + std::to_string(static_cast<int32_t>(stage)) + std::string("' was not found."));
}

bool MaterialLayoutBuilder::IsEngineManagedBinding(::ShaderBindingAsset* binding)
{
    if (binding == nullptr)
    {
throw new ArgumentNullException("binding");
    }
return binding->Type == ShaderResourceType::ConstantBuffer && binding->Slot == 0 && String::Equals(binding->Name, TransformBufferName, StringComparison::Ordinal);}

void MaterialLayoutBuilder::ValidateMatchingBinding(::MaterialLayoutBinding* existingBinding, ::MaterialLayoutBinding* newBinding)
{
    if (existingBinding == nullptr)
    {
throw new ArgumentNullException("existingBinding");
    }
    if (newBinding == nullptr)
    {
throw new ArgumentNullException("newBinding");
    }
    if (existingBinding->get_ResourceType() != newBinding->get_ResourceType())
    {
throw new InvalidOperationException(std::string("Material binding '") + existingBinding->get_Name() + std::string("' uses conflicting resource types across shader stages."));
    }
else     if (existingBinding->get_Set() != newBinding->get_Set())
    {
throw new InvalidOperationException(std::string("Material binding '") + existingBinding->get_Name() + std::string("' uses conflicting sets across shader stages."));
    }
else     if (existingBinding->get_Slot() != newBinding->get_Slot())
    {
throw new InvalidOperationException(std::string("Material binding '") + existingBinding->get_Name() + std::string("' uses conflicting slots across shader stages."));
    }
else     if (existingBinding->get_Size() != newBinding->get_Size())
    {
throw new InvalidOperationException(std::string("Material binding '") + existingBinding->get_Name() + std::string("' uses conflicting sizes across shader stages."));
    }
}

