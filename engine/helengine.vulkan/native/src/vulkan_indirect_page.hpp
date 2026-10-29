#pragma once
#include "vulkan_indirect_culler.hpp"
#include "vulkan_indirect_range.hpp"
#include "vulkan_indirect_bounds.hpp"
#include "gpu_buffer.hpp"
#include <array>
#include <memory>
#include <vector>
namespace helengine::vulkan {
/// <summary>Retains immutable bounds and indexed-command templates; compute changes visibility without CPU readback.</summary>
class VulkanIndirectPage final {
public:
    /// <summary>Uploads bounds and templates once, borrowing a pipeline which outlives the page.</summary>
    VulkanIndirectPage(VulkanContext& context,VulkanIndirectCuller* culler,std::span<const VulkanIndirectRange> ranges,std::span<const VulkanIndirectBounds> bounds);
    /// <summary>Releases page descriptors before their owned buffers.</summary>
    ~VulkanIndirectPage();
    /// <summary>Prohibits copying GPU ownership.</summary>
    VulkanIndirectPage(const VulkanIndirectPage&)=delete;
    /// <summary>Prohibits assignment of native resource handles.</summary>
    VulkanIndirectPage& operator=(const VulkanIndirectPage&)=delete;
    /// <summary>Records current camera selection after the owner binds the shared pipeline and synchronizes all page buffers; the owner adds the shared indirect-read barrier afterward.</summary>
    void Cull(VkCommandBuffer commands,std::span<const float,16> matrix) const;
    /// <summary>Records ordered indirect calls or ordinary ranges on a non-compute queue; returns actual API call count.</summary>
    size_t Draw(VkCommandBuffer commands,bool indirect=true) const;
    /// <summary>Explicitly synchronizes a diagnostic command readback; never called by normal rendering.</summary>
    size_t ReadVisibleCommandCount() const;
    /// <summary>Reports persistent bounds plus command bytes, excluding separately owned geometry.</summary>
    size_t GetBytes() const { return Culler?Ranges.size()*(sizeof(VulkanIndirectBounds)+sizeof(VkDrawIndexedIndirectCommand)):0; }
    /// <summary>Reports logical command slots, not the unobserved GPU-visible count.</summary>
    size_t GetCommandCount() const { return Ranges.size(); }
    /// <summary>Reports whether the page actually dispatches compute.</summary>
    bool UsesCompute() const { return Culler!=nullptr; }
private:
    /// <summary>Stages immutable bytes into device-local persistent storage.</summary>
    std::unique_ptr<GpuBuffer> Upload(const void* data,size_t bytes,VkBufferUsageFlags usage,VkAccessFlags access);
    /// <summary>Borrows the synchronous device owner.</summary>
    VulkanContext& Context;
    /// <summary>Borrows the renderer-wide compute pipeline, absent on unsupported queues.</summary>
    VulkanIndirectCuller* Culler;
    /// <summary>Retains direct fallback ranges in the same authored order.</summary>
    std::vector<VulkanIndirectRange> Ranges;
    /// <summary>Combines all direct fallback ranges only when their ordered triangle interval has no gaps.</summary>
    uint32_t DirectIndexCount=0;
    /// <summary>Owns immutable std430 position bounds and exact original indexed-command metadata.</summary>
    std::unique_ptr<GpuBuffer> Bounds;
    /// <summary>Owns persistent indexed commands completely regenerated from immutable metadata by compute.</summary>
    std::unique_ptr<GpuBuffer> Commands;
    /// <summary>Owns a bounded descriptor pool for this page's two storage buffers.</summary>
    VkDescriptorPool Pool=VK_NULL_HANDLE;
    /// <summary>Borrows the descriptor set owned by Pool.</summary>
    VkDescriptorSet Set=VK_NULL_HANDLE;
};
}
