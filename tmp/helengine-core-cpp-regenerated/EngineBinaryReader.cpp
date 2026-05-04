#ifdef DrawText
#undef DrawText
#endif
#include "EngineBinaryReader.hpp"
#include "runtime/native_exceptions.hpp"
#include "EngineBinaryReader.hpp"
#include "system/io/stream.hpp"
#include "int2.hpp"
#include "int4.hpp"
#include "float2.hpp"
#include "float3.hpp"
#include "float4.hpp"
#include "runtime/native_string.hpp"
#include "runtime/array.hpp"
#include "SceneEntityReference.hpp"
#include "EngineBinaryEndianness.hpp"
#include "BinaryReaderLE.hpp"
#include "BinaryReaderBE.hpp"
#include "runtime/array.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_disposable.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_event.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_span.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/bit_converter.hpp"
#include "system/io/stream.hpp"
#include "system/text/encoding.hpp"

::EngineBinaryEndianness EngineBinaryReader::get_Endianness()
{
return this->Endianness;
}

::EngineBinaryReader* EngineBinaryReader::Create(::Stream* stream, ::EngineBinaryEndianness endianness, bool leaveOpen)
{
    if (stream == nullptr)
    {
throw new ArgumentNullException("stream");
    }
else     if (endianness == EngineBinaryEndianness::LittleEndian)
    {
return new ::BinaryReaderLE(stream, leaveOpen);    }
else     if (endianness == EngineBinaryEndianness::BigEndian)
    {
return new ::BinaryReaderBE(stream, leaveOpen);    }
throw new InvalidOperationException(std::string("Unsupported binary payload endianness '") + std::to_string(static_cast<uint8_t>(endianness)) + std::string("'."));
}

void EngineBinaryReader::Dispose()
{
    if (!this->LeaveOpen)
    {
this->BaseStream->Dispose();
    }
}

template <typename T>
Array<T>* EngineBinaryReader::ReadArray(Func<::EngineBinaryReader*, T>* readElement)
{
    if (readElement == nullptr)
    {
throw new ArgumentNullException("readElement");
    }
const int32_t length = this->ReadInt32();
    if (length == -1)
    {
return nullptr;    }
else     if (length < -1)
    {
throw new InvalidOperationException("Array length cannot be negative.");
    }
else     if (length == 0)
    {
return Array<T>::Empty();    }
Array<T> *values = new Array<T>(length);
for (int32_t i = 0; i < values->Length; i++) {
(*values)[i] = (*readElement)(this);
}
return values;}

uint8_t EngineBinaryReader::ReadByte()
{
const int32_t value = this->BaseStream->ReadByte();
    if (value < 0)
    {
throw new EndOfStreamException("Unexpected end of stream while reading engine binary data.");
    }
return static_cast<uint8_t>(value);}

Array<uint8_t>* EngineBinaryReader::ReadByteArray()
{
const int32_t length = this->ReadInt32();
    if (length == -1)
    {
return nullptr;    }
else     if (length < -1)
    {
throw new InvalidOperationException("Byte array length cannot be negative.");
    }
else     if (length == 0)
    {
return Array<uint8_t>::Empty();    }
return this->ReadBytes(length);}

::float2 EngineBinaryReader::ReadFloat2()
{
return ([&]() {
auto __ctor_arg_00000000 = this->ReadSingle();
auto __ctor_arg_00000001 = this->ReadSingle();
return ::float2(__ctor_arg_00000000, __ctor_arg_00000001);
})();}

::float3 EngineBinaryReader::ReadFloat3()
{
return ([&]() {
auto __ctor_arg_00000002 = this->ReadSingle();
auto __ctor_arg_00000003 = this->ReadSingle();
auto __ctor_arg_00000004 = this->ReadSingle();
return ::float3(__ctor_arg_00000002, __ctor_arg_00000003, __ctor_arg_00000004);
})();}

::float4 EngineBinaryReader::ReadFloat4()
{
return ([&]() {
auto __ctor_arg_00000005 = this->ReadSingle();
auto __ctor_arg_00000006 = this->ReadSingle();
auto __ctor_arg_00000007 = this->ReadSingle();
auto __ctor_arg_00000008 = this->ReadSingle();
return ::float4(__ctor_arg_00000005, __ctor_arg_00000006, __ctor_arg_00000007, __ctor_arg_00000008);
})();}

int2* EngineBinaryReader::ReadInt2()
{
return ([&]() {
auto __ctor_arg_00000009 = this->ReadInt32();
auto __ctor_arg_0000000A = this->ReadInt32();
return new int2(__ctor_arg_00000009, __ctor_arg_0000000A);
})();}

::int4 EngineBinaryReader::ReadInt4()
{
return ([&]() {
auto __ctor_arg_0000000B = this->ReadInt32();
auto __ctor_arg_0000000C = this->ReadInt32();
auto __ctor_arg_0000000D = this->ReadInt32();
auto __ctor_arg_0000000E = this->ReadInt32();
return ::int4(__ctor_arg_0000000B, __ctor_arg_0000000C, __ctor_arg_0000000D, __ctor_arg_0000000E);
})();}

::SceneEntityReference* EngineBinaryReader::ReadSceneEntityReference()
{
    if (this->ReadByte() == 0)
    {
return nullptr;    }
return ([&]() {
auto __object_0000000F = new ::SceneEntityReference();
__object_0000000F->set_EntityId(this->ReadString());
return __object_0000000F;
})();}

float EngineBinaryReader::ReadSingle()
{
return BitConverter::Int32BitsToSingle(this->ReadInt32());}

std::string EngineBinaryReader::ReadString()
{
const int32_t length = this->ReadInt32();
    if (length == -1)
    {
return nullptr;    }
else     if (length < -1)
    {
throw new InvalidOperationException("String length cannot be negative.");
    }
else     if (length == 0)
    {
return String::Empty;    }
Array<uint8_t> *bytes = this->ReadBytes(length);
return Encoding::GetString(Encoding::UTF8, bytes);}

EngineBinaryReader::EngineBinaryReader(::Stream* stream, bool leaveOpen) : Endianness(), BaseStream(), LeaveOpen()
{
    if (stream == nullptr)
    {
throw new ArgumentNullException("stream");
    }
this->BaseStream = stream;
this->LeaveOpen = leaveOpen;
}

Array<uint8_t>* EngineBinaryReader::ReadBytes(int32_t length)
{
    if (length < 0)
    {
throw ([&]() {
auto __ctor_arg_00000010 = "length";
auto __ctor_arg_00000011 = "Length cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_00000010, __ctor_arg_00000011);
})();
    }
Array<uint8_t> *buffer = new Array<uint8_t>(length);
this->ReadRequiredBytes(buffer);
return buffer;}

void EngineBinaryReader::ReadRequiredBytes(Span<uint8_t> buffer)
{
int32_t totalBytesRead = 0;
while (totalBytesRead < buffer.Length) {
const int32_t bytesRead = this->BaseStream->Read(buffer.Slice(totalBytesRead));
    if (bytesRead <= 0)
    {
throw new EndOfStreamException("Unexpected end of stream while reading engine binary data.");
    }
totalBytesRead += bytesRead;
}
}

