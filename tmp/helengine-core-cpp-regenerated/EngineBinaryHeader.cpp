#ifdef DrawText
#undef DrawText
#endif
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

::EngineBinaryEndianness EngineBinaryHeader::get_Endianness()
{
return this->Endianness;
}

uint16_t EngineBinaryHeader::get_FormatId()
{
return this->FormatId;
}

uint16_t EngineBinaryHeader::get_RecordKind()
{
return this->RecordKind;
}

uint16_t EngineBinaryHeader::get_ValueKind()
{
return this->ValueKind;
}

uint8_t EngineBinaryHeader::get_Version()
{
return this->Version;
}

EngineBinaryHeader::EngineBinaryHeader(::EngineBinaryEndianness endianness, uint8_t version, uint16_t formatId, uint16_t recordKind, uint16_t valueKind) : Endianness(), FormatId(), RecordKind(), ValueKind(), Version()
{
this->Endianness = endianness;
this->Version = version;
this->FormatId = formatId;
this->RecordKind = recordKind;
this->ValueKind = valueKind;
}

