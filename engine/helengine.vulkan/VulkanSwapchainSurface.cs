using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using VulkanSemaphore = Silk.NET.Vulkan.Semaphore;

namespace helengine.vulkan {
    /// <summary>
    /// Manages a Vulkan swapchain and per-frame resources for a window surface.
    /// </summary>
    public sealed unsafe class VulkanSwapchainSurface : IDisposable {
        /// <summary>
        /// Maximum number of frames to keep in flight.
        /// </summary>
        const int MaxFramesInFlight = 2;

        /// <summary>
        /// Shared Vulkan context for device access.
        /// </summary>
        readonly VulkanContext context;
        /// <summary>
        /// Native window handle used for surface creation.
        /// </summary>
        readonly IntPtr windowHandle;
        /// <summary>
        /// Presentation surface for the window.
        /// </summary>
        SurfaceKHR surface;
        /// <summary>
        /// Swapchain used to present images.
        /// </summary>
        SwapchainKHR swapchain;
        /// <summary>
        /// Swapchain images retrieved from the presentation layer.
        /// </summary>
        Image[] images = Array.Empty<Image>();
        /// <summary>
        /// Image views for the swapchain images.
        /// </summary>
        ImageView[] imageViews = Array.Empty<ImageView>();
        /// <summary>
        /// Depth buffer image.
        /// </summary>
        Image depthImage;
        /// <summary>
        /// Device memory backing the depth buffer.
        /// </summary>
        DeviceMemory depthMemory;
        /// <summary>
        /// Image view for the depth buffer.
        /// </summary>
        ImageView depthView;
        /// <summary>
        /// Render pass used for drawing to the swapchain.
        /// </summary>
        RenderPass renderPass;
        /// <summary>
        /// Framebuffers matching each swapchain image view.
        /// </summary>
        Framebuffer[] framebuffers = Array.Empty<Framebuffer>();
        /// <summary>
        /// Command pool used for render command buffers.
        /// </summary>
        CommandPool commandPool;
        /// <summary>
        /// Command buffers used for per-image rendering.
        /// </summary>
        CommandBuffer[] commandBuffers = Array.Empty<CommandBuffer>();
        /// <summary>
        /// Semaphores signaled when images are available for rendering.
        /// </summary>
        VulkanSemaphore[] imageAvailableSemaphores = Array.Empty<VulkanSemaphore>();
        /// <summary>
        /// Semaphores signaled when rendering is finished.
        /// </summary>
        VulkanSemaphore[] renderFinishedSemaphores = Array.Empty<VulkanSemaphore>();
        /// <summary>
        /// Fences used to throttle frames in flight.
        /// </summary>
        Fence[] frameFences = Array.Empty<Fence>();
        /// <summary>
        /// Tracks which fence owns a swapchain image.
        /// </summary>
        Fence[] imagesInFlight = Array.Empty<Fence>();
        /// <summary>
        /// Swapchain image format.
        /// </summary>
        Format imageFormat;
        /// <summary>
        /// Swapchain extent in pixels.
        /// </summary>
        Extent2D extent;
        /// <summary>
        /// Logical width used by UI layout before DPI scaling.
        /// </summary>
        int logicalWidth;
        /// <summary>
        /// Logical height used by UI layout before DPI scaling.
        /// </summary>
        int logicalHeight;
        /// <summary>
        /// Current frame index for in-flight tracking.
        /// </summary>
        int frameIndex;
        /// <summary>
        /// Version counter incremented when swapchain resources are recreated.
        /// </summary>
        int swapchainVersion;
        /// <summary>
        /// Tracks whether the surface has been disposed.
        /// </summary>
        bool disposed;

