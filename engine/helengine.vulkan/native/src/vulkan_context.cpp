#include "vulkan_context.hpp"
#include "vulkan_frame_trace_scope.hpp"
#include "vulkan_queue_selection.hpp"
#include "vulkan_extension_policy.hpp"
#include <algorithm>
#include <cstring>
#include <stdexcept>
#include <vector>
#include <iostream>

namespace helengine::vulkan {
void VulkanContext::Require(VkResult result, const char* operation) {
    if (result != VK_SUCCESS) { throw std::runtime_error(std::string(operation) + " failed: VkResult=" + std::to_string(result)); }
}

VulkanContext::VulkanContext(std::span<const char* const> extensions, bool validation, uint32_t minimumApi, VulkanSurfaceSource* surfaceSource) : SurfaceSource(surfaceSource) {
    try {
        uint32_t loaderVersion = VK_API_VERSION_1_0;
        auto enumerateVersion = reinterpret_cast<PFN_vkEnumerateInstanceVersion>(vkGetInstanceProcAddr(nullptr, "vkEnumerateInstanceVersion"));
        if (enumerateVersion) { Require(enumerateVersion(&loaderVersion), "vkEnumerateInstanceVersion"); }
        if (minimumApi < VK_API_VERSION_1_1 || loaderVersion < minimumApi) { throw std::runtime_error("Vulkan loader does not satisfy required API " + std::to_string(VK_VERSION_MAJOR(minimumApi)) + "." + std::to_string(VK_VERSION_MINOR(minimumApi)) + "."); }
        ApiVersion=minimumApi;
        const char* validationLayer = "VK_LAYER_KHRONOS_validation";
        if (validation) {
            uint32_t count = 0;
            Require(vkEnumerateInstanceLayerProperties(&count, nullptr), "vkEnumerateInstanceLayerProperties(count)");
            std::vector<VkLayerProperties> layers(count);
            Require(vkEnumerateInstanceLayerProperties(&count, layers.data()), "vkEnumerateInstanceLayerProperties");
            if (std::none_of(layers.begin(), layers.end(), [validationLayer](const auto& layer) { return std::strcmp(layer.layerName, validationLayer) == 0; })) {
                throw std::runtime_error("Explicit Vulkan validation requested but VK_LAYER_KHRONOS_validation is unavailable.");
            }
        }
        uint32_t instanceExtensionCount=0;
        Require(vkEnumerateInstanceExtensionProperties(nullptr,&instanceExtensionCount,nullptr),"vkEnumerateInstanceExtensionProperties(count)");
        std::vector<VkExtensionProperties> instanceExtensions(instanceExtensionCount);
        Require(vkEnumerateInstanceExtensionProperties(nullptr,&instanceExtensionCount,instanceExtensions.data()),"vkEnumerateInstanceExtensionProperties");
        const auto instanceFlags=VulkanExtensionPolicy::InstanceFlags(instanceExtensions);
        std::vector<const char*> enabledExtensions(extensions.begin(),extensions.end());
        if(instanceFlags && std::none_of(enabledExtensions.begin(),enabledExtensions.end(),[](const auto* name) { return std::strcmp(name,VK_KHR_PORTABILITY_ENUMERATION_EXTENSION_NAME)==0; })) enabledExtensions.push_back(VK_KHR_PORTABILITY_ENUMERATION_EXTENSION_NAME);
        if (validation) { enabledExtensions.push_back(VK_EXT_DEBUG_UTILS_EXTENSION_NAME); }
        VkDebugUtilsMessengerCreateInfoEXT debug {VK_STRUCTURE_TYPE_DEBUG_UTILS_MESSENGER_CREATE_INFO_EXT};
        debug.messageSeverity = VK_DEBUG_UTILS_MESSAGE_SEVERITY_WARNING_BIT_EXT | VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT;
        debug.messageType = VK_DEBUG_UTILS_MESSAGE_TYPE_GENERAL_BIT_EXT | VK_DEBUG_UTILS_MESSAGE_TYPE_VALIDATION_BIT_EXT | VK_DEBUG_UTILS_MESSAGE_TYPE_PERFORMANCE_BIT_EXT;
        debug.pfnUserCallback = ReportValidation; debug.pUserData = this;
        VkApplicationInfo application {VK_STRUCTURE_TYPE_APPLICATION_INFO};
        application.pApplicationName = "HelEngine Vulkan";
        application.apiVersion = minimumApi;
        VkInstanceCreateInfo create {VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO};
        create.pApplicationInfo = &application;
        create.flags=instanceFlags;
        create.enabledExtensionCount = static_cast<uint32_t>(enabledExtensions.size());
        create.ppEnabledExtensionNames = enabledExtensions.data();
        create.pNext = validation ? &debug : nullptr;
        create.enabledLayerCount = validation ? 1 : 0;
        create.ppEnabledLayerNames = validation ? &validationLayer : nullptr;
        Require(vkCreateInstance(&create, nullptr, &Instance), "vkCreateInstance");
        if (validation) {
            const auto createMessenger = reinterpret_cast<PFN_vkCreateDebugUtilsMessengerEXT>(vkGetInstanceProcAddr(Instance,"vkCreateDebugUtilsMessengerEXT"));
            if (!createMessenger) { throw std::runtime_error("Vulkan loader did not expose requested VK_EXT_debug_utils messenger entry point."); }
            Require(createMessenger(Instance,&debug,nullptr,&DebugMessenger),"vkCreateDebugUtilsMessengerEXT");
        }
        if (SurfaceSource) {
            Surface=SurfaceSource->Create(Instance);
            if (!Surface) { throw std::runtime_error("Vulkan surface provider returned a null surface."); }
        }
        uint32_t count = 0;
        Require(vkEnumeratePhysicalDevices(Instance, &count, nullptr), "vkEnumeratePhysicalDevices(count)");
        if (count == 0) { throw std::runtime_error("No Vulkan physical device is available."); }
        std::vector<VkPhysicalDevice> devices(count);
        Require(vkEnumeratePhysicalDevices(Instance, &count, devices.data()), "vkEnumeratePhysicalDevices");
        int bestScore = -1;
        std::vector<VkExtensionProperties> selectedExtensions;
        for (VkPhysicalDevice candidate : devices) {
            VkPhysicalDeviceProperties properties {};
            vkGetPhysicalDeviceProperties(candidate, &properties);
            if (properties.apiVersion < minimumApi) { continue; }
            uint32_t familyCount = 0;
            vkGetPhysicalDeviceQueueFamilyProperties(candidate, &familyCount, nullptr);
            std::vector<VkQueueFamilyProperties> families(familyCount);
            vkGetPhysicalDeviceQueueFamilyProperties(candidate, &familyCount, families.data());
            std::vector<VkBool32> presentation(familyCount,VK_FALSE);
            uint32_t extensionCount = 0;
            Require(vkEnumerateDeviceExtensionProperties(candidate,nullptr,&extensionCount,nullptr),"vkEnumerateDeviceExtensionProperties(count)");
            std::vector<VkExtensionProperties> deviceExtensions(extensionCount);
            Require(vkEnumerateDeviceExtensionProperties(candidate,nullptr,&extensionCount,deviceExtensions.data()),"vkEnumerateDeviceExtensionProperties");
            if (Surface) {
                if (std::none_of(deviceExtensions.begin(),deviceExtensions.end(),[](const auto& extension) { return std::strcmp(extension.extensionName,VK_KHR_SWAPCHAIN_EXTENSION_NAME)==0; })) { continue; }
                uint32_t formatCount=0,modeCount=0;
                Require(vkGetPhysicalDeviceSurfaceFormatsKHR(candidate,Surface,&formatCount,nullptr),"vkGetPhysicalDeviceSurfaceFormatsKHR(count)");
                Require(vkGetPhysicalDeviceSurfacePresentModesKHR(candidate,Surface,&modeCount,nullptr),"vkGetPhysicalDeviceSurfacePresentModesKHR(count)");
                if (!formatCount || !modeCount) { continue; }
                for (uint32_t index=0;index<familyCount;++index) {
                    Require(vkGetPhysicalDeviceSurfaceSupportKHR(candidate,index,Surface,&presentation[index]),"vkGetPhysicalDeviceSurfaceSupportKHR");
                }
            }
            const VulkanQueueSelection queues(families,presentation,Surface!=VK_NULL_HANDLE);
            if (!queues.IsValid()) { continue; }
            const int score = properties.deviceType == VK_PHYSICAL_DEVICE_TYPE_DISCRETE_GPU ? 3 : properties.deviceType == VK_PHYSICAL_DEVICE_TYPE_INTEGRATED_GPU ? 2 : 1;
            if (score > bestScore) {
                bestScore = score; PhysicalDevice = candidate; QueueFamily = queues.GraphicsFamily;
                PresentQueueFamily=queues.PresentFamily; Properties = properties; selectedExtensions=deviceExtensions;
                GraphicsComputeEnabled=(families[queues.GraphicsFamily].queueFlags&VK_QUEUE_COMPUTE_BIT)!=0;
            }
        }
        if (PhysicalDevice == VK_NULL_HANDLE) { throw std::runtime_error("No graphics device satisfies required Vulkan API " + std::to_string(VK_VERSION_MAJOR(minimumApi)) + "." + std::to_string(VK_VERSION_MINOR(minimumApi)) + "."); }
        float priority = 1;
        VkDeviceQueueCreateInfo queues[2] {};
        queues[0].sType=queues[1].sType=VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO;
        queues[0].queueFamilyIndex=QueueFamily; queues[1].queueFamilyIndex=PresentQueueFamily;
        queues[0].queueCount=queues[1].queueCount=1;
        queues[0].pQueuePriorities=queues[1].pQueuePriorities=&priority;
        VkDeviceCreateInfo device {VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO};
        device.queueCreateInfoCount = QueueFamily==PresentQueueFamily ? 1 : 2;
        device.pQueueCreateInfos = queues;
        const auto deviceExtensions=VulkanExtensionPolicy::DeviceExtensions(Surface!=VK_NULL_HANDLE,selectedExtensions);
        device.enabledExtensionCount=static_cast<uint32_t>(deviceExtensions.size());
        device.ppEnabledExtensionNames=deviceExtensions.data();
        VkPhysicalDeviceFeatures supported{},enabled{};
        vkGetPhysicalDeviceFeatures(PhysicalDevice,&supported);
        enabled.multiDrawIndirect=supported.multiDrawIndirect;
        device.pEnabledFeatures=&enabled;
        MultiDrawIndirectEnabled=enabled.multiDrawIndirect==VK_TRUE;
        Require(vkCreateDevice(PhysicalDevice, &device, nullptr, &Device), "vkCreateDevice(graphics)");
        vkGetDeviceQueue(Device, QueueFamily, 0, &Queue);
        vkGetDeviceQueue(Device, PresentQueueFamily, 0, &PresentQueue);
        VkCommandPoolCreateInfo pool {VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO};
        pool.flags = VK_COMMAND_POOL_CREATE_TRANSIENT_BIT;
        pool.queueFamilyIndex = QueueFamily;
        Require(vkCreateCommandPool(Device, &pool, nullptr, &CommandPool), "vkCreateCommandPool");
    } catch (...) { Release(); throw; }
}

VulkanContext::~VulkanContext() { Release(); }

void VulkanContext::Release() noexcept {
    if (Device) {
        vkDeviceWaitIdle(Device);
        if (CommandPool) { vkDestroyCommandPool(Device, CommandPool, nullptr); }
        vkDestroyDevice(Device, nullptr);
        Device = VK_NULL_HANDLE;
    }
    if (Surface) { SurfaceSource->Destroy(Instance,Surface); Surface=VK_NULL_HANDLE; }
    if (DebugMessenger) {
        const auto destroyMessenger = reinterpret_cast<PFN_vkDestroyDebugUtilsMessengerEXT>(vkGetInstanceProcAddr(Instance,"vkDestroyDebugUtilsMessengerEXT"));
        if (destroyMessenger) { destroyMessenger(Instance,DebugMessenger,nullptr); }
        DebugMessenger = VK_NULL_HANDLE;
    }
    if (Instance) { vkDestroyInstance(Instance, nullptr); Instance = VK_NULL_HANDLE; }
}

VKAPI_ATTR VkBool32 VKAPI_CALL VulkanContext::ReportValidation(VkDebugUtilsMessageSeverityFlagBitsEXT severity, VkDebugUtilsMessageTypeFlagsEXT, const VkDebugUtilsMessengerCallbackDataEXT* data, void* userData) {
    if (severity & VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT) { static_cast<VulkanContext*>(userData)->ValidationErrors.fetch_add(1); }
    std::cerr << "Vulkan validation: " << (data && data->pMessage ? data->pMessage : "message unavailable") << '\n';
    return VK_FALSE;
}

uint32_t VulkanContext::FindMemoryType(uint32_t bits, VkMemoryPropertyFlags flags) const {
    VkPhysicalDeviceMemoryProperties memory {};
    vkGetPhysicalDeviceMemoryProperties(PhysicalDevice, &memory);
    for (uint32_t index = 0; index < memory.memoryTypeCount; ++index) {
        if ((bits & (1U << index)) && (memory.memoryTypes[index].propertyFlags & flags) == flags) { return index; }
    }
    throw std::runtime_error("No Vulkan memory type satisfies flags=" + std::to_string(flags));
}

VkCommandBuffer VulkanContext::BeginCommands() {
    VkCommandBufferAllocateInfo allocate {VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO};
    allocate.commandPool = CommandPool; allocate.level = VK_COMMAND_BUFFER_LEVEL_PRIMARY; allocate.commandBufferCount = 1;
    VkCommandBuffer commands = VK_NULL_HANDLE;
    Require(vkAllocateCommandBuffers(Device, &allocate, &commands), "vkAllocateCommandBuffers");
    VkCommandBufferBeginInfo begin {VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO}; begin.flags = VK_COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT;
    VkResult result = vkBeginCommandBuffer(commands, &begin);
    if (result != VK_SUCCESS) { vkFreeCommandBuffers(Device, CommandPool, 1, &commands); Require(result, "vkBeginCommandBuffer"); }
    return commands;
}

void VulkanContext::InvalidateSampledResources() {
    if(SampledResourceRevision==UINT64_MAX) throw std::overflow_error("Vulkan sampled resource revision exhausted.");
    ++SampledResourceRevision;
}

void VulkanContext::SubmitAndWait(VkCommandBuffer commands, VkSemaphore wait, VkSemaphore signal) {
    VulkanFrameTraceScope SubmitTrace("Vulkan.SubmitAndWait");
    VkFence fence = VK_NULL_HANDLE;
    try {
        VulkanFrameTraceScope PrepareTrace("Vulkan.EndCommandsAndCreateFence");
        Require(vkEndCommandBuffer(commands), "vkEndCommandBuffer");
        VkFenceCreateInfo create {VK_STRUCTURE_TYPE_FENCE_CREATE_INFO};
        Require(vkCreateFence(Device, &create, nullptr, &fence), "vkCreateFence");
        const VkPipelineStageFlags waitStage = VK_PIPELINE_STAGE_TRANSFER_BIT;
        VkSubmitInfo submit {VK_STRUCTURE_TYPE_SUBMIT_INFO}; submit.commandBufferCount = 1; submit.pCommandBuffers = &commands;
        submit.waitSemaphoreCount = wait ? 1 : 0; submit.pWaitSemaphores = wait ? &wait : nullptr; submit.pWaitDstStageMask = wait ? &waitStage : nullptr;
        submit.signalSemaphoreCount = signal ? 1 : 0; submit.pSignalSemaphores = signal ? &signal : nullptr;
        PrepareTrace.Finish();
        VulkanFrameTraceScope QueueTrace("Vulkan.QueueSubmit");
        Require(vkQueueSubmit(Queue, 1, &submit, fence), "vkQueueSubmit");
        QueueTrace.Finish();
        VulkanFrameTraceScope WaitTrace("Vulkan.FenceWait");
        Require(vkWaitForFences(Device, 1, &fence, VK_TRUE, UINT64_MAX), "vkWaitForFences");
    } catch (...) {
        vkDeviceWaitIdle(Device);
        if (fence) { vkDestroyFence(Device, fence, nullptr); }
        vkFreeCommandBuffers(Device, CommandPool, 1, &commands);
        throw;
    }
    VulkanFrameTraceScope ReleaseTrace("Vulkan.ReleaseSubmissionResources");
    vkDestroyFence(Device, fence, nullptr);
    vkFreeCommandBuffers(Device, CommandPool, 1, &commands);
}
}
