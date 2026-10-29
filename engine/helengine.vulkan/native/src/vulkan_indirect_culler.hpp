#pragma once
#include "vulkan_context.hpp"
namespace helengine::vulkan {
/// <summary>Owns one compute pipeline shared by persistent static pages on a renderer.</summary>
class VulkanIndirectCuller final {
public:
    /// <summary>Loads the packaged culling program and creates its two-storage-buffer contract.</summary>
    VulkanIndirectCuller(VulkanContext& context,const std::string& shaderRoot);
    /// <summary>Releases pipeline before its layouts after all page use has completed.</summary>
    ~VulkanIndirectCuller();
    /// <summary>Prohibits copying native pipeline ownership.</summary>
    VulkanIndirectCuller(const VulkanIndirectCuller&)=delete;
    /// <summary>Prohibits assigning duplicated native handles.</summary>
    VulkanIndirectCuller& operator=(const VulkanIndirectCuller&)=delete;
    /// <summary>Borrows the storage layout used by every page descriptor set.</summary>
    VkDescriptorSetLayout GetDescriptors() const { return Descriptors; }
    /// <summary>Borrows the compute layout with matrix/count push constants.</summary>
    VkPipelineLayout GetLayout() const { return Layout; }
    /// <summary>Borrows the compiled compute pipeline.</summary>
    VkPipeline GetPipeline() const { return Pipeline; }
private:
    /// <summary>Cleans partial initialization and normal final ownership.</summary>
    void Release() noexcept;
    /// <summary>Borrows the owning logical device.</summary>
    VkDevice Device;
    /// <summary>Owns bounds and command storage binding declarations.</summary>
    VkDescriptorSetLayout Descriptors=VK_NULL_HANDLE;
    /// <summary>Owns compute push constant and descriptor compatibility.</summary>
    VkPipelineLayout Layout=VK_NULL_HANDLE;
    /// <summary>Owns the compiled frustum-selection pipeline.</summary>
    VkPipeline Pipeline=VK_NULL_HANDLE;
};
}
