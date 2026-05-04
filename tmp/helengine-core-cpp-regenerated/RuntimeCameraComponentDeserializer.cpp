#ifdef DrawText
#undef DrawText
#endif
#include "RuntimeCameraComponentDeserializer.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/io/memory-stream.hpp"
#include "EngineBinaryReader.hpp"
#include "Component.hpp"
#include "float4.hpp"
#include "CameraClearSettings.hpp"
#include "EngineBinaryEndianness.hpp"
#include "CameraComponent.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
#include "runtime/native_cast.hpp"
#include "runtime/native_datetime.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_disposable.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_event.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_nullable.hpp"
#include "runtime/native_span.hpp"
#include "runtime/native_stack.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/app_context.hpp"
#include "system/binary_primitives.hpp"
#include "system/bit_converter.hpp"
#include "system/diagnostics/debug.hpp"
#include "system/diagnostics/stopwatch.hpp"
#include "system/guid.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream-reader.hpp"
#include "system/io/stream.hpp"
#include "system/io/string-reader.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/security/cryptography/sha256.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "system/text/string-builder.hpp"

std::string RuntimeCameraComponentDeserializer::get_ComponentTypeId()
{
return ComponentType;
}

::Component* RuntimeCameraComponentDeserializer::Deserialize(::SceneComponentAssetRecord* record, ::RuntimeSceneAssetReferenceResolver* referenceResolver)
{
    if (record == nullptr)
    {
throw new ArgumentNullException("record");
    }
    if (!String::Equals(record->get_ComponentTypeId(), ComponentType, StringComparison::OrdinalIgnoreCase))
    {
throw new InvalidOperationException(std::string("Camera component deserializer cannot deserialize '") + record->get_ComponentTypeId() + std::string("'."));
    }
{
::MemoryStream *stream = ([&]() {
auto __ctor_arg_00000182 = ([&]() {
Array<uint8_t>* __coalesce_00000183 = record->get_Payload();
return __coalesce_00000183 != nullptr ? __coalesce_00000183 : Array<uint8_t>::Empty();
})();
auto __ctor_arg_00000184 = false;
return new ::MemoryStream(__ctor_arg_00000182, __ctor_arg_00000184);
})();
{
::EngineBinaryReader *reader = EngineBinaryReader::Create(stream, EngineBinaryEndianness::LittleEndian, true);
const uint8_t version = reader->ReadByte();
    if (version != CurrentVersion)
    {
throw new InvalidOperationException(std::string("Unsupported camera component payload version '") + std::to_string(version) + std::string("'."));
    }
return ([&]() {
auto __object_00000185 = new ::CameraComponent();
__object_00000185->set_CameraDrawOrder(reader->ReadByte());
__object_00000185->set_LayerMask(reader->ReadUInt16());
__object_00000185->set_Viewport(this->ReadFloat4(reader));
__object_00000185->set_ClearSettings(this->ReadClearSettings(reader));
return __object_00000185;
})();}
}
}

std::string RuntimeCameraComponentDeserializer::ComponentType = "helengine.CameraComponent";

uint8_t RuntimeCameraComponentDeserializer::CurrentVersion = 1;

::CameraClearSettings RuntimeCameraComponentDeserializer::ReadClearSettings(::EngineBinaryReader* reader)
{
    if (reader == nullptr)
    {
throw new ArgumentNullException("reader");
    }
return ([&]() {
auto __ctor_arg_00000186 = reader->ReadByte() != 0;
auto __ctor_arg_00000187 = ReadFloat4(reader);
auto __ctor_arg_00000188 = reader->ReadByte() != 0;
auto __ctor_arg_00000189 = reader->ReadSingle();
auto __ctor_arg_0000018A = reader->ReadByte() != 0;
auto __ctor_arg_0000018B = reader->ReadByte();
return ::CameraClearSettings(__ctor_arg_00000186, __ctor_arg_00000187, __ctor_arg_00000188, __ctor_arg_00000189, __ctor_arg_0000018A, __ctor_arg_0000018B);
})();}

::float4 RuntimeCameraComponentDeserializer::ReadFloat4(::EngineBinaryReader* reader)
{
    if (reader == nullptr)
    {
throw new ArgumentNullException("reader");
    }
return ([&]() {
auto __ctor_arg_0000018C = reader->ReadSingle();
auto __ctor_arg_0000018D = reader->ReadSingle();
auto __ctor_arg_0000018E = reader->ReadSingle();
auto __ctor_arg_0000018F = reader->ReadSingle();
return ::float4(__ctor_arg_0000018C, __ctor_arg_0000018D, __ctor_arg_0000018E, __ctor_arg_0000018F);
})();}

