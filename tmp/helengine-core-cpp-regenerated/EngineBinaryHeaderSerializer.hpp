#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class BinaryWriterLE;
class BinaryReaderLE;
class EngineBinaryHeader;

#include "runtime/native_exceptions.hpp"
#include "BinaryWriterLE.hpp"
#include "BinaryWriterLE.hpp"
#include "BinaryReaderLE.hpp"
#include "runtime/native_exceptions.hpp"
#include "EngineBinaryEndianness.hpp"
#include "BinaryReaderLE.hpp"
#include "EngineBinaryHeader.hpp"
#include "EngineBinaryHeader.hpp"
#include "system/io/stream.hpp"

class EngineBinaryHeaderSerializer
{
public:
    virtual ~EngineBinaryHeaderSerializer() = default;

    static ::EngineBinaryHeader* Read(::Stream* stream);

    static void Write(::Stream* stream, ::EngineBinaryHeader* header);
private:
    static void ValidateEndianness(::EngineBinaryEndianness endianness);
};
