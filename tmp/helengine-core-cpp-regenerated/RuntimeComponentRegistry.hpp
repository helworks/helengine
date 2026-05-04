#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class IRuntimeComponentDeserializer;

#include "runtime/native_exceptions.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_exceptions.hpp"
#include "IRuntimeComponentDeserializer.hpp"
#include "runtime/native_dictionary.hpp"
#include "IRuntimeComponentDeserializer.hpp"

class RuntimeComponentRegistry
{
public:
    virtual ~RuntimeComponentRegistry() = default;

    static ::RuntimeComponentRegistry* CreateDefault();

    ::IRuntimeComponentDeserializer* GetDeserializer(std::string componentTypeId);

    void Register(::IRuntimeComponentDeserializer* deserializer);

    RuntimeComponentRegistry();

    bool TryGet(std::string componentTypeId, ::IRuntimeComponentDeserializer*& deserializer);
private:
    Dictionary<std::string, ::IRuntimeComponentDeserializer*>* DeserializersByTypeId;
};
