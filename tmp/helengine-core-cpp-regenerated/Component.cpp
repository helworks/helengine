#ifdef DrawText
#undef DrawText
#endif
#include "Component.hpp"
#include "runtime/array.hpp"
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
#include "system/bit_converter.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/stream.hpp"
#include "system/text/encoding.hpp"

Component::Component() : Parent()
{
}

::Entity* Component::get_Parent()
{
return this->Parent;
}

void Component::set_Parent(::Entity* value)
{
this->Parent = value;
}

void Component::ComponentAdded(::Entity* entity)
{
this->set_Parent(entity);
}

void Component::ComponentRemoved(::Entity* entity)
{
this->set_Parent(nullptr);
}

void Component::ParentEnabledChange(bool newEnabled)
{
}

void Component::ParentStaticChange(bool newEnabled)
{
}

