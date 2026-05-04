#ifdef DrawText
#undef DrawText
#endif
#include "RuntimeMaterial.hpp"
#include "runtime/native_exceptions.hpp"
#include "RuntimeMaterial.hpp"
#include "MaterialPropertyBlock.hpp"
#include "RuntimeTexture.hpp"
#include "MaterialLayout.hpp"
#include "runtime/native_list.hpp"
#include "MaterialConstantBufferAsset.hpp"
#include "MaterialRenderState.hpp"
#include "MaterialLayoutBinding.hpp"
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
#include "runtime/native_stack.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/app_context.hpp"
#include "system/binary_primitives.hpp"
#include "system/bit_converter.hpp"
#include "system/diagnostics/debug.hpp"
#include "system/diagnostics/stopwatch.hpp"
#include "system/guid.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream-reader.hpp"
#include "system/io/stream.hpp"
#include "system/io/string-reader.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/security/cryptography/sha256.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "system/text/string-builder.hpp"

::MaterialLayout* RuntimeMaterial::get_Layout()
{
return this->Layout;
}

void RuntimeMaterial::set_Layout(::MaterialLayout* value)
{
this->Layout = value;
}

::RuntimeMaterial* RuntimeMaterial::get_ParentMaterial()
{
return this->ParentMaterialValue;
}

::MaterialPropertyBlock* RuntimeMaterial::get_Properties()
{
return this->Properties;
}

void RuntimeMaterial::set_Properties(::MaterialPropertyBlock* value)
{
this->Properties = value;
}

::MaterialRenderState* RuntimeMaterial::get_RenderState()
{
return this->RenderState;
}

void RuntimeMaterial::set_RenderState(::MaterialRenderState* value)
{
this->RenderState = value;
}

void RuntimeMaterial::ApplyConstantBufferDefaults(Array<::MaterialConstantBufferAsset*>* constantBuffers)
{
    if (constantBuffers == nullptr)
    {
throw new ArgumentNullException("constantBuffers");
    }
for (int32_t constantBufferIndex = 0; constantBufferIndex < constantBuffers->Length; constantBufferIndex++) {
::MaterialConstantBufferAsset *constantBuffer = (*constantBuffers)[constantBufferIndex];
    if (constantBuffer == nullptr)
    {
throw new InvalidOperationException("Material constant buffers contain a null entry.");
    }
this->Properties->SetConstantBufferData(constantBuffer->get_Name(), constantBuffer->get_Data());
}
}

::RuntimeMaterial* RuntimeMaterial::ResolveRootMaterial()
{
::RuntimeMaterial *material = this;
while (material->ParentMaterialValue != nullptr) {
material = material->ParentMaterialValue;
}
return material;}

::RuntimeTexture* RuntimeMaterial::ResolveTexture()
{
::RuntimeTexture* propertyTexture;
    if (this->Properties->TryGetFirstTexture(propertyTexture))
    {
return propertyTexture;    }
else     if (this->ParentMaterialValue != nullptr)
    {
return this->ParentMaterialValue->ResolveTexture();    }
throw new InvalidOperationException("Runtime material does not define a texture for the active material layout.");
}

RuntimeMaterial::RuntimeMaterial() : Layout(), Properties(), RenderState(), ChildMaterialsValue(), ParentMaterialValue()
{
this->set_Layout(MaterialLayout::get_Empty());
this->set_RenderState(new ::MaterialRenderState());
this->set_Properties(new ::MaterialPropertyBlock(this->Layout));
this->ChildMaterialsValue = new List<::RuntimeMaterial*>();
}

void RuntimeMaterial::SetLayout(::MaterialLayout* layout)
{
    if (layout == nullptr)
    {
throw new ArgumentNullException("layout");
    }
else     if (this->ParentMaterialValue != nullptr)
    {
throw new InvalidOperationException("Parented runtime materials inherit their layout from the parent material.");
    }
this->ApplyResolvedLayout(layout);
this->SynchronizeChildMaterials();
}

void RuntimeMaterial::SetParentMaterial(::RuntimeMaterial* parentMaterial)
{
    if (parentMaterial == nullptr)
    {
throw new ArgumentNullException("parentMaterial");
    }
    if ((this->ParentMaterialValue == parentMaterial))
    {
return;    }
this->ValidateParentMaterial(parentMaterial);
::RuntimeMaterial *previousParentMaterial = this->ParentMaterialValue;
    if (previousParentMaterial != nullptr)
    {
previousParentMaterial->UnregisterChildMaterial(this);
    }
this->ParentMaterialValue = parentMaterial;
parentMaterial->RegisterChildMaterial(this);
this->SynchronizeWithParentMaterial();
}