        /// <summary>
        /// Creates a swapchain surface for the given window handle.
        /// </summary>
        /// <param name="context">Shared Vulkan context.</param>
        /// <param name="windowHandle">Native window handle.</param>
        /// <param name="width">Initial logical width before DPI scaling.</param>
        /// <param name="height">Initial logical height before DPI scaling.</param>
        public VulkanSwapchainSurface(VulkanContext context, IntPtr windowHandle, int width, int height) {
            this.context = context;
            this.windowHandle = windowHandle;
            if (width <= 0) {
                throw new ArgumentOutOfRangeException(nameof(width), "Swapchain width must be greater than zero.");
            }
            if (height <= 0) {
                throw new ArgumentOutOfRangeException(nameof(height), "Swapchain height must be greater than zero.");
            }
            logicalWidth = width;
            logicalHeight = height;
            CreateSurface();
            CreateSwapchain(width, height, default);
            CreateImageViews();
            CreateDepthResources();
            CreateRenderPass();
            CreateFramebuffers();
            CreateCommandPool();
            CreateCommandBuffers();
            CreateSyncObjects();
        }

        /// <summary>
        /// Gets the native window handle for this surface.
        /// </summary>
        public IntPtr WindowHandle { get { return windowHandle; } }

        /// <summary>
        /// Gets the swapchain image format.
        /// </summary>
        public Format ImageFormat { get { return imageFormat; } }

        /// <summary>
        /// Gets the swapchain extent in pixels.
        /// </summary>
        public Extent2D Extent { get { return extent; } }

        /// <summary>
        /// Gets the logical width used by UI layout.
        /// </summary>
        public int LogicalWidth { get { return logicalWidth; } }

        /// <summary>
        /// Gets the logical height used by UI layout.
        /// </summary>
        public int LogicalHeight { get { return logicalHeight; } }

        /// <summary>
        /// Gets the render pass used for drawing.
        /// </summary>
        public RenderPass RenderPass { get { return renderPass; } }

        /// <summary>
        /// Gets the framebuffer list for swapchain images.
        /// </summary>
        public Framebuffer[] Framebuffers { get { return framebuffers; } }

        /// <summary>
        /// Gets the swapchain version counter.
        /// </summary>
        public int SwapchainVersion { get { return swapchainVersion; } }

        /// <summary>
        /// Begins a new frame, acquiring a swapchain image and recording command buffer.
        /// </summary>
        /// <param name="imageIndex">Index of the acquired swapchain image.</param>
        /// <param name="commandBuffer">Command buffer ready for recording.</param>
        /// <returns>True if the frame can proceed.</returns>
        public unsafe bool BeginFrame(out uint imageIndex, out CommandBuffer commandBuffer) {
            commandBuffer = default;
            imageIndex = 0;

            context.Api.WaitForFences(context.Device, 1, in frameFences[frameIndex], true, ulong.MaxValue);

            Result acquireResult = context.SwapchainExtension.AcquireNextImage(
                context.Device,
                swapchain,
                ulong.MaxValue,
                imageAvailableSemaphores[frameIndex],
                default,
                ref imageIndex);

            if (acquireResult == Result.ErrorOutOfDateKhr) {
                return false;
            }

            if (acquireResult != Result.Success && acquireResult != Result.SuboptimalKhr) {
                throw new InvalidOperationException($"Failed to acquire swapchain image: {acquireResult}.");
            }

            if (imagesInFlight[imageIndex].Handle != 0) {
                context.Api.WaitForFences(context.Device, 1, in imagesInFlight[imageIndex], true, ulong.MaxValue);
            }

            imagesInFlight[imageIndex] = frameFences[frameIndex];
            context.Api.ResetFences(context.Device, 1, in frameFences[frameIndex]);

            context.Api.ResetCommandBuffer(commandBuffers[imageIndex], 0);

            CommandBufferBeginInfo beginInfo = new CommandBufferBeginInfo {
                SType = StructureType.CommandBufferBeginInfo
            };

            context.Api.BeginCommandBuffer(commandBuffers[imageIndex], beginInfo);
            commandBuffer = commandBuffers[imageIndex];
            return true;
        }

