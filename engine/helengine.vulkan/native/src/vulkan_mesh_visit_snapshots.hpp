#pragma once
#include "vulkan_mesh_material_snapshot.hpp"
#include <memory>
#include <stdexcept>
#include <unordered_map>

namespace helengine::vulkan {
/// <summary>Memoizes immutable material payloads only within one synchronous drawable visit; queued submissions retain their own const shared references.</summary>
class VulkanMeshVisitSnapshots final {
    /// <summary>Uses object identity solely to avoid rereading the same material in this visit, never to decide batch compatibility.</summary>
    std::unordered_map<const void*,std::shared_ptr<const VulkanMeshMaterialSnapshot>> Snapshots;
public:
    /// <summary>Constructs before insertion, preserving original exceptions and leaving failed or empty factories unpublished.</summary>
    template<class TFactory> std::shared_ptr<const VulkanMeshMaterialSnapshot> GetOrCreate(const void* material,TFactory&& factory) {
        if(!material) throw std::invalid_argument("Visit snapshot requires a live material identity.");
        const auto found=Snapshots.find(material);
        if(found!=Snapshots.end()) return found->second;
        std::shared_ptr<const VulkanMeshMaterialSnapshot> snapshot=factory();
        if(!snapshot) throw std::invalid_argument("Visit snapshot factory returned no immutable payload.");
        Snapshots.emplace(material,snapshot);return snapshot;
    }
};
}
