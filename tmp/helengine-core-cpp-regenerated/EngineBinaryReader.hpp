#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_disposable.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/io/stream.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_string.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "runtime/native_exceptions.hpp"
#include "EngineBinaryEndianness.hpp"
#include "system/io/stream.hpp"
#include "runtime/array.hpp"
#include "system/func.hpp"
#include "runtime/array.hpp"
#include "runtime/native_span.hpp"

class EngineBinaryReader : public IDisposable
{
public:
    ::EngineBinaryEndianness Endianness;

    ::EngineBinaryEndianness get_Endianness();

    static ::EngineBinaryReader* Create(::Stream* stream, ::EngineBinaryEndianness endianness, bool leaveOpen);

    void Dispose();

    template <typename T>
    Array<T>* ReadArray(Func<::EngineBinaryReader*, T>* readElement);

    uint8_t ReadByte();

    Array<uint8_t>* ReadByteArray();

    virtual int32_t ReadInt32() = 0;

    virtual int64_t ReadInt64() = 0;

    float ReadSingle();

    std::string ReadString();

    virtual uint16_t ReadUInt16() = 0;

    virtual uint32_t ReadUInt32() = 0;
protected:
    EngineBinaryReader(::Stream* stream, bool leaveOpen);

    Array<uint8_t>* ReadBytes(int32_t length);

    void ReadRequiredBytes(Span<uint8_t> buffer);
private:
    ::Stream* BaseStream;

    bool LeaveOpen;
};