        /// <summary>
        /// Begins the render pass for the current swapchain image.
        /// </summary>
        /// <param name="commandBuffer">Command buffer being recorded.</param>
        /// <param name="imageIndex">Swapchain image index.</param>
        /// <param name="clearR">Clear color red component.</param>
        /// <param name="clearG">Clear color green component.</param>
        /// <param name="clearB">Clear color blue component.</param>
        /// <param name="clearA">Clear color alpha component.</param>
        public unsafe void BeginRenderPass(CommandBuffer commandBuffer, uint imageIndex, float clearR, float clearG, float clearB, float clearA) {
            ClearValue* clearValues = stackalloc ClearValue[2];
            clearValues[0] = new ClearValue {
                Color = new ClearColorValue(clearR, clearG, clearB, clearA)
            };
            clearValues[1] = new ClearValue {
                DepthStencil = new ClearDepthStencilValue(1f, 0)
            };

            RenderPassBeginInfo renderPassInfo = new RenderPassBeginInfo {
                SType = StructureType.RenderPassBeginInfo,
                RenderPass = renderPass,
                Framebuffer = framebuffers[imageIndex],
                RenderArea = new Rect2D { Extent = extent },
                ClearValueCount = 2,
                PClearValues = clearValues
            };

            context.Api.CmdBeginRenderPass(commandBuffer, renderPassInfo, SubpassContents.Inline);
        }

        /// <summary>
        /// Ends the active render pass.
        /// </summary>
        /// <param name="commandBuffer">Command buffer being recorded.</param>
        public void EndRenderPass(CommandBuffer commandBuffer) {
            context.Api.CmdEndRenderPass(commandBuffer);
        }

        /// <summary>
        /// Ends recording, submits the frame, and presents the swapchain image.
        /// </summary>
        /// <param name="commandBuffer">Command buffer used for rendering.</param>
        /// <param name="imageIndex">Swapchain image index.</param>
        public unsafe void EndFrame(CommandBuffer commandBuffer, uint imageIndex) {
            context.Api.EndCommandBuffer(commandBuffer);

            VulkanSemaphore* waitSemaphores = stackalloc VulkanSemaphore[] { imageAvailableSemaphores[frameIndex] };
            VulkanSemaphore* signalSemaphores = stackalloc VulkanSemaphore[] { renderFinishedSemaphores[frameIndex] };
            PipelineStageFlags* waitStages = stackalloc PipelineStageFlags[] { PipelineStageFlags.PipelineStageColorAttachmentOutputBit };

            SubmitInfo submitInfo = new SubmitInfo {
                SType = StructureType.SubmitInfo,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = waitSemaphores,
                PWaitDstStageMask = waitStages,
                CommandBufferCount = 1,
                PCommandBuffers = &commandBuffer,
                SignalSemaphoreCount = 1,
                PSignalSemaphores = signalSemaphores
            };

            Result submitResult = context.Api.QueueSubmit(context.GraphicsQueue, 1, in submitInfo, frameFences[frameIndex]);
            if (submitResult != Result.Success) {
                throw new InvalidOperationException($"Vulkan queue submit failed: {submitResult}.");
            }

            SwapchainKHR* swapchains = stackalloc SwapchainKHR[] { swapchain };

            PresentInfoKHR presentInfo = new PresentInfoKHR {
                SType = StructureType.PresentInfoKhr,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = signalSemaphores,
                SwapchainCount = 1,
                PSwapchains = swapchains,
                PImageIndices = &imageIndex
            };

            Result presentResult = context.SwapchainExtension.QueuePresent(context.GraphicsQueue, presentInfo);
            if (presentResult == Result.ErrorOutOfDateKhr || presentResult == Result.SuboptimalKhr) {
                Recreate(logicalWidth, logicalHeight);
            } else if (presentResult != Result.Success) {
                throw new InvalidOperationException($"Vulkan present failed: {presentResult}.");
            }

            frameIndex = (frameIndex + 1) % MaxFramesInFlight;
        }

        /// <summary>
        /// Recreates swapchain-dependent resources for a resized surface.
        /// </summary>
        /// <param name="width">New logical width before DPI scaling.</param>
        /// <param name="height">New logical height before DPI scaling.</param>
        public void Recreate(int width, int height) {
            if (width <= 0 || height <= 0) {
                return;
            }

            logicalWidth = width;
            logicalHeight = height;

            context.Api.DeviceWaitIdle(context.Device);

            DestroySwapchainResources();

            CreateSwapchain(width, height, default);
            CreateImageViews();
            CreateDepthResources();
            CreateRenderPass();
            CreateFramebuffers();
            CreateCommandBuffers();

            imagesInFlight = new Fence[images.Length];
            swapchainVersion++;
        }

