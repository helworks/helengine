#ifdef DrawText
#undef DrawText
#endif
#include "BinaryWriterLE.hpp"
#include "runtime/native_span.hpp"
#include "system/binary_primitives.hpp"
#include "system/io/stream.hpp"
#include "EngineBinaryEndianness.hpp"
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

::EngineBinaryEndianness BinaryWriterLE::get_Endianness()
{
return EngineBinaryEndianness::LittleEndian;
}

BinaryWriterLE::BinaryWriterLE(::Stream* stream, bool leaveOpen) : EngineBinaryWriter(stream, leaveOpen)
{
}

void BinaryWriterLE::WriteInt32(int32_t value)
{
uint8_t buffer[sizeof(int32_t)];
BinaryPrimitives::WriteInt32LittleEndian(buffer, value);
BaseStream->Write(buffer);
}

void BinaryWriterLE::WriteInt64(int64_t value)
{
uint8_t buffer[sizeof(int64_t)];
BinaryPrimitives::WriteInt64LittleEndian(buffer, value);
BaseStream->Write(buffer);
}

void BinaryWriterLE::WriteUInt16(uint16_t value)
{
uint8_t buffer[sizeof(uint16_t)];
BinaryPrimitives::WriteUInt16LittleEndian(buffer, value);
BaseStream->Write(buffer);
}

void BinaryWriterLE::WriteUInt32(uint32_t value)
{
uint8_t buffer[sizeof(uint32_t)];
BinaryPrimitives::WriteUInt32LittleEndian(buffer, value);
BaseStream->Write(buffer);
}

