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

class BinaryWriterLE : public EngineBinaryWriter
{
public:
    virtual ~BinaryWriterLE() = default;

    ::EngineBinaryEndianness get_Endianness();

    BinaryWriterLE(::Stream* stream, bool leaveOpen);

    void WriteInt32(int32_t value);

    void WriteInt64(int64_t value);

    void WriteUInt16(uint16_t value);

    void WriteUInt32(uint32_t value);
};
