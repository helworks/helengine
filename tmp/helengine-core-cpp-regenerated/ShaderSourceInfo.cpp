#ifdef DrawText
#undef DrawText
#endif
#include "ShaderSourceInfo.hpp"
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
#include "system/security/cryptography/sha256.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "system/text/string-builder.hpp"

std::string ShaderSourceInfo::get_Path()
{
return this->Path;
}

std::string ShaderSourceInfo::get_Source()
{
return this->Source;
}

ShaderSourceInfo::ShaderSourceInfo(std::string path, std::string source) : Path(), Source()
{
    if (String::IsNullOrWhiteSpace(path))
    {
throw ([&]() {
auto __ctor_arg_00000165 = "Shader path must be provided.";
auto __ctor_arg_00000166 = "path";
return new ArgumentException(__ctor_arg_00000165, __ctor_arg_00000166);
})();
    }
    if (String::IsNullOrWhiteSpace(source))
    {
throw ([&]() {
auto __ctor_arg_00000167 = "Shader source must be provided.";
auto __ctor_arg_00000168 = "source";
return new ArgumentException(__ctor_arg_00000167, __ctor_arg_00000168);
})();
    }
this->Path = path;
this->Source = source;
}

