#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/array.hpp"
#include "runtime/native_string.hpp"
#include "RuntimeCodeModuleLoadState.hpp"

class RuntimeCodeModuleManifestEntry
{
public:
    virtual ~RuntimeCodeModuleManifestEntry() = default;

    Array<std::string>* DependencyModuleIds;

    Array<std::string>* get_DependencyModuleIds();

    ::RuntimeCodeModuleLoadState LoadState;

    ::RuntimeCodeModuleLoadState get_LoadState();

    std::string ModuleId;

    std::string get_ModuleId();

    std::string RuntimeSpecializationId;

    std::string get_RuntimeSpecializationId();

    RuntimeCodeModuleManifestEntry(std::string moduleId, std::string runtimeSpecializationId, ::RuntimeCodeModuleLoadState loadState, Array<std::string>* dependencyModuleIds);
};
