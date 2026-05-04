#ifdef DrawText
#undef DrawText
#endif
#include "MaterialLayoutBinding.hpp"
#include "runtime/array.hpp"
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
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/app_context.hpp"
#include "system/bit_converter.hpp"
#include "system/diagnostics/debug.hpp"
#include "system/guid.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"

std::string MaterialLayoutBinding::get_Name()
{
return this->Name;
}

::ShaderResourceType MaterialLayoutBinding::get_ResourceType()
{
return this->ResourceType;
}

int32_t MaterialLayoutBinding::get_Set()
{
return this->Set;
}

int32_t MaterialLayoutBinding::get_Size()
{
return this->Size;
}

int32_t MaterialLayoutBinding::get_Slot()
{
return this->Slot;
}

MaterialLayoutBinding::MaterialLayoutBinding(std::string name, ::ShaderResourceType resourceType, int32_t set, int32_t slot, int32_t size) : Name(), ResourceType(), Set(0), Size(0), Slot(0)
{
    if (String::IsNullOrWhiteSpace(name))
    {
throw ([&]() {
auto __ctor_arg_0000009F = "Binding name must be provided.";
auto __ctor_arg_000000A0 = "name";
return new ArgumentException(__ctor_arg_0000009F, __ctor_arg_000000A0);
})();
    }
    if (set < 0)
    {
throw ([&]() {
auto __ctor_arg_000000A1 = "set";
auto __ctor_arg_000000A2 = "Binding set cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_000000A1, __ctor_arg_000000A2);
})();
    }
    if (slot < 0)
    {
throw ([&]() {
auto __ctor_arg_000000A3 = "slot";
auto __ctor_arg_000000A4 = "Binding slot cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_000000A3, __ctor_arg_000000A4);
})();
    }
    if (size < 0)
    {
throw ([&]() {
auto __ctor_arg_000000A5 = "size";
auto __ctor_arg_000000A6 = "Binding size cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_000000A5, __ctor_arg_000000A6);
})();
    }
this->Name = name;
this->ResourceType = resourceType;
this->Set = set;
this->Slot = slot;
this->Size = size;
}

