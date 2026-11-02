#include "vulkan_extension_policy.hpp"
#include <cstring>
#include <stdexcept>
#include <iostream>
using namespace helengine::vulkan;
/// <summary>Verifies runtime extension discovery without assuming all Apple drivers share portability requirements.</summary>
int main() {
    try {
        VkExtensionProperties subset{},enumeration{};
        std::strcpy(subset.extensionName,"VK_KHR_portability_subset");
        std::strcpy(enumeration.extensionName,VK_KHR_PORTABILITY_ENUMERATION_EXTENSION_NAME);
        if(VulkanExtensionPolicy::InstanceFlags({}) || VulkanExtensionPolicy::DeviceExtensions(false,{}).size()) throw std::runtime_error("Absent portability extensions were enabled.");
        const std::array<VkExtensionProperties,1> instance{enumeration},device{subset};
        if(VulkanExtensionPolicy::InstanceFlags(instance)!=VK_INSTANCE_CREATE_ENUMERATE_PORTABILITY_BIT_KHR) throw std::runtime_error("Advertised portability enumeration was ignored.");
        const auto offscreen=VulkanExtensionPolicy::DeviceExtensions(false,device);
        const auto present=VulkanExtensionPolicy::DeviceExtensions(true,device);
        if(offscreen.size()!=1 || std::strcmp(offscreen[0],"VK_KHR_portability_subset") || present.size()!=2 || std::strcmp(present[0],VK_KHR_SWAPCHAIN_EXTENSION_NAME)) throw std::runtime_error("Surface/device extension negotiation is incorrect.");
        std::cout<<"PASS Vulkan runtime extension policy\n";return 0;
    } catch(const std::exception& error) {std::cerr<<error.what()<<'\n';return 1;}
}
