#include "vulkan_context.hpp"
#include <iostream>
#include <stdexcept>
using namespace helengine::vulkan;
/// <summary>Exercises construction failure through the real provider boundary without creating a desktop window.</summary>
class FailedSurface final : public VulkanSurfaceSource {
public:
    /// <summary>Counts real factory calls after Vulkan instance creation.</summary>
    int Creates=0;
    /// <summary>Rejects destruction of unpublished surface ownership.</summary>
    int Destroys=0;
    /// <summary>Selects an original exception rather than an invalid null return.</summary>
    bool Throw=false;
    /// <summary>Fails deterministically without owning any native surface.</summary>
    VkSurfaceKHR Create(VkInstance instance) override { if(!instance) throw std::logic_error("Factory received no instance."); ++Creates;if(Throw) throw std::runtime_error("source failure");return VK_NULL_HANDLE; }
    /// <summary>Counts any attempted destruction, which must remain zero after failed construction.</summary>
    void Destroy(VkInstance,VkSurfaceKHR) noexcept override { ++Destroys; }
};
/// <summary>Links and runs the portable device owner with no SDL/Wayland libraries, checking offscreen and partial initialization.</summary>
int main() {
    try {
        { VulkanContext context({},true,VK_API_VERSION_1_2);if(!context.GetDevice() || context.GetValidationErrorCount()) throw std::runtime_error("Invalid portable offscreen context."); }
        FailedSurface source;
        for(int mode=0;mode<2;++mode) {
            source.Throw=mode!=0;bool failed=false;
            try { VulkanContext context({},true,VK_API_VERSION_1_2,&source); }
            catch(const std::runtime_error& error) { failed=std::string(error.what())==(mode?"source failure":"Vulkan surface provider returned a null surface."); }
            if(!failed || source.Creates!=mode+1 || source.Destroys) throw std::runtime_error("Surface failure ownership or error detail changed.");
        }
        std::cout<<"PASS portable device, null surface and source failure cleanup\n";return 0;
    } catch(const std::exception& error) {std::cerr<<error.what()<<'\n';return 1;}
}
