#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class int4;
class float2;
class float3;
class float4;
class SceneEntityReference;

#include "runtime/native_disposable.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/io/stream.hpp"
#include "runtime/native_exceptions.hpp"
#include "int2.hpp"
#include "int4.hpp"
#include "float2.hpp"
#include "float3.hpp"
#include "float4.hpp"
#include "runtime/native_string.hpp"
#include "runtime/array.hpp"
#include "SceneEntityReference.hpp"
#include "runtime/array.hpp"
#include "runtime/native_exceptions.hpp"
#include "EngineBinaryEndianness.hpp"
#include "system/io/stream.hpp"
#include "runtime/array.hpp"
#include "system/func.hpp"
#include "runtime/array.hpp"
#include "float2.hpp"
#include "float3.hpp"
#include "float4.hpp"
#include "int2.hpp"
#include "int4.hpp"
#include "SceneEntityReference.hpp"
#include "runtime/native_span.hpp"

class EngineBinaryReader : public IDisposable
{
public:
    virtual ~EngineBinaryReader() = default;

    ::EngineBinaryEndianness Endianness;

    ::EngineBinaryEndianness get_Endianness();

    static ::EngineBinaryReader* Create(::Stream* stream, ::EngineBinaryEndianness endianness, bool leaveOpen);

    void Dispose();

    template <typename T>
    Array<T>* ReadArray(Func<::EngineBinaryReader*, T>* readElement);

    uint8_t ReadByte();

    Array<uint8_t>* ReadByteArray();

    ::float2 ReadFloat2();

    ::float3 ReadFloat3();

    ::float4 ReadFloat4();

    int2* ReadInt2();

    virtual int32_t ReadInt32() = 0;

    ::int4 ReadInt4();

    virtual int64_t ReadInt64() = 0;

    ::SceneEntityReference* ReadSceneEntityReference();

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
