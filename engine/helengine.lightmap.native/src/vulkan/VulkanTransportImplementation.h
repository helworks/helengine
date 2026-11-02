#pragma once
#include "VulkanTransport.h"
#include "VulkanBuffer.h"
#include "VulkanFunctions.h"
namespace hellm::detail {
/// <summary>Device state and reusable ray-query resources behind the Vulkan transport facade.</summary>
class VulkanTransport::Implementation {
public:
    /// <summary>Release resources in dependency order even after initialization fails.</summary>
    ~Implementation();
    /// <summary>Load Vulkan, select a capable device and prepare immutable geometry.</summary>
    void Initialize(const GpuScene &scene);
    /// <summary>Evaluate a validated batch using persistent static geometry and reusable host buffers.</summary>
    void Trace(const std::vector<GpuTexel> &texels, const std::vector<GpuCoverage> &coverages,
        const GpuParameters &parameters, std::vector<float> &rgba);
    /// <summary>Selected device name reported to the caller.</summary>
    std::string Name;
private:
    /// <summary>Platform loader library handle, released after Vulkan teardown.</summary>
    void *Library = nullptr;
    /// <summary>Global entry-point resolver supplied by the platform loader.</summary>
    PFN_vkGetInstanceProcAddr GetInstanceProcAddr = nullptr;
    /// <summary>Instance controlling physical-device enumeration.</summary>
    VkInstance Instance = VK_NULL_HANDLE;
    /// <summary>Selected Vulkan physical device.</summary>
    VkPhysicalDevice PhysicalDevice = VK_NULL_HANDLE;
    /// <summary>Logical device with required ray-query capabilities enabled.</summary>
    VkDevice Device = VK_NULL_HANDLE;
    /// <summary>Compute-capable queue for all scene builds and batch dispatches.</summary>
    VkQueue Queue = VK_NULL_HANDLE;
    /// <summary>Selected compute queue family.</summary>
    uint32_t QueueFamily = 0;
    /// <summary>Physical allocation type properties used by buffer creation.</summary>
    VkPhysicalDeviceMemoryProperties MemoryProperties{};
    /// <summary>Device limits validated before allocations and dispatch.</summary>
    VkPhysicalDeviceProperties Properties{};
    /// <summary>Acceleration-structure limits and scratch alignment.</summary>
    VkPhysicalDeviceAccelerationStructurePropertiesKHR AccelerationProperties{VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_ACCELERATION_STRUCTURE_PROPERTIES_KHR};
    /// <summary>Resettable command pool shared by initialization and batch dispatch.</summary>
    VkCommandPool CommandPool = VK_NULL_HANDLE;
    /// <summary>Single reusable primary command buffer.</summary>
    VkCommandBuffer Command = VK_NULL_HANDLE;
    /// <summary>Completion fence ensuring coherent readback and safe reuse.</summary>
    VkFence Fence = VK_NULL_HANDLE;
    /// <summary>Descriptor binding schema shared with the compute shader.</summary>
    VkDescriptorSetLayout DescriptorLayout = VK_NULL_HANDLE;
    /// <summary>Pool holding the single reusable scene descriptor set.</summary>
    VkDescriptorPool DescriptorPool = VK_NULL_HANDLE;
    /// <summary>Descriptor set refreshed when a batch buffer grows.</summary>
    VkDescriptorSet DescriptorSet = VK_NULL_HANDLE;
    /// <summary>Compute layout containing the 64-byte push-constant contract.</summary>
    VkPipelineLayout PipelineLayout = VK_NULL_HANDLE;
    /// <summary>Compiled ray-query irradiance compute pipeline.</summary>
    VkPipeline Pipeline = VK_NULL_HANDLE;
    /// <summary>Scene geometry bottom-level acceleration structure.</summary>
    VkAccelerationStructureKHR Blas = VK_NULL_HANDLE;
    /// <summary>Identity-instance top-level acceleration structure.</summary>
    VkAccelerationStructureKHR Tlas = VK_NULL_HANDLE;
    /// <summary>Backing allocations for acceleration structures and geometry.</summary>
    std::vector<std::unique_ptr<VulkanBuffer>> SceneBuffers;
    /// <summary>Static shader triangle buffer.</summary>
    VulkanBuffer *Triangles = nullptr;
    /// <summary>Static shader material buffer.</summary>
    VulkanBuffer *Materials = nullptr;
    /// <summary>Static shader texture pixel buffer.</summary>
    VulkanBuffer *Pixels = nullptr;
    /// <summary>Grow-only reusable coherent texel input buffer.</summary>
    std::unique_ptr<VulkanBuffer> Texels;
    /// <summary>Grow-only reusable coherent coverage input buffer.</summary>
    std::unique_ptr<VulkanBuffer> Coverages;
    /// <summary>Grow-only reusable coherent irradiance output buffer.</summary>
    std::unique_ptr<VulkanBuffer> Output;
    /// <summary>Dynamically resolved instance entry points.</summary>
#define HELLM_DECLARE_FUNCTION(name) PFN_vk##name name = nullptr;
    HELLM_VULKAN_INSTANCE_FUNCTIONS(HELLM_DECLARE_FUNCTION)
    /// <summary>Dynamically resolved device entry points.</summary>
    HELLM_VULKAN_DEVICE_FUNCTIONS(HELLM_DECLARE_FUNCTION)
#undef HELLM_DECLARE_FUNCTION
    /// <summary>Allocate buffer memory, checking descriptor range only for descriptor-bound allocations, and map coherent host inputs when requested.</summary>
    std::unique_ptr<VulkanBuffer> CreateBufferAllocation(VkDeviceSize size, VkBufferUsageFlags usage, bool host, bool descriptorBound);
    /// <summary>Upload immutable shader data and retain ownership for device lifetime.</summary>
    VulkanBuffer *Upload(const void *data, VkDeviceSize bytes, VkBufferUsageFlags usage);
    /// <summary>Return a buffer address after checking nonzero driver output.</summary>
    VkDeviceAddress Address(const VulkanBuffer &buffer) const;
    /// <summary>Reset the command pool and begin a fresh one-time submission.</summary>
    void Begin();
    /// <summary>Submit current commands and wait at most sixty seconds for completion.</summary>
    void Submit();
    /// <summary>Build one acceleration structure with an aligned temporary scratch allocation.</summary>
    VkAccelerationStructureKHR Build(VkAccelerationStructureTypeKHR type,
        const VkAccelerationStructureGeometryKHR &geometry, uint32_t primitives);
    /// <summary>Create scene descriptors and the embedded SPIR-V compute pipeline.</summary>
    void CreatePipeline();
};
} // namespace hellm::detail
