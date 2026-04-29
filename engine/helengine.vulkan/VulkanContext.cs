using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace helengine.vulkan {
    /// <summary>
    /// Owns the Vulkan instance, device, and queue used by the renderer.
    /// </summary>
    public sealed unsafe class VulkanContext : IDisposable {
        /// <summary>
        /// Vulkan instance extension required for presentation surfaces.
        /// </summary>
        const string VulkanSurfaceExtension = "VK_KHR_surface";
        /// <summary>
        /// Vulkan device extension required for swapchain presentation.
        /// </summary>
        const string VulkanSwapchainExtension = "VK_KHR_swapchain";
        /// <summary>
        /// Vulkan instance extension required for Win32 surfaces.
        /// </summary>
        const string VulkanWin32SurfaceExtension = "VK_KHR_win32_surface";

        /// <summary>
        /// API entry point for Vulkan calls.
        /// </summary>
        readonly Vk api;
        /// <summary>
        /// Vulkan instance handle.
        /// </summary>
        Instance instance;
        /// <summary>
        /// Selected physical device providing GPU access.
        /// </summary>
        PhysicalDevice physicalDevice;
        /// <summary>
        /// Logical device created from the selected physical device.
        /// </summary>
        Device device;
        /// <summary>
        /// Graphics queue for submitting render commands.
        /// </summary>
        Queue graphicsQueue;
        /// <summary>
        /// Queue family index used for graphics submissions.
        /// </summary>
        uint graphicsQueueFamilyIndex;
        /// <summary>
        /// Surface extension for presentation queries.
        /// </summary>
        KhrSurface khrSurface = null!;
        /// <summary>
        /// Swapchain extension for presentation surfaces.
        /// </summary>
        KhrSwapchain khrSwapchain = null!;
        /// <summary>
        /// Command pool used for transient upload operations.
        /// </summary>
        CommandPool transferCommandPool;
        /// <summary>
        /// Cached physical device memory properties.
        /// </summary>
        PhysicalDeviceMemoryProperties memoryProperties;
        /// <summary>
        /// Tracks whether the context has been disposed.
        /// </summary>
        bool disposed;

        /// <summary>
        /// Initializes the Vulkan instance, device, and default command pool.
        /// </summary>
        public VulkanContext() {
            api = Vk.GetApi();
            CreateInstance();
            PickPhysicalDevice();
            CreateLogicalDevice();
            CreateCommandPool();
        }

        /// <summary>
        /// Gets the Vulkan API entry point.
        /// </summary>
        public Vk Api { get { return api; } }

        /// <summary>
        /// Gets the Vulkan instance handle.
        /// </summary>
        public Instance Instance { get { return instance; } }

        /// <summary>
        /// Gets the selected physical device.
        /// </summary>
        public PhysicalDevice PhysicalDevice { get { return physicalDevice; } }

        /// <summary>
        /// Gets the logical device handle.
        /// </summary>
        public Device Device { get { return device; } }

        /// <summary>
        /// Gets the graphics queue.
        /// </summary>
        public Queue GraphicsQueue { get { return graphicsQueue; } }

        /// <summary>
        /// Gets the graphics queue family index.
        /// </summary>
        public uint GraphicsQueueFamilyIndex { get { return graphicsQueueFamilyIndex; } }

        /// <summary>
        /// Gets the surface extension loader for this instance.
        /// </summary>
        public KhrSurface SurfaceExtension { get { return khrSurface; } }

        /// <summary>
        /// Gets the swapchain extension loader for this device.
        /// </summary>
        public KhrSwapchain SwapchainExtension { get { return khrSwapchain; } }

        /// <summary>
        /// Gets the transient command pool used for uploads.
        /// </summary>
        public CommandPool TransferCommandPool { get { return transferCommandPool; } }

        /// <summary>
        /// Locates a memory type index that satisfies the requested flags.
        /// </summary>
        /// <param name="typeFilter">Bitmask of supported memory types.</param>
        /// <param name="properties">Required memory properties.</param>
        /// <returns>Index of a compatible memory type.</returns>
        public uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties) {
            for (uint i = 0; i < memoryProperties.MemoryTypeCount; i++) {
                if ((typeFilter & (1u << (int)i)) != 0 &&
                    (memoryProperties.MemoryTypes[(int)i].PropertyFlags & properties) == properties) {
                    return i;
                }
            }

            throw new InvalidOperationException("Unable to find a compatible Vulkan memory type.");
        }

        /// <summary>
        /// Begins a single-use command buffer for immediate GPU work.
        /// </summary>
        /// <returns>Command buffer ready for recording.</returns>
        public unsafe CommandBuffer BeginSingleTimeCommands() {
            CommandBufferAllocateInfo allocInfo = new CommandBufferAllocateInfo {
                SType = StructureType.CommandBufferAllocateInfo,
                Level = CommandBufferLevel.Primary,
                CommandPool = transferCommandPool,
                CommandBufferCount = 1
            };

            CommandBuffer commandBuffer;
            api.AllocateCommandBuffers(device, allocInfo, out commandBuffer);

            CommandBufferBeginInfo beginInfo = new CommandBufferBeginInfo {
                SType = StructureType.CommandBufferBeginInfo,
                Flags = CommandBufferUsageFlags.CommandBufferUsageOneTimeSubmitBit
            };

            api.BeginCommandBuffer(commandBuffer, beginInfo);
            return commandBuffer;
        }

        /// <summary>
        /// Ends and submits a single-use command buffer, waiting for completion.
        /// </summary>
        /// <param name="commandBuffer">Command buffer to submit.</param>
        public unsafe void EndSingleTimeCommands(CommandBuffer commandBuffer) {
            api.EndCommandBuffer(commandBuffer);

            SubmitInfo submitInfo = new SubmitInfo {
                SType = StructureType.SubmitInfo,
                CommandBufferCount = 1,
                PCommandBuffers = &commandBuffer
            };

            Result submitResult = api.QueueSubmit(graphicsQueue, 1, in submitInfo, default);
            if (submitResult != Result.Success) {
                throw new InvalidOperationException($"Vulkan queue submit failed: {submitResult}.");
            }
            api.QueueWaitIdle(graphicsQueue);

            api.FreeCommandBuffers(device, transferCommandPool, 1, in commandBuffer);
        }

        /// <summary>
        /// Releases Vulkan resources owned by the context.
        /// </summary>
        public void Dispose() {
            if (disposed) {
                return;
            }

            disposed = true;

            if (device.Handle != 0) {
                api.DeviceWaitIdle(device);
                api.DestroyCommandPool(device, transferCommandPool, null);
                api.DestroyDevice(device, null);
            }

            if (instance.Handle != 0) {
                api.DestroyInstance(instance, null);
            }
        }

        /// <summary>
        /// Creates the Vulkan instance and loads extension entry points.
        /// </summary>
        unsafe void CreateInstance() {
            string[] extensions = GetRequiredInstanceExtensions();
            nint extensionsPtr = SilkMarshal.StringArrayToPtr(extensions);

            ApplicationInfo appInfo = new ApplicationInfo {
                SType = StructureType.ApplicationInfo,
                PApplicationName = (byte*)SilkMarshal.StringToPtr("helengine"),
                ApplicationVersion = new Version32(1, 0, 0),
                PEngineName = (byte*)SilkMarshal.StringToPtr("helengine"),
                EngineVersion = new Version32(1, 0, 0),
                ApiVersion = Vk.Version12
            };

            InstanceCreateInfo createInfo = new InstanceCreateInfo {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &appInfo,
                EnabledExtensionCount = (uint)extensions.Length,
                PpEnabledExtensionNames = (byte**)extensionsPtr
            };

            Result result = api.CreateInstance(createInfo, null, out instance);
            SilkMarshal.Free((nint)appInfo.PApplicationName);
            SilkMarshal.Free((nint)appInfo.PEngineName);
            SilkMarshal.Free(extensionsPtr);

            if (result != Result.Success) {
                throw new InvalidOperationException($"Vulkan instance creation failed: {result}.");
            }

            if (!api.TryGetInstanceExtension(instance, out khrSurface)) {
                throw new InvalidOperationException("Failed to load VK_KHR_surface extension.");
            }
        }

        /// <summary>
        /// Selects a physical device with graphics queue support.
        /// </summary>
        unsafe void PickPhysicalDevice() {
            uint deviceCount = 0;
            api.EnumeratePhysicalDevices(instance, ref deviceCount, null);
            if (deviceCount == 0) {
                throw new InvalidOperationException("No Vulkan physical devices were found.");
            }

            var devices = new PhysicalDevice[deviceCount];
            fixed (PhysicalDevice* devicesPtr = devices) {
                api.EnumeratePhysicalDevices(instance, ref deviceCount, devicesPtr);
            }

            for (int i = 0; i < devices.Length; i++) {
                uint familyIndex;
                if (TryGetGraphicsQueueFamily(devices[i], out familyIndex)) {
                    physicalDevice = devices[i];
                    graphicsQueueFamilyIndex = familyIndex;
                    api.GetPhysicalDeviceMemoryProperties(physicalDevice, out memoryProperties);
                    return;
                }
            }

            throw new InvalidOperationException("No Vulkan physical device with graphics support was found.");
        }

        /// <summary>
        /// Creates the logical device and graphics queue.
        /// </summary>
        unsafe void CreateLogicalDevice() {
            float queuePriority = 1.0f;
            DeviceQueueCreateInfo queueCreateInfo = new DeviceQueueCreateInfo {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = graphicsQueueFamilyIndex,
                QueueCount = 1,
                PQueuePriorities = &queuePriority
            };

            string[] deviceExtensions = new[] { VulkanSwapchainExtension };
            nint extensionsPtr = SilkMarshal.StringArrayToPtr(deviceExtensions);

            DeviceCreateInfo createInfo = new DeviceCreateInfo {
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = 1,
                PQueueCreateInfos = &queueCreateInfo,
                EnabledExtensionCount = (uint)deviceExtensions.Length,
                PpEnabledExtensionNames = (byte**)extensionsPtr
            };

            Result result = api.CreateDevice(physicalDevice, createInfo, null, out device);
            SilkMarshal.Free(extensionsPtr);

            if (result != Result.Success) {
                throw new InvalidOperationException($"Vulkan device creation failed: {result}.");
            }

            api.GetDeviceQueue(device, graphicsQueueFamilyIndex, 0, out graphicsQueue);

            if (!api.TryGetDeviceExtension(instance, device, out khrSwapchain)) {
                throw new InvalidOperationException("Failed to load VK_KHR_swapchain extension.");
            }
        }

        /// <summary>
        /// Creates a command pool used for one-off upload commands.
        /// </summary>
        unsafe void CreateCommandPool() {
            CommandPoolCreateInfo poolInfo = new CommandPoolCreateInfo {
                SType = StructureType.CommandPoolCreateInfo,
                Flags = CommandPoolCreateFlags.CommandPoolCreateTransientBit,
                QueueFamilyIndex = graphicsQueueFamilyIndex
            };

            Result result = api.CreateCommandPool(device, poolInfo, null, out transferCommandPool);
            if (result != Result.Success) {
                throw new InvalidOperationException($"Vulkan command pool creation failed: {result}.");
            }
        }

        /// <summary>
        /// Checks for a graphics-capable queue family on a physical device.
        /// </summary>
        /// <param name="device">Physical device to inspect.</param>
        /// <param name="queueFamilyIndex">Queue family index when found.</param>
        /// <returns>True when a graphics queue family is available.</returns>
        unsafe bool TryGetGraphicsQueueFamily(PhysicalDevice device, out uint queueFamilyIndex) {
            uint queueFamilyCount = 0;
            api.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, null);

            var queueFamilies = new QueueFamilyProperties[queueFamilyCount];
            fixed (QueueFamilyProperties* queueFamiliesPtr = queueFamilies) {
                api.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, queueFamiliesPtr);
            }

            for (uint i = 0; i < queueFamilyCount; i++) {
                if ((queueFamilies[i].QueueFlags & QueueFlags.QueueGraphicsBit) != 0) {
                    queueFamilyIndex = i;
                    return true;
                }
            }

            queueFamilyIndex = 0;
            return false;
        }

        /// <summary>
        /// Gets the instance extensions required for presentation on this platform.
        /// </summary>
        /// <returns>Array of instance extension names.</returns>
        string[] GetRequiredInstanceExtensions() {
            if (!OperatingSystem.IsWindows()) {
                throw new PlatformNotSupportedException("Vulkan renderer currently requires Win32 surface support.");
            }

            return new[] { VulkanSurfaceExtension, VulkanWin32SurfaceExtension };
        }
    }
}