        /// <summary>
        /// Releases resources owned by the swapchain surface.
        /// </summary>
        public void Dispose() {
            if (disposed) {
                return;
            }

            disposed = true;

            context.Api.DeviceWaitIdle(context.Device);

            DestroySwapchainResources();

            for (int i = 0; i < imageAvailableSemaphores.Length; i++) {
                context.Api.DestroySemaphore(context.Device, imageAvailableSemaphores[i], null);
                context.Api.DestroySemaphore(context.Device, renderFinishedSemaphores[i], null);
                context.Api.DestroyFence(context.Device, frameFences[i], null);
            }

            context.Api.DestroyCommandPool(context.Device, commandPool, null);
            context.SurfaceExtension.DestroySurface(context.Instance, surface, null);
        }

        /// <summary>
        /// Creates the Vulkan surface for the window handle.
        /// </summary>
        unsafe void CreateSurface() {
            if (!context.Api.TryGetInstanceExtension(context.Instance, out KhrWin32Surface win32Surface)) {
                throw new InvalidOperationException("Failed to load VK_KHR_win32_surface extension.");
            }

            IntPtr hInstance = Win32Interop.GetModuleHandle(null);
            if (hInstance == IntPtr.Zero) {
                throw new InvalidOperationException("Failed to resolve Win32 module handle.");
            }

            Win32SurfaceCreateInfoKHR createInfo = new Win32SurfaceCreateInfoKHR {
                SType = StructureType.Win32SurfaceCreateInfoKhr,
                Hinstance = hInstance,
                Hwnd = windowHandle
            };

            Result result = win32Surface.CreateWin32Surface(context.Instance, in createInfo, null, out surface);
            if (result != Result.Success) {
                throw new InvalidOperationException($"Vulkan Win32 surface creation failed: {result}.");
            }
        }

        /// <summary>
        /// Creates the swapchain and retrieves its images.
        /// </summary>
        /// <param name="width">Target width.</param>
        /// <param name="height">Target height.</param>
        /// <param name="oldSwapchain">Old swapchain handle, if any.</param>
        unsafe void CreateSwapchain(int width, int height, SwapchainKHR oldSwapchain) {
            if (!SupportsPresent()) {
                throw new InvalidOperationException("Selected GPU does not support presentation for this surface.");
            }

            SurfaceCapabilitiesKHR capabilities;
            context.SurfaceExtension.GetPhysicalDeviceSurfaceCapabilities(context.PhysicalDevice, surface, out capabilities);

            SurfaceFormatKHR surfaceFormat = ChooseSurfaceFormat();
            PresentModeKHR presentMode = ChoosePresentMode();
            extent = ChooseSwapchainExtent(capabilities, width, height);

            uint imageCount = capabilities.MinImageCount + 1;
            if (capabilities.MaxImageCount > 0 && imageCount > capabilities.MaxImageCount) {
                imageCount = capabilities.MaxImageCount;
            }

            SwapchainCreateInfoKHR createInfo = new SwapchainCreateInfoKHR {
                SType = StructureType.SwapchainCreateInfoKhr,
                Surface = surface,
                MinImageCount = imageCount,
                ImageFormat = surfaceFormat.Format,
                ImageColorSpace = surfaceFormat.ColorSpace,
                ImageExtent = extent,
                ImageArrayLayers = 1,
                ImageUsage = ImageUsageFlags.ImageUsageColorAttachmentBit,
                ImageSharingMode = SharingMode.Exclusive,
                PreTransform = capabilities.CurrentTransform,
                CompositeAlpha = CompositeAlphaFlagsKHR.CompositeAlphaOpaqueBitKhr,
                PresentMode = presentMode,
                Clipped = true,
                OldSwapchain = oldSwapchain
            };

            Result result = context.SwapchainExtension.CreateSwapchain(context.Device, createInfo, null, out swapchain);
            if (result != Result.Success) {
                throw new InvalidOperationException($"Vulkan swapchain creation failed: {result}.");
            }

            uint swapchainImageCount = 0;
            context.SwapchainExtension.GetSwapchainImages(context.Device, swapchain, ref swapchainImageCount, null);
            images = new Image[swapchainImageCount];
            fixed (Image* imagesPtr = images) {
                context.SwapchainExtension.GetSwapchainImages(context.Device, swapchain, ref swapchainImageCount, imagesPtr);
            }

            imageFormat = surfaceFormat.Format;
            imagesInFlight = new Fence[images.Length];
        }

