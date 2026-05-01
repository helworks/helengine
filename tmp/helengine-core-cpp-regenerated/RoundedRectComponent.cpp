#ifdef DrawText
#undef DrawText
#endif
#include "RoundedRectComponent.hpp"
#include "Component.hpp"
#include "Entity.hpp"
#include "Core.hpp"
#include "ObjectManager.hpp"
#include "RenderManager2D.hpp"
#include "int2.hpp"
#include "float4.hpp"
#include "byte4.hpp"
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

::byte4 RoundedRectComponent::get_BorderColor()
{
return this->BorderColor;
}

void RoundedRectComponent::set_BorderColor(::byte4 value)
{
this->BorderColor = value;
}

float RoundedRectComponent::get_BorderThickness()
{
return this->BorderThickness;
}

void RoundedRectComponent::set_BorderThickness(float value)
{
this->BorderThickness = value;
}

::byte4 RoundedRectComponent::get_Color()
{
return this->Color;
}

void RoundedRectComponent::set_Color(::byte4 value)
{
this->Color = value;
}

::byte4 RoundedRectComponent::get_FillColor()
{
return this->FillColor;
}

void RoundedRectComponent::set_FillColor(::byte4 value)
{
this->FillColor = value;
}

uint8_t RoundedRectComponent::get_LayerMask()
{
return this->LayerMask;
}

void RoundedRectComponent::set_LayerMask(uint8_t value)
{
this->LayerMask = value;
}

float RoundedRectComponent::get_Radius()
{
return this->Radius;
}

void RoundedRectComponent::set_Radius(float value)
{
this->Radius = value;
}

uint8_t RoundedRectComponent::get_RenderOrder2D()
{
return this->renderOrder2D;}

void RoundedRectComponent::set_RenderOrder2D(uint8_t value)
{
    if (this->renderOrder2D != value)
    {
    if (Parent != nullptr && Parent->get_IsHierarchyEnabled())
    {
Core::get_Instance()->get_ObjectManager()->RemoveFromRender2D(this);
this->renderOrder2D = value;
Core::get_Instance()->get_ObjectManager()->RegisterForRender2D(this);
    }
else {
this->renderOrder2D = value;
}
    }
}

float RoundedRectComponent::get_Rotation()
{
return this->Rotation;
}

void RoundedRectComponent::set_Rotation(float value)
{
this->Rotation = value;
}

::int2 RoundedRectComponent::get_Size()
{
return this->Size;
}

void RoundedRectComponent::set_Size(::int2 value)
{
this->Size = value;
}

::float4 RoundedRectComponent::get_SourceRect()
{
return this->SourceRect;
}

void RoundedRectComponent::set_SourceRect(::float4 value)
{
this->SourceRect = value;
}

void RoundedRectComponent::ComponentAdded(::Entity* entity)
{
Component::ComponentAdded(entity);
    if (entity->get_IsHierarchyEnabled())
    {
Core::get_Instance()->get_ObjectManager()->RegisterForRender2D(this);
    }
}

void RoundedRectComponent::Draw()
{
Core::get_Instance()->get_RenderManager2D()->DrawRoundedRect(this);
}

void RoundedRectComponent::ParentEnabledChange(bool newEnabled)
{
Component::ParentEnabledChange(newEnabled);
    if (newEnabled)
    {
Core::get_Instance()->get_ObjectManager()->RegisterForRender2D(this);
    }
else {
Core::get_Instance()->get_ObjectManager()->RemoveFromRender2D(this);
}
}

RoundedRectComponent::RoundedRectComponent() : BorderColor(), BorderThickness(), Color(), FillColor(), LayerMask(), Radius(), Rotation(), Size(), SourceRect(), renderOrder2D()
{
this->set_Size(::int2(64, 32));
this->set_SourceRect(::float4(0, 0, 1, 1));
this->set_Color(::byte4(255, 255, 255, 255));
this->set_Radius(8.0f);
this->set_BorderThickness(0.0f);
this->set_FillColor(::byte4(255, 255, 255, 255));
this->set_BorderColor(::byte4(0, 0, 0, 255));
}

::Entity* RoundedRectComponent::get_Parent()
{
return this->Component::get_Parent();
}

void RoundedRectComponent::set_Parent(::Entity* value)
{
this->Component::set_Parent(value);
}

