#ifdef DrawText
#undef DrawText
#endif
#include "ShaderConstantMember.hpp"
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
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "system/text/string-builder.hpp"

std::string ShaderConstantMember::get_Name()
{
return this->Name;
}

int32_t ShaderConstantMember::get_Offset()
{
return this->Offset;
}

int32_t ShaderConstantMember::get_Size()
{
return this->Size;
}

std::string ShaderConstantMember::get_Type()
{
return this->Type;
}

ShaderConstantMember::ShaderConstantMember(std::string name, std::string type, int32_t offset, int32_t size) : Name(), Offset(0), Size(0), Type()
{
    if (String::IsNullOrWhiteSpace(name))
    {
throw ([&]() {
auto __ctor_arg_00000121 = "Member name must be provided.";
auto __ctor_arg_00000122 = "name";
return new ArgumentException(__ctor_arg_00000121, __ctor_arg_00000122);
})();
    }
    if (String::IsNullOrWhiteSpace(type))
    {
throw ([&]() {
auto __ctor_arg_00000123 = "Member type must be provided.";
auto __ctor_arg_00000124 = "type";
return new ArgumentException(__ctor_arg_00000123, __ctor_arg_00000124);
})();
    }
    if (offset < 0)
    {
throw ([&]() {
auto __ctor_arg_00000125 = "offset";
auto __ctor_arg_00000126 = "Offset cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_00000125, __ctor_arg_00000126);
})();
    }
    if (size < 0)
    {
throw ([&]() {
auto __ctor_arg_00000127 = "size";
auto __ctor_arg_00000128 = "Size cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_00000127, __ctor_arg_00000128);
})();
    }
this->Name = name;
this->Type = type;
this->Offset = offset;
this->Size = size;
}