        /// <summary>
        /// Creates image views for all swapchain images.
        /// </summary>
        unsafe void CreateImageViews() {
            imageViews = new ImageView[images.Length];
            for (int i = 0; i < images.Length; i++) {
                ImageViewCreateInfo viewInfo = new ImageViewCreateInfo {
                    SType = StructureType.ImageViewCreateInfo,
                    Image = images[i],
                    ViewType = ImageViewType.Type2D,
                    Format = imageFormat,
                    SubresourceRange = new ImageSubresourceRange {
                        AspectMask = ImageAspectFlags.ImageAspectColorBit,
                        BaseMipLevel = 0,
                        LevelCount = 1,
                        BaseArrayLayer = 0,
                        LayerCount = 1
                    }
                };

                Result result = context.Api.CreateImageView(context.Device, viewInfo, null, out imageViews[i]);
                if (result != Result.Success) {
                    throw new InvalidOperationException($"Failed to create swapchain image view: {result}.");
                }
            }
        }

        /// <summary>
        /// Creates the depth buffer and view.
        /// </summary>
        unsafe void CreateDepthResources() {
            Format depthFormat = ChooseDepthFormat();

            ImageCreateInfo imageInfo = new ImageCreateInfo {
                SType = StructureType.ImageCreateInfo,
                ImageType = ImageType.Type2D,
                Extent = new Extent3D(extent.Width, extent.Height, 1),
                MipLevels = 1,
                ArrayLayers = 1,
                Format = depthFormat,
                Tiling = ImageTiling.Optimal,
                InitialLayout = ImageLayout.Undefined,
                Usage = ImageUsageFlags.ImageUsageDepthStencilAttachmentBit,
                SharingMode = SharingMode.Exclusive,
                Samples = SampleCountFlags.SampleCount1Bit
            };

            Result imageResult = context.Api.CreateImage(context.Device, imageInfo, null, out depthImage);
            if (imageResult != Result.Success) {
                throw new InvalidOperationException($"Failed to create depth image: {imageResult}.");
            }

            MemoryRequirements memoryRequirements;
            context.Api.GetImageMemoryRequirements(context.Device, depthImage, out memoryRequirements);

            MemoryAllocateInfo allocInfo = new MemoryAllocateInfo {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = memoryRequirements.Size,
                MemoryTypeIndex = context.FindMemoryType(memoryRequirements.MemoryTypeBits, MemoryPropertyFlags.MemoryPropertyDeviceLocalBit)
            };

            Result allocResult = context.Api.AllocateMemory(context.Device, allocInfo, null, out depthMemory);
            if (allocResult != Result.Success) {
                throw new InvalidOperationException($"Failed to allocate depth memory: {allocResult}.");
            }

            context.Api.BindImageMemory(context.Device, depthImage, depthMemory, 0);

            ImageViewCreateInfo viewInfo = new ImageViewCreateInfo {
                SType = StructureType.ImageViewCreateInfo,
                Image = depthImage,
                ViewType = ImageViewType.Type2D,
                Format = depthFormat,
                SubresourceRange = new ImageSubresourceRange {
                    AspectMask = ImageAspectFlags.ImageAspectDepthBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                }
            };

            Result viewResult = context.Api.CreateImageView(context.Device, viewInfo, null, out depthView);
            if (viewResult != Result.Success) {
                throw new InvalidOperationException($"Failed to create depth image view: {viewResult}.");
            }
        }

