#pragma once
#include "vulkan_context.hpp"

namespace helengine::vulkan {
/// <summary>Owns one device-local RGBA8 image, its allocation and its color image view.</summary>
class GpuImage final {
public:
    /// <summary>Creates an optimal-tiled RGBA8 image with explicit usage and no implicit initial contents.</summary>
    GpuImage(VulkanContext& context, uint32_t width, uint32_t height, VkImageUsageFlags usage) : Context(context), Width(width), Height(height) {
        VkImageCreateInfo create {VK_STRUCTURE_TYPE_IMAGE_CREATE_INFO}; create.imageType = VK_IMAGE_TYPE_2D; create.format = VK_FORMAT_R8G8B8A8_UNORM;
        create.extent = {width, height, 1}; create.mipLevels = 1; create.arrayLayers = 1; create.samples = VK_SAMPLE_COUNT_1_BIT;
        create.tiling = VK_IMAGE_TILING_OPTIMAL; create.usage = usage; create.sharingMode = VK_SHARING_MODE_EXCLUSIVE;
        VulkanContext::Require(vkCreateImage(Context.GetDevice(), &create, nullptr, &Image), "vkCreateImage(RGBA8)");
        try {
            VkMemoryRequirements requirements {}; vkGetImageMemoryRequirements(Context.GetDevice(), Image, &requirements);
            VkMemoryAllocateInfo allocate {VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO}; allocate.allocationSize = requirements.size;
            allocate.memoryTypeIndex = Context.FindMemoryType(requirements.memoryTypeBits, VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT);
            VulkanContext::Require(vkAllocateMemory(Context.GetDevice(), &allocate, nullptr, &Memory), "vkAllocateMemory(image)");
            VulkanContext::Require(vkBindImageMemory(Context.GetDevice(), Image, Memory, 0), "vkBindImageMemory");
            VkImageViewCreateInfo view {VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO}; view.image = Image; view.viewType = VK_IMAGE_VIEW_TYPE_2D; view.format = create.format;
            view.subresourceRange = {VK_IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1};
            VulkanContext::Require(vkCreateImageView(Context.GetDevice(), &view, nullptr, &View), "vkCreateImageView(RGBA8)");
        } catch (...) { Release(); throw; }
    }
    /// <summary>Releases the view, image and allocation after the owner has synchronized pending GPU use.</summary>
    ~GpuImage() { Release(); }
    /// <summary>Prevents shared ownership of a native image allocation.</summary>
    GpuImage(const GpuImage&) = delete;
    /// <summary>Prevents handle copying through assignment.</summary>
    GpuImage& operator=(const GpuImage&) = delete;
    /// <summary>Exposes the image for transitions, copies and render commands.</summary>
    VkImage Get() const { return Image; }
    /// <summary>Exposes the color image view for descriptors and framebuffer attachment.</summary>
    VkImageView GetView() const { return View; }
    /// <summary>Returns the immutable texture width used to validate region uploads.</summary>
    uint32_t GetWidth() const { return Width; }
    /// <summary>Returns the immutable texture height used to validate region uploads.</summary>
    uint32_t GetHeight() const { return Height; }
private:
    /// <summary>Releases initialized resources in reverse dependency order.</summary>
    void Release() noexcept { if (View) { vkDestroyImageView(Context.GetDevice(), View, nullptr); } if (Image) { vkDestroyImage(Context.GetDevice(), Image, nullptr); } if (Memory) { vkFreeMemory(Context.GetDevice(), Memory, nullptr); } }
    /// <summary>Borrows the device context for this allocation's lifetime.</summary>
    VulkanContext& Context;
    /// <summary>Owns the optimal-tiled image.</summary>
    VkImage Image = VK_NULL_HANDLE;
    /// <summary>Owns the color view used by the renderer.</summary>
    VkImageView View = VK_NULL_HANDLE;
    /// <summary>Owns dedicated device-local memory.</summary>
    VkDeviceMemory Memory = VK_NULL_HANDLE;
    /// <summary>Stores the allocated image extent independently of renderer target size.</summary>
    uint32_t Width, Height;
};
}
