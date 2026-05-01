#ifdef DrawText
#undef DrawText
#endif
#include "ShaderVariant.hpp"
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

List<std::string>* ShaderVariant::get_Defines()
{
return new List<std::string>(this->defines);}

std::string ShaderVariant::get_Name()
{
return this->Name;
}

ShaderVariant::ShaderVariant(std::string name, Array<std::string>* defines) : Name(), defines()
{
    if (String::IsNullOrWhiteSpace(name))
    {
throw ([&]() {
auto __ctor_arg_e8a3ed76 = "Variant name must be provided.";
auto __ctor_arg_1c81841a = "name";
return new ArgumentException(__ctor_arg_e8a3ed76, __ctor_arg_1c81841a);
})();
    }
    if (defines == nullptr)
    {
throw new ArgumentNullException("defines");
    }
this->Name = name;
this->defines = defines;
}

