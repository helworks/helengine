#pragma once
#include <vulkan/vulkan.h>
#include <span>
#include <stdexcept>

namespace helengine::vulkan {
/// <summary>Selects nonempty queue families from queried capabilities, preferring a shared graphics/present family while accepting valid separate families.</summary>
class VulkanQueueSelection final {
public:
    /// <summary>Plans queue ownership from actual family and surface-support snapshots; missing required capabilities leave an invalid selection.</summary>
    VulkanQueueSelection(std::span<const VkQueueFamilyProperties> families,std::span<const VkBool32> present,bool requirePresentation) {
        if (requirePresentation && present.size()!=families.size()) { throw std::invalid_argument("Vulkan surface queue capabilities must match the queried queue family count."); }
        for (uint32_t index=0;index<families.size();++index) {
            if (!families[index].queueCount) { continue; }
            const bool graphics=(families[index].queueFlags & VK_QUEUE_GRAPHICS_BIT)!=0;
            const bool canPresent=requirePresentation && present[index];
            if (graphics && GraphicsFamily==UINT32_MAX) { GraphicsFamily=index; }
            if (canPresent && PresentFamily==UINT32_MAX) { PresentFamily=index; }
            if (graphics && canPresent) { GraphicsFamily=PresentFamily=index; return; }
        }
        if (!requirePresentation) { PresentFamily=GraphicsFamily; }
    }
    /// <summary>Reports whether every required queue capability was actually found.</summary>
    bool IsValid() const { return GraphicsFamily!=UINT32_MAX && PresentFamily!=UINT32_MAX; }
    /// <summary>Stores the selected graphics family; UINT32_MAX means no usable graphics queue.</summary>
    uint32_t GraphicsFamily=UINT32_MAX;
    /// <summary>Stores the present family or the graphics family for a surface-free offscreen context.</summary>
    uint32_t PresentFamily=UINT32_MAX;
};
}
