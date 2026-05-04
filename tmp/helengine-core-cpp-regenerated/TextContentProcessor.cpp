#ifdef DrawText
#undef DrawText
#endif
#include "TextContentProcessor.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/io/stream-reader.hpp"
#include "system/text/encoding.hpp"
#include "TextContent.hpp"
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
#include "system/binary_primitives.hpp"
#include "system/bit_converter.hpp"
#include "system/diagnostics/debug.hpp"
#include "system/diagnostics/stopwatch.hpp"
#include "system/guid.hpp"
#include "system/io/directory.hpp"
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

Type* TextContentProcessor::get_OutputType()
{
return he_cpp_type_of<TextContent>("TextContent");
}

::TextContent* TextContentProcessor::Read(::Stream* stream)
{
    if (stream == nullptr)
    {
throw new ArgumentNullException("stream");
    }
{
StreamReader *reader = new StreamReader(stream, Encoding::UTF8, true, 1024, true);
return ([&]() {
auto __object_000001AB = new ::TextContent();
__object_000001AB->set_Text(reader->ReadToEnd());
return __object_000001AB;
})();}
}

void* TextContentProcessor::ReadObject(::Stream* stream)
{
return this->Read(stream);}

