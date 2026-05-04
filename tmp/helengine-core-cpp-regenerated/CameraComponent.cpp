#ifdef DrawText
#undef DrawText
#endif
#include "CameraComponent.hpp"
#include "runtime/native_exceptions.hpp"
#include "CoreInitializationOptions.hpp"
#include "Core.hpp"
#include "Component.hpp"
#include "Entity.hpp"
#include "ObjectManager.hpp"
#include "float4.hpp"
#include "CameraClearSettings.hpp"
#include "RenderList2D.hpp"
#include "RenderList3D.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
#include "runtime/native_cast.hpp"
#include "runtime/native_datetime.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_disposable.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_equatable.hpp"
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
#include "system/io/directory.hpp"
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

uint8_t CameraComponent::get_CameraDrawOrder()
{
return this->cameraDrawOrder;}

void CameraComponent::set_CameraDrawOrder(uint8_t value)
{
    if (this->cameraDrawOrder != value)
    {
    if (Parent != nullptr && Parent->get_IsHierarchyEnabled())
    {
Core::get_Instance()->get_ObjectManager()->RemoveCamera(this);
this->cameraDrawOrder = value;
Core::get_Instance()->get_ObjectManager()->RegisterCamera(this);
    }
else {
this->cameraDrawOrder = value;
}
    }
}

::CameraClearSettings CameraComponent::get_ClearSettings()
{
return this->ClearSettings;
}

void CameraComponent::set_ClearSettings(::CameraClearSettings value)
{
this->ClearSettings = value;
}

uint16_t CameraComponent::get_LayerMask()
{
return this->layerMask;}

void CameraComponent::set_LayerMask(uint16_t value)
{
    if (this->layerMask != value)
    {
    if (Parent != nullptr && Parent->get_IsHierarchyEnabled())
    {
Core::get_Instance()->get_ObjectManager()->RemoveCamera(this);
this->layerMask = value;
Core::get_Instance()->get_ObjectManager()->RegisterCamera(this);
    }
else {
this->layerMask = value;
}
    }
}

::IRenderQueue2D* CameraComponent::get_RenderQueue2D()
{
return this->renderList2D;}

::IRenderQueue3D* CameraComponent::get_RenderQueue3D()
{
return this->renderList3D;}

::RenderTarget* CameraComponent::get_RenderTarget()
{
return this->RenderTarget;
}

void CameraComponent::set_RenderTarget(::RenderTarget* value)
{
this->RenderTarget = value;
}

::float4 CameraComponent::get_Viewport()
{
return this->Viewport;
}

void CameraComponent::set_Viewport(::float4 value)
{
this->Viewport = value;
}

CameraComponent::CameraComponent() : ClearSettings(), RenderTarget(), Viewport(), cameraDrawOrder(), layerMask(), renderList2D(), renderList3D()
{
this->set_LayerMask(0b11111111);
this->set_Viewport(::float4(0, 0, 1, 1));
this->set_ClearSettings(([&]() {
auto __ctor_arg_000001AC = true;
auto __ctor_arg_000001AD = ::float4(0.0f, 0.0f, 0.0f, 0.0f);
auto __ctor_arg_000001AE = true;
auto __ctor_arg_000001AF = 1.0f;
auto __ctor_arg_000001B0 = false;
auto __ctor_arg_000001B1 = 0;
return ::CameraClearSettings(__ctor_arg_000001AC, __ctor_arg_000001AD, __ctor_arg_000001AE, __ctor_arg_000001AF, __ctor_arg_000001B0, __ctor_arg_000001B1);
})());
this->InitializeLists();
}

void CameraComponent::ComponentAdded(::Entity* entity)
{
Component::ComponentAdded(entity);
    if (entity->get_IsHierarchyEnabled())
    {
Core::get_Instance()->get_ObjectManager()->RegisterCamera(this);
    }
}

void CameraComponent::ParentEnabledChange(bool newEnabled)
{
Component::ParentEnabledChange(newEnabled);
    if (newEnabled)
    {
Core::get_Instance()->get_ObjectManager()->RegisterCamera(this);
    }
else {
Core::get_Instance()->get_ObjectManager()->RemoveCamera(this);
}
}

::Entity* CameraComponent::get_Parent()
{
return this->Component::get_Parent();
}

void CameraComponent::set_Parent(::Entity* value)
{
this->Component::set_Parent(value);
}

void CameraComponent::InitializeLists()
{
    if (Core::get_Instance() == nullptr || Core::get_Instance()->get_InitializationOptions() == nullptr)
    {
throw new InvalidOperationException("Core initialization options must be set before creating camera lists.");
    }
::CoreInitializationOptions *settings = Core::get_Instance()->get_InitializationOptions();
settings->Normalize();
this->renderList2D = new ::RenderList2D(settings->get_RenderList2DInitialCapacity());
this->renderList3D = new ::RenderList3D(settings->get_RenderList3DInitialCapacity());
}

