#ifdef DrawText
#undef DrawText
#endif
#include "RenderManager3D.hpp"
#include "runtime/native_exceptions.hpp"
#include "int2.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_disposable.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_event.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_span.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/bit_converter.hpp"
#include "system/io/stream.hpp"
#include "system/math.hpp"
#include "system/text/encoding.hpp"

RenderManager3D::RenderManager3D() : MainWindowSize(), WindowResized(), setOneWindow()
{
}

::int2 RenderManager3D::get_MainWindowSize()
{
return this->MainWindowSize;
}

void RenderManager3D::set_MainWindowSize(::int2 value)
{
this->MainWindowSize = value;
}

void RenderManager3D::AddWindow(intptr_t handle, int32_t width, int32_t height)
{
    if (!this->setOneWindow)
    {
this->set_MainWindowSize(::int2(width, height));
    }
this->setOneWindow = true;
}

::RuntimeMaterial* RenderManager3D::BuildMaterialFromRaw(::MaterialAsset* materialAsset, ::ShaderAsset* shaderAsset)
{
throw new NotSupportedException("This renderer does not support material creation.");
}

::RenderTarget* RenderManager3D::CreateRenderTarget(int32_t width, int32_t height)
{
throw new NotSupportedException("This renderer does not support render target creation.");
}

void RenderManager3D::Dispose()
{
}

void RenderManager3D::Draw()
{
}

void RenderManager3D::InvalidateShaderResources(std::string shaderAssetId, ::ShaderAsset* shaderAsset)
{
}

void RenderManager3D::OnWindowResize(intptr_t handle, int32_t newWidth, int32_t newHeight)
{
    if (!this->setOneWindow || (this->MainWindowSize.X == 0 && this->MainWindowSize.Y == 0))
    {
this->set_MainWindowSize(::int2(newWidth, newHeight));
    }
this->WindowResized.Invoke(handle, newWidth, newHeight);
}

void RenderManager3D::Update()
{
}

