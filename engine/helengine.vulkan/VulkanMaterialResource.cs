using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using System.Runtime.InteropServices;

namespace helengine.vulkan {
    /// <summary>
    /// Vulkan-backed runtime material resource that owns shader modules and graphics pipeline state.
    /// </summary>
    public sealed unsafe class VulkanMaterialResource : RuntimeMaterial, IDisposable {
        /// <summary>
        /// Vertex shader entry point expected in compiled SPIR-V modules.
        /// </summary>
        const string VertexEntryPoint = "VS";
        /// <summary>
        /// Fragment shader entry point expected in compiled SPIR-V modules.
        /// </summary>
        const string PixelEntryPoint = "PS";

        /// <summary>
        /// Shared Vulkan context used to create and destroy GPU objects.
        /// </summary>
        readonly VulkanContext Context;
        /// <summary>
        /// Tracks whether this material resource has been disposed.
        /// </summary>
        bool Disposed;
        /// <summary>
        /// Current graphics pipeline created for this material.
        /// </summary>
        Pipeline Pipeline;
        /// <summary>
        /// Render pass associated with the active pipeline.
        /// </summary>
        RenderPass PipelineRenderPass;
        /// <summary>
        /// Swapchain version associated with the active pipeline.
        /// </summary>
        int PipelineSwapchainVersion;

        /// <summary>
        /// Initializes a new Vulkan material resource and creates shader modules from SPIR-V bytecode.
        /// </summary>
        /// <param name="context">Shared Vulkan context.</param>
        /// <param name="shaderAssetId">Shader asset identifier associated with the material.</param>
        /// <param name="vertexProgram">Vertex program selected by the material.</param>
        /// <param name="pixelProgram">Pixel program selected by the material.</param>
        /// <param name="variant">Shader variant selected by the material.</param>
        /// <param name="vertexBytecode">Compiled vertex SPIR-V bytecode.</param>
        /// <param name="pixelBytecode">Compiled fragment SPIR-V bytecode.</param>
        public VulkanMaterialResource(
            VulkanContext context,
            string shaderAssetId,
            string vertexProgram,
            string pixelProgram,
            string variant,
            byte[] vertexBytecode,
            byte[] pixelBytecode) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            if (string.IsNullOrWhiteSpace(shaderAssetId)) {
                throw new InvalidOperationException("Material shader asset id must be provided.");
            }

            if (string.IsNullOrWhiteSpace(vertexProgram)) {
                throw new InvalidOperationException("Material vertex program must be provided.");
            }

            if (string.IsNullOrWhiteSpace(pixelProgram)) {
                throw new InvalidOperationException("Material pixel program must be provided.");
            }

            if (string.IsNullOrWhiteSpace(variant)) {
                throw new InvalidOperationException("Material variant must be provided.");
            }

            if (vertexBytecode == null || vertexBytecode.Length == 0) {
                throw new InvalidOperationException("Vulkan vertex shader bytecode must be provided.");
            }

            if (pixelBytecode == null || pixelBytecode.Length == 0) {
                throw new InvalidOperationException("Vulkan pixel shader bytecode must be provided.");
            }

            Context = context;
            ShaderAssetId = shaderAssetId;
            VertexProgram = vertexProgram;
            PixelProgram = pixelProgram;
            Variant = variant;
            VertexShaderModule = CreateShaderModule(vertexBytecode);
            PixelShaderModule = CreateShaderModule(pixelBytecode);
            PipelineSwapchainVersion = -1;
        }

        /// <summary>
        /// Gets the shader asset identifier selected by this material.
        /// </summary>
        public string ShaderAssetId { get; }

        /// <summary>
        /// Gets the vertex program selected by this material.
        /// </summary>
        public string VertexProgram { get; }

        /// <summary>
        /// Gets the pixel program selected by this material.
        /// </summary>
        public string PixelProgram { get; }

        /// <summary>
        /// Gets the shader variant selected by this material.
        /// </summary>
        public string Variant { get; }

        /// <summary>
        /// Gets the compiled Vulkan vertex shader module.
        /// </summary>
        public ShaderModule VertexShaderModule { get; private set; }

