#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class EngineBinaryReader;

#include "EngineBinaryReader.hpp"
#include "runtime/native_disposable.hpp"
#include "runtime/native_span.hpp"
#include "EngineBinaryEndianness.hpp"
#include "system/io/stream.hpp"

class BinaryReaderLE : public EngineBinaryReader
{
public:
    ::EngineBinaryEndianness get_Endianness();

    BinaryReaderLE(::Stream* stream, bool leaveOpen);

    int32_t ReadInt32();

    int64_t ReadInt64();

    uint16_t ReadUInt16();

    uint32_t ReadUInt32();
};
