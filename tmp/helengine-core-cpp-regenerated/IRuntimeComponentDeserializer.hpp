#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class Component;
class SceneComponentAssetRecord;
class RuntimeSceneAssetReferenceResolver;

#include "runtime/native_string.hpp"
#include "Component.hpp"
#include "SceneComponentAssetRecord.hpp"
#include "RuntimeSceneAssetReferenceResolver.hpp"

class IRuntimeComponentDeserializer
{
public:
    virtual std::string get_ComponentTypeId() = 0;

    virtual ::Component* Deserialize(::SceneComponentAssetRecord* record, ::RuntimeSceneAssetReferenceResolver* referenceResolver) = 0;
};
