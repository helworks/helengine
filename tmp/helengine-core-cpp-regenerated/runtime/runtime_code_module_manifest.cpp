#include "runtime/runtime_code_module_manifest.hpp"

#include <cstring>
#include <stdexcept>

static const HERuntimeCodeModuleEntry* kRuntimeCodeModuleEntries = nullptr;
static const std::size_t kRuntimeCodeModuleEntryCount = 0;

const HERuntimeCodeModuleEntry* he_runtime_code_module_entries(std::size_t* count) {
    if (count != nullptr) {
        *count = kRuntimeCodeModuleEntryCount;
    }

    return kRuntimeCodeModuleEntries;
}

HERuntimeCodeModuleLoadState he_runtime_code_module_load_state(const char* moduleId) {
    if (moduleId == nullptr || moduleId[0] == '\0') {
        throw std::invalid_argument("Runtime code module id is required.");
    }

    for (std::size_t index = 0; index < kRuntimeCodeModuleEntryCount; index++) {
        const HERuntimeCodeModuleEntry& entry = kRuntimeCodeModuleEntries[index];
        if (std::strcmp(entry.ModuleId, moduleId) == 0) {
            return entry.LoadState;
        }
    }

    throw std::runtime_error("Runtime code module was not found in the residency manifest.");
}

bool he_runtime_code_module_can_unload(const char* moduleId) {
    return he_runtime_code_module_load_state(moduleId) != HERuntimeCodeModuleLoadState::ResidentAtStartup;
}
