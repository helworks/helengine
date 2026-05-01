#ifdef DrawText
#undef DrawText
#endif
#include "RuntimeSceneLoadService.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/array.hpp"
#include "runtime/native_list.hpp"
#include "Entity.hpp"
#include "runtime/native_string.hpp"
#include "Component.hpp"
#include "system/io/memory-stream.hpp"
#include "EngineBinaryReader.hpp"
#include "SceneAssetReference.hpp"
#include "MeshComponent.hpp"
#include "CameraComponent.hpp"
#include "float4.hpp"
#include "CameraClearSettings.hpp"
#include "SceneEntityAsset.hpp"
#include "EngineBinaryEndianness.hpp"
#include "SceneComponentAssetRecord.hpp"
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
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/app_context.hpp"
#include "system/bit_converter.hpp"
#include "system/diagnostics/debug.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"

List<::Entity*>* RuntimeSceneLoadService::Load(::SceneAsset* sceneAsset)
{
    if (sceneAsset == nullptr)
    {
throw new ArgumentNullException("sceneAsset");
    }
Array<::SceneEntityAsset*> *rootEntityAssets = ([&]() {
Array<::SceneEntityAsset*>* __coalesce_0b1835e8 = sceneAsset->get_RootEntities();
return __coalesce_0b1835e8 != nullptr ? __coalesce_0b1835e8 : Array<SceneEntityAsset*>::Empty();
})();
List<::Entity*> *rootEntities = new List<::Entity*>(rootEntityAssets->Length);
for (int32_t index = 0; index < rootEntityAssets->Length; index++) {
rootEntities->Add(this->LoadEntity((*rootEntityAssets)[index]));
}
return rootEntities;}

RuntimeSceneLoadService::RuntimeSceneLoadService(::RuntimeSceneAssetReferenceResolver* referenceResolver) : ReferenceResolver()
{
this->ReferenceResolver = (referenceResolver != nullptr ? referenceResolver : throw new ArgumentNullException("referenceResolver"));
}

uint8_t RuntimeSceneLoadService::CameraComponentPayloadVersion = 1;

std::string RuntimeSceneLoadService::CameraComponentTypeId = "helengine.CameraComponent";

uint8_t RuntimeSceneLoadService::MeshComponentPayloadVersion = 1;

std::string RuntimeSceneLoadService::MeshComponentTypeId = "helengine.MeshComponent";

::CameraComponent* RuntimeSceneLoadService::LoadCameraComponent(::SceneComponentAssetRecord* record)
{
{
MemoryStream *stream = ([&]() {
auto __ctor_arg_8526a710 = ([&]() {
Array<uint8_t>* __coalesce_f8df8af3 = record->get_Payload();
return __coalesce_f8df8af3 != nullptr ? __coalesce_f8df8af3 : Array<uint8_t>::Empty();
})();
auto __ctor_arg_aa51b708 = false;
return new MemoryStream(__ctor_arg_8526a710, __ctor_arg_aa51b708);
})();
{
::EngineBinaryReader *reader = EngineBinaryReader::Create(stream, EngineBinaryEndianness::LittleEndian, true);
const uint8_t version = reader->ReadByte();
    if (version != CameraComponentPayloadVersion)
    {
throw new InvalidOperationException(std::string("Unsupported camera component payload version '") + std::to_string(version) + std::string("'."));
    }
return ([&]() {
auto __object_7e9bd34f = new ::CameraComponent();
__object_7e9bd34f->set_CameraDrawOrder(reader->ReadByte());
__object_7e9bd34f->set_LayerMask(reader->ReadUInt16());
__object_7e9bd34f->set_Viewport(this->ReadFloat4(reader));
__object_7e9bd34f->set_ClearSettings(this->ReadClearSettings(reader));
return __object_7e9bd34f;
})();}
}
}

::Component* RuntimeSceneLoadService::LoadComponent(::SceneComponentAssetRecord* record)
{
    if (record == nullptr)
    {
throw new ArgumentNullException("record");
    }
    if (String::Equals(record->get_ComponentTypeId(), MeshComponentTypeId, StringComparison::Ordinal))
    {
return this->LoadMeshComponent(record);    }
    if (String::Equals(record->get_ComponentTypeId(), CameraComponentTypeId, StringComparison::Ordinal))
    {
return this->LoadCameraComponent(record);    }
throw new InvalidOperationException(std::string("Player builds do not support serialized component type '") + record->get_ComponentTypeId() + std::string("' yet."));
}

