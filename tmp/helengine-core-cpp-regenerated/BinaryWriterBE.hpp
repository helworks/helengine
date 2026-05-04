#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class EngineBinaryWriter;

#include "EngineBinaryWriter.hpp"
#include "runtime/native_disposable.hpp"
#include "runtime/native_span.hpp"
#include "system/binary_primitives.hpp"
#include "system/binary_primitives.hpp"
#include "system/io/stream.hpp"
#include "EngineBinaryEndianness.hpp"
#include "system/io/stream.hpp"

class BinaryWriterBE : public EngineBinaryWriter
{
public:
    virtual ~BinaryWriterBE() = default;

    ::EngineBinaryEndianness get_Endianness();

    BinaryWriterBE(::Stream* stream, bool leaveOpen);

    void WriteInt32(int32_t value);

    void WriteInt64(int64_t value);

    void WriteUInt16(uint16_t value);

    void WriteUInt32(uint32_t value);
};
