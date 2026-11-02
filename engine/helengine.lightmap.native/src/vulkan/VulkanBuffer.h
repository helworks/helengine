#pragma once
#include <vulkan/vulkan.h>
namespace hellm::detail {
/// <summary>Own a Vulkan buffer allocation and optional persistent coherent mapping.</summary>
struct VulkanBuffer {
    /// <summary>Device that owns the allocation.</summary>
    VkDevice Device = VK_NULL_HANDLE;
    /// <summary>Buffer object bound to Memory.</summary>
    VkBuffer Buffer = VK_NULL_HANDLE;
    /// <summary>Backing memory allocation.</summary>
    VkDeviceMemory Memory = VK_NULL_HANDLE;
    /// <summary>Allocated buffer byte capacity.</summary>
    VkDeviceSize Size = 0;
    /// <summary>Persistent host mapping, present only for coherent host-visible allocations.</summary>
    void *Mapping = nullptr;
    /// <summary>Dynamically loaded buffer destruction entry point.</summary>
    PFN_vkDestroyBuffer DestroyBuffer = nullptr;
    /// <summary>Dynamically loaded allocation release entry point.</summary>
    PFN_vkFreeMemory FreeMemory = nullptr;
    /// <summary>Dynamically loaded memory unmapping entry point.</summary>
    PFN_vkUnmapMemory UnmapMemory = nullptr;
    /// <summary>Release mappings before buffer and memory, including partially initialized allocations.</summary>
    ~VulkanBuffer() {
        if (Mapping) UnmapMemory(Device, Memory);
        if (Buffer) DestroyBuffer(Device, Buffer, nullptr);
        if (Memory) FreeMemory(Device, Memory, nullptr);
    }
};
} // namespace hellm::detail