        /// <summary>
        /// Gets the compiled Vulkan fragment shader module.
        /// </summary>
        public ShaderModule PixelShaderModule { get; private set; }

        /// <summary>
        /// Ensures the graphics pipeline is valid for the current surface and returns it.
        /// </summary>
        /// <param name="surface">Surface being rendered.</param>
        /// <param name="pipelineLayout">Pipeline layout to use.</param>
        /// <returns>Graphics pipeline handle.</returns>
        public Pipeline EnsurePipeline(VulkanSwapchainSurface surface, PipelineLayout pipelineLayout) {
            if (Disposed) {
                throw new ObjectDisposedException(nameof(VulkanMaterialResource));
            }

            if (surface == null) {
                throw new ArgumentNullException(nameof(surface));
            }

            if (pipelineLayout.Handle == 0) {
                throw new InvalidOperationException("Pipeline layout must be created before material pipeline creation.");
            }

            if (Pipeline.Handle != 0 &&
                PipelineRenderPass.Handle == surface.RenderPass.Handle &&
                PipelineSwapchainVersion == surface.SwapchainVersion) {
                return Pipeline;
            }

            DestroyPipeline();
            CreatePipeline(surface, pipelineLayout);
            PipelineRenderPass = surface.RenderPass;
            PipelineSwapchainVersion = surface.SwapchainVersion;
            return Pipeline;
        }

        /// <summary>
        /// Replaces shader modules and invalidates pipeline state after hot reload.
        /// </summary>
        /// <param name="vertexBytecode">Compiled vertex SPIR-V bytecode.</param>
        /// <param name="pixelBytecode">Compiled fragment SPIR-V bytecode.</param>
        public void UpdateShaderBytecode(byte[] vertexBytecode, byte[] pixelBytecode) {
            if (Disposed) {
                throw new ObjectDisposedException(nameof(VulkanMaterialResource));
            }

            if (vertexBytecode == null || vertexBytecode.Length == 0) {
                throw new InvalidOperationException("Vulkan vertex shader bytecode must be provided.");
            }

            if (pixelBytecode == null || pixelBytecode.Length == 0) {
                throw new InvalidOperationException("Vulkan pixel shader bytecode must be provided.");
            }

            DestroyPipeline();
            DestroyShaderModules();
            VertexShaderModule = CreateShaderModule(vertexBytecode);
            PixelShaderModule = CreateShaderModule(pixelBytecode);
        }

        /// <summary>
        /// Releases Vulkan resources owned by this material.
        /// </summary>
        public void Dispose() {
            if (Disposed) {
                return;
            }

            Disposed = true;
            DestroyPipeline();
            DestroyShaderModules();
        }

