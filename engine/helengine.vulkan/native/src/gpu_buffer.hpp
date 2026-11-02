#pragma once
#include "vulkan_context.hpp"
#include <cstring>

namespace helengine::vulkan {
/// <summary>Owns a Vulkan buffer and its allocation; mapped writes/reads require host-coherent memory chosen explicitly by the caller.</summary>
class GpuBuffer final {
public:
    /// <summary>Allocates and binds a buffer, releasing both handles if initialization fails.</summary>
    GpuBuffer(VulkanContext& context, VkDeviceSize size, VkBufferUsageFlags usage, VkMemoryPropertyFlags flags) : Context(context), Size(size) {
        VkBufferCreateInfo create {VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO}; create.size = size; create.usage = usage; create.sharingMode = VK_SHARING_MODE_EXCLUSIVE;
        VulkanContext::Require(vkCreateBuffer(Context.GetDevice(), &create, nullptr, &Buffer), "vkCreateBuffer");
        try {
            VkMemoryRequirements requirements {}; vkGetBufferMemoryRequirements(Context.GetDevice(), Buffer, &requirements);
            VkMemoryAllocateInfo allocate {VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO}; allocate.allocationSize = requirements.size;
            allocate.memoryTypeIndex = Context.FindMemoryType(requirements.memoryTypeBits, flags);
            VulkanContext::Require(vkAllocateMemory(Context.GetDevice(), &allocate, nullptr, &Memory), "vkAllocateMemory(buffer)");
            VulkanContext::Require(vkBindBufferMemory(Context.GetDevice(), Buffer, Memory, 0), "vkBindBufferMemory");
        } catch (...) { Release(); throw; }
    }
    /// <summary>Releases the buffer before its backing allocation after the owner has synchronized GPU use.</summary>
    ~GpuBuffer() { Release(); }
    /// <summary>Prevents two owners from releasing the same native allocation.</summary>
    GpuBuffer(const GpuBuffer&) = delete;
    /// <summary>Prevents replacing an owned GPU allocation by copying handles.</summary>
    GpuBuffer& operator=(const GpuBuffer&) = delete;
    /// <summary>Maps the coherent allocation for a bounded upload or readback.</summary>
    void* Map() { void* mapped = nullptr; VulkanContext::Require(vkMapMemory(Context.GetDevice(), Memory, 0, Size, 0, &mapped), "vkMapMemory"); return mapped; }
    /// <summary>Ends a mapped upload or readback before the allocation is used or released.</summary>
    void Unmap() { vkUnmapMemory(Context.GetDevice(), Memory); }
    /// <summary>Returns the buffer for native copy, vertex or index commands.</summary>
    VkBuffer Get() const { return Buffer; }
private:
    /// <summary>Releases all successfully initialized handles.</summary>
    void Release() noexcept { if (Buffer) { vkDestroyBuffer(Context.GetDevice(), Buffer, nullptr); } if (Memory) { vkFreeMemory(Context.GetDevice(), Memory, nullptr); } }
    /// <summary>Borrows a context that outlives this resource.</summary>
    VulkanContext& Context;
    /// <summary>Stores the buffer handle owned by this resource.</summary>
    VkBuffer Buffer = VK_NULL_HANDLE;
    /// <summary>Stores the dedicated backing memory.</summary>
    VkDeviceMemory Memory = VK_NULL_HANDLE;
    /// <summary>Stores the map range requested at construction.</summary>
    VkDeviceSize Size;
};
}
