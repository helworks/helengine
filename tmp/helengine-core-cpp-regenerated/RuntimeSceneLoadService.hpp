#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class Logger;
class Entity;
class Component;
class RuntimeComponentRegistry;
class RuntimeSceneAssetReferenceResolver;
class SceneAsset;
class SceneComponentAssetRecord;
class SceneEntityAsset;

#include "runtime/native_exceptions.hpp"
#include "Logger.hpp"
#include "Logger.hpp"
#include "system/diagnostics/stopwatch.hpp"
#include "system/diagnostics/stopwatch.hpp"
#include "runtime/array.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_list.hpp"
#include "Entity.hpp"
#include "Entity.hpp"
#include "runtime/array.hpp"
#include "Component.hpp"
#include "RuntimeComponentRegistry.hpp"
#include "RuntimeSceneAssetReferenceResolver.hpp"
#include "runtime/native_list.hpp"
#include "SceneAsset.hpp"
#include "Component.hpp"
#include "SceneComponentAssetRecord.hpp"
#include "SceneEntityAsset.hpp"

class RuntimeSceneLoadService
{
public:
    virtual ~RuntimeSceneLoadService() = default;

    List<::Entity*>* Load(::SceneAsset* sceneAsset);

    RuntimeSceneLoadService(::RuntimeSceneAssetReferenceResolver* referenceResolver);

    RuntimeSceneLoadService(::RuntimeSceneAssetReferenceResolver* referenceResolver, ::RuntimeComponentRegistry* componentRegistry);
private:
    ::RuntimeComponentRegistry* ComponentRegistry;

    ::RuntimeSceneAssetReferenceResolver* ReferenceResolver;

    ::Component* LoadComponent(::SceneComponentAssetRecord* record);

    ::Entity* LoadEntity(::SceneEntityAsset* entityAsset);
};
