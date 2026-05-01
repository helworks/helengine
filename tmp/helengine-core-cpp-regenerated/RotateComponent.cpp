#ifdef DrawText
#undef DrawText
#endif
#include "RotateComponent.hpp"
#include "UpdateComponent.hpp"
#include "float4.hpp"
#include "float3.hpp"
#include "Entity.hpp"
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
#include "system/io/directory.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream.hpp"
#include "system/io/string-reader.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/security/cryptography/sha256.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "system/text/string-builder.hpp"

void RotateComponent::Update()
{
UpdateComponent::Update();
::float4 deltaRotation;
::float3 axis = ::float3(0, 1, 0);
float4::CreateFromAxisAngle(axis, 0.07f, deltaRotation);
::float4 orientation = Parent->get_Orientation();
orientation = deltaRotation * orientation;
orientation.Normalize();
Parent->set_Orientation(orientation);
}

uint8_t RotateComponent::get_UpdateOrder()
{
return this->UpdateComponent::get_UpdateOrder();
}

void RotateComponent::set_UpdateOrder(uint8_t value)
{
this->UpdateComponent::set_UpdateOrder(value);
}

::Entity* RotateComponent::get_Parent()
{
return this->Component::get_Parent();
}

void RotateComponent::set_Parent(::Entity* value)
{
this->Component::set_Parent(value);
}

