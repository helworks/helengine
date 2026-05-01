#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class Entity;
class Component;
class EngineBinaryReader;
class SceneAssetReference;
class MeshComponent;
class CameraComponent;
class float4;
class CameraClearSettings;
class RuntimeSceneAssetReferenceResolver;
class SceneAsset;
class SceneComponentAssetRecord;
class SceneEntityAsset;

#include "runtime/native_exceptions.hpp"
#include "runtime/array.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_list.hpp"
#include "Entity.hpp"
#include "Entity.hpp"
#include "runtime/array.hpp"
#include "runtime/native_string.hpp"
#include "Component.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/io/memory-stream.hpp"
#include "EngineBinaryReader.hpp"
#include "EngineBinaryReader.hpp"
#include "SceneAssetReference.hpp"
#include "MeshComponent.hpp"
#include "MeshComponent.hpp"
#include "CameraComponent.hpp"
#include "SceneAssetReference.hpp"
#include "float4.hpp"
#include "CameraClearSettings.hpp"
#include "RuntimeSceneAssetReferenceResolver.hpp"
#include "runtime/native_list.hpp"
#include "SceneAsset.hpp"
#include "CameraComponent.hpp"
#include "SceneComponentAssetRecord.hpp"
#include "Component.hpp"
#include "SceneEntityAsset.hpp"
#include "CameraClearSettings.hpp"
#include "float4.hpp"

class RuntimeSceneLoadService
{
public:
    List<::Entity*>* Load(::SceneAsset* sceneAsset);

    RuntimeSceneLoadService(::RuntimeSceneAssetReferenceResolver* referenceResolver);
private:
    static uint8_t CameraComponentPayloadVersion;

    static std::string CameraComponentTypeId;

    static uint8_t MeshComponentPayloadVersion;

    static std::string MeshComponentTypeId;

    ::RuntimeSceneAssetReferenceResolver* ReferenceResolver;

    ::CameraComponent* LoadCameraComponent(::SceneComponentAssetRecord* record);

    ::Component* LoadComponent(::SceneComponentAssetRecord* record);

    ::Entity* LoadEntity(::SceneEntityAsset* entityAsset);

    ::MeshComponent* LoadMeshComponent(::SceneComponentAssetRecord* record);

    ::CameraClearSettings ReadClearSettings(::EngineBinaryReader* reader);

    ::float4 ReadFloat4(::EngineBinaryReader* reader);

    ::SceneAssetReference* ReadOptionalReference(::EngineBinaryReader* reader);
};