::Entity* RuntimeSceneLoadService::LoadEntity(::SceneEntityAsset* entityAsset)
{
    if (entityAsset == nullptr)
    {
throw new ArgumentNullException("entityAsset");
    }
::Entity *entity = ([&]() {
auto __object_0960d1cb = new ::Entity();
__object_0960d1cb->set_LocalPosition(entityAsset->get_LocalPosition());
__object_0960d1cb->set_LocalScale(entityAsset->get_LocalScale());
__object_0960d1cb->set_LocalOrientation(entityAsset->get_LocalOrientation());
return __object_0960d1cb;
})();
entity->InitComponents();
entity->InitChildren();
Array<::SceneComponentAssetRecord*> *componentRecords = ([&]() {
Array<::SceneComponentAssetRecord*>* __coalesce_8dcb5f72 = entityAsset->get_Components();
return __coalesce_8dcb5f72 != nullptr ? __coalesce_8dcb5f72 : Array<SceneComponentAssetRecord*>::Empty();
})();
for (int32_t index = 0; index < componentRecords->Length; index++) {
entity->AddComponent(this->LoadComponent((*componentRecords)[index]));
}
Array<::SceneEntityAsset*> *childEntityAssets = ([&]() {
Array<::SceneEntityAsset*>* __coalesce_24528381 = entityAsset->get_Children();
return __coalesce_24528381 != nullptr ? __coalesce_24528381 : Array<SceneEntityAsset*>::Empty();
})();
for (int32_t index = 0; index < childEntityAssets->Length; index++) {
entity->AddChild(this->LoadEntity((*childEntityAssets)[index]));
}
return entity;}

::MeshComponent* RuntimeSceneLoadService::LoadMeshComponent(::SceneComponentAssetRecord* record)
{
{
MemoryStream *stream = ([&]() {
auto __ctor_arg_b63203e0 = ([&]() {
Array<uint8_t>* __coalesce_405b1670 = record->get_Payload();
return __coalesce_405b1670 != nullptr ? __coalesce_405b1670 : Array<uint8_t>::Empty();
})();
auto __ctor_arg_7e8d4b75 = false;
return new MemoryStream(__ctor_arg_b63203e0, __ctor_arg_7e8d4b75);
})();
{
::EngineBinaryReader *reader = EngineBinaryReader::Create(stream, EngineBinaryEndianness::LittleEndian, true);
const uint8_t version = reader->ReadByte();
    if (version != MeshComponentPayloadVersion)
    {
throw new InvalidOperationException(std::string("Unsupported mesh component payload version '") + std::to_string(version) + std::string("'."));
    }
::SceneAssetReference *modelReference = this->ReadOptionalReference(reader);
::SceneAssetReference *materialReference = this->ReadOptionalReference(reader);
const uint8_t renderOrder3D = reader->ReadByte();
::MeshComponent *meshComponent = ([&]() {
auto __object_2920e3fe = new ::MeshComponent();
__object_2920e3fe->set_RenderOrder3D(renderOrder3D);
return __object_2920e3fe;
})();
    if (modelReference != nullptr)
    {
meshComponent->set_Model(this->ReferenceResolver->ResolveModel(modelReference));
    }
    if (materialReference != nullptr)
    {
meshComponent->set_Material(this->ReferenceResolver->ResolveMaterial(materialReference));
    }
return meshComponent;}
}
}

::CameraClearSettings RuntimeSceneLoadService::ReadClearSettings(::EngineBinaryReader* reader)
{
    if (reader == nullptr)
    {
throw new ArgumentNullException("reader");
    }
return ([&]() {
auto __ctor_arg_2d8eda70 = reader->ReadByte() != 0;
auto __ctor_arg_28c3de3a = this->ReadFloat4(reader);
auto __ctor_arg_8ac81d2c = reader->ReadByte() != 0;
auto __ctor_arg_23e9f1ad = reader->ReadSingle();
auto __ctor_arg_b6358306 = reader->ReadByte() != 0;
auto __ctor_arg_773c63b0 = reader->ReadByte();
return ::CameraClearSettings(__ctor_arg_2d8eda70, __ctor_arg_28c3de3a, __ctor_arg_8ac81d2c, __ctor_arg_23e9f1ad, __ctor_arg_b6358306, __ctor_arg_773c63b0);
})();}

::float4 RuntimeSceneLoadService::ReadFloat4(::EngineBinaryReader* reader)
{
    if (reader == nullptr)
    {
throw new ArgumentNullException("reader");
    }
return ([&]() {
auto __ctor_arg_44e716af = reader->ReadSingle();
auto __ctor_arg_b2d21566 = reader->ReadSingle();
auto __ctor_arg_a8140d6e = reader->ReadSingle();
auto __ctor_arg_0783573c = reader->ReadSingle();
return ::float4(__ctor_arg_44e716af, __ctor_arg_b2d21566, __ctor_arg_a8140d6e, __ctor_arg_0783573c);
})();}

::SceneAssetReference* RuntimeSceneLoadService::ReadOptionalReference(::EngineBinaryReader* reader)
{
    if (reader == nullptr)
    {
throw new ArgumentNullException("reader");
    }
    if (reader->ReadByte() == 0)
    {
return nullptr;    }
return ([&]() {
auto __object_a33fda4f = new ::SceneAssetReference();
__object_a33fda4f->set_SourceKind(static_cast<SceneAssetReferenceSourceKind>(reader->ReadInt32()));
__object_a33fda4f->set_RelativePath(reader->ReadString());
__object_a33fda4f->set_ProviderId(reader->ReadString());
__object_a33fda4f->set_AssetId(reader->ReadString());
return __object_a33fda4f;
})();}

