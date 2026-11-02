#pragma once
#include <cstdint>
#include <stdexcept>

namespace helengine::vulkan {
/// <summary>Stores explicit bounded batching selections supplied by the Linux builder before runtime initialization.</summary>
class VulkanMeshBatchingSettings final {
public:
    /// <summary>Selects persistent batching for entities carrying the engine Static contract.</summary>
    bool StaticEnabled=false;
    /// <summary>Limits additional resident static geometry across this renderer's caches.</summary>
    uint64_t StaticBudgetBytes=uint64_t{256}*1024*1024;
    /// <summary>Selects transient batching of compatible small current-frame geometry.</summary>
    bool DynamicEnabled=false;
    /// <summary>Limits unique referenced vertices in each dynamic input.</summary>
    uint32_t DynamicMaxVertices=1024;
    /// <summary>Limits reusable transient geometry capacity without multiplying it by FPS.</summary>
    uint64_t DynamicBudgetBytes=uint64_t{4}*1024*1024;
    /// <summary>Rejects invalid required bounds before loading assets or creating GPU batch resources.</summary>
    void Validate() const {
        if(!StaticBudgetBytes || !DynamicMaxVertices || !DynamicBudgetBytes) throw std::invalid_argument("Linux mesh batching requires positive static/dynamic budgets and dynamic vertex limit.");
    }
    /// <summary>Uses build-time values selected through the platform graphics metadata, never environment overrides.</summary>
    static VulkanMeshBatchingSettings FromBuild() {
        VulkanMeshBatchingSettings settings;
#if defined(HELENGINE_VULKAN_STATIC_MESH_BATCHING)
        settings.StaticEnabled=HELENGINE_VULKAN_STATIC_MESH_BATCHING!=0;
        settings.StaticBudgetBytes=uint64_t{HELENGINE_VULKAN_STATIC_MESH_BATCHING_BUDGET_MIB}*1024*1024;
        settings.DynamicEnabled=HELENGINE_VULKAN_DYNAMIC_MESH_BATCHING!=0;
        settings.DynamicMaxVertices=HELENGINE_VULKAN_DYNAMIC_MESH_BATCHING_MAX_VERTICES;
        settings.DynamicBudgetBytes=uint64_t{HELENGINE_VULKAN_DYNAMIC_MESH_BATCHING_BUDGET_MIB}*1024*1024;
#endif
        settings.Validate();
        return settings;
    }
};
}
