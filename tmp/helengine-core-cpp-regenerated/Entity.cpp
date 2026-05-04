#ifdef DrawText
#undef DrawText
#endif
#include "Entity.hpp"
#include "runtime/native_exceptions.hpp"
#include "Entity.hpp"
#include "runtime/native_list.hpp"
#include "Component.hpp"
#include "float4.hpp"
#include "float3.hpp"
#include "Core.hpp"
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

List<::Entity*>* Entity::get_Children()
{
return this->Children;
}

void Entity::set_Children(List<::Entity*>* value)
{
this->Children = value;
}

List<::Component*>* Entity::get_Components()
{
return this->Components;
}

void Entity::set_Components(List<::Component*>* value)
{
this->Components = value;
}

bool Entity::get_Enabled()
{
return this->isEnabled;}

void Entity::set_Enabled(bool value)
{
const bool wasHierarchyEnabled = this->get_IsHierarchyEnabled();
    if (this->isEnabled != value)
    {
this->isEnabled = value;
const bool isHierarchyEnabled = this->get_IsHierarchyEnabled();
    if (wasHierarchyEnabled != isHierarchyEnabled)
    {
this->ParentEnabledChange(isHierarchyEnabled);
    }
    }
}

bool Entity::get_IsHierarchyEnabled()
{
    if (!this->isEnabled)
    {
return false;    }
    if (this->Parent == nullptr)
    {
return true;    }
return this->Parent->get_IsHierarchyEnabled();}

uint16_t Entity::get_LayerMask()
{
return this->LayerMask;
}

void Entity::set_LayerMask(uint16_t value)
{
this->LayerMask = value;
}

::float4 Entity::get_LocalOrientation()
{
return this->orientation;}

void Entity::set_LocalOrientation(::float4 value)
{
this->orientation = value;
}

::float3 Entity::get_LocalPosition()
{
return this->position;}

void Entity::set_LocalPosition(::float3 value)
{
this->position = value;
}

::float3 Entity::get_LocalScale()
{
return this->scale;}

void Entity::set_LocalScale(::float3 value)
{
this->scale = value;
}

::float4 Entity::get_Orientation()
{
::float4 ori = this->orientation;
    if (this->Parent != nullptr)
    {
ori = ori * this->Parent->get_Orientation();
    }
return ori;}

void Entity::set_Orientation(::float4 value)
{
this->orientation = value;
}

::Entity* Entity::get_Parent()
{
return this->Parent;
}

void Entity::set_Parent(::Entity* value)
{
this->Parent = value;
}

::float3 Entity::get_Position()
{
::float3 pos = this->position;
    if (this->Parent != nullptr)
    {
::float3 rotatedLocal = float4::RotateVector(pos, this->Parent->get_Orientation());
pos = rotatedLocal + this->Parent->get_Position();
    }
return pos;}

void Entity::set_Position(::float3 value)
{
this->position = value;
}

::float3 Entity::get_Scale()
{
::float3 sca = this->scale;
    if (this->Parent != nullptr)
    {
sca = sca + this->Parent->get_Scale();
    }
return sca;}

void Entity::set_Scale(::float3 value)
{
this->scale = value;
}

bool Entity::get_Static()
{
return this->isStatic;}

void Entity::set_Static(bool value)
{
    if (this->isStatic != value)
    {
this->ParentStaticChange(value);
    }
this->isStatic = value;
}

void Entity::AddChild(::Entity* entity)
{
    if (entity->get_Parent() != nullptr)
    {
throw new Exception("Parent is not empty");
    }
const bool wasHierarchyEnabled = entity->get_IsHierarchyEnabled();
entity->set_Parent(this);
this->Children->Add(entity);
const bool isHierarchyEnabled = entity->get_IsHierarchyEnabled();
    if (wasHierarchyEnabled != isHierarchyEnabled)
    {
entity->ParentEnabledChange(isHierarchyEnabled);
    }
}

void Entity::AddComponent(::Component* comp)
{
this->Components->Add(comp);
comp->ComponentAdded(this);
}

void Entity::Dispose()
{
}

Entity::Entity() : Children(), Components(), LayerMask(), Parent(), isEnabled(), isStatic(), orientation(), position(), scale()
{
this->isEnabled = true;
this->set_Orientation(float4::get_Identity());
this->set_Scale(float3::get_One());
this->set_LayerMask(0b00000001);
Core::get_Instance()->get_ObjectManager()->RegisterEntity(this);
}

void Entity::InitChildren()
{
this->set_Children(new List<::Entity*>());
}

void Entity::InitComponents()
{
this->set_Components(new List<::Component*>());
}

void Entity::RemoveChild(::Entity* entity)
{
    if (entity == nullptr)
    {
throw new ArgumentNullException("entity");
    }
else     if (this->Children == nullptr)
    {
throw new InvalidOperationException("Children collection has not been initialized.");
    }
else     if (entity->get_Parent() != this)
    {
throw new InvalidOperationException("Entity is not parented to this parent.");
    }
const bool wasHierarchyEnabled = entity->get_IsHierarchyEnabled();
    if (!this->Children->Remove(entity))
    {
throw new InvalidOperationException("Entity could not be removed from the child collection.");
    }
entity->set_Parent(nullptr);
const bool isHierarchyEnabled = entity->get_IsHierarchyEnabled();
    if (wasHierarchyEnabled != isHierarchyEnabled)
    {
entity->ParentEnabledChange(isHierarchyEnabled);
    }
}

void Entity::RemoveComponent(::Component* comp)
{
    if (comp == nullptr)
    {
throw new ArgumentNullException("comp");
    }
else     if (this->Components == nullptr)
    {
throw new InvalidOperationException("Components collection has not been initialized.");
    }
else     if (comp->get_Parent() != this)
    {
throw new InvalidOperationException("Component is not attached to this entity.");
    }
    if (!this->Components->Remove(comp))
    {
throw new InvalidOperationException("Component could not be removed from the component collection.");
    }
    if (this->get_IsHierarchyEnabled())
    {
comp->ParentEnabledChange(false);
    }
comp->ComponentRemoved(this);
}

void Entity::ParentEnabledChange(bool newEnabled)
{
    if (this->Components != nullptr)
    {
for (int32_t i = 0; i < this->Components->Count(); i++) {
(*this->Components)[i]->ParentEnabledChange(newEnabled);
}
    }
    if (this->Children != nullptr)
    {
for (int32_t i = 0; i < this->Children->Count(); i++) {
(*this->Children)[i]->ParentEnabledChange((*this->Children)[i]->get_IsHierarchyEnabled());
}
    }
}

void Entity::ParentStaticChange(bool newEnabled)
{
    if (this->Components != nullptr)
    {
for (int32_t i = 0; i < this->Components->Count(); i++) {
(*this->Components)[i]->ParentStaticChange(newEnabled);
}
    }
    if (this->Children != nullptr)
    {
for (int32_t i = 0; i < this->Children->Count(); i++) {
(*this->Children)[i]->ParentStaticChange(newEnabled);
}
    }
}

