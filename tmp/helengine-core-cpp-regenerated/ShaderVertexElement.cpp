#ifdef DrawText
#undef DrawText
#endif
#include "ShaderVertexElement.hpp"
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

std::string ShaderVertexElement::get_Format()
{
return this->Format;
}

int32_t ShaderVertexElement::get_Index()
{
return this->Index;
}

std::string ShaderVertexElement::get_Semantic()
{
return this->Semantic;
}

ShaderVertexElement::ShaderVertexElement(std::string semantic, int32_t index, std::string format) : Format(), Index(0), Semantic()
{
    if (String::IsNullOrWhiteSpace(semantic))
    {
throw ([&]() {
auto __ctor_arg_00000170 = "Semantic must be provided.";
auto __ctor_arg_00000171 = "semantic";
return new ArgumentException(__ctor_arg_00000170, __ctor_arg_00000171);
})();
    }
    if (String::IsNullOrWhiteSpace(format))
    {
throw ([&]() {
auto __ctor_arg_00000172 = "Format must be provided.";
auto __ctor_arg_00000173 = "format";
return new ArgumentException(__ctor_arg_00000172, __ctor_arg_00000173);
})();
    }
    if (index < 0)
    {
throw ([&]() {
auto __ctor_arg_00000174 = "index";
auto __ctor_arg_00000175 = "Index cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_00000174, __ctor_arg_00000175);
})();
    }
this->Semantic = semantic;
this->Index = index;
this->Format = format;
}

