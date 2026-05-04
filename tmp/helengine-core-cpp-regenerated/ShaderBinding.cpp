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
#include "system/diagnostics/stopwatch.hpp"
#include "system/guid.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream-reader.hpp"
#include "system/io/stream.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "system/text/string-builder.hpp"

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
auto __ctor_arg_000000DB = "Binding name must be provided.";
auto __ctor_arg_000000DC = "name";
return new ArgumentException(__ctor_arg_000000DB, __ctor_arg_000000DC);
})();
    }
    if (set < 0)
    {
throw ([&]() {
auto __ctor_arg_000000DD = "set";
auto __ctor_arg_000000DE = "Set cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_000000DD, __ctor_arg_000000DE);
})();
    }
    if (slot < 0)
    {
throw ([&]() {
auto __ctor_arg_000000DF = "slot";
auto __ctor_arg_000000E0 = "Slot cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_000000DF, __ctor_arg_000000E0);
})();
    }
    if (size < 0)
    {
throw ([&]() {
auto __ctor_arg_000000E1 = "size";
auto __ctor_arg_000000E2 = "Size cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_000000E1, __ctor_arg_000000E2);
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

