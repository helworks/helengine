#ifdef DrawText
#undef DrawText
#endif
#include "CoreInitializationOptions.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
#include "runtime/native_cast.hpp"
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
#include "system/app_context.hpp"
#include "system/bit_converter.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream.hpp"
#include "system/math.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"

CoreInitializationOptions::CoreInitializationOptions() : ContentRootPath(AppContext::BaseDirectory), RenderList2DInitialCapacity(64), RenderList3DInitialCapacity(64), RenderOrderLayers3D(4), UpdateListInitialCapacity(64), UpdateOrderLayers(4)
{
}

std::string CoreInitializationOptions::get_ContentRootPath()
{
return this->ContentRootPath;
}

void CoreInitializationOptions::set_ContentRootPath(std::string value)
{
this->ContentRootPath = value;
}

int32_t CoreInitializationOptions::get_RenderList2DInitialCapacity()
{
return this->RenderList2DInitialCapacity;
}

void CoreInitializationOptions::set_RenderList2DInitialCapacity(int32_t value)
{
this->RenderList2DInitialCapacity = value;
}

int32_t CoreInitializationOptions::get_RenderList3DInitialCapacity()
{
return this->RenderList3DInitialCapacity;
}

void CoreInitializationOptions::set_RenderList3DInitialCapacity(int32_t value)
{
this->RenderList3DInitialCapacity = value;
}

uint8_t CoreInitializationOptions::get_RenderOrderLayers3D()
{
return this->RenderOrderLayers3D;
}

void CoreInitializationOptions::set_RenderOrderLayers3D(uint8_t value)
{
this->RenderOrderLayers3D = value;
}

int32_t CoreInitializationOptions::get_UpdateListInitialCapacity()
{
return this->UpdateListInitialCapacity;
}

void CoreInitializationOptions::set_UpdateListInitialCapacity(int32_t value)
{
this->UpdateListInitialCapacity = value;
}

uint8_t CoreInitializationOptions::get_UpdateOrderLayers()
{
return this->UpdateOrderLayers;
}

void CoreInitializationOptions::set_UpdateOrderLayers(uint8_t value)
{
this->UpdateOrderLayers = value;
}

void CoreInitializationOptions::Normalize()
{
    if (String::IsNullOrWhiteSpace(this->ContentRootPath))
    {
throw new InvalidOperationException("ContentRootPath must be provided.");
    }
    if (this->UpdateOrderLayers < 1)
    {
throw new InvalidOperationException("UpdateOrderLayers must be at least 1.");
    }
    if (this->RenderOrderLayers3D < 1)
    {
throw new InvalidOperationException("RenderOrderLayers3D must be at least 1.");
    }
    if (this->UpdateListInitialCapacity < 0)
    {
throw new InvalidOperationException("UpdateListInitialCapacity cannot be negative.");
    }
    if (this->RenderList2DInitialCapacity < 0)
    {
throw new InvalidOperationException("RenderList2DInitialCapacity cannot be negative.");
    }
    if (this->RenderList3DInitialCapacity < 0)
    {
throw new InvalidOperationException("RenderList3DInitialCapacity cannot be negative.");
    }
}