        /// <summary>
        /// Creates the render pass for color and depth rendering.
        /// </summary>
        unsafe void CreateRenderPass() {
            AttachmentDescription colorAttachment = new AttachmentDescription {
                Format = imageFormat,
                Samples = SampleCountFlags.SampleCount1Bit,
                LoadOp = AttachmentLoadOp.Clear,
                StoreOp = AttachmentStoreOp.Store,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.Undefined,
                FinalLayout = ImageLayout.PresentSrcKhr
            };

            AttachmentDescription depthAttachment = new AttachmentDescription {
                Format = ChooseDepthFormat(),
                Samples = SampleCountFlags.SampleCount1Bit,
                LoadOp = AttachmentLoadOp.Clear,
                StoreOp = AttachmentStoreOp.DontCare,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.Undefined,
                FinalLayout = ImageLayout.DepthStencilAttachmentOptimal
            };

            AttachmentReference colorAttachmentRef = new AttachmentReference {
                Attachment = 0,
                Layout = ImageLayout.ColorAttachmentOptimal
            };

            AttachmentReference depthAttachmentRef = new AttachmentReference {
                Attachment = 1,
                Layout = ImageLayout.DepthStencilAttachmentOptimal
            };

            SubpassDescription subpass = new SubpassDescription {
                PipelineBindPoint = PipelineBindPoint.Graphics,
                ColorAttachmentCount = 1,
                PColorAttachments = &colorAttachmentRef,
                PDepthStencilAttachment = &depthAttachmentRef
            };

            SubpassDependency dependency = new SubpassDependency {
                SrcSubpass = Vk.SubpassExternal,
                DstSubpass = 0,
                SrcStageMask = PipelineStageFlags.PipelineStageColorAttachmentOutputBit | PipelineStageFlags.PipelineStageEarlyFragmentTestsBit,
                SrcAccessMask = 0,
                DstStageMask = PipelineStageFlags.PipelineStageColorAttachmentOutputBit | PipelineStageFlags.PipelineStageEarlyFragmentTestsBit,
                DstAccessMask = AccessFlags.AccessColorAttachmentWriteBit | AccessFlags.AccessDepthStencilAttachmentWriteBit
            };

            AttachmentDescription* attachments = stackalloc AttachmentDescription[2];
            attachments[0] = colorAttachment;
            attachments[1] = depthAttachment;

            RenderPassCreateInfo renderPassInfo = new RenderPassCreateInfo {
                SType = StructureType.RenderPassCreateInfo,
                AttachmentCount = 2,
                PAttachments = attachments,
                SubpassCount = 1,
                PSubpasses = &subpass,
                DependencyCount = 1,
                PDependencies = &dependency
            };

            Result result = context.Api.CreateRenderPass(context.Device, renderPassInfo, null, out renderPass);
            if (result != Result.Success) {
                throw new InvalidOperationException($"Failed to create Vulkan render pass: {result}.");
            }
        }

        /// <summary>
        /// Creates framebuffers for each swapchain image view.
        /// </summary>
        unsafe void CreateFramebuffers() {
            framebuffers = new Framebuffer[imageViews.Length];
            for (int i = 0; i < imageViews.Length; i++) {
                ImageView* attachments = stackalloc ImageView[2];
                attachments[0] = imageViews[i];
                attachments[1] = depthView;

                FramebufferCreateInfo framebufferInfo = new FramebufferCreateInfo {
                    SType = StructureType.FramebufferCreateInfo,
                    RenderPass = renderPass,
                    AttachmentCount = 2,
                    PAttachments = attachments,
                    Width = extent.Width,
                    Height = extent.Height,
                    Layers = 1
                };

                Result result = context.Api.CreateFramebuffer(context.Device, framebufferInfo, null, out framebuffers[i]);
                if (result != Result.Success) {
                    throw new InvalidOperationException($"Failed to create Vulkan framebuffer: {result}.");
                }
            }
        }

