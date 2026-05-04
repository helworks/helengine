#ifdef DrawText
#undef DrawText
#endif
#include "BinarySerializationExtensions.hpp"
#include "runtime/native_exceptions.hpp"
#include "EngineBinaryWriter.hpp"
#include "int2.hpp"
#include "int4.hpp"
#include "float2.hpp"
#include "float3.hpp"
#include "float4.hpp"
#include "runtime/native_string.hpp"
#include "SceneEntityReference.hpp"
#include "runtime/array.hpp"
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
#include "system/bit_converter.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/stream.hpp"
#include "system/text/encoding.hpp"

::float2 BinarySerializationExtensions::ReadFloat2(::EngineBinaryReader* reader)
{
    if (reader == nullptr)
    {
throw new ArgumentNullException("reader");
    }
return ([&]() {
auto __ctor_arg_00000012 = reader->ReadSingle();
auto __ctor_arg_00000013 = reader->ReadSingle();
return ::float2(__ctor_arg_00000012, __ctor_arg_00000013);
})();}

::float3 BinarySerializationExtensions::ReadFloat3(::EngineBinaryReader* reader)
{
    if (reader == nullptr)
    {
throw new ArgumentNullException("reader");
    }
return ([&]() {
auto __ctor_arg_00000014 = reader->ReadSingle();
auto __ctor_arg_00000015 = reader->ReadSingle();
auto __ctor_arg_00000016 = reader->ReadSingle();
return ::float3(__ctor_arg_00000014, __ctor_arg_00000015, __ctor_arg_00000016);
})();}

::float4 BinarySerializationExtensions::ReadFloat4(::EngineBinaryReader* reader)
{
    if (reader == nullptr)
    {
throw new ArgumentNullException("reader");
    }
return ([&]() {
auto __ctor_arg_00000017 = reader->ReadSingle();
auto __ctor_arg_00000018 = reader->ReadSingle();
auto __ctor_arg_00000019 = reader->ReadSingle();
auto __ctor_arg_0000001A = reader->ReadSingle();
return ::float4(__ctor_arg_00000017, __ctor_arg_00000018, __ctor_arg_00000019, __ctor_arg_0000001A);
})();}

int2* BinarySerializationExtensions::ReadInt2(::EngineBinaryReader* reader)
{
    if (reader == nullptr)
    {
throw new ArgumentNullException("reader");
    }
return ([&]() {
auto __ctor_arg_0000001B = reader->ReadInt32();
auto __ctor_arg_0000001C = reader->ReadInt32();
return new int2(__ctor_arg_0000001B, __ctor_arg_0000001C);
})();}

::int4 BinarySerializationExtensions::ReadInt4(::EngineBinaryReader* reader)
{
    if (reader == nullptr)
    {
throw new ArgumentNullException("reader");
    }
return ([&]() {
auto __ctor_arg_0000001D = reader->ReadInt32();
auto __ctor_arg_0000001E = reader->ReadInt32();
auto __ctor_arg_0000001F = reader->ReadInt32();
auto __ctor_arg_00000020 = reader->ReadInt32();
return ::int4(__ctor_arg_0000001D, __ctor_arg_0000001E, __ctor_arg_0000001F, __ctor_arg_00000020);
})();}

::SceneEntityReference* BinarySerializationExtensions::ReadSceneEntityReference(::EngineBinaryReader* reader)
{
    if (reader == nullptr)
    {
throw new ArgumentNullException("reader");
    }
    if (reader->ReadByte() == 0)
    {
return nullptr;    }
return ([&]() {
auto __object_00000021 = new ::SceneEntityReference();
__object_00000021->set_EntityId(reader->ReadString());
return __object_00000021;
})();}

void BinarySerializationExtensions::WriteFloat2(::EngineBinaryWriter* writer, ::float2 value)
{
    if (writer == nullptr)
    {
throw new ArgumentNullException("writer");
    }
writer->WriteSingle(value.X);
writer->WriteSingle(value.Y);
}

void BinarySerializationExtensions::WriteFloat3(::EngineBinaryWriter* writer, ::float3 value)
{
    if (writer == nullptr)
    {
throw new ArgumentNullException("writer");
    }
writer->WriteSingle(value.X);
writer->WriteSingle(value.Y);
writer->WriteSingle(value.Z);
}

void BinarySerializationExtensions::WriteFloat4(::EngineBinaryWriter* writer, ::float4 value)
{
    if (writer == nullptr)
    {
throw new ArgumentNullException("writer");
    }
writer->WriteSingle(value.X);
writer->WriteSingle(value.Y);
writer->WriteSingle(value.Z);
writer->WriteSingle(value.W);
}

void BinarySerializationExtensions::WriteInt2(::EngineBinaryWriter* writer, int2* value)
{
    if (writer == nullptr)
    {
throw new ArgumentNullException("writer");
    }
writer->WriteInt32(value->X);
writer->WriteInt32(value->Y);
}

void BinarySerializationExtensions::WriteInt4(::EngineBinaryWriter* writer, ::int4 value)
{
    if (writer == nullptr)
    {
throw new ArgumentNullException("writer");
    }
writer->WriteInt32(value.X);
writer->WriteInt32(value.Y);
writer->WriteInt32(value.Z);
writer->WriteInt32(value.W);
}

void BinarySerializationExtensions::WriteSceneEntityReference(::EngineBinaryWriter* writer, ::SceneEntityReference* reference)
{
    if (writer == nullptr)
    {
throw new ArgumentNullException("writer");
    }
writer->WriteByte(reference == nullptr ? static_cast<uint8_t>(0) : static_cast<uint8_t>(1));
    if (reference == nullptr)
    {
return;    }
    if (String::IsNullOrWhiteSpace(reference->get_EntityId()))
    {
throw new InvalidOperationException("Scene entity references must define an entity id.");
    }
writer->WriteString(reference->get_EntityId());
}

