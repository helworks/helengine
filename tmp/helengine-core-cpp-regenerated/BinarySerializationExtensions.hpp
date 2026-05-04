#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class EngineBinaryWriter;
class int4;
class float2;
class float3;
class float4;
class SceneEntityReference;
class EngineBinaryReader;

#include "runtime/native_exceptions.hpp"
#include "EngineBinaryWriter.hpp"
#include "int2.hpp"
#include "int4.hpp"
#include "float2.hpp"
#include "float3.hpp"
#include "float4.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "SceneEntityReference.hpp"
#include "float2.hpp"
#include "EngineBinaryReader.hpp"
#include "float3.hpp"
#include "float4.hpp"
#include "int2.hpp"
#include "int4.hpp"
#include "SceneEntityReference.hpp"
#include "EngineBinaryWriter.hpp"

class BinarySerializationExtensions
{
public:
    virtual ~BinarySerializationExtensions() = default;

    static ::float2 ReadFloat2(::EngineBinaryReader* reader);

    static ::float3 ReadFloat3(::EngineBinaryReader* reader);

    static ::float4 ReadFloat4(::EngineBinaryReader* reader);

    static int2* ReadInt2(::EngineBinaryReader* reader);

    static ::int4 ReadInt4(::EngineBinaryReader* reader);

    static ::SceneEntityReference* ReadSceneEntityReference(::EngineBinaryReader* reader);

    static void WriteFloat2(::EngineBinaryWriter* writer, ::float2 value);

    static void WriteFloat3(::EngineBinaryWriter* writer, ::float3 value);

    static void WriteFloat4(::EngineBinaryWriter* writer, ::float4 value);

    static void WriteInt2(::EngineBinaryWriter* writer, int2* value);

    static void WriteInt4(::EngineBinaryWriter* writer, ::int4 value);

    static void WriteSceneEntityReference(::EngineBinaryWriter* writer, ::SceneEntityReference* reference);
};