        /// <summary>
        /// Creates a command pool for render command buffers.
        /// </summary>
        unsafe void CreateCommandPool() {
            CommandPoolCreateInfo poolInfo = new CommandPoolCreateInfo {
                SType = StructureType.CommandPoolCreateInfo,
                Flags = CommandPoolCreateFlags.CommandPoolCreateResetCommandBufferBit,
                QueueFamilyIndex = context.GraphicsQueueFamilyIndex
            };

            Result result = context.Api.CreateCommandPool(context.Device, poolInfo, null, out commandPool);
            if (result != Result.Success) {
                throw new InvalidOperationException($"Failed to create Vulkan command pool: {result}.");
            }
        }

        /// <summary>
        /// Allocates command buffers for each swapchain image.
        /// </summary>
        unsafe void CreateCommandBuffers() {
            if (commandBuffers.Length > 0) {
                fixed (CommandBuffer* existingBuffers = commandBuffers) {
                    context.Api.FreeCommandBuffers(context.Device, commandPool, (uint)commandBuffers.Length, existingBuffers);
                }
            }

            commandBuffers = new CommandBuffer[images.Length];

            CommandBufferAllocateInfo allocInfo = new CommandBufferAllocateInfo {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = commandPool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = (uint)commandBuffers.Length
            };

            fixed (CommandBuffer* buffersPtr = commandBuffers) {
                Result result = context.Api.AllocateCommandBuffers(context.Device, allocInfo, buffersPtr);
                if (result != Result.Success) {
                    throw new InvalidOperationException($"Failed to allocate Vulkan command buffers: {result}.");
                }
            }
        }

        /// <summary>
        /// Creates per-frame synchronization primitives.
        /// </summary>
        unsafe void CreateSyncObjects() {
            imageAvailableSemaphores = new VulkanSemaphore[MaxFramesInFlight];
            renderFinishedSemaphores = new VulkanSemaphore[MaxFramesInFlight];
            frameFences = new Fence[MaxFramesInFlight];

            SemaphoreCreateInfo semaphoreInfo = new SemaphoreCreateInfo {
                SType = StructureType.SemaphoreCreateInfo
            };

            FenceCreateInfo fenceInfo = new FenceCreateInfo {
                SType = StructureType.FenceCreateInfo,
                Flags = FenceCreateFlags.FenceCreateSignaledBit
            };

            for (int i = 0; i < MaxFramesInFlight; i++) {
                Result imageSemaphoreResult = context.Api.CreateSemaphore(context.Device, semaphoreInfo, null, out imageAvailableSemaphores[i]);
                Result renderSemaphoreResult = context.Api.CreateSemaphore(context.Device, semaphoreInfo, null, out renderFinishedSemaphores[i]);
                Result fenceResult = context.Api.CreateFence(context.Device, fenceInfo, null, out frameFences[i]);

                if (imageSemaphoreResult != Result.Success || renderSemaphoreResult != Result.Success || fenceResult != Result.Success) {
                    throw new InvalidOperationException("Failed to create Vulkan synchronization objects.");
                }
            }
        }

        /// <summary>
        /// Checks whether the current physical device can present to this surface.
        /// </summary>
        /// <returns>True when presentation is supported.</returns>
        unsafe bool SupportsPresent() {
            Bool32 supported = false;
            context.SurfaceExtension.GetPhysicalDeviceSurfaceSupport(context.PhysicalDevice, context.GraphicsQueueFamilyIndex, surface, out supported);
            return supported;
        }

        /// <summary>
        /// Chooses the preferred surface format.
        /// </summary>
        /// <returns>Surface format to use.</returns>
        unsafe SurfaceFormatKHR ChooseSurfaceFormat() {
            uint formatCount = 0;
            context.SurfaceExtension.GetPhysicalDeviceSurfaceFormats(context.PhysicalDevice, surface, ref formatCount, null);

            var formats = new SurfaceFormatKHR[formatCount];
            fixed (SurfaceFormatKHR* formatsPtr = formats) {
                context.SurfaceExtension.GetPhysicalDeviceSurfaceFormats(context.PhysicalDevice, surface, ref formatCount, formatsPtr);
            }

            for (int i = 0; i < formats.Length; i++) {
                if (formats[i].Format == Format.B8G8R8A8Unorm && formats[i].ColorSpace == ColorSpaceKHR.ColorSpaceSrgbNonlinearKhr) {
                    return formats[i];
                }
            }

            return formats[0];
        }

