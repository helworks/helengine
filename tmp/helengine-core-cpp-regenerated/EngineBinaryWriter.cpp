#ifdef DrawText
#undef DrawText
#endif
#include "EngineBinaryWriter.hpp"
#include "runtime/native_exceptions.hpp"
#include "EngineBinaryWriter.hpp"
#include "system/io/stream.hpp"
#include "runtime/array.hpp"
#include "system/text/encoding.hpp"
#include "runtime/native_string.hpp"
#include "EngineBinaryEndianness.hpp"
#include "BinaryWriterLE.hpp"
#include "BinaryWriterBE.hpp"
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

::EngineBinaryEndianness EngineBinaryWriter::get_Endianness()
{
return this->Endianness;
}

::EngineBinaryWriter* EngineBinaryWriter::Create(::Stream* stream, ::EngineBinaryEndianness endianness, bool leaveOpen)
{
    if (stream == nullptr)
    {
throw new ArgumentNullException("stream");
    }
else     if (endianness == EngineBinaryEndianness::LittleEndian)
    {
return new ::BinaryWriterLE(stream, leaveOpen);    }
else     if (endianness == EngineBinaryEndianness::BigEndian)
    {
return new ::BinaryWriterBE(stream, leaveOpen);    }
throw new InvalidOperationException(std::string("Unsupported binary payload endianness '") + std::to_string(static_cast<uint8_t>(endianness)) + std::string("'."));
}

void EngineBinaryWriter::Dispose()
{
    if (!this->LeaveOpen)
    {
this->BaseStream->Dispose();
    }
}

template <typename T>
void EngineBinaryWriter::WriteArray(Array<T>* values, Action<::EngineBinaryWriter*, T>* writeElement)
{
    if (writeElement == nullptr)
    {
throw new ArgumentNullException("writeElement");
    }
    if (values == nullptr)
    {
this->WriteInt32(-1);
return;    }
this->WriteInt32(values->Length);
for (int32_t i = 0; i < values->Length; i++) {
(*writeElement)(this, (*values)[i]);
}
}

void EngineBinaryWriter::WriteByte(uint8_t value)
{
this->BaseStream->WriteByte(value);
}

void EngineBinaryWriter::WriteByteArray(Array<uint8_t>* value)
{
    if (value == nullptr)
    {
this->WriteInt32(-1);
return;    }
this->WriteInt32(value->Length);
this->BaseStream->Write(value, 0, value->Length);
}

void EngineBinaryWriter::WriteFloat2(::float2 value)
{
this->WriteSingle(value.X);
this->WriteSingle(value.Y);
}

void EngineBinaryWriter::WriteFloat3(::float3 value)
{
this->WriteSingle(value.X);
this->WriteSingle(value.Y);
this->WriteSingle(value.Z);
}

void EngineBinaryWriter::WriteFloat4(::float4 value)
{
this->WriteSingle(value.X);
this->WriteSingle(value.Y);
this->WriteSingle(value.Z);
this->WriteSingle(value.W);
}

void EngineBinaryWriter::WriteInt2(int2* value)
{
this->WriteInt32(value->X);
this->WriteInt32(value->Y);
}

void EngineBinaryWriter::WriteInt4(::int4 value)
{
this->WriteInt32(value.X);
this->WriteInt32(value.Y);
this->WriteInt32(value.Z);
this->WriteInt32(value.W);
}

void EngineBinaryWriter::WriteSceneEntityReference(::SceneEntityReference* reference)
{
this->WriteByte(reference == nullptr ? static_cast<uint8_t>(0) : static_cast<uint8_t>(1));
    if (reference == nullptr)
    {
return;    }
    if (String::IsNullOrWhiteSpace(reference->get_EntityId()))
    {
throw new InvalidOperationException("Scene entity references must define an entity id.");
    }
this->WriteString(reference->get_EntityId());
}

void EngineBinaryWriter::WriteSingle(float value)
{
this->WriteInt32(BitConverter::SingleToInt32Bits(value));
}

void EngineBinaryWriter::WriteString(std::string value)
{
    if (String::IsNullOrEmpty(value))
    {
this->WriteInt32(-1);
return;    }
Array<uint8_t> *bytes = Encoding::GetBytes(Encoding::UTF8, value);
this->WriteInt32(bytes->Length);
this->BaseStream->Write(bytes, 0, bytes->Length);
}

EngineBinaryWriter::EngineBinaryWriter(::Stream* stream, bool leaveOpen) : Endianness(), LeaveOpen(), BaseStream()
{
    if (stream == nullptr)
    {
throw new ArgumentNullException("stream");
    }
this->BaseStream = stream;
this->LeaveOpen = leaveOpen;
}

