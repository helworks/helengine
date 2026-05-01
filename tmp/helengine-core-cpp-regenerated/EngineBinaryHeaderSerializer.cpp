#ifdef DrawText
#undef DrawText
#endif
#include "EngineBinaryHeaderSerializer.hpp"
#include "runtime/native_exceptions.hpp"
#include "BinaryWriterLE.hpp"
#include "BinaryReaderLE.hpp"
#include "EngineBinaryEndianness.hpp"
#include "EngineBinaryHeader.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
#include "runtime/native_cast.hpp"
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
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream.hpp"
#include "system/math.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"

::EngineBinaryHeader* EngineBinaryHeaderSerializer::Read(::Stream* stream)
{
    if (stream == nullptr)
    {
throw new ArgumentNullException("stream");
    }
{
::BinaryReaderLE *reader = new ::BinaryReaderLE(stream, true);
    if (reader->ReadByte() != static_cast<uint8_t>('H') || reader->ReadByte() != static_cast<uint8_t>('E') || reader->ReadByte() != static_cast<uint8_t>('L') || reader->ReadByte() != static_cast<uint8_t>('E'))
    {
throw new InvalidOperationException("The binary payload does not start with the HELE header.");
    }
::EngineBinaryEndianness endianness = static_cast<EngineBinaryEndianness>(reader->ReadByte());
ValidateEndianness(endianness);
const uint8_t version = reader->ReadByte();
const uint16_t formatId = reader->ReadUInt16();
const uint16_t recordKind = reader->ReadUInt16();
const uint16_t valueKind = reader->ReadUInt16();
return new ::EngineBinaryHeader(endianness, version, formatId, recordKind, valueKind);}
}

void EngineBinaryHeaderSerializer::Write(::Stream* stream, ::EngineBinaryHeader* header)
{
    if (stream == nullptr)
    {
throw new ArgumentNullException("stream");
    }
else     if (header == nullptr)
    {
throw new ArgumentNullException("header");
    }
ValidateEndianness(header->get_Endianness());
{
::BinaryWriterLE *writer = new ::BinaryWriterLE(stream, true);
writer->WriteByte(static_cast<uint8_t>('H'));
writer->WriteByte(static_cast<uint8_t>('E'));
writer->WriteByte(static_cast<uint8_t>('L'));
writer->WriteByte(static_cast<uint8_t>('E'));
writer->WriteByte(static_cast<uint8_t>(header->get_Endianness()));
writer->WriteByte(header->get_Version());
writer->WriteUInt16(header->get_FormatId());
writer->WriteUInt16(header->get_RecordKind());
writer->WriteUInt16(header->get_ValueKind());
}
}

void EngineBinaryHeaderSerializer::ValidateEndianness(::EngineBinaryEndianness endianness)
{
    if (endianness != EngineBinaryEndianness::LittleEndian && endianness != EngineBinaryEndianness::BigEndian)
    {
throw new InvalidOperationException(std::string("Unsupported binary payload endianness '") + std::to_string(static_cast<uint8_t>(endianness)) + std::string("'."));
    }
}

