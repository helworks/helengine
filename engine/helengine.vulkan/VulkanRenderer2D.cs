using Silk.NET.Core.Native;
using Silk.NET.Shaderc;
using Silk.NET.Vulkan;
using System.Runtime.InteropServices;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace helengine.vulkan {
    /// <summary>
    /// Vulkan-backed renderer responsible for 2D sprites, text, and UI shapes.
    /// </summary>
    public unsafe class VulkanRenderer2D : RenderManager2D, IRenderVisitor2D {
        /// <summary>
        /// Number of vertices in a quad.
        /// </summary>
        const int QuadVertexCount = 4;
        /// <summary>
        /// Number of indices in a quad.
        /// </summary>
        const int QuadIndexCount = 6;
        /// <summary>
        /// Maximum number of descriptor sets allocated for textures.
        /// </summary>
        const int MaxDescriptorSets = 2048;
        /// <summary>
        /// Maximum number of quads that can be recorded in a single frame.
        /// </summary>
        const int MaxQuadsPerFrame = 8192;

        /// <summary>
        /// Shared Vulkan context for device access.
        /// </summary>
        readonly VulkanContext context;
        /// <summary>
        /// Static quad index buffer data.
        /// </summary>
        readonly uint[] quadIndices;
        /// <summary>
        /// Descriptor set layout for sampled textures.
        /// </summary>
        DescriptorSetLayout descriptorSetLayout;
        /// <summary>
        /// Pipeline layout used for UI rendering.
        /// </summary>
        PipelineLayout pipelineLayout;
        /// <summary>
        /// Graphics pipeline used for sprite rendering.
        /// </summary>
        Pipeline pipeline;
        /// <summary>
        /// Render pass the pipeline was created against.
        /// </summary>
        RenderPass pipelineRenderPass;
        /// <summary>
        /// Swapchain version used to build the current pipeline.
        /// </summary>
        int pipelineSwapchainVersion;
        /// <summary>
        /// Vertex shader module for sprites.
        /// </summary>
        ShaderModule vertexShader;
        /// <summary>
        /// Fragment shader module for sprites.
        /// </summary>
        ShaderModule fragmentShader;
        /// <summary>
        /// Descriptor pool used to allocate texture descriptor sets.
        /// </summary>
        DescriptorPool descriptorPool;
        /// <summary>
        /// Sampler shared across all textures.
        /// </summary>
        Sampler sampler;
        /// <summary>
        /// Vertex buffer for per-quad updates.
        /// </summary>
        VulkanGpuBuffer vertexBuffer = null!;
        /// <summary>
        /// Index buffer storing the quad indices.
        /// </summary>
        VulkanGpuBuffer indexBuffer = null!;
        /// <summary>
        /// White texture used for solid-color primitives.
        /// </summary>
        VulkanTextureResource whiteTexture = null!;
        /// <summary>
        /// Surface currently being rendered.
        /// </summary>
        VulkanSwapchainSurface currentSurface = null!;
        /// <summary>
        /// Command buffer currently recording draw calls.
        /// </summary>
        CommandBuffer currentCommandBuffer;
        /// <summary>
        /// Width of the active camera viewport in logical units.
        /// </summary>
        double currentViewportWidth;
        /// <summary>
        /// Height of the active camera viewport in logical units.
        /// </summary>
        double currentViewportHeight;
        /// <summary>
        /// X offset of the active viewport in logical units.
        /// </summary>
        double currentViewportOffsetX;
        /// <summary>
        /// Y offset of the active viewport in logical units.
        /// </summary>
        double currentViewportOffsetY;
        /// <summary>
        /// Tracks whether a frame is active for drawing.
        /// </summary>
        bool frameActive;
        /// <summary>
        /// Tracks how many quads have been written into the dynamic vertex buffer for the current frame.
        /// </summary>
        int recordedQuadCount;
        /// <summary>
        /// Current scissor rectangle in physical pixels for the active camera viewport.
        /// </summary>
        int currentScissorX;
        /// <summary>
        /// Current scissor rectangle in physical pixels for the active camera viewport.
        /// </summary>
        int currentScissorY;
        /// <summary>
        /// Current scissor rectangle in physical pixels for the active camera viewport.
        /// </summary>
        int currentScissorWidth;
        /// <summary>
        /// Current scissor rectangle in physical pixels for the active camera viewport.
        /// </summary>
        int currentScissorHeight;
        /// <summary>
        /// Reusable helper that resolves nested clip chains for the current drawable.
        /// </summary>
        readonly ClipRegionStackBuilder2D ClipRegionStackBuilder;
        /// <summary>
        /// Clip owners currently active in the render traversal.
        /// </summary>
        readonly List<IClipRegion2D> ActiveClipChain;
        /// <summary>
        /// Clip owners resolved for the drawable currently being visited.
        /// </summary>
        readonly List<IClipRegion2D> NextClipChain;
        /// <summary>
        /// Effective clip rectangles for the active clip chain.
        /// </summary>
        readonly List<float4> ActiveClipRects;
        /// <summary>
        /// Tracks whether the renderer has been disposed.
        /// </summary>
        bool disposed;

        /// <summary>
        /// Initializes the Vulkan 2D renderer.
        /// </summary>
        /// <param name="context">Shared Vulkan context.</param>
        public VulkanRenderer2D(VulkanContext context) {
            this.context = context;
            quadIndices = new uint[] { 0, 1, 2, 2, 3, 0 };
            ClipRegionStackBuilder = new ClipRegionStackBuilder2D();
            ActiveClipChain = new List<IClipRegion2D>();
            NextClipChain = new List<IClipRegion2D>();
            ActiveClipRects = new List<float4>();

            CreateDescriptorSetLayout();
            CreatePipelineLayout();
            CreateDescriptorPool();
            CreateSampler();
            CreateShaderModules();
            CreateQuadBuffers();
            CreateWhiteTexture();
        }

        /// <summary>
        /// Registers a swapchain surface with the renderer.
        /// </summary>
        /// <param name="surface">Swapchain surface to attach.</param>
        public void AttachSurface(VulkanSwapchainSurface surface) {
            EnsurePipeline(surface);
        }

        /// <summary>
        /// Handles swapchain recreation by rebuilding pipeline state when needed.
        /// </summary>
        /// <param name="surface">Surface that was recreated.</param>
        public void HandleSwapchainRecreated(VulkanSwapchainSurface surface) {
            EnsurePipeline(surface);
        }

        /// <summary>
        /// Starts a new 2D frame for a swapchain surface.
        /// </summary>
        /// <param name="surface">Surface to render into.</param>
        /// <param name="commandBuffer">Command buffer to record.</param>
        public void BeginFrame(VulkanSwapchainSurface surface, CommandBuffer commandBuffer) {
            currentSurface = surface;
            currentCommandBuffer = commandBuffer;
            frameActive = true;
            recordedQuadCount = 0;

            EnsurePipeline(surface);
        }

        /// <summary>
        /// Ends the current 2D frame.
        /// </summary>
        public void EndFrame() {
            frameActive = false;
            currentCommandBuffer = default;
            currentViewportWidth = 0;
            currentViewportHeight = 0;
            currentViewportOffsetX = 0;
            currentViewportOffsetY = 0;
            recordedQuadCount = 0;
            currentScissorX = 0;
            currentScissorY = 0;
            currentScissorWidth = 0;
            currentScissorHeight = 0;
            ActiveClipChain.Clear();
            NextClipChain.Clear();
            ActiveClipRects.Clear();
        }

        /// <summary>
        /// Renders all 2D drawables for a camera.
        /// </summary>
        /// <param name="camera">Camera supplying the render queue.</param>
        public void RenderCamera(ICamera camera) {
            if (!frameActive) {
                throw new InvalidOperationException("Cannot render 2D camera outside of an active frame.");
            }

            ActiveClipChain.Clear();
            NextClipChain.Clear();
            ActiveClipRects.Clear();

            float4 viewport = camera.Viewport;
            double offsetX = viewport.X;
            double offsetY = viewport.Y;
            double width = viewport.Z;
            double height = viewport.W;
            double logicalSurfaceWidth = currentSurface.LogicalWidth;
            double logicalSurfaceHeight = currentSurface.LogicalHeight;

            if (width <= 1.0 && height <= 1.0) {
                offsetX *= logicalSurfaceWidth;
                offsetY *= logicalSurfaceHeight;
                width *= logicalSurfaceWidth;
                height *= logicalSurfaceHeight;
            }

            if (width <= 0.0 || height <= 0.0) {
                return;
            }

            double pixelScaleX = currentSurface.Extent.Width / logicalSurfaceWidth;
            double pixelScaleY = currentSurface.Extent.Height / logicalSurfaceHeight;
            double pixelOffsetX = offsetX * pixelScaleX;
            double pixelOffsetY = offsetY * pixelScaleY;
            double pixelWidth = width * pixelScaleX;
            double pixelHeight = height * pixelScaleY;
            double snappedPixelOffsetX = Math.Round(pixelOffsetX);
            double snappedPixelOffsetY = Math.Round(pixelOffsetY);
            double snappedPixelWidth = Math.Max(1.0, Math.Round(pixelWidth));
            double snappedPixelHeight = Math.Max(1.0, Math.Round(pixelHeight));

            currentViewportOffsetX = snappedPixelOffsetX / pixelScaleX;
            currentViewportOffsetY = snappedPixelOffsetY / pixelScaleY;
            currentViewportWidth = snappedPixelWidth / pixelScaleX;
            currentViewportHeight = snappedPixelHeight / pixelScaleY;
            currentScissorX = (int)snappedPixelOffsetX;
            currentScissorY = (int)snappedPixelOffsetY;
            currentScissorWidth = (int)snappedPixelWidth;
            currentScissorHeight = (int)snappedPixelHeight;
            SetViewportAndScissor(snappedPixelOffsetX, snappedPixelOffsetY, snappedPixelWidth, snappedPixelHeight);

            IRenderQueue2D renderQueue = camera.RenderQueue2D;
            renderQueue.VisitOrdered(this);
        }

        /// <summary>
        /// Draws a single 2D drawable encountered during queue traversal.
        /// </summary>
        /// <param name="drawable">Drawable to render.</param>
        public void Visit(IDrawable2D drawable) {
            if (drawable?.Parent == null || !drawable.Parent.Enabled) {
                return;
            }

            ClipRegionStackBuilder.BuildClipChain(drawable, NextClipChain);
            SyncClipTransitions();
            drawable.Draw();
        }

        /// <summary>
        /// Builds a runtime texture from raw texture data.
        /// </summary>
        /// <param name="data">Raw texture data.</param>
        /// <returns>Runtime texture instance.</returns>
        public override RuntimeTexture BuildTextureFromRaw(TextureAsset data) {
            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            if (data.Width == 0 || data.Height == 0) {
                throw new ArgumentException("Texture data must define a non-zero size.", nameof(data));
            }

            if (data.Colors == null || data.Colors.Length == 0) {
                throw new ArgumentException("Texture data must provide color data.", nameof(data));
            }

            int expectedLength = data.Width * data.Height * 4;
            if (data.Colors.Length < expectedLength) {
                throw new ArgumentException("Texture data size does not match expected RGBA length.", nameof(data));
            }

            return CreateTextureResource(data);
        }

        /// <summary>
        /// Draws a sprite component.
        /// </summary>
        /// <param name="sprite">Sprite to draw.</param>
        public override void DrawSprite(ISpriteDrawable2D sprite) {
            if (sprite?.Parent == null || !sprite.Parent.Enabled) {
                return;
            }

            if (!frameActive) {
                throw new InvalidOperationException("Cannot draw sprites outside of an active frame.");
            }

            if (sprite.Texture == null) {
                return;
            }

            if (sprite.Texture is not VulkanTextureResource texture) {
                throw new InvalidOperationException("Sprite texture was not created by the Vulkan renderer.");
            }

            int2 size = sprite.Size;
            if (size.X <= 0 || size.Y <= 0) {
                size = new int2(texture.Width, texture.Height);
            }

            float3 position = sprite.Parent.Position;
            DrawQuad(texture, position.X, position.Y, size.X, size.Y, sprite.SourceRect, sprite.Color);
        }

        /// <summary>
        /// Draws text for a text drawable.
        /// </summary>
        /// <param name="text">Text drawable.</param>
        public override void DrawText(ITextDrawable2D text) {
            if (text?.Parent == null || !text.Parent.Enabled) {
                return;
            }

            if (!frameActive) {
                throw new InvalidOperationException("Cannot draw text outside of an active frame.");
            }

            FontAsset font = text.Font;
            if (font.Texture is not VulkanTextureResource texture) {
                throw new InvalidOperationException("Font texture was not created by the Vulkan renderer.");
            }

            float3 position = text.Parent.Position;
            byte4 color = text.Color;
            string content = text.Text ?? string.Empty;
            if (text.WrapText) {
                content = TextLayoutUtils.WrapText(content, font, text.Size.X);
            }

            double offsetX = 0.0;
            double offsetY = 0.0;
            double lineHeight = Math.Max(font.LineHeight, 1.0f);
            double baseX = Math.Round(position.X);
            double baseY = Math.Round(position.Y);

            for (int i = 0; i < content.Length; i++) {
                char c = content[i];

                if (c == '\n') {
                    offsetY += lineHeight;
                    offsetX = 0.0;
                    continue;
                }

                if (c == ' ') {
                    offsetX += font.FontInfo.SpaceWidth;
                    continue;
                }

                if (!font.Characters.TryGetValue(c, out FontChar info)) {
                    continue;
                }

                double pixelW = info.SourceRect.Z * texture.Width;
                double pixelH = info.SourceRect.W * texture.Height;
                double snappedLineOffsetY = Math.Round(offsetY);

                double drawX = baseX + offsetX;
                double drawY = baseY + snappedLineOffsetY + info.OffsetY;

                DrawQuad(texture, drawX, drawY, pixelW, pixelH, info.SourceRect, color);

                double advance = info.AdvanceWidth > 0 ? info.AdvanceWidth : pixelW;
                offsetX += advance;
            }
        }

        /// <summary>
        /// Draws a rounded rectangle.
        /// </summary>
        /// <param name="shape">Rounded rectangle drawable.</param>
        public override void DrawRoundedRect(IRoundedRectDrawable2D shape) {
            if (shape?.Parent == null || !shape.Parent.Enabled) {
                return;
            }

            if (!frameActive) {
                throw new InvalidOperationException("Cannot draw shapes outside of an active frame.");
            }

            int2 size = shape.Size;
            if (size.X <= 0 || size.Y <= 0) {
                return;
            }

            float3 position = shape.Parent.Position;
            double borderThickness = Math.Max(shape.BorderThickness, 0.0f);

            if (borderThickness > 0.0) {
                DrawQuad(whiteTexture, position.X, position.Y, size.X, size.Y, new float4(0, 0, 1, 1), shape.BorderColor);
            }

            double innerX = position.X + borderThickness;
            double innerY = position.Y + borderThickness;
            double innerW = size.X - (borderThickness * 2.0);
            double innerH = size.Y - (borderThickness * 2.0);

            if (innerW > 0.0 && innerH > 0.0) {
                DrawQuad(whiteTexture, innerX, innerY, innerW, innerH, new float4(0, 0, 1, 1), shape.FillColor);
            }
        }

        /// <summary>
        /// Releases Vulkan resources owned by the 2D renderer.
        /// </summary>
        public override void Dispose() {
            if (disposed) {
                return;
            }

            disposed = true;
            context.Api.DeviceWaitIdle(context.Device);

            DestroyTextureResource(whiteTexture);
            vertexBuffer.Dispose();
            indexBuffer.Dispose();

            DestroyPipeline();

            if (sampler.Handle != 0) {
                context.Api.DestroySampler(context.Device, sampler, null);
            }

            if (descriptorPool.Handle != 0) {
                context.Api.DestroyDescriptorPool(context.Device, descriptorPool, null);
            }

            if (pipelineLayout.Handle != 0) {
                context.Api.DestroyPipelineLayout(context.Device, pipelineLayout, null);
            }

            if (descriptorSetLayout.Handle != 0) {
                context.Api.DestroyDescriptorSetLayout(context.Device, descriptorSetLayout, null);
            }

            if (vertexShader.Handle != 0) {
                context.Api.DestroyShaderModule(context.Device, vertexShader, null);
            }

            if (fragmentShader.Handle != 0) {
                context.Api.DestroyShaderModule(context.Device, fragmentShader, null);
            }
        }

        /// <summary>
        /// Ensures the pipeline is compatible with the given surface.
        /// </summary>
        /// <param name="surface">Surface to render into.</param>
        void EnsurePipeline(VulkanSwapchainSurface surface) {
            if (pipeline.Handle != 0 &&
                pipelineRenderPass.Handle == surface.RenderPass.Handle &&
                pipelineSwapchainVersion == surface.SwapchainVersion) {
                return;
            }

            DestroyPipeline();
            CreatePipeline(surface);
            pipelineRenderPass = surface.RenderPass;
            pipelineSwapchainVersion = surface.SwapchainVersion;
        }

        /// <summary>
        /// Creates the descriptor set layout for sprite textures.
        /// </summary>
        unsafe void CreateDescriptorSetLayout() {
            DescriptorSetLayoutBinding samplerLayoutBinding = new DescriptorSetLayoutBinding {
                Binding = 0,
                DescriptorType = DescriptorType.CombinedImageSampler,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.ShaderStageFragmentBit
            };

            DescriptorSetLayoutCreateInfo layoutInfo = new DescriptorSetLayoutCreateInfo {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = 1,
                PBindings = &samplerLayoutBinding
            };

            Result result = context.Api.CreateDescriptorSetLayout(context.Device, layoutInfo, null, out descriptorSetLayout);
            if (result != Result.Success) {
                throw new InvalidOperationException($"Failed to create descriptor set layout: {result}.");
            }
        }

        /// <summary>
        /// Creates the pipeline layout for sprite rendering.
        /// </summary>
        unsafe void CreatePipelineLayout() {
            DescriptorSetLayout* layouts = stackalloc DescriptorSetLayout[] { descriptorSetLayout };
            PipelineLayoutCreateInfo pipelineLayoutInfo = new PipelineLayoutCreateInfo {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = 1,
                PSetLayouts = layouts
            };

            Result result = context.Api.CreatePipelineLayout(context.Device, pipelineLayoutInfo, null, out pipelineLayout);
            if (result != Result.Success) {
                throw new InvalidOperationException($"Failed to create pipeline layout: {result}.");
            }
        }

        /// <summary>
        /// Creates the descriptor pool for texture sampling.
        /// </summary>
        unsafe void CreateDescriptorPool() {
            DescriptorPoolSize poolSize = new DescriptorPoolSize {
                Type = DescriptorType.CombinedImageSampler,
                DescriptorCount = MaxDescriptorSets
            };

            DescriptorPoolCreateInfo poolInfo = new DescriptorPoolCreateInfo {
                SType = StructureType.DescriptorPoolCreateInfo,
                PoolSizeCount = 1,
                PPoolSizes = &poolSize,
                MaxSets = MaxDescriptorSets
            };

            Result result = context.Api.CreateDescriptorPool(context.Device, poolInfo, null, out descriptorPool);
            if (result != Result.Success) {
                throw new InvalidOperationException($"Failed to create descriptor pool: {result}.");
            }
        }

        /// <summary>
        /// Creates the sampler shared by all textures.
        /// </summary>
        unsafe void CreateSampler() {
            SamplerCreateInfo samplerInfo = new SamplerCreateInfo {
                SType = StructureType.SamplerCreateInfo,
                MagFilter = Filter.Nearest,
                MinFilter = Filter.Nearest,
                AddressModeU = SamplerAddressMode.ClampToEdge,
                AddressModeV = SamplerAddressMode.ClampToEdge,
                AddressModeW = SamplerAddressMode.ClampToEdge,
                AnisotropyEnable = false,
                MaxAnisotropy = 1,
                BorderColor = BorderColor.FloatOpaqueWhite,
                UnnormalizedCoordinates = false,
                CompareEnable = false,
                MipmapMode = SamplerMipmapMode.Nearest,
                MinLod = 0,
                MaxLod = 0
            };

            Result result = context.Api.CreateSampler(context.Device, samplerInfo, null, out sampler);
            if (result != Result.Success) {
                throw new InvalidOperationException($"Failed to create Vulkan sampler: {result}.");
            }
        }

        /// <summary>
        /// Creates shader modules for sprite rendering.
        /// </summary>
        void CreateShaderModules() {
            vertexShader = CompileShaderModule(GetVertexShaderSource(), ShaderKind.VertexShader, "sprite.vert");
            fragmentShader = CompileShaderModule(GetFragmentShaderSource(), ShaderKind.FragmentShader, "sprite.frag");
        }

        /// <summary>
        /// Creates the per-quad vertex and index buffers.
        /// </summary>
        void CreateQuadBuffers() {
            ulong vertexSize = (ulong)(VulkanSpriteVertex.SizeInBytes * QuadVertexCount * MaxQuadsPerFrame);
            vertexBuffer = new VulkanGpuBuffer(
                context,
                vertexSize,
                BufferUsageFlags.BufferUsageVertexBufferBit,
                MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyHostCoherentBit);

            ulong indexSize = (ulong)(sizeof(uint) * QuadIndexCount);
            indexBuffer = new VulkanGpuBuffer(
                context,
                indexSize,
                BufferUsageFlags.BufferUsageIndexBufferBit,
                MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyHostCoherentBit);
            indexBuffer.Update(quadIndices);
        }

        /// <summary>
        /// Creates the 1x1 white texture used for solid fills.
        /// </summary>
        void CreateWhiteTexture() {
            var asset = new TextureAsset {
                Width = 1,
                Height = 1,
                Colors = new byte[] { 255, 255, 255, 255 }
            };

            whiteTexture = CreateTextureResource(asset);
        }

        /// <summary>
        /// Creates the sprite graphics pipeline for a surface.
        /// </summary>
        /// <param name="surface">Surface providing the render pass.</param>
        unsafe void CreatePipeline(VulkanSwapchainSurface surface) {
            PipelineShaderStageCreateInfo* shaderStages = stackalloc PipelineShaderStageCreateInfo[2];
            shaderStages[0] = new PipelineShaderStageCreateInfo {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.ShaderStageVertexBit,
                Module = vertexShader,
                PName = (byte*)SilkMarshal.StringToPtr("main")
            };
            shaderStages[1] = new PipelineShaderStageCreateInfo {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.ShaderStageFragmentBit,
                Module = fragmentShader,
                PName = (byte*)SilkMarshal.StringToPtr("main")
            };

            VertexInputBindingDescription bindingDescription = new VertexInputBindingDescription {
                Binding = 0,
                Stride = (uint)VulkanSpriteVertex.SizeInBytes,
                InputRate = VertexInputRate.Vertex
            };

            VertexInputAttributeDescription* attributeDescriptions = stackalloc VertexInputAttributeDescription[3];
            attributeDescriptions[0] = new VertexInputAttributeDescription {
                Binding = 0,
                Location = 0,
                Format = Format.R32G32Sfloat,
                Offset = 0
            };
            attributeDescriptions[1] = new VertexInputAttributeDescription {
                Binding = 0,
                Location = 1,
                Format = Format.R32G32Sfloat,
                Offset = 8
            };
            attributeDescriptions[2] = new VertexInputAttributeDescription {
                Binding = 0,
                Location = 2,
                Format = Format.R32G32B32A32Sfloat,
                Offset = 16
            };

            PipelineVertexInputStateCreateInfo vertexInputInfo = new PipelineVertexInputStateCreateInfo {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = 1,
                PVertexBindingDescriptions = &bindingDescription,
                VertexAttributeDescriptionCount = 3,
                PVertexAttributeDescriptions = attributeDescriptions
            };

            PipelineInputAssemblyStateCreateInfo inputAssembly = new PipelineInputAssemblyStateCreateInfo {
                SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = PrimitiveTopology.TriangleList,
                PrimitiveRestartEnable = false
            };

            PipelineViewportStateCreateInfo viewportState = new PipelineViewportStateCreateInfo {
                SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = 1,
                ScissorCount = 1
            };

            PipelineRasterizationStateCreateInfo rasterizer = new PipelineRasterizationStateCreateInfo {
                SType = StructureType.PipelineRasterizationStateCreateInfo,
                DepthClampEnable = false,
                RasterizerDiscardEnable = false,
                PolygonMode = PolygonMode.Fill,
                LineWidth = 1.0f,
                CullMode = CullModeFlags.CullModeNone,
                FrontFace = FrontFace.CounterClockwise,
                DepthBiasEnable = false
            };

            PipelineMultisampleStateCreateInfo multisampling = new PipelineMultisampleStateCreateInfo {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                RasterizationSamples = SampleCountFlags.SampleCount1Bit
            };

            PipelineColorBlendAttachmentState colorBlendAttachment = new PipelineColorBlendAttachmentState {
                BlendEnable = true,
                SrcColorBlendFactor = BlendFactor.SrcAlpha,
                DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
                ColorBlendOp = BlendOp.Add,
                SrcAlphaBlendFactor = BlendFactor.One,
                DstAlphaBlendFactor = BlendFactor.Zero,
                AlphaBlendOp = BlendOp.Add,
                ColorWriteMask = ColorComponentFlags.ColorComponentRBit |
                                 ColorComponentFlags.ColorComponentGBit |
                                 ColorComponentFlags.ColorComponentBBit |
                                 ColorComponentFlags.ColorComponentABit
            };

            PipelineColorBlendStateCreateInfo colorBlending = new PipelineColorBlendStateCreateInfo {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                LogicOpEnable = false,
                AttachmentCount = 1,
                PAttachments = &colorBlendAttachment
            };

            PipelineDepthStencilStateCreateInfo depthStencil = new PipelineDepthStencilStateCreateInfo {
                SType = StructureType.PipelineDepthStencilStateCreateInfo,
                DepthTestEnable = false,
                DepthWriteEnable = false,
                DepthCompareOp = CompareOp.Always
            };

            DynamicState* dynamicStates = stackalloc DynamicState[2];
            dynamicStates[0] = DynamicState.Viewport;
            dynamicStates[1] = DynamicState.Scissor;

            PipelineDynamicStateCreateInfo dynamicState = new PipelineDynamicStateCreateInfo {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = 2,
                PDynamicStates = dynamicStates
            };

            GraphicsPipelineCreateInfo pipelineInfo = new GraphicsPipelineCreateInfo {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = shaderStages,
                PVertexInputState = &vertexInputInfo,
                PInputAssemblyState = &inputAssembly,
                PViewportState = &viewportState,
                PRasterizationState = &rasterizer,
                PMultisampleState = &multisampling,
                PDepthStencilState = &depthStencil,
                PColorBlendState = &colorBlending,
                PDynamicState = &dynamicState,
                Layout = pipelineLayout,
                RenderPass = surface.RenderPass,
                Subpass = 0
            };

            Result result = context.Api.CreateGraphicsPipelines(context.Device, default, 1, pipelineInfo, null, out pipeline);
            SilkMarshal.Free((nint)shaderStages[0].PName);
            SilkMarshal.Free((nint)shaderStages[1].PName);

            if (result != Result.Success) {
                throw new InvalidOperationException($"Failed to create Vulkan graphics pipeline: {result}.");
            }
        }

        /// <summary>
        /// Destroys the active graphics pipeline.
        /// </summary>
        void DestroyPipeline() {
            if (pipeline.Handle != 0) {
                context.Api.DestroyPipeline(context.Device, pipeline, null);
                pipeline = default;
            }
        }

        /// <summary>
        /// Compiles a shader and creates a shader module.
        /// </summary>
        /// <param name="source">GLSL shader source.</param>
        /// <param name="kind">Shader stage kind.</param>
        /// <param name="name">Shader name for diagnostics.</param>
        /// <returns>Created shader module.</returns>
        ShaderModule CompileShaderModule(string source, ShaderKind kind, string name) {
            Shaderc shaderc = Shaderc.GetApi();

            Compiler* compiler = shaderc.CompilerInitialize();
            if (compiler == null) {
                throw new InvalidOperationException("Failed to initialize Shaderc compiler.");
            }

            CompileOptions* options = shaderc.CompileOptionsInitialize();
            if (options == null) {
                shaderc.CompilerRelease(compiler);
                throw new InvalidOperationException("Failed to initialize Shaderc compile options.");
            }

            shaderc.CompileOptionsSetTargetEnv(options, TargetEnv.Vulkan, (uint)EnvVersion.Vulkan12);

            CompilationResult* result = shaderc.CompileIntoSpv(
                compiler,
                source,
                (nuint)source.Length,
                kind,
                name,
                "main",
                options);

            CompilationStatus status = shaderc.ResultGetCompilationStatus(result);
            if (status != CompilationStatus.Success) {
                string error = shaderc.ResultGetErrorMessageS(result);
                shaderc.ResultRelease(result);
                shaderc.CompileOptionsRelease(options);
                shaderc.CompilerRelease(compiler);
                throw new InvalidOperationException($"Shader compilation failed: {error}");
            }

            nuint byteLength = shaderc.ResultGetLength(result);
            if (byteLength == 0) {
                shaderc.ResultRelease(result);
                shaderc.CompileOptionsRelease(options);
                shaderc.CompilerRelease(compiler);
                throw new InvalidOperationException("Shader compilation produced no output.");
            }

            byte* byteData = shaderc.ResultGetBytes(result);
            byte[] spirv = new byte[(int)byteLength];
            Marshal.Copy((IntPtr)byteData, spirv, 0, (int)byteLength);

            shaderc.ResultRelease(result);
            shaderc.CompileOptionsRelease(options);
            shaderc.CompilerRelease(compiler);

            return CreateShaderModule(spirv);
        }

        /// <summary>
        /// Creates a Vulkan shader module from SPIR-V bytes.
        /// </summary>
        /// <param name="spirv">SPIR-V bytecode.</param>
        /// <returns>Shader module handle.</returns>
        unsafe ShaderModule CreateShaderModule(byte[] spirv) {
            nint codePtr = SilkMarshal.Allocate(spirv.Length);
            Marshal.Copy(spirv, 0, codePtr, spirv.Length);

            ShaderModuleCreateInfo createInfo = new ShaderModuleCreateInfo {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)spirv.Length,
                PCode = (uint*)codePtr
            };

            Result result = context.Api.CreateShaderModule(context.Device, createInfo, null, out ShaderModule module);
            SilkMarshal.Free(codePtr);

            if (result != Result.Success) {
                throw new InvalidOperationException($"Failed to create Vulkan shader module: {result}.");
            }

            return module;
        }

        /// <summary>
        /// Creates a Vulkan texture resource from a texture asset.
        /// </summary>
        /// <param name="data">Texture asset to upload.</param>
        /// <returns>Texture resource.</returns>
        VulkanTextureResource CreateTextureResource(TextureAsset data) {
            ulong imageSize = (ulong)data.Colors.Length;
            var stagingBuffer = new VulkanGpuBuffer(
                context,
                imageSize,
                BufferUsageFlags.BufferUsageTransferSrcBit,
                MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyHostCoherentBit);
            stagingBuffer.Update(data.Colors);

            Image image;
            DeviceMemory memory;
            CreateImage(data.Width, data.Height, Format.R8G8B8A8Unorm, ImageUsageFlags.ImageUsageTransferDstBit | ImageUsageFlags.ImageUsageSampledBit, out image, out memory);

            TransitionImageLayout(image, ImageLayout.Undefined, ImageLayout.TransferDstOptimal);
            CopyBufferToImage(stagingBuffer.Handle, image, data.Width, data.Height);
            TransitionImageLayout(image, ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal);

            stagingBuffer.Dispose();

            ImageView imageView = CreateImageView(image, Format.R8G8B8A8Unorm);
            DescriptorSet descriptorSet = AllocateTextureDescriptorSet(imageView);

            return new VulkanTextureResource {
                Image = image,
                Memory = memory,
                ImageView = imageView,
                DescriptorSet = descriptorSet,
                Width = data.Width,
                Height = data.Height
            };
        }

        /// <summary>
        /// Releases a Vulkan texture resource.
        /// </summary>
        /// <param name="texture">Texture resource to destroy.</param>
        void DestroyTextureResource(VulkanTextureResource texture) {
            if (texture == null) {
                return;
            }

            if (texture.ImageView.Handle != 0) {
                context.Api.DestroyImageView(context.Device, texture.ImageView, null);
            }

            if (texture.Image.Handle != 0) {
                context.Api.DestroyImage(context.Device, texture.Image, null);
            }

            if (texture.Memory.Handle != 0) {
                context.Api.FreeMemory(context.Device, texture.Memory, null);
            }
        }

        /// <summary>
        /// Allocates a descriptor set for a texture image view.
        /// </summary>
        /// <param name="imageView">Image view to sample.</param>
        /// <returns>Allocated descriptor set.</returns>
        unsafe DescriptorSet AllocateTextureDescriptorSet(ImageView imageView) {
            DescriptorSetLayout* layouts = stackalloc DescriptorSetLayout[] { descriptorSetLayout };
            DescriptorSetAllocateInfo allocInfo = new DescriptorSetAllocateInfo {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = descriptorPool,
                DescriptorSetCount = 1,
                PSetLayouts = layouts
            };

            DescriptorSet descriptorSet;
            Result allocResult = context.Api.AllocateDescriptorSets(context.Device, allocInfo, out descriptorSet);
            if (allocResult != Result.Success) {
                throw new InvalidOperationException($"Failed to allocate descriptor set: {allocResult}.");
            }

            DescriptorImageInfo imageInfo = new DescriptorImageInfo {
                Sampler = sampler,
                ImageView = imageView,
                ImageLayout = ImageLayout.ShaderReadOnlyOptimal
            };

            WriteDescriptorSet descriptorWrite = new WriteDescriptorSet {
                SType = StructureType.WriteDescriptorSet,
                DstSet = descriptorSet,
                DstBinding = 0,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.CombinedImageSampler,
                PImageInfo = &imageInfo
            };

            WriteDescriptorSet* descriptorWrites = stackalloc WriteDescriptorSet[] { descriptorWrite };
            context.Api.UpdateDescriptorSets(context.Device, 1, descriptorWrites, 0, null);
            return descriptorSet;
        }

        /// <summary>
        /// Creates a Vulkan image and allocates memory for it.
        /// </summary>
        /// <param name="width">Image width in pixels.</param>
        /// <param name="height">Image height in pixels.</param>
        /// <param name="format">Image format.</param>
        /// <param name="usage">Image usage flags.</param>
        /// <param name="image">Created image handle.</param>
        /// <param name="memory">Allocated device memory.</param>
        unsafe void CreateImage(ushort width, ushort height, Format format, ImageUsageFlags usage, out Image image, out DeviceMemory memory) {
            ImageCreateInfo imageInfo = new ImageCreateInfo {
                SType = StructureType.ImageCreateInfo,
                ImageType = ImageType.Type2D,
                Extent = new Extent3D(width, height, 1),
                MipLevels = 1,
                ArrayLayers = 1,
                Format = format,
                Tiling = ImageTiling.Optimal,
                InitialLayout = ImageLayout.Undefined,
                Usage = usage,
                SharingMode = SharingMode.Exclusive,
                Samples = SampleCountFlags.SampleCount1Bit
            };

            Result imageResult = context.Api.CreateImage(context.Device, imageInfo, null, out image);
            if (imageResult != Result.Success) {
                throw new InvalidOperationException($"Failed to create Vulkan image: {imageResult}.");
            }

            MemoryRequirements memoryRequirements;
            context.Api.GetImageMemoryRequirements(context.Device, image, out memoryRequirements);

            MemoryAllocateInfo allocInfo = new MemoryAllocateInfo {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = memoryRequirements.Size,
                MemoryTypeIndex = context.FindMemoryType(memoryRequirements.MemoryTypeBits, MemoryPropertyFlags.MemoryPropertyDeviceLocalBit)
            };

            Result allocResult = context.Api.AllocateMemory(context.Device, allocInfo, null, out memory);
            if (allocResult != Result.Success) {
                throw new InvalidOperationException($"Failed to allocate Vulkan image memory: {allocResult}.");
            }

            context.Api.BindImageMemory(context.Device, image, memory, 0);
        }

        /// <summary>
        /// Creates an image view for a Vulkan image.
        /// </summary>
        /// <param name="image">Image to view.</param>
        /// <param name="format">Image format.</param>
        /// <returns>Created image view.</returns>
        unsafe ImageView CreateImageView(Image image, Format format) {
            ImageViewCreateInfo viewInfo = new ImageViewCreateInfo {
                SType = StructureType.ImageViewCreateInfo,
                Image = image,
                ViewType = ImageViewType.Type2D,
                Format = format,
                SubresourceRange = new ImageSubresourceRange {
                    AspectMask = ImageAspectFlags.ImageAspectColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                }
            };

            Result result = context.Api.CreateImageView(context.Device, viewInfo, null, out ImageView imageView);
            if (result != Result.Success) {
                throw new InvalidOperationException($"Failed to create Vulkan image view: {result}.");
            }

            return imageView;
        }

        /// <summary>
        /// Transitions an image between layouts.
        /// </summary>
        /// <param name="image">Image to transition.</param>
        /// <param name="format">Image format.</param>
        /// <param name="oldLayout">Old layout.</param>
        /// <param name="newLayout">New layout.</param>
        unsafe void TransitionImageLayout(Image image, ImageLayout oldLayout, ImageLayout newLayout) {
            CommandBuffer commandBuffer = context.BeginSingleTimeCommands();

            ImageMemoryBarrier barrier = new ImageMemoryBarrier {
                SType = StructureType.ImageMemoryBarrier,
                OldLayout = oldLayout,
                NewLayout = newLayout,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                Image = image,
                SubresourceRange = new ImageSubresourceRange {
                    AspectMask = ImageAspectFlags.ImageAspectColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                }
            };

            PipelineStageFlags sourceStage;
            PipelineStageFlags destinationStage;

            if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.TransferDstOptimal) {
                barrier.SrcAccessMask = 0;
                barrier.DstAccessMask = AccessFlags.AccessTransferWriteBit;
                sourceStage = PipelineStageFlags.PipelineStageTopOfPipeBit;
                destinationStage = PipelineStageFlags.PipelineStageTransferBit;
            } else if (oldLayout == ImageLayout.TransferDstOptimal && newLayout == ImageLayout.ShaderReadOnlyOptimal) {
                barrier.SrcAccessMask = AccessFlags.AccessTransferWriteBit;
                barrier.DstAccessMask = AccessFlags.AccessShaderReadBit;
                sourceStage = PipelineStageFlags.PipelineStageTransferBit;
                destinationStage = PipelineStageFlags.PipelineStageFragmentShaderBit;
            } else {
                throw new InvalidOperationException("Unsupported image layout transition.");
            }

            context.Api.CmdPipelineBarrier(
                commandBuffer,
                sourceStage,
                destinationStage,
                0,
                0,
                null,
                0,
                null,
                1,
                in barrier);

            context.EndSingleTimeCommands(commandBuffer);
        }

        /// <summary>
        /// Copies a buffer into an image.
        /// </summary>
        /// <param name="buffer">Buffer containing image data.</param>
        /// <param name="image">Destination image.</param>
        /// <param name="width">Image width.</param>
        /// <param name="height">Image height.</param>
        unsafe void CopyBufferToImage(VkBuffer buffer, Image image, ushort width, ushort height) {
            CommandBuffer commandBuffer = context.BeginSingleTimeCommands();

            BufferImageCopy region = new BufferImageCopy {
                BufferOffset = 0,
                BufferRowLength = 0,
                BufferImageHeight = 0,
                ImageSubresource = new ImageSubresourceLayers {
                    AspectMask = ImageAspectFlags.ImageAspectColorBit,
                    MipLevel = 0,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                },
                ImageOffset = new Offset3D(0, 0, 0),
                ImageExtent = new Extent3D(width, height, 1)
            };

            context.Api.CmdCopyBufferToImage(commandBuffer, buffer, image, ImageLayout.TransferDstOptimal, 1, in region);
            context.EndSingleTimeCommands(commandBuffer);
        }

        /// <summary>
        /// Configures the Vulkan viewport and scissor for the camera.
        /// </summary>
        /// <param name="offsetX">Viewport X offset in physical pixels.</param>
        /// <param name="offsetY">Viewport Y offset in physical pixels.</param>
        /// <param name="width">Viewport width in physical pixels.</param>
        /// <param name="height">Viewport height in physical pixels.</param>
        unsafe void SetViewportAndScissor(double offsetX, double offsetY, double width, double height) {
            int viewportX = (int)Math.Round(offsetX);
            int viewportY = (int)Math.Round(offsetY);
            int viewportWidth = Math.Max(1, (int)Math.Round(width));
            int viewportHeight = Math.Max(1, (int)Math.Round(height));

            int maxWidth = (int)currentSurface.Extent.Width;
            int maxHeight = (int)currentSurface.Extent.Height;
            if (viewportX < 0) {
                viewportWidth += viewportX;
                viewportX = 0;
            }

            if (viewportY < 0) {
                viewportHeight += viewportY;
                viewportY = 0;
            }

            viewportWidth = Math.Min(viewportWidth, Math.Max(1, maxWidth - viewportX));
            viewportHeight = Math.Min(viewportHeight, Math.Max(1, maxHeight - viewportY));
            currentScissorX = viewportX;
            currentScissorY = viewportY;
            currentScissorWidth = viewportWidth;
            currentScissorHeight = viewportHeight;

            Viewport vkViewport = new Viewport {
                X = viewportX,
                Y = viewportY,
                Width = viewportWidth,
                Height = viewportHeight,
                MinDepth = 0f,
                MaxDepth = 1f
            };

            Rect2D scissor = new Rect2D {
                Offset = new Offset2D(viewportX, viewportY),
                Extent = new Extent2D((uint)viewportWidth, (uint)viewportHeight)
            };

            Viewport* viewports = stackalloc Viewport[] { vkViewport };
            Rect2D* scissors = stackalloc Rect2D[] { scissor };
            context.Api.CmdSetViewport(currentCommandBuffer, 0, 1, viewports);
            context.Api.CmdSetScissor(currentCommandBuffer, 0, 1, scissors);
        }

        /// <summary>
        /// Synchronizes the active clip stack with the drawable currently being visited and applies the resulting scissor rectangle.
        /// </summary>
        void SyncClipTransitions() {
            int sharedPrefixLength = GetSharedPrefixLength();

            while (ActiveClipChain.Count > sharedPrefixLength) {
                ActiveClipChain.RemoveAt(ActiveClipChain.Count - 1);
                ActiveClipRects.RemoveAt(ActiveClipRects.Count - 1);
            }

            while (ActiveClipChain.Count < NextClipChain.Count) {
                IClipRegion2D clipRegion = NextClipChain[ActiveClipChain.Count];
                float4 resolvedRect = ResolveClipRectForPush(clipRegion);
                ActiveClipChain.Add(clipRegion);
                ActiveClipRects.Add(resolvedRect);
            }

            if (ActiveClipRects.Count > 0) {
                ApplyClipScissor(ActiveClipRects[ActiveClipRects.Count - 1]);
            } else {
                ApplyCameraScissor();
            }
        }

        /// <summary>
        /// Returns the number of leading clip owners shared between the current and next clip chains.
        /// </summary>
        /// <returns>Shared clip-chain prefix length.</returns>
        int GetSharedPrefixLength() {
            int sharedPrefixLength = 0;
            int maxSharedLength = Math.Min(ActiveClipChain.Count, NextClipChain.Count);
            while (sharedPrefixLength < maxSharedLength &&
                   ReferenceEquals(ActiveClipChain[sharedPrefixLength], NextClipChain[sharedPrefixLength])) {
                sharedPrefixLength++;
            }

            return sharedPrefixLength;
        }

        /// <summary>
        /// Resolves one clip region against the current active clip stack.
        /// </summary>
        /// <param name="clipRegion">Clip region to resolve.</param>
        /// <returns>Effective clip rectangle in logical screen coordinates.</returns>
        float4 ResolveClipRectForPush(IClipRegion2D clipRegion) {
            float4 resolvedRect = clipRegion.GetClipRect();
            if (ActiveClipRects.Count <= 0) {
                return resolvedRect;
            }

            float4 currentRect = ActiveClipRects[ActiveClipRects.Count - 1];
            return ClipRegionStackBuilder.Intersect(currentRect, resolvedRect);
        }

        /// <summary>
        /// Restores the camera viewport scissor after the clip stack empties.
        /// </summary>
        unsafe void ApplyCameraScissor() {
            Rect2D scissor = new Rect2D {
                Offset = new Offset2D(currentScissorX, currentScissorY),
                Extent = new Extent2D((uint)currentScissorWidth, (uint)currentScissorHeight)
            };

            Rect2D* scissors = stackalloc Rect2D[] { scissor };
            context.Api.CmdSetScissor(currentCommandBuffer, 0, 1, scissors);
        }

        /// <summary>
        /// Applies one resolved clip rectangle to the active Vulkan scissor state.
        /// </summary>
        /// <param name="clipRect">Logical clip rectangle resolved for the current drawable.</param>
        unsafe void ApplyClipScissor(float4 clipRect) {
            float4 viewportRect = new float4((float)currentViewportOffsetX, (float)currentViewportOffsetY, (float)currentViewportWidth, (float)currentViewportHeight);
            float4 effectiveRect = ClipRegionStackBuilder.Intersect(viewportRect, clipRect);

            double pixelScaleX = currentSurface.Extent.Width / currentSurface.LogicalWidth;
            double pixelScaleY = currentSurface.Extent.Height / currentSurface.LogicalHeight;
            int scissorX = (int)Math.Round(effectiveRect.X * pixelScaleX);
            int scissorY = (int)Math.Round(effectiveRect.Y * pixelScaleY);
            int scissorWidth = Math.Max(0, (int)Math.Round(effectiveRect.Z * pixelScaleX));
            int scissorHeight = Math.Max(0, (int)Math.Round(effectiveRect.W * pixelScaleY));

            Rect2D scissor = new Rect2D {
                Offset = new Offset2D(scissorX, scissorY),
                Extent = new Extent2D((uint)scissorWidth, (uint)scissorHeight)
            };

            Rect2D* scissors = stackalloc Rect2D[] { scissor };
            context.Api.CmdSetScissor(currentCommandBuffer, 0, 1, scissors);
        }

        /// <summary>
        /// Draws a textured quad using the sprite pipeline.
        /// </summary>
        /// <param name="texture">Texture to sample.</param>
        /// <param name="x">Left position in pixels.</param>
        /// <param name="y">Top position in pixels.</param>
        /// <param name="width">Width in pixels.</param>
        /// <param name="height">Height in pixels.</param>
        /// <param name="uvRect">Source UV rectangle.</param>
        /// <param name="color">Vertex color modulation.</param>
        unsafe void DrawQuad(VulkanTextureResource texture, double x, double y, double width, double height, float4 uvRect, byte4 color) {
            if (currentViewportWidth <= 0.0 || currentViewportHeight <= 0.0) {
                return;
            }

            if (recordedQuadCount >= MaxQuadsPerFrame) {
                throw new InvalidOperationException("Exceeded the Vulkan 2D per-frame quad capacity.");
            }

            double left = x;
            double top = y;
            double right = x + width;
            double bottom = y + height;

            float ndcLeft = (float)ComputeNdcX(left);
            float ndcRight = (float)ComputeNdcX(right);
            float ndcTop = (float)ComputeNdcY(top);
            float ndcBottom = (float)ComputeNdcY(bottom);

            float u0 = uvRect.X;
            float v0 = uvRect.Y;
            float u1 = uvRect.X + uvRect.Z;
            float v1 = uvRect.Y + uvRect.W;

            float4 colorVector = new float4(
                (float)(color.X / 255.0),
                (float)(color.Y / 255.0),
                (float)(color.Z / 255.0),
                (float)(color.W / 255.0));

            var vertices = new VulkanSpriteVertex[QuadVertexCount];
            vertices[0] = new VulkanSpriteVertex(new float2(ndcLeft, ndcTop), new float2(u0, v0), colorVector);
            vertices[1] = new VulkanSpriteVertex(new float2(ndcRight, ndcTop), new float2(u1, v0), colorVector);
            vertices[2] = new VulkanSpriteVertex(new float2(ndcRight, ndcBottom), new float2(u1, v1), colorVector);
            vertices[3] = new VulkanSpriteVertex(new float2(ndcLeft, ndcBottom), new float2(u0, v1), colorVector);

            int quadIndex = recordedQuadCount;
            ulong vertexByteOffset = (ulong)(quadIndex * QuadVertexCount * VulkanSpriteVertex.SizeInBytes);
            vertexBuffer.Update(vertices, vertexByteOffset);
            recordedQuadCount++;

            context.Api.CmdBindPipeline(currentCommandBuffer, PipelineBindPoint.Graphics, pipeline);

            DescriptorSet descriptorSet = texture.DescriptorSet;
            DescriptorSet* descriptorSets = stackalloc DescriptorSet[] { descriptorSet };
            context.Api.CmdBindDescriptorSets(currentCommandBuffer, PipelineBindPoint.Graphics, pipelineLayout, 0, 1, descriptorSets, 0, null);

            ulong offset = vertexByteOffset;
            VkBuffer* vertexBuffers = stackalloc VkBuffer[] { vertexBuffer.Handle };
            ulong* offsets = stackalloc ulong[] { offset };
            context.Api.CmdBindVertexBuffers(currentCommandBuffer, 0, 1, vertexBuffers, offsets);
            context.Api.CmdBindIndexBuffer(currentCommandBuffer, indexBuffer.Handle, 0, IndexType.Uint32);

            context.Api.CmdDrawIndexed(currentCommandBuffer, QuadIndexCount, 1, 0, 0, 0);
        }

        /// <summary>
        /// Converts a pixel X coordinate into NDC space.
        /// </summary>
        /// <param name="pixelX">Logical pixel coordinate.</param>
        /// <returns>Normalized device coordinate.</returns>
        double ComputeNdcX(double pixelX) {
            return ((pixelX - currentViewportOffsetX) / currentViewportWidth) * 2.0 - 1.0;
        }

        /// <summary>
        /// Converts a pixel Y coordinate into NDC space.
        /// </summary>
        /// <param name="pixelY">Logical pixel coordinate.</param>
        /// <returns>Normalized device coordinate.</returns>
        double ComputeNdcY(double pixelY) {
            return ((pixelY - currentViewportOffsetY) / currentViewportHeight) * 2.0 - 1.0;
        }

        /// <summary>
        /// Provides the GLSL source for the sprite vertex shader.
        /// </summary>
        /// <returns>Vertex shader GLSL source.</returns>
        string GetVertexShaderSource() {
            return @"#version 450
layout(location = 0) in vec2 inPos;
layout(location = 1) in vec2 inUv;
layout(location = 2) in vec4 inColor;

layout(location = 0) out vec2 fragUv;
layout(location = 1) out vec4 fragColor;

void main() {
    gl_Position = vec4(inPos, 0.0, 1.0);
    fragUv = inUv;
    fragColor = inColor;
}";
        }

        /// <summary>
        /// Provides the GLSL source for the sprite fragment shader.
        /// </summary>
        /// <returns>Fragment shader GLSL source.</returns>
        string GetFragmentShaderSource() {
            return @"#version 450
layout(set = 0, binding = 0) uniform sampler2D texSampler;

layout(location = 0) in vec2 fragUv;
layout(location = 1) in vec4 fragColor;

layout(location = 0) out vec4 outColor;

void main() {
    vec4 texColor = texture(texSampler, fragUv);
    outColor = texColor * fragColor;
}";
        }
    }
}
