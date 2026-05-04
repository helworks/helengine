#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class IRuntimeComponentDeserializer;
class EngineBinaryReader;
class SceneAssetReference;
class FPSComponent;
class Component;
class SceneComponentAssetRecord;
class RuntimeSceneAssetReferenceResolver;

#include "IRuntimeComponentDeserializer.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/io/memory-stream.hpp"
#include "EngineBinaryReader.hpp"
#include "EngineBinaryReader.hpp"
#include "SceneAssetReference.hpp"
#include "FPSComponent.hpp"
#include "Component.hpp"
#include "SceneAssetReference.hpp"
#include "runtime/native_string.hpp"
#include "Component.hpp"
#include "SceneComponentAssetRecord.hpp"
#include "RuntimeSceneAssetReferenceResolver.hpp"

class RuntimeFPSComponentDeserializer : public IRuntimeComponentDeserializer
{
public:
    virtual ~RuntimeFPSComponentDeserializer() = default;

    std::string get_ComponentTypeId();

    ::Component* Deserialize(::SceneComponentAssetRecord* record, ::RuntimeSceneAssetReferenceResolver* referenceResolver);
private:
    static std::string ComponentType;

    static uint8_t CurrentVersion;

    ::SceneAssetReference* ReadOptionalReference(::EngineBinaryReader* reader);
};
