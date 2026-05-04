#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class float2;
class float3;
class float4;
class int4;
class SceneEntityReference;

#include "runtime/native_disposable.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/io/stream.hpp"
#include "runtime/array.hpp"
#include "system/text/encoding.hpp"
#include "system/text/encoding.hpp"
#include "runtime/native_string.hpp"
#include "EngineBinaryEndianness.hpp"
#include "system/io/stream.hpp"
#include "runtime/array.hpp"
#include "system/action.hpp"
#include "runtime/array.hpp"
#include "float2.hpp"
#include "float3.hpp"
#include "float4.hpp"
#include "int2.hpp"
#include "int4.hpp"
#include "SceneEntityReference.hpp"

class EngineBinaryWriter : public IDisposable
{
public:
    virtual ~EngineBinaryWriter() = default;

    ::EngineBinaryEndianness Endianness;

    ::EngineBinaryEndianness get_Endianness();

    static ::EngineBinaryWriter* Create(::Stream* stream, ::EngineBinaryEndianness endianness, bool leaveOpen);

    void Dispose();

    template <typename T>
    void WriteArray(Array<T>* values, Action<::EngineBinaryWriter*, T>* writeElement);

    void WriteByte(uint8_t value);

    void WriteByteArray(Array<uint8_t>* value);

    void WriteFloat2(::float2 value);

    void WriteFloat3(::float3 value);

    void WriteFloat4(::float4 value);

    void WriteInt2(int2* value);

    virtual void WriteInt32(int32_t value) = 0;

    void WriteInt4(::int4 value);

    virtual void WriteInt64(int64_t value) = 0;

    void WriteSceneEntityReference(::SceneEntityReference* reference);

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
