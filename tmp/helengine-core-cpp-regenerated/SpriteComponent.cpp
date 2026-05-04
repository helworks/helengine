#ifdef DrawText
#undef DrawText
#endif
#include "SpriteComponent.hpp"
#include "Component.hpp"
#include "Entity.hpp"
#include "Core.hpp"
#include "ObjectManager.hpp"
#include "RenderManager2D.hpp"
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

int2* SpriteComponent::get_AnchorSize()
{
return this->Size;
}

::byte4 SpriteComponent::get_Color()
{
return this->Color;
}

void SpriteComponent::set_Color(::byte4 value)
{
this->Color = value;
}

uint8_t SpriteComponent::get_LayerMask()
{
return this->LayerMask;
}

void SpriteComponent::set_LayerMask(uint8_t value)
{
this->LayerMask = value;
}

uint8_t SpriteComponent::get_RenderOrder2D()
{
return this->renderOrder2D;}

void SpriteComponent::set_RenderOrder2D(uint8_t value)
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

float SpriteComponent::get_Rotation()
{
return this->Rotation;
}

void SpriteComponent::set_Rotation(float value)
{
this->Rotation = value;
}

int2* SpriteComponent::get_Size()
{
return this->Size;
}

void SpriteComponent::set_Size(int2* value)
{
this->Size = value;
}

::float4 SpriteComponent::get_SourceRect()
{
return this->SourceRect;
}

void SpriteComponent::set_SourceRect(::float4 value)
{
this->SourceRect = value;
}

::RuntimeTexture* SpriteComponent::get_Texture()
{
return this->Texture;
}

void SpriteComponent::set_Texture(::RuntimeTexture* value)
{
this->Texture = value;
}

void SpriteComponent::ComponentAdded(::Entity* entity)
{
Component::ComponentAdded(entity);
    if (entity->get_IsHierarchyEnabled())
    {
Core::get_Instance()->get_ObjectManager()->RegisterForRender2D(this);
    }
}

void SpriteComponent::Draw()
{
Core::get_Instance()->get_RenderManager2D()->DrawSprite(this);
}

void SpriteComponent::ParentEnabledChange(bool newEnabled)
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

SpriteComponent::SpriteComponent() : Color(), LayerMask(), Rotation(), Size(), SourceRect(), Texture(), renderOrder2D()
{
this->set_SourceRect(::float4(0, 0, 1, 1));
this->set_Color(::byte4(255, 255, 255, 255));
}

::Entity* SpriteComponent::get_Parent()
{
return this->Component::get_Parent();
}

void SpriteComponent::set_Parent(::Entity* value)
{
this->Component::set_Parent(value);
}