        /// <summary>
        /// Creates a graphics pipeline compatible with the target surface render pass.
        /// </summary>
        /// <param name="surface">Surface to render into.</param>
        /// <param name="pipelineLayout">Pipeline layout to bind.</param>
        void CreatePipeline(VulkanSwapchainSurface surface, PipelineLayout pipelineLayout) {
            nint vertexEntry = SilkMarshal.StringToPtr(VertexEntryPoint);
            nint pixelEntry = SilkMarshal.StringToPtr(PixelEntryPoint);
            try {
                PipelineShaderStageCreateInfo* shaderStages = stackalloc PipelineShaderStageCreateInfo[2];
                shaderStages[0] = new PipelineShaderStageCreateInfo {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = ShaderStageFlags.ShaderStageVertexBit,
                    Module = VertexShaderModule,
                    PName = (byte*)vertexEntry
                };
                shaderStages[1] = new PipelineShaderStageCreateInfo {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = ShaderStageFlags.ShaderStageFragmentBit,
                    Module = PixelShaderModule,
                    PName = (byte*)pixelEntry
                };

                VertexInputBindingDescription vertexBinding = new VertexInputBindingDescription {
                    Binding = 0,
                    Stride = (uint)VulkanVertex3D.SizeInBytes,
                    InputRate = VertexInputRate.Vertex
                };

                VertexInputAttributeDescription* vertexAttributes = stackalloc VertexInputAttributeDescription[3];
                vertexAttributes[0] = new VertexInputAttributeDescription {
                    Binding = 0,
                    Location = 0,
                    Format = Format.R32G32B32Sfloat,
                    Offset = 0
                };
                vertexAttributes[1] = new VertexInputAttributeDescription {
                    Binding = 0,
                    Location = 1,
                    Format = Format.R32G32B32Sfloat,
                    Offset = 12
                };
                vertexAttributes[2] = new VertexInputAttributeDescription {
                    Binding = 0,
                    Location = 2,
                    Format = Format.R32G32Sfloat,
                    Offset = 24
                };

                PipelineVertexInputStateCreateInfo vertexInputInfo = new PipelineVertexInputStateCreateInfo {
                    SType = StructureType.PipelineVertexInputStateCreateInfo,
                    VertexBindingDescriptionCount = 1,
                    PVertexBindingDescriptions = &vertexBinding,
                    VertexAttributeDescriptionCount = 3,
                    PVertexAttributeDescriptions = vertexAttributes
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
                    CullMode = CullModeFlags.CullModeBackBit,
                    FrontFace = FrontFace.CounterClockwise,
                    DepthBiasEnable = false
                };

                PipelineMultisampleStateCreateInfo multisampling = new PipelineMultisampleStateCreateInfo {
                    SType = StructureType.PipelineMultisampleStateCreateInfo,
                    SampleShadingEnable = false,
                    RasterizationSamples = SampleCountFlags.SampleCount1Bit
                };

                PipelineDepthStencilStateCreateInfo depthStencil = new PipelineDepthStencilStateCreateInfo {
                    SType = StructureType.PipelineDepthStencilStateCreateInfo,
                    DepthTestEnable = true,
                    DepthWriteEnable = true,
                    DepthCompareOp = CompareOp.Less,
                    DepthBoundsTestEnable = false,
                    StencilTestEnable = false
                };

                PipelineColorBlendAttachmentState colorBlendAttachment = new PipelineColorBlendAttachmentState {
                    BlendEnable = false,
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

                Result result = Context.Api.CreateGraphicsPipelines(Context.Device, default, 1, in pipelineInfo, null, out Pipeline);
                if (result != Result.Success) {
                    throw new InvalidOperationException($"Failed to create Vulkan 3D graphics pipeline: {result}.");
                }
            } finally {
                SilkMarshal.Free(vertexEntry);
                SilkMarshal.Free(pixelEntry);
            }
        }

        /// <summary>
        /// Destroys the active graphics pipeline if one exists.
        /// </summary>
        void DestroyPipeline() {
            if (Pipeline.Handle == 0) {
                return;
            }

            Context.Api.DestroyPipeline(Context.Device, Pipeline, null);
            Pipeline = default;
            PipelineRenderPass = default;
            PipelineSwapchainVersion = -1;
        }

        /// <summary>
        /// Destroys shader modules owned by this material.
        /// </summary>
        void DestroyShaderModules() {
            if (VertexShaderModule.Handle != 0) {
                Context.Api.DestroyShaderModule(Context.Device, VertexShaderModule, null);
                VertexShaderModule = default;
            }

            if (PixelShaderModule.Handle != 0) {
                Context.Api.DestroyShaderModule(Context.Device, PixelShaderModule, null);
                PixelShaderModule = default;
            }
        }

        /// <summary>
        /// Creates a Vulkan shader module from SPIR-V bytecode.
        /// </summary>
        /// <param name="spirv">SPIR-V bytecode payload.</param>
        /// <returns>Created shader module.</returns>
        ShaderModule CreateShaderModule(byte[] spirv) {
            nint codePtr = SilkMarshal.Allocate(spirv.Length);
            Marshal.Copy(spirv, 0, codePtr, spirv.Length);
            try {
                ShaderModuleCreateInfo createInfo = new ShaderModuleCreateInfo {
                    SType = StructureType.ShaderModuleCreateInfo,
                    CodeSize = (nuint)spirv.Length,
                    PCode = (uint*)codePtr
                };

                Result result = Context.Api.CreateShaderModule(Context.Device, createInfo, null, out ShaderModule module);
                if (result != Result.Success) {
                    throw new InvalidOperationException($"Failed to create Vulkan shader module: {result}.");
                }

                return module;
            } finally {
                SilkMarshal.Free(codePtr);
            }
        }
    }
}
