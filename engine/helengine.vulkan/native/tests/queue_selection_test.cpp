#include "vulkan_queue_selection.hpp"
#include <array>
#include <iostream>
#include <stdexcept>

/// <summary>Verifies capability selection accepts separate graphics/present families and never chooses an empty or unsupported queue.</summary>
int main() {
    try {
        std::array<VkQueueFamilyProperties,3> families {}; families[0].queueFlags=VK_QUEUE_GRAPHICS_BIT; families[0].queueCount=1; families[1].queueCount=1;
        std::array<VkBool32,3> present {VK_FALSE,VK_TRUE,VK_FALSE};
        helengine::vulkan::VulkanQueueSelection selection(families,present,true);
        if (!selection.IsValid() || selection.GraphicsFamily!=0 || selection.PresentFamily!=1) { throw std::runtime_error("A valid separate graphics/present device was rejected."); }
        families[2].queueFlags=VK_QUEUE_GRAPHICS_BIT; families[2].queueCount=1; present[2]=VK_TRUE;
        selection=helengine::vulkan::VulkanQueueSelection(families,present,true);
        if (selection.GraphicsFamily!=2 || selection.PresentFamily!=2) { throw std::runtime_error("A combined graphics/present family should avoid unnecessary cross-family sharing."); }
        families[2].queueCount=0;
        selection=helengine::vulkan::VulkanQueueSelection(families,present,true);
        if (selection.GraphicsFamily!=0 || selection.PresentFamily!=1) { throw std::runtime_error("An empty queue family was selected."); }
        present.fill(VK_FALSE); selection=helengine::vulkan::VulkanQueueSelection(families,present,true);
        if (selection.IsValid()) { throw std::runtime_error("A presentation device with no present-capable family was accepted."); }
        selection=helengine::vulkan::VulkanQueueSelection(families,{},false);
        if (!selection.IsValid() || selection.GraphicsFamily!=0 || selection.PresentFamily!=0) { throw std::runtime_error("Offscreen rendering must not invent a surface requirement."); }
        std::cout << "PASS Vulkan queue selection graphics/present combined/separate empty-family offscreen\n"; return 0;
    } catch (const std::exception& error) { std::cerr << "FAIL: " << error.what() << '\n'; return 1; }
}
