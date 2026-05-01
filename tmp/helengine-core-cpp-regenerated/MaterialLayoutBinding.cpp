#ifdef DrawText
#undef DrawText
#endif
#include "MaterialLayoutBinding.hpp"
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
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/app_context.hpp"
#include "system/bit_converter.hpp"
#include "system/diagnostics/debug.hpp"
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
auto __ctor_arg_12c00dc1 = "Binding name must be provided.";
auto __ctor_arg_07889ecc = "name";
return new ArgumentException(__ctor_arg_12c00dc1, __ctor_arg_07889ecc);
})();
    }
    if (set < 0)
    {
throw ([&]() {
auto __ctor_arg_ad1ed5bc = "set";
auto __ctor_arg_5787e0fa = "Binding set cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_ad1ed5bc, __ctor_arg_5787e0fa);
})();
    }
    if (slot < 0)
    {
throw ([&]() {
auto __ctor_arg_11e750e1 = "slot";
auto __ctor_arg_e74cee2e = "Binding slot cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_11e750e1, __ctor_arg_e74cee2e);
})();
    }
    if (size < 0)
    {
throw ([&]() {
auto __ctor_arg_0ebb5198 = "size";
auto __ctor_arg_20394a77 = "Binding size cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_0ebb5198, __ctor_arg_20394a77);
})();
    }
this->Name = name;
this->ResourceType = resourceType;
this->Set = set;
this->Slot = slot;
this->Size = size;
}

