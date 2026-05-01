#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_disposable.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/io/stream.hpp"
#include "runtime/array.hpp"
#include "system/text/encoding.hpp"
#include "system/text/encoding.hpp"
#include "EngineBinaryEndianness.hpp"
#include "system/io/stream.hpp"
#include "runtime/array.hpp"
#include "system/action.hpp"
#include "runtime/array.hpp"
#include "runtime/native_string.hpp"

class EngineBinaryWriter : public IDisposable
{
public:
    ::EngineBinaryEndianness Endianness;

    ::EngineBinaryEndianness get_Endianness();

    static ::EngineBinaryWriter* Create(::Stream* stream, ::EngineBinaryEndianness endianness, bool leaveOpen);

    void Dispose();

    template <typename T>
    void WriteArray(Array<T>* values, Action<::EngineBinaryWriter*, T>* writeElement);

    void WriteByte(uint8_t value);

    void WriteByteArray(Array<uint8_t>* value);

    virtual void WriteInt32(int32_t value) = 0;

    virtual void WriteInt64(int64_t value) = 0;

    void WriteSingle(float value);

    void WriteString(std::string value);

    virtual void WriteUInt16(uint16_t value) = 0;

    virtual void WriteUInt32(uint32_t value) = 0;
protected:
    ::Stream* BaseStream;

    EngineBinaryWriter(::Stream* stream, bool leaveOpen);
private:
    bool LeaveOpen;
};
