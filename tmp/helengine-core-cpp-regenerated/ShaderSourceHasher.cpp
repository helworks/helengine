#ifdef DrawText
#undef DrawText
#endif
#include "ShaderSourceHasher.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/array.hpp"
#include "system/text/encoding.hpp"
#include "SHA256.hpp"
#include "runtime/native_string.hpp"
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

std::string ShaderSourceHasher::ComputeHash(std::string source)
{
    if (String::IsNullOrEmpty(source))
    {
throw new ArgumentNullException("source");
    }
Array<uint8_t> *data = Encoding::GetBytes(Encoding::UTF8, source);
Array<uint8_t> *hash = SHA256::HashData(data);
return this->ConvertToHex(hash);}

std::string ShaderSourceHasher::ConvertToHex(Array<uint8_t>* data)
{
Array<char> *buffer = new Array<char>(data->Length * 2);
int32_t outputIndex = 0;
for (int32_t i = 0; i < data->Length; i++) {
const uint8_t value = (*data)[i];
const int32_t high = value >> 4;
const int32_t low = value & 0xF;
(*buffer)[outputIndex++] = this->ToHexChar(high);
(*buffer)[outputIndex++] = this->ToHexChar(low);
}
return std::string(buffer->Data, static_cast<size_t>(buffer->Length));}

char ShaderSourceHasher::ToHexChar(int32_t value)
{
    if (value < 10)
    {
return static_cast<char>(('0' + value));    }
return static_cast<char>(('a' + (value - 10)));}

