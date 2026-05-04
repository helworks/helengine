#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class RuntimeData;
class MaterialPropertyBlock;
class RuntimeTexture;
class MaterialLayout;
class MaterialRenderState;
class MaterialConstantBufferAsset;

#include "RuntimeData.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_exceptions.hpp"
#include "MaterialPropertyBlock.hpp"
#include "RuntimeTexture.hpp"
#include "MaterialLayout.hpp"
#include "MaterialPropertyBlock.hpp"
#include "runtime/native_list.hpp"
#include "MaterialRenderState.hpp"
#include "runtime/native_list.hpp"
#include "runtime/array.hpp"
#include "MaterialConstantBufferAsset.hpp"
#include "RuntimeTexture.hpp"

class RuntimeMaterial : public RuntimeData
{
public:
    virtual ~RuntimeMaterial() = default;

    ::MaterialLayout* Layout;

    ::MaterialLayout* get_Layout();
    void set_Layout(::MaterialLayout* value);

    ::RuntimeMaterial* get_ParentMaterial();

    ::MaterialPropertyBlock* Properties;

    ::MaterialPropertyBlock* get_Properties();
    void set_Properties(::MaterialPropertyBlock* value);

    ::MaterialRenderState* RenderState;

    ::MaterialRenderState* get_RenderState();
    void set_RenderState(::MaterialRenderState* value);

    void ApplyConstantBufferDefaults(Array<::MaterialConstantBufferAsset*>* constantBuffers);

    ::RuntimeMaterial* ResolveRootMaterial();

    ::RuntimeTexture* ResolveTexture();

    RuntimeMaterial();

    void SetLayout(::MaterialLayout* layout);

    void SetParentMaterial(::RuntimeMaterial* parentMaterial);

    void SetRenderState(::MaterialRenderState* renderState);

    std::string get_Id();

    void set_Id(std::string value);
private:
    List<::RuntimeMaterial*>* ChildMaterialsValue;

    ::RuntimeMaterial* ParentMaterialValue;

    void ApplyResolvedLayout(::MaterialLayout* layout);

    void RegisterChildMaterial(::RuntimeMaterial* childMaterial);

    void RestoreConstantBufferBindings(::MaterialLayout* previousLayout, ::MaterialPropertyBlock* previousProperties);

    void RestoreTextureBindings(::MaterialLayout* previousLayout, ::MaterialPropertyBlock* previousProperties);

    void SynchronizeChildMaterials();

    void SynchronizeWithParentMaterial();

    void UnregisterChildMaterial(::RuntimeMaterial* childMaterial);

    void ValidateParentMaterial(::RuntimeMaterial* parentMaterial);
};
