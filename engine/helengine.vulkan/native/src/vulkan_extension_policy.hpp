#pragma once
#include <vulkan/vulkan.h>
#include <array>
#include <cstring>
#include <span>
#include <vector>

namespace helengine::vulkan {
/// <summary>Enables portability contracts only when the selected loader/device advertises them, independent of operating-system names.</summary>
class VulkanExtensionPolicy final {
public:
    /// <summary>Finds an exact supported extension name without retaining pointers into driver enumeration storage.</summary>
    static bool Has(std::span<const VkExtensionProperties> extensions,const char* name) {
        for(const auto& extension:extensions) if(std::strcmp(extension.extensionName,name)==0) return true;
        return false;
    }
    /// <summary>Allows enumeration of portability devices only when the loader exposes the required instance extension.</summary>
    static VkInstanceCreateFlags InstanceFlags(std::span<const VkExtensionProperties> extensions) {
        return Has(extensions,VK_KHR_PORTABILITY_ENUMERATION_EXTENSION_NAME)?VK_INSTANCE_CREATE_ENUMERATE_PORTABILITY_BIT_KHR:0;
    }
    /// <summary>Requests swapchain support for presentation and the mandatory subset contract for devices advertising it.</summary>
    static std::vector<const char*> DeviceExtensions(bool presentation,std::span<const VkExtensionProperties> extensions) {
        std::vector<const char*> result;
        if(presentation) result.push_back(VK_KHR_SWAPCHAIN_EXTENSION_NAME);
        if(Has(extensions,"VK_KHR_portability_subset")) result.push_back("VK_KHR_portability_subset");
        return result;
    }
};
}
