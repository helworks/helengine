#pragma once
#include <vulkan/vulkan.h>
#include "vulkan_surface_source.hpp"
#include <cstdint>
#include <span>
#include <string>
#include <atomic>


namespace helengine::vulkan {
/// <summary>Owns Vulkan instance, selected graphics device, queue and transient commands; never requests a desktop surface implicitly.</summary>
class VulkanContext final {
public:
    /// <summary>Creates the requested Vulkan API; an explicit platform provider creates a surface before device selection so presentation capabilities constrain the selected device.</summary>
    explicit VulkanContext(std::span<const char* const> extensions = {}, bool validation = false, uint32_t minimumApi = VK_API_VERSION_1_1, VulkanSurfaceSource* surfaceSource = nullptr);
    /// <summary>Waits for owned GPU work, then releases resources in reverse initialization order.</summary>
    ~VulkanContext();
    /// <summary>Prevents duplicating ownership of native Vulkan handles.</summary>
    VulkanContext(const VulkanContext&) = delete;
    /// <summary>Prevents assignment of native Vulkan ownership.</summary>
    VulkanContext& operator=(const VulkanContext&) = delete;
    /// <summary>Exposes the instance for platform surface creation.</summary>
    VkInstance GetInstance() const { return Instance; }
    /// <summary>Exposes the selected hardware or software physical device for capability queries.</summary>
    VkPhysicalDevice GetPhysicalDevice() const { return PhysicalDevice; }
    /// <summary>Exposes the owned logical device for renderer resource creation.</summary>
    VkDevice GetDevice() const { return Device; }
    /// <summary>Exposes the graphics queue used by this single render thread.</summary>
    VkQueue GetQueue() const { return Queue; }
    /// <summary>Returns the family used for graphics command allocation and submission.</summary>
    uint32_t GetQueueFamily() const { return QueueFamily; }
    /// <summary>Returns the surface-compatible presentation queue, which can belong to a different family.</summary>
    VkQueue GetPresentQueue() const { return PresentQueue; }
    /// <summary>Returns the presentation family, or the graphics family for offscreen contexts.</summary>
    uint32_t GetPresentQueueFamily() const { return PresentQueueFamily; }
    /// <summary>Borrows the surface owned by this context; offscreen contexts expose a null handle.</summary>
    VkSurfaceKHR GetSurface() const { return Surface; }
    /// <summary>Returns the instance API explicitly negotiated by this context, independently of a newer physical-device capability.</summary>
    uint32_t GetApiVersion() const { return ApiVersion; }
    /// <summary>Reports the optional multi-draw feature actually enabled on the logical device.</summary>
    bool IsMultiDrawIndirectEnabled() const { return MultiDrawIndirectEnabled; }
    /// <summary>Reports compute support on the selected graphics command queue.</summary>
    bool IsGraphicsComputeEnabled() const { return GraphicsComputeEnabled; }
    /// <summary>Returns the selected device properties for truthful device diagnostics.</summary>
    const VkPhysicalDeviceProperties& GetProperties() const { return Properties; }
    /// <summary>Returns validation errors captured by the explicit debug messenger; tests reject nonzero counts.</summary>
    uint32_t GetValidationErrorCount() const { return ValidationErrors.load(); }
    /// <summary>Returns the sampled-resource lifetime generation used to reject recycled image and sampler handles.</summary>
    uint64_t GetSampledResourceRevision() const { return SampledResourceRevision; }
    /// <summary>Invalidates retained descriptors before sampled resource creation or retirement on the render thread.</summary>
    void InvalidateSampledResources();
    /// <summary>Finds a compatible memory type or rejects unsupported requested memory flags.</summary>
    uint32_t FindMemoryType(uint32_t bits, VkMemoryPropertyFlags flags) const;
    /// <summary>Allocates and begins one primary command buffer for a synchronous upload/render transaction.</summary>
    VkCommandBuffer BeginCommands();
    /// <summary>Submits commands and waits on a fence before freeing the command buffer or allowing resources to be reused.</summary>
    void SubmitAndWait(VkCommandBuffer commands, VkSemaphore wait = VK_NULL_HANDLE, VkSemaphore signal = VK_NULL_HANDLE);
    /// <summary>Throws a contextual native exception when a Vulkan operation does not succeed.</summary>
    static void Require(VkResult result, const char* operation);
private:
    /// <summary>Releases initialized handles, including partially initialized constructor state.</summary>
    void Release() noexcept;
    /// <summary>Counts validation errors and emits their original contextual messages without throwing through the Vulkan callback ABI.</summary>
    static VKAPI_ATTR VkBool32 VKAPI_CALL ReportValidation(VkDebugUtilsMessageSeverityFlagBitsEXT severity, VkDebugUtilsMessageTypeFlagsEXT type, const VkDebugUtilsMessengerCallbackDataEXT* data, void* userData);
    /// <summary>Owns the Vulkan loader instance.</summary>
    VkInstance Instance = VK_NULL_HANDLE;
    /// <summary>Borrows the selected physical graphics device from Instance.</summary>
    VkPhysicalDevice PhysicalDevice = VK_NULL_HANDLE;
    /// <summary>Owns the logical device used by all renderer resources.</summary>
    VkDevice Device = VK_NULL_HANDLE;
    /// <summary>Borrows the graphics queue from Device.</summary>
    VkQueue Queue = VK_NULL_HANDLE;
    /// <summary>Borrows the selected presentation queue from Device.</summary>
    VkQueue PresentQueue = VK_NULL_HANDLE;
    /// <summary>Owns the explicitly requested platform surface and outlives all device presentation resources.</summary>
    VkSurfaceKHR Surface = VK_NULL_HANDLE;
    /// <summary>Borrows the surface factory until the owned surface is destroyed; the host must outlive this context.</summary>
    VulkanSurfaceSource* SurfaceSource = nullptr;
    /// <summary>Owns command buffer allocation for synchronous transfers and draws.</summary>
    VkCommandPool CommandPool = VK_NULL_HANDLE;
    /// <summary>Stores the selected family index without inventing a fallback index.</summary>
    uint32_t QueueFamily = UINT32_MAX;
    /// <summary>Stores the selected surface-compatible present family.</summary>
    uint32_t PresentQueueFamily = UINT32_MAX;
    /// <summary>Stores the validated requested instance API for shader capability checks.</summary>
    uint32_t ApiVersion = VK_API_VERSION_1_0;
    /// <summary>Stores actual API/device capabilities for logging and feature checks.</summary>
    VkPhysicalDeviceProperties Properties {};
    /// <summary>Records enabled indirect multi-draw support rather than merely advertised capability.</summary>
    bool MultiDrawIndirectEnabled=false;
    /// <summary>Records whether graphics command buffers may dispatch compute work.</summary>
    bool GraphicsComputeEnabled=false;
    /// <summary>Owns the explicit validation messenger when diagnostic validation was requested.</summary>
    VkDebugUtilsMessengerEXT DebugMessenger = VK_NULL_HANDLE;
    /// <summary>Advances when sampled resources are created or retired; never wraps to an older descriptor generation.</summary>
    uint64_t SampledResourceRevision=0;
    /// <summary>Counts asynchronous driver-layer error messages for test and runtime diagnostics.</summary>
    std::atomic<uint32_t> ValidationErrors {0};
};
}
