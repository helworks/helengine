#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class int2;
class RuntimeMaterial;
class MaterialAsset;
class ShaderAsset;
class RuntimeModel;
class ModelAsset;
class RenderTarget;

#include "runtime/native_disposable.hpp"
#include "runtime/native_exceptions.hpp"
#include "int2.hpp"
#include "runtime/native_event.hpp"
#include "RuntimeMaterial.hpp"
#include "MaterialAsset.hpp"
#include "ShaderAsset.hpp"
#include "RuntimeModel.hpp"
#include "ModelAsset.hpp"
#include "RenderTarget.hpp"
#include "runtime/native_string.hpp"

class RenderManager3D : public IDisposable
{
public:
    RenderManager3D();

    ::int2 MainWindowSize;

    ::int2 get_MainWindowSize();
    void set_MainWindowSize(::int2 value);

    ::Event WindowResized;

    virtual void AddWindow(intptr_t handle, int32_t width, int32_t height);

    virtual ::RuntimeMaterial* BuildMaterialFromRaw(::MaterialAsset* materialAsset, ::ShaderAsset* shaderAsset);

    virtual ::RuntimeModel* BuildModelFromRaw(::ModelAsset* data) = 0;

    virtual ::RenderTarget* CreateRenderTarget(int32_t width, int32_t height);

    virtual void Dispose();

    virtual void Draw();

    virtual void InvalidateShaderResources(std::string shaderAssetId, ::ShaderAsset* shaderAsset);

    virtual void OnWindowResize(intptr_t handle, int32_t newWidth, int32_t newHeight);

    virtual void Update();
private:
    bool setOneWindow;
};
