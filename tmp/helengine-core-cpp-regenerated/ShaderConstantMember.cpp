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
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
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
auto __ctor_arg_cc3d75f1 = "Member name must be provided.";
auto __ctor_arg_41b2d644 = "name";
return new ArgumentException(__ctor_arg_cc3d75f1, __ctor_arg_41b2d644);
})();
    }
    if (String::IsNullOrWhiteSpace(type))
    {
throw ([&]() {
auto __ctor_arg_77ec2a85 = "Member type must be provided.";
auto __ctor_arg_84432fb6 = "type";
return new ArgumentException(__ctor_arg_77ec2a85, __ctor_arg_84432fb6);
})();
    }
    if (offset < 0)
    {
throw ([&]() {
auto __ctor_arg_a54c29d2 = "offset";
auto __ctor_arg_1b50e9c4 = "Offset cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_a54c29d2, __ctor_arg_1b50e9c4);
})();
    }
    if (size < 0)
    {
throw ([&]() {
auto __ctor_arg_41a94475 = "size";
auto __ctor_arg_c7a34977 = "Size cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_41a94475, __ctor_arg_c7a34977);
})();
    }
this->Name = name;
this->Type = type;
this->Offset = offset;
this->Size = size;
}

