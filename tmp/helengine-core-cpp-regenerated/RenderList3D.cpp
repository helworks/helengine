#ifdef DrawText
#undef DrawText
#endif
#include "RenderList3D.hpp"
#include "runtime/native_list.hpp"
#include "Logger.hpp"
#include "runtime/native_exceptions.hpp"
#include "IDrawable3D.hpp"
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

int32_t RenderList3D::get_Count()
{
return this->items->Count();}

void RenderList3D::Add(::IDrawable3D* drawable)
{
const int32_t insertIndex = this->FindInsertIndex(drawable != nullptr ? drawable->get_RenderOrder3D() : static_cast<uint8_t>(0));
this->items->Insert(insertIndex, drawable);
}

void RenderList3D::Clear()
{
this->items->Clear();
}

void RenderList3D::EnsureCapacity(int32_t desiredCount)
{
this->EnsureCapacity(desiredCount, false);
}

void RenderList3D::EnsureCapacity(int32_t desiredCount, bool warnOnExpand)
{
    if (desiredCount <= this->items->Capacity())
    {
return;    }
const int32_t oldCap = this->items->Capacity();
this->items->SetCapacity(desiredCount);
    if (warnOnExpand)
    {
Logger::WriteWarning(std::string("RenderList3D expanded from ") + std::to_string(oldCap) + std::string(" to ") + std::to_string(items->Capacity()) + std::string("."));
    }
}

bool RenderList3D::Remove(::IDrawable3D* drawable)
{
const int32_t index = this->FindIndexByReference(drawable);
    if (index < 0)
    {
return false;    }
this->items->RemoveAt(index);
return true;}

RenderList3D::RenderList3D(int32_t initialCapacity) : items()
{
    if (initialCapacity < 0)
    {
throw new ArgumentOutOfRangeException("initialCapacity");
    }
this->items = new List<::IDrawable3D*>(initialCapacity);
}

void RenderList3D::VisitOrdered(::IRenderVisitor3D* visitor)
{
    if (visitor == nullptr)
    {
throw new ArgumentNullException("visitor");
    }
for (int32_t i = 0; i < this->items->Count(); i++) {
visitor->Visit((*this->items)[i]);
}
}

int32_t RenderList3D::FindIndexByReference(::IDrawable3D* drawable)
{
for (int32_t i = 0; i < this->items->Count(); i++) {
    if (((*this->items)[i] == drawable))
    {
return i;    }
}
return -1;}

int32_t RenderList3D::FindInsertIndex(uint8_t renderOrder)
{
for (int32_t i = 0; i < this->items->Count(); i++) {
::IDrawable3D *current = (*this->items)[i];
const uint8_t currentOrder = current != nullptr ? current->get_RenderOrder3D() : static_cast<uint8_t>(0);
    if (renderOrder < currentOrder)
    {
return i;    }
}
return this->items->Count();}

