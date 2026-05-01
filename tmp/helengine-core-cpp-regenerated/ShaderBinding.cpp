#ifdef DrawText
#undef DrawText
#endif
#include "ShaderBinding.hpp"
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

List<::ShaderConstantMember*>* ShaderBinding::get_Members()
{
return new List<ShaderConstantMember*>(this->members);}

std::string ShaderBinding::get_Name()
{
return this->Name;
}

int32_t ShaderBinding::get_Set()
{
return this->Set;
}

int32_t ShaderBinding::get_Size()
{
return this->Size;
}

int32_t ShaderBinding::get_Slot()
{
return this->Slot;
}

::ShaderResourceType ShaderBinding::get_Type()
{
return this->Type;
}

ShaderBinding::ShaderBinding(std::string name, ::ShaderResourceType type, int32_t set, int32_t slot, int32_t size, Array<::ShaderConstantMember*>* members) : Name(), Set(0), Size(0), Slot(0), Type(), members()
{
    if (String::IsNullOrWhiteSpace(name))
    {
throw ([&]() {
auto __ctor_arg_e7555d3a = "Binding name must be provided.";
auto __ctor_arg_3db72f4d = "name";
return new ArgumentException(__ctor_arg_e7555d3a, __ctor_arg_3db72f4d);
})();
    }
    if (set < 0)
    {
throw ([&]() {
auto __ctor_arg_3bbe983a = "set";
auto __ctor_arg_1cb5ce70 = "Set cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_3bbe983a, __ctor_arg_1cb5ce70);
})();
    }
    if (slot < 0)
    {
throw ([&]() {
auto __ctor_arg_b595234a = "slot";
auto __ctor_arg_4e890d6f = "Slot cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_b595234a, __ctor_arg_4e890d6f);
})();
    }
    if (size < 0)
    {
throw ([&]() {
auto __ctor_arg_af599f8e = "size";
auto __ctor_arg_f1d0c538 = "Size cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_af599f8e, __ctor_arg_f1d0c538);
})();
    }
    if (members == nullptr)
    {
throw new ArgumentNullException("members");
    }
this->Name = name;
this->Type = type;
this->Set = set;
this->Slot = slot;
this->Size = size;
this->members = members;
}