void RuntimeMaterial::SetRenderState(::MaterialRenderState* renderState)
{
    if (renderState == nullptr)
    {
throw new ArgumentNullException("renderState");
    }
else     if (this->ParentMaterialValue != nullptr)
    {
throw new InvalidOperationException("Parented runtime materials inherit their render state from the parent material.");
    }
this->set_RenderState(renderState->Clone());
this->SynchronizeChildMaterials();
}

std::string RuntimeMaterial::get_Id()
{
return this->RuntimeData::get_Id();
}

void RuntimeMaterial::set_Id(std::string value)
{
this->RuntimeData::set_Id(value);
}

void RuntimeMaterial::ApplyResolvedLayout(::MaterialLayout* layout)
{
::MaterialLayout *previousLayout = this->Layout;
::MaterialPropertyBlock *previousProperties = this->Properties;
this->set_Layout(layout);
this->set_Properties(new ::MaterialPropertyBlock(layout));
this->RestoreTextureBindings(previousLayout, previousProperties);
this->RestoreConstantBufferBindings(previousLayout, previousProperties);
}

void RuntimeMaterial::RegisterChildMaterial(::RuntimeMaterial* childMaterial)
{
    if (childMaterial == nullptr)
    {
throw new ArgumentNullException("childMaterial");
    }
else     if (this->ChildMaterialsValue->Contains(childMaterial))
    {
throw new InvalidOperationException("Child materials cannot be registered to the same parent more than once.");
    }
this->ChildMaterialsValue->Add(childMaterial);
}

void RuntimeMaterial::RestoreConstantBufferBindings(::MaterialLayout* previousLayout, ::MaterialPropertyBlock* previousProperties)
{
    if (this->Layout->get_ConstantBufferBindings()->Length == 0 || previousLayout == nullptr || previousProperties == nullptr)
    {
return;    }
else     if (previousLayout->get_ConstantBufferBindings()->Length == 0)
    {
return;    }
this->Properties->CopyMatchingValuesFrom(previousProperties);
}

void RuntimeMaterial::RestoreTextureBindings(::MaterialLayout* previousLayout, ::MaterialPropertyBlock* previousProperties)
{
    if (this->Layout->get_TextureBindings()->Length == 0)
    {
return;    }
else     if (previousLayout == nullptr || previousProperties == nullptr)
    {
return;    }
else     if (previousLayout->get_TextureBindings()->Length == 0)
    {
return;    }
for (int32_t textureIndex = 0; textureIndex < this->Layout->get_TextureBindings()->Length; textureIndex++) {
::MaterialLayoutBinding *binding = (*this->Layout->get_TextureBindings())[textureIndex];
const int32_t previousBindingIndex = previousLayout->FindTextureBindingIndex(binding->get_Name());
    if (previousBindingIndex < 0)
    {
continue;
    }
::RuntimeTexture *previousTexture = previousProperties->GetTexture(previousBindingIndex);
    if (previousTexture == nullptr)
    {
continue;
    }
this->Properties->SetTexture(textureIndex, previousTexture);
}
}

void RuntimeMaterial::SynchronizeChildMaterials()
{
for (int32_t childIndex = 0; childIndex < this->ChildMaterialsValue->Count(); childIndex++) {
::RuntimeMaterial *childMaterial = (*this->ChildMaterialsValue)[childIndex];
    if (childMaterial == nullptr)
    {
continue;
    }
childMaterial->SynchronizeWithParentMaterial();
}
}

void RuntimeMaterial::SynchronizeWithParentMaterial()
{
    if (this->ParentMaterialValue == nullptr)
    {
return;    }
    if (!(this->Layout == ParentMaterialValue->get_Layout()))
    {
this->ApplyResolvedLayout(this->ParentMaterialValue->get_Layout());
    }
this->set_RenderState(this->ParentMaterialValue->get_RenderState()->Clone());
this->SynchronizeChildMaterials();
}

void RuntimeMaterial::UnregisterChildMaterial(::RuntimeMaterial* childMaterial)
{
    if (childMaterial == nullptr)
    {
throw new ArgumentNullException("childMaterial");
    }
this->ChildMaterialsValue->Remove(childMaterial);
}

void RuntimeMaterial::ValidateParentMaterial(::RuntimeMaterial* parentMaterial)
{
::RuntimeMaterial *currentMaterial = parentMaterial;
while (currentMaterial != nullptr) {
    if ((currentMaterial == this))
    {
throw new InvalidOperationException("Runtime materials cannot inherit from themselves or from one of their children.");
    }
currentMaterial = currentMaterial->ParentMaterialValue;
}
}

