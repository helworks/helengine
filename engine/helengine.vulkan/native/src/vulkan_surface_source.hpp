#pragma once
#include <vulkan/vulkan.h>

namespace helengine::vulkan {
/// <summary>Supplies a platform surface before physical-device selection; the provider must outlive its context.</summary>
class VulkanSurfaceSource {
public:
    /// <summary>Allows platform providers to release their own borrowed-window bookkeeping.</summary>
    virtual ~VulkanSurfaceSource()=default;
    /// <summary>Creates one surface for the supplied instance or throws without publishing partial ownership.</summary>
    virtual VkSurfaceKHR Create(VkInstance instance)=0;
    /// <summary>Destroys a successfully created surface after the context has retired device use.</summary>
    virtual void Destroy(VkInstance instance,VkSurfaceKHR surface) noexcept=0;
};
}
