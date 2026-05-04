#ifdef DrawText
#undef DrawText
#endif
#include "InteractableComponent.hpp"
#include "Component.hpp"
#include "Entity.hpp"
#include "Core.hpp"
#include "ObjectManager.hpp"
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

InteractableComponent::InteractableComponent() : CursorEvent(), HoverCursor(), Size()
{
}

::PointerCursorKind InteractableComponent::get_HoverCursor()
{
return this->HoverCursor;
}

void InteractableComponent::set_HoverCursor(::PointerCursorKind value)
{
this->HoverCursor = value;
}

int2* InteractableComponent::get_Size()
{
return this->Size;
}

void InteractableComponent::set_Size(int2* value)
{
this->Size = value;
}

void InteractableComponent::ComponentAdded(::Entity* entity)
{
Component::ComponentAdded(entity);
    if (entity->get_IsHierarchyEnabled())
    {
Core::get_Instance()->get_ObjectManager()->RegisterInteractable(this);
    }
}

void InteractableComponent::OnCursor(int2* relPos, int2* delta, ::PointerInteraction state)
{
this->CursorEvent.Invoke(relPos, delta, state);
}

void InteractableComponent::ParentEnabledChange(bool newEnabled)
{
Component::ParentEnabledChange(newEnabled);
    if (newEnabled)
    {
Core::get_Instance()->get_ObjectManager()->RegisterInteractable(this);
    }
else {
Core::get_Instance()->get_ObjectManager()->RemoveInteractable(this);
}
}

::Entity* InteractableComponent::get_Parent()
{
return this->Component::get_Parent();
}

void InteractableComponent::set_Parent(::Entity* value)
{
this->Component::set_Parent(value);
}

