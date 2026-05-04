#pragma once

#include <cstddef>

enum class HERuntimeCodeModuleLoadState {
    ResidentAtStartup = 0,
    SceneResident = 1,
    Unloadable = 2
};

struct HERuntimeCodeModuleEntry {
    const char* ModuleId;
    const char* RuntimeSpecializationId;
    HERuntimeCodeModuleLoadState LoadState;
    const char* const* DependencyModuleIds;
    std::size_t DependencyModuleCount;
};

const HERuntimeCodeModuleEntry* he_runtime_code_module_entries(std::size_t* count);
HERuntimeCodeModuleLoadState he_runtime_code_module_load_state(const char* moduleId);
bool he_runtime_code_module_can_unload(const char* moduleId);