        /// <summary>
        /// Chooses the presentation mode.
        /// </summary>
        /// <returns>Presentation mode to use.</returns>
        unsafe PresentModeKHR ChoosePresentMode() {
            uint presentModeCount = 0;
            context.SurfaceExtension.GetPhysicalDeviceSurfacePresentModes(context.PhysicalDevice, surface, ref presentModeCount, null);

            var presentModes = new PresentModeKHR[presentModeCount];
            fixed (PresentModeKHR* presentModesPtr = presentModes) {
                context.SurfaceExtension.GetPhysicalDeviceSurfacePresentModes(context.PhysicalDevice, surface, ref presentModeCount, presentModesPtr);
            }

            for (int i = 0; i < presentModes.Length; i++) {
                if (presentModes[i] == PresentModeKHR.PresentModeMailboxKhr) {
                    return presentModes[i];
                }
            }

            return PresentModeKHR.PresentModeFifoKhr;
        }

        /// <summary>
        /// Chooses the swapchain extent based on surface capabilities.
        /// </summary>
        /// <param name="capabilities">Surface capability data.</param>
        /// <param name="width">Requested width.</param>
        /// <param name="height">Requested height.</param>
        /// <returns>Extent to use for the swapchain.</returns>
        Extent2D ChooseSwapchainExtent(SurfaceCapabilitiesKHR capabilities, int width, int height) {
            if (capabilities.CurrentExtent.Width != uint.MaxValue) {
                return capabilities.CurrentExtent;
            }

            uint clampedWidth = (uint)Math.Min(Math.Max(width, (int)capabilities.MinImageExtent.Width), (int)capabilities.MaxImageExtent.Width);
            uint clampedHeight = (uint)Math.Min(Math.Max(height, (int)capabilities.MinImageExtent.Height), (int)capabilities.MaxImageExtent.Height);
            return new Extent2D(clampedWidth, clampedHeight);
        }

        /// <summary>
        /// Chooses an appropriate depth format.
        /// </summary>
        /// <returns>Depth format supported by the device.</returns>
        Format ChooseDepthFormat() {
            Format[] candidates = new[] { Format.D32Sfloat, Format.D32SfloatS8Uint, Format.D24UnormS8Uint };
            for (int i = 0; i < candidates.Length; i++) {
                FormatProperties props;
                context.Api.GetPhysicalDeviceFormatProperties(context.PhysicalDevice, candidates[i], out props);
                if ((props.OptimalTilingFeatures & FormatFeatureFlags.FormatFeatureDepthStencilAttachmentBit) != 0) {
                    return candidates[i];
                }
            }

            throw new InvalidOperationException("No compatible Vulkan depth format was found.");
        }

        /// <summary>
        /// Destroys swapchain-dependent resources.
        /// </summary>
        void DestroySwapchainResources() {
            for (int i = 0; i < framebuffers.Length; i++) {
                context.Api.DestroyFramebuffer(context.Device, framebuffers[i], null);
            }

            if (renderPass.Handle != 0) {
                context.Api.DestroyRenderPass(context.Device, renderPass, null);
            }

            if (depthView.Handle != 0) {
                context.Api.DestroyImageView(context.Device, depthView, null);
            }

            if (depthImage.Handle != 0) {
                context.Api.DestroyImage(context.Device, depthImage, null);
            }

            if (depthMemory.Handle != 0) {
                context.Api.FreeMemory(context.Device, depthMemory, null);
            }

            for (int i = 0; i < imageViews.Length; i++) {
                context.Api.DestroyImageView(context.Device, imageViews[i], null);
            }

            if (swapchain.Handle != 0) {
                context.SwapchainExtension.DestroySwapchain(context.Device, swapchain, null);
            }
        }
    }
}
