#ifdef DrawText
#undef DrawText
#endif
#include "RuntimeMeshComponentDeserializer.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/io/memory-stream.hpp"
#include "EngineBinaryReader.hpp"
#include "SceneAssetReference.hpp"
#include "MeshComponent.hpp"
#include "Component.hpp"
#include "EngineBinaryEndianness.hpp"
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

std::string RuntimeMeshComponentDeserializer::get_ComponentTypeId()
{
return ComponentType;
}

::Component* RuntimeMeshComponentDeserializer::Deserialize(::SceneComponentAssetRecord* record, ::RuntimeSceneAssetReferenceResolver* referenceResolver)
{
    if (record == nullptr)
    {
throw new ArgumentNullException("record");
    }
    if (referenceResolver == nullptr)
    {
throw new ArgumentNullException("referenceResolver");
    }
    if (!String::Equals(record->get_ComponentTypeId(), ComponentType, StringComparison::OrdinalIgnoreCase))
    {
throw new InvalidOperationException(std::string("Mesh component deserializer cannot deserialize '") + record->get_ComponentTypeId() + std::string("'."));
    }
{
::MemoryStream *stream = ([&]() {
auto __ctor_arg_00000195 = ([&]() {
Array<uint8_t>* __coalesce_00000196 = record->get_Payload();
return __coalesce_00000196 != nullptr ? __coalesce_00000196 : Array<uint8_t>::Empty();
})();
auto __ctor_arg_00000197 = false;
return new ::MemoryStream(__ctor_arg_00000195, __ctor_arg_00000197);
})();
{
::EngineBinaryReader *reader = EngineBinaryReader::Create(stream, EngineBinaryEndianness::LittleEndian, true);
const uint8_t version = reader->ReadByte();
    if (version != CurrentVersion)
    {
throw new InvalidOperationException(std::string("Unsupported mesh component payload version '") + std::to_string(version) + std::string("'."));
    }
::SceneAssetReference *modelReference = this->ReadOptionalReference(reader);
::SceneAssetReference *materialReference = this->ReadOptionalReference(reader);
const uint8_t renderOrder3D = reader->ReadByte();
::MeshComponent *meshComponent = ([&]() {
auto __object_00000198 = new ::MeshComponent();
__object_00000198->set_RenderOrder3D(renderOrder3D);
return __object_00000198;
})();
    if (modelReference != nullptr)
    {
meshComponent->set_Model(referenceResolver->ResolveModel(modelReference));
    }
    if (materialReference != nullptr)
    {
meshComponent->set_Material(referenceResolver->ResolveMaterial(materialReference));
    }
return meshComponent;}
}
}

std::string RuntimeMeshComponentDeserializer::ComponentType = "helengine.MeshComponent";

uint8_t RuntimeMeshComponentDeserializer::CurrentVersion = 1;

::SceneAssetReference* RuntimeMeshComponentDeserializer::ReadOptionalReference(::EngineBinaryReader* reader)
{
    if (reader == nullptr)
    {
throw new ArgumentNullException("reader");
    }
    if (reader->ReadByte() == 0)
    {
return nullptr;    }
return ([&]() {
auto __object_00000199 = new ::SceneAssetReference();
__object_00000199->set_SourceKind(static_cast<SceneAssetReferenceSourceKind>(reader->ReadInt32()));
__object_00000199->set_RelativePath(reader->ReadString());
__object_00000199->set_ProviderId(reader->ReadString());
__object_00000199->set_AssetId(reader->ReadString());
return __object_00000199;
})();}

