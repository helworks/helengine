using Silk.NET.Vulkan;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace helengine.vulkan {
    /// <summary>
    /// Vulkan-backed renderer responsible for swapchain management and 3D rendering.
    /// </summary>
    public class VulkanRenderer3D : RenderManager3D, IRenderVisitor3D {
        /// <summary>
        /// Maximum number of per-draw transform matrices reserved in the dynamic uniform buffer.
        /// </summary>
        const uint MaxTransformMatricesPerFrame = 4096;
        /// <summary>
        /// Size in bytes of the built-in default mesh transform payload.
        /// </summary>
        static readonly uint TransformBufferSizeBytes = (uint)Marshal.SizeOf<StandardMeshShaderData>();
        /// <summary>
        /// Size in bytes of the legacy single-matrix transform payload used by gizmo shaders and other non-standard materials.
        /// </summary>
        static readonly uint LegacyTransformBufferSizeBytes = (uint)Marshal.SizeOf<float4x4>();
        /// <summary>
        /// Maximum number of textured 3D materials that can allocate descriptor sets.
        /// </summary>
        const uint MaxMaterialTextures = 2048;
        /// <summary>
        /// Binding slot used by the shared transform constant buffer inside Vulkan material descriptor sets.
        /// </summary>
        const uint TransformDescriptorBindingSlot = 0;
        /// <summary>
        /// Default forward axis for cameras before rotation.
        /// </summary>
        static readonly float3 DefaultForward = new float3(0f, 0f, -1f);
        /// <summary>
        /// Default up axis for cameras before rotation.
        /// </summary>
        static readonly float3 DefaultUp = new float3(0f, 1f, 0f);
        /// <summary>
        /// Binding slot used by textured materials for their primary sampled image.
        /// </summary>
        static readonly uint MaterialTextureDescriptorBindingSlot = (uint)ShaderBindingPolicies.Default.GetSlot(ShaderResourceType.Texture2D, 0);
        /// <summary>
        /// Binding slot used by textured materials for their primary sampler state.
        /// </summary>
        static readonly uint MaterialSamplerDescriptorBindingSlot = (uint)ShaderBindingPolicies.Default.GetSlot(ShaderResourceType.Sampler, 0);

        /// <summary>
        /// Shared Vulkan context for the renderer.
        /// </summary>
        readonly VulkanContext context;
        /// <summary>
        /// Swapchain surfaces tracked by the renderer.
        /// </summary>
        readonly List<VulkanSwapchainSurface> surfaces;
        /// <summary>
        /// Lookup of swapchain surfaces by window handle.
        /// </summary>
        readonly Dictionary<IntPtr, VulkanSwapchainSurface> surfacesByHandle;
        /// <summary>
        /// 2D renderer used for UI overlays.
        /// </summary>
        readonly VulkanRenderer2D renderer2D;
        /// <summary>
        /// Materials grouped by shader asset id for hot reload updates.
        /// </summary>
        readonly Dictionary<string, List<VulkanMaterialResource>> materialsByShaderAssetId;
        /// <summary>
        /// Tracks the runtime texture currently written into each material descriptor set.
        /// </summary>
        readonly Dictionary<VulkanMaterialResource, RuntimeTexture> materialBoundTextures;
        /// <summary>
        /// Descriptor set layout used by 3D materials to bind their transform buffer, sampled image, and sampler state.
        /// </summary>
        DescriptorSetLayout materialDescriptorSetLayout;
        /// <summary>
        /// Pipeline layout used for 3D material pipelines.
        /// </summary>
        PipelineLayout materialPipelineLayout;
        /// <summary>
        /// Descriptor pool used to allocate 3D material descriptor sets.
        /// </summary>
        DescriptorPool materialDescriptorPool;
        /// <summary>
        /// Sampler shared by textured 3D materials.
        /// </summary>
        Sampler materialTextureSampler;
        /// <summary>
        /// Dynamic uniform buffer storing world-view-projection matrices.
        /// </summary>
        VulkanGpuBuffer transformUniformBuffer;
        /// <summary>
        /// Stride in bytes between matrix entries in the dynamic uniform buffer.
        /// </summary>
        ulong transformBufferStride;
        /// <summary>
        /// Tracks how many transform entries have been written for the active frame.
        /// </summary>
        uint transformDrawCount;
        /// <summary>
        /// Surface currently being rendered.
        /// </summary>
        VulkanSwapchainSurface activeSurface;
        /// <summary>
        /// Command buffer currently recording 3D draw calls.
        /// </summary>
        CommandBuffer activeCommandBuffer;
        /// <summary>
        /// Cached view-projection matrix for the active camera render pass.
        /// </summary>
        float4x4 currentViewProjection;
        /// <summary>
        /// World-space camera position for the active 3D camera pass.
        /// </summary>
        float3 currentCameraPosition;
        /// <summary>
        /// Tracks whether the renderer is inside an active surface frame.
        /// </summary>
        bool frameActive;
        /// <summary>
        /// Tracks whether the renderer has been disposed.
        /// </summary>
        bool disposed;

        /// <summary>
        /// Initializes the Vulkan renderer and its shared context.
        /// </summary>
        public VulkanRenderer3D() {
            context = new VulkanContext();
            surfaces = new List<VulkanSwapchainSurface>();
            surfacesByHandle = new Dictionary<IntPtr, VulkanSwapchainSurface>();
            renderer2D = new VulkanRenderer2D(context);
            materialsByShaderAssetId = new Dictionary<string, List<VulkanMaterialResource>>(StringComparer.OrdinalIgnoreCase);
            materialBoundTextures = new Dictionary<VulkanMaterialResource, RuntimeTexture>();

            CreateMaterialResources();
            WindowResized += OnWindowResized;
        }

        /// <summary>
        /// Gets the Vulkan API entry point.
        /// </summary>
        public Vk Api { get { return context.Api; } }

        /// <summary>
        /// Gets the 2D renderer used for UI rendering.
        /// </summary>
        public VulkanRenderer2D Render2D { get { return renderer2D; } }

        /// <summary>
        /// Adds a window and creates a swapchain surface for it.
        /// </summary>
        /// <param name="handle">Native window handle.</param>
        /// <param name="width">Window width.</param>
        /// <param name="height">Window height.</param>
        public override void AddWindow(IntPtr handle, int width, int height) {
            base.AddWindow(handle, width, height);

            var surface = new VulkanSwapchainSurface(context, handle, width, height);
            surfaces.Add(surface);
            surfacesByHandle.Add(handle, surface);
            renderer2D.AttachSurface(surface);
        }

        /// <summary>
        /// Builds a runtime model from raw asset data.
        /// </summary>
        /// <param name="data">Raw model data.</param>
        /// <returns>Runtime model instance.</returns>
        public override RuntimeModel BuildModelFromRaw(ModelAsset data) {
            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            if (data.Positions == null || data.Positions.Length == 0) {
                throw new ArgumentException("Model data must include positions.", nameof(data));
            }

            if (data.Normals == null || data.Normals.Length != data.Positions.Length) {
                throw new ArgumentException("Model data must include matching normals.", nameof(data));
            }

            if (data.TexCoords == null || data.TexCoords.Length != data.Positions.Length) {
                throw new ArgumentException("Model data must include matching texture coordinates.", nameof(data));
            }

            ModelAssetIndexData indexData = ModelAssetIndexData.Resolve(data);
            var vertices = new VulkanVertex3D[data.Positions.Length];
            for (int i = 0; i < data.Positions.Length; i++) {
                float3 position = data.Positions[i];
                float3 normal = data.Normals[i];
                float2 texCoord = data.TexCoords[i];
                vertices[i] = new VulkanVertex3D(position, normal, texCoord);
            }

            ulong vertexBufferSize = (ulong)(vertices.Length * VulkanVertex3D.SizeInBytes);
            var vertexBuffer = new VulkanGpuBuffer(
                context,
                vertexBufferSize,
                BufferUsageFlags.BufferUsageVertexBufferBit,
                MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyHostCoherentBit);
            vertexBuffer.Update(vertices);

            VulkanGpuBuffer indexBuffer = null;
            int indexCount = 0;
            bool uses32BitIndices = false;
            if (indexData.IndexCount > 0) {
                indexCount = indexData.IndexCount;
                uses32BitIndices = indexData.Uses32BitIndices;
                ulong indexBufferSize = uses32BitIndices
                    ? (ulong)(indexData.IndexCount * sizeof(uint))
                    : (ulong)(indexData.IndexCount * sizeof(ushort));
                indexBuffer = new VulkanGpuBuffer(
                    context,
                    indexBufferSize,
                    BufferUsageFlags.BufferUsageIndexBufferBit,
                    MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyHostCoherentBit);
                if (uses32BitIndices) {
                    indexBuffer.Update(indexData.Indices32);
                } else {
                    indexBuffer.Update(indexData.Indices16);
                }
            }

            var model = new VulkanModelResource {
                VertexBuffer = vertexBuffer,
                IndexBuffer = indexBuffer,
                VertexCount = vertices.Length,
                IndexCount = indexCount,
                Uses32BitIndices = uses32BitIndices
            };
            model.SetSubmeshes(ModelSubmeshResolver.BuildRuntimeSubmeshes(data));

            return model;
        }

        /// <summary>
        /// Creates a Vulkan render target descriptor.
        /// </summary>
        /// <param name="width">Render target width in pixels.</param>
        /// <param name="height">Render target height in pixels.</param>
        /// <returns>Render target descriptor for camera assignment.</returns>
        public override RenderTarget CreateRenderTarget(int width, int height) {
            return new VulkanRenderTargetResource(width, height);
        }

        /// <summary>
        /// Builds a runtime material from raw asset data.
        /// </summary>
        /// <param name="materialAsset">Raw material asset definition.</param>
        /// <param name="shaderAsset">Shader asset used by the material.</param>
        /// <returns>Runtime material instance.</returns>
        public override RuntimeMaterial BuildMaterialFromRaw(MaterialAsset materialAsset, ShaderAsset shaderAsset) {
            if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }

            if (shaderAsset == null) {
                throw new ArgumentNullException(nameof(shaderAsset));
            }

            if (string.IsNullOrWhiteSpace(materialAsset.ShaderAssetId)) {
                throw new InvalidOperationException("Material assets must reference a shader asset id.");
            }

            if (!string.Equals(materialAsset.ShaderAssetId, shaderAsset.Id, StringComparison.Ordinal)) {
                throw new InvalidOperationException("Material asset shader id does not match the provided shader asset.");
            }

            string targetName = ShaderTargetNames.GetTargetName(ShaderCompileTarget.Vulkan);
            if (!string.Equals(shaderAsset.TargetName, targetName, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("Shader asset target does not match the Vulkan renderer.");
            }

            ShaderBinaryAsset vertexBinary = GetShaderBinary(shaderAsset, materialAsset.VertexProgram, ShaderStage.Vertex, materialAsset.Variant);
            ShaderBinaryAsset pixelBinary = GetShaderBinary(shaderAsset, materialAsset.PixelProgram, ShaderStage.Pixel, materialAsset.Variant);
            ShaderProgramAsset vertexProgram = GetShaderProgram(shaderAsset, materialAsset.VertexProgram, ShaderStage.Vertex);
            ShaderProgramAsset pixelProgram = GetShaderProgram(shaderAsset, materialAsset.PixelProgram, ShaderStage.Pixel);

            var material = new VulkanMaterialResource(
                context,
                materialAsset.ShaderAssetId,
                materialAsset.VertexProgram,
                materialAsset.PixelProgram,
                materialAsset.Variant,
                vertexProgram.EntryPoint,
                pixelProgram.EntryPoint,
                vertexBinary.Bytecode,
                pixelBinary.Bytecode);
            material.SetId(materialAsset.Id);
            MaterialLayout layout = MaterialLayoutBuilder.Build(materialAsset, shaderAsset);
            material.SetLayout(layout);
            material.SetRenderState(materialAsset.RenderState);
            material.ApplyConstantBufferDefaults(materialAsset.ConstantBuffers ?? Array.Empty<MaterialConstantBufferAsset>());
            StandardMaterialTextureBindingDefaults.Apply(material);
            material.MaterialDescriptorSet = AllocateMaterialDescriptorSet();
            RegisterMaterial(material);
            return material;
        }

        /// <summary>
        /// Invalidates cached shader resources and updates materials for the specified shader asset.
        /// </summary>
        /// <param name="shaderAssetId">Shader asset identifier to invalidate.</param>
        /// <param name="shaderAsset">Updated shader asset data.</param>
        public override void InvalidateShaderResources(string shaderAssetId, ShaderAsset shaderAsset) {
            if (string.IsNullOrWhiteSpace(shaderAssetId)) {
                throw new ArgumentException("Shader asset id must be provided.", nameof(shaderAssetId));
            }

            if (shaderAsset == null) {
                throw new ArgumentNullException(nameof(shaderAsset));
            }

            if (!materialsByShaderAssetId.TryGetValue(shaderAssetId, out List<VulkanMaterialResource> materials)) {
                return;
            }

            for (int i = 0; i < materials.Count; i++) {
                VulkanMaterialResource material = materials[i];
                if (material == null) {
                    continue;
                }

                ShaderBinaryAsset vertexBinary = GetShaderBinary(shaderAsset, material.VertexProgram, ShaderStage.Vertex, material.Variant);
                ShaderBinaryAsset pixelBinary = GetShaderBinary(shaderAsset, material.PixelProgram, ShaderStage.Pixel, material.Variant);
                MaterialLayout layout = BuildMaterialLayout(material, shaderAsset);
                material.UpdateShaderBytecode(vertexBinary.Bytecode, pixelBinary.Bytecode);
                material.SetLayout(layout);
            }
        }

        /// <summary>
        /// Executes the full render pass for all windows and cameras.
        /// </summary>
        public override void Draw() {
            base.Draw();

            if (surfaces.Count == 0) {
                return;
            }

            var cameras = Core.Instance.ObjectManager.Cameras;
            for (int i = 0; i < surfaces.Count; i++) {
                DrawSurface(surfaces[i], cameras);
            }
        }

        /// <summary>
        /// Releases Vulkan resources owned by the renderer.
        /// </summary>
        public override unsafe void Dispose() {
            if (disposed) {
                return;
            }

            disposed = true;

            WindowResized -= OnWindowResized;
            context.Api.DeviceWaitIdle(context.Device);

            renderer2D.Dispose();
            DisposeMaterials();

            for (int i = 0; i < surfaces.Count; i++) {
                surfaces[i].Dispose();
            }

            surfaces.Clear();
            surfacesByHandle.Clear();

            if (transformUniformBuffer != null) {
                transformUniformBuffer.Dispose();
                transformUniformBuffer = null;
            }

            if (materialDescriptorPool.Handle != 0) {
                context.Api.DestroyDescriptorPool(context.Device, materialDescriptorPool, null);
                materialDescriptorPool = default;
            }

            if (materialTextureSampler.Handle != 0) {
                context.Api.DestroySampler(context.Device, materialTextureSampler, null);
                materialTextureSampler = default;
            }

            if (materialPipelineLayout.Handle != 0) {
                context.Api.DestroyPipelineLayout(context.Device, materialPipelineLayout, null);
                materialPipelineLayout = default;
            }

            if (materialDescriptorSetLayout.Handle != 0) {
                context.Api.DestroyDescriptorSetLayout(context.Device, materialDescriptorSetLayout, null);
                materialDescriptorSetLayout = default;
            }

            context.Dispose();

            base.Dispose();
        }

        /// <summary>
        /// Draws a single 3D drawable encountered during queue traversal.
        /// </summary>
        /// <param name="drawable">Drawable to render.</param>
        public unsafe void Visit(IDrawable3D drawable) {
            if (!frameActive) {
                throw new InvalidOperationException("Cannot render 3D drawables outside of an active frame.");
            }

            if (drawable?.Parent == null || !drawable.Parent.Enabled) {
                return;
            }

            if (drawable.Model is not VulkanModelResource model) {
                throw new InvalidOperationException("Drawable models must use VulkanModelResource when rendering with Vulkan.");
            }

            if (model.VertexBuffer == null || model.VertexCount <= 0) {
                return;
            }

            ulong vertexOffset = 0;
            VkBuffer vertexBuffer = model.VertexBuffer.Handle;
            VkBuffer* vertexBuffers = stackalloc VkBuffer[] { vertexBuffer };
            ulong* vertexOffsets = stackalloc ulong[] { vertexOffset };
            context.Api.CmdBindVertexBuffers(activeCommandBuffer, 0, 1, vertexBuffers, vertexOffsets);

            if (model.IndexBuffer != null && model.IndexCount > 0) {
                IndexType indexType = model.Uses32BitIndices ? IndexType.Uint32 : IndexType.Uint16;
                context.Api.CmdBindIndexBuffer(activeCommandBuffer, model.IndexBuffer.Handle, 0, indexType);
            }

            RuntimeSubmesh[] submeshes = ResolveSubmeshes(model);
            for (int submeshIndex = 0; submeshIndex < submeshes.Length; submeshIndex++) {
                RuntimeMaterial runtimeMaterial = ResolveMaterial(drawable, submeshIndex);
                if (runtimeMaterial == null) {
                    continue;
                }

                RuntimeMaterial rootMaterial = runtimeMaterial.ResolveRootMaterial();
                if (rootMaterial is not VulkanMaterialResource material) {
                    throw new InvalidOperationException("Drawable materials must resolve to VulkanMaterialResource through their parent chain.");
                }

                Pipeline materialPipeline = material.EnsurePipeline(activeSurface, materialPipelineLayout);
                context.Api.CmdBindPipeline(activeCommandBuffer, PipelineBindPoint.Graphics, materialPipeline);

                uint dynamicOffset = ReserveTransformSlot();
                if (BuiltInMaterialIds.UsesStandardMeshTransform(rootMaterial.Id)) {
                    StandardMeshShaderData transformData = BuildStandardMeshShaderData(drawable.Parent);
                    UpdateTransformBuffer(transformData, dynamicOffset);
                } else {
                    float4x4 transformData = BuildWorldViewProjectionMatrix(drawable.Parent);
                    UpdateTransformBuffer(transformData, dynamicOffset);
                }

                DescriptorSet descriptorSet = EnsureMaterialDescriptorSet(material, runtimeMaterial);
                DescriptorSet* descriptorSets = stackalloc DescriptorSet[] { descriptorSet };
                uint* dynamicOffsets = stackalloc uint[] { dynamicOffset };
                context.Api.CmdBindDescriptorSets(
                    activeCommandBuffer,
                    PipelineBindPoint.Graphics,
                    materialPipelineLayout,
                    0,
                    1,
                    descriptorSets,
                    1,
                    dynamicOffsets);

                DrawSubmesh(model, submeshes[submeshIndex]);
            }
        }

        /// <summary>
        /// Resolves the runtime submeshes that should be drawn for the supplied model.
        /// </summary>
        /// <param name="model">Runtime model referenced by the drawable.</param>
        /// <returns>Runtime submeshes that should be drawn.</returns>
        static RuntimeSubmesh[] ResolveSubmeshes(RuntimeModel model) {
            if (model == null || model.Submeshes == null || model.Submeshes.Length == 0) {
                throw new InvalidOperationException("Drawable models must provide at least one runtime submesh.");
            }

            return model.Submeshes;
        }

        /// <summary>
        /// Resolves the runtime material bound to one submesh slot.
        /// </summary>
        /// <param name="drawable">Drawable that owns the material slots.</param>
        /// <param name="submeshIndex">Zero-based submesh index to resolve.</param>
        /// <returns>Runtime material bound to the requested submesh slot.</returns>
        static RuntimeMaterial ResolveMaterial(IDrawable3D drawable, int submeshIndex) {
            if (drawable == null) {
                throw new ArgumentNullException(nameof(drawable));
            } else if (submeshIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(submeshIndex), "Submesh index must be non-negative.");
            }

            RuntimeMaterial[] materials = drawable.Materials;
            if (materials == null || materials.Length == 0) {
                return drawable.Material;
            }
            if (submeshIndex < materials.Length) {
                return materials[submeshIndex];
            }

            return materials[0];
        }

        /// <summary>
        /// Draws one resolved submesh from the currently bound model resource.
        /// </summary>
        /// <param name="model">Model resource currently bound to the command buffer.</param>
        /// <param name="submesh">Resolved submesh range to draw.</param>
        unsafe void DrawSubmesh(VulkanModelResource model, RuntimeSubmesh submesh) {
            if (model == null) {
                throw new ArgumentNullException(nameof(model));
            } else if (submesh == null) {
                throw new ArgumentNullException(nameof(submesh));
            }

            if (model.IndexBuffer != null && model.IndexCount > 0) {
                context.Api.CmdDrawIndexed(activeCommandBuffer, (uint)submesh.IndexCount, 1, (uint)submesh.IndexStart, 0, 0);
            } else {
                context.Api.CmdDraw(activeCommandBuffer, (uint)submesh.IndexCount, 1, (uint)submesh.IndexStart, 0);
            }
        }

        /// <summary>
        /// Responds to window resize events and rebuilds the swapchain.
        /// </summary>
        /// <param name="handle">Window handle.</param>
        /// <param name="width">New width.</param>
        /// <param name="height">New height.</param>
        void OnWindowResized(IntPtr handle, int width, int height) {
            if (!surfacesByHandle.TryGetValue(handle, out VulkanSwapchainSurface surface)) {
                return;
            }

            surface.Recreate(width, height);
            renderer2D.HandleSwapchainRecreated(surface);
        }

        /// <summary>
        /// Renders all cameras for a specific surface.
        /// </summary>
        /// <param name="surface">Swapchain surface to render.</param>
        /// <param name="cameras">List of cameras to render.</param>
        void DrawSurface(VulkanSwapchainSurface surface, IReadOnlyList<ICamera> cameras) {
            CommandBuffer commandBuffer;
            uint imageIndex;
            if (!surface.BeginFrame(out imageIndex, out commandBuffer)) {
                return;
            }

            float4 clearColor = ResolveSurfaceClearColor(cameras);
            surface.BeginRenderPass(commandBuffer, imageIndex, clearColor.X, clearColor.Y, clearColor.Z, clearColor.W);

            renderer2D.BeginFrame(surface, commandBuffer);
            frameActive = true;
            activeSurface = surface;
            activeCommandBuffer = commandBuffer;
            transformDrawCount = 0;

            for (int i = 0; i < cameras.Count; i++) {
                RenderCamera(cameras[i], surface);
            }

            frameActive = false;
            activeSurface = null;
            activeCommandBuffer = default;
            renderer2D.EndFrame();
            surface.EndRenderPass(commandBuffer);

            surface.EndFrame(commandBuffer, imageIndex);
        }

        /// <summary>
        /// Resolves the clear color for the surface render pass from active camera settings.
        /// </summary>
        /// <param name="cameras">Cameras rendered on the surface.</param>
        /// <returns>Clear color used to begin the render pass.</returns>
        float4 ResolveSurfaceClearColor(IReadOnlyList<ICamera> cameras) {
            if (cameras == null) {
                throw new ArgumentNullException(nameof(cameras));
            }

            for (int i = 0; i < cameras.Count; i++) {
                ICamera camera = cameras[i];
                if (camera == null || camera.Parent == null || !camera.Parent.Enabled) {
                    continue;
                }

                CameraClearSettings clearSettings = camera.ClearSettings;
                if (!clearSettings.ClearColorEnabled) {
                    continue;
                }

                return clearSettings.ClearColor;
            }

            return new float4(0f, 0f, 0f, 1f);
        }

        /// <summary>
        /// Renders the 3D queue followed by the 2D overlay for a camera.
        /// </summary>
        /// <param name="camera">Camera to render.</param>
        /// <param name="surface">Surface currently being rendered.</param>
        void RenderCamera(ICamera camera, VulkanSwapchainSurface surface) {
            if (camera == null || camera.Parent == null || !camera.Parent.Enabled) {
                return;
            }

            double aspectRatio = SetViewportAndScissor(camera, surface);
            if (aspectRatio <= 0.0) {
                return;
            }

            float4x4 view;
            float3 cameraPos = camera.Parent.Position;
            currentCameraPosition = cameraPos;
            float4 cameraOrientation = camera.Parent.Orientation;
            float3 cameraForward = float4.RotateVector(DefaultForward, cameraOrientation);
            float3 cameraUp = float4.RotateVector(DefaultUp, cameraOrientation);
            float3 cameraTarget = cameraPos + cameraForward;
            float4x4.CreateLookAt(ref cameraPos, ref cameraTarget, ref cameraUp, out view);

            float4x4 projection = CameraProjectionUtils.CreatePerspectiveProjection(camera, (float)(Math.PI / 4.0), (float)aspectRatio);
            ApplyVulkanProjectionAdjustments(ref projection);
            float4x4.Multiply(ref view, ref projection, out currentViewProjection);

            IRenderQueue3D renderQueue = camera.RenderQueue3D;
            renderQueue.VisitOrdered(this);

            renderer2D.RenderCamera(camera);
        }

        /// <summary>
        /// Configures the Vulkan viewport and scissor for the active camera.
        /// </summary>
        /// <param name="camera">Camera being rendered.</param>
        /// <param name="surface">Surface receiving the render output.</param>
        /// <returns>Aspect ratio used for projection matrix creation.</returns>
        unsafe double SetViewportAndScissor(ICamera camera, VulkanSwapchainSurface surface) {
            float4 viewport = camera.Viewport;
            double offsetX = viewport.X;
            double offsetY = viewport.Y;
            double width = viewport.Z;
            double height = viewport.W;
            double logicalSurfaceWidth = surface.LogicalWidth;
            double logicalSurfaceHeight = surface.LogicalHeight;

            if (width <= 1.0 && height <= 1.0) {
                offsetX *= logicalSurfaceWidth;
                offsetY *= logicalSurfaceHeight;
                width *= logicalSurfaceWidth;
                height *= logicalSurfaceHeight;
            }

            if (width <= 0.0 || height <= 0.0) {
                return 0.0;
            }

            double pixelScaleX = surface.Extent.Width / logicalSurfaceWidth;
            double pixelScaleY = surface.Extent.Height / logicalSurfaceHeight;
            double pixelOffsetX = offsetX * pixelScaleX;
            double pixelOffsetY = offsetY * pixelScaleY;
            double pixelWidth = width * pixelScaleX;
            double pixelHeight = height * pixelScaleY;
            int viewportX = (int)Math.Round(pixelOffsetX);
            int viewportY = (int)Math.Round(pixelOffsetY);
            int viewportWidth = Math.Max(1, (int)Math.Round(pixelWidth));
            int viewportHeight = Math.Max(1, (int)Math.Round(pixelHeight));

            int maxWidth = (int)surface.Extent.Width;
            int maxHeight = (int)surface.Extent.Height;
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
            context.Api.CmdSetViewport(activeCommandBuffer, 0, 1, viewports);
            context.Api.CmdSetScissor(activeCommandBuffer, 0, 1, scissors);

            return viewportWidth / (double)viewportHeight;
        }

        /// <summary>
        /// Applies Vulkan-specific clip-space adjustments to keep world orientation aligned with DirectX output.
        /// </summary>
        /// <param name="projection">Projection matrix to adjust in place.</param>
        void ApplyVulkanProjectionAdjustments(ref float4x4 projection) {
            projection.M22 = -projection.M22;
        }

        /// <summary>
        /// Builds the transform payload required by the built-in default mesh shader.
        /// </summary>
        /// <param name="entity">Entity to build transform data for.</param>
        /// <returns>Per-draw standard mesh shader data.</returns>
        StandardMeshShaderData BuildStandardMeshShaderData(Entity entity) {
            float4 orientation = entity.Orientation;
            float4x4 rotation;
            float4x4.CreateFromQuaternion(ref orientation, out rotation);

            float3 scale = entity.Scale;
            float4x4 size;
            float4x4.CreateScale(scale.X, scale.Y, scale.Z, out size);

            float4x4 rotationScale;
            float4x4.Multiply(ref rotation, ref size, out rotationScale);

            float3 position = entity.Position;
            float4x4 translation;
            float4x4.CreateTranslation(ref position, out translation);

            float4x4 world;
            float4x4.Multiply(ref rotationScale, ref translation, out world);

            float4x4 worldViewProj;
            float4x4.Multiply(ref world, ref currentViewProjection, out worldViewProj);

            float4x4 worldTransposed;
            float4x4.Transpose(ref world, out worldTransposed);
            float4x4 transposed;
            float4x4.Transpose(ref worldViewProj, out transposed);
            float4x4 normalMatrix;
            float4x4.InverseTranspose(ref world, out normalMatrix);
            return new StandardMeshShaderData {
                World = worldTransposed,
                WorldViewProj = transposed,
                NormalMatrix = normalMatrix,
                CameraPosition = new float4(currentCameraPosition.X, currentCameraPosition.Y, currentCameraPosition.Z, 0f)
            };
        }

        /// <summary>
        /// Builds the legacy world-view-projection transform used by gizmo and helper shaders.
        /// </summary>
        /// <param name="entity">Entity to build transform data for.</param>
        /// <returns>Transposed world-view-projection matrix ready for the shader constant buffer.</returns>
        float4x4 BuildWorldViewProjectionMatrix(Entity entity) {
            float4 orientation = entity.Orientation;
            float4x4 rotation;
            float4x4.CreateFromQuaternion(ref orientation, out rotation);

            float3 scale = entity.Scale;
            float4x4 size;
            float4x4.CreateScale(scale.X, scale.Y, scale.Z, out size);

            float4x4 rotationScale;
            float4x4.Multiply(ref rotation, ref size, out rotationScale);

            float3 position = entity.Position;
            float4x4 translation;
            float4x4.CreateTranslation(ref position, out translation);

            float4x4 world;
            float4x4.Multiply(ref rotationScale, ref translation, out world);

            float4x4 worldViewProj;
            float4x4.Multiply(ref world, ref currentViewProjection, out worldViewProj);

            float4x4 transposed;
            float4x4.Transpose(ref worldViewProj, out transposed);
            return transposed;
        }

        /// <summary>
        /// Reserves the next aligned transform offset in the dynamic uniform buffer.
        /// </summary>
        /// <returns>Dynamic buffer offset in bytes.</returns>
        uint ReserveTransformSlot() {
            if (transformDrawCount >= MaxTransformMatricesPerFrame) {
                throw new InvalidOperationException("Exceeded the per-frame Vulkan transform buffer capacity.");
            }

            uint offset = (uint)(transformDrawCount * transformBufferStride);
            transformDrawCount++;
            return offset;
        }

        /// <summary>
        /// Writes a transform payload to the dynamic uniform buffer at the specified offset.
        /// </summary>
        /// <param name="transformData">Built-in default mesh transform payload.</param>
        /// <param name="offset">Byte offset into the dynamic uniform buffer.</param>
        unsafe void UpdateTransformBuffer(StandardMeshShaderData transformData, uint offset) {
            void* mapped;
            Result mapResult = context.Api.MapMemory(
                context.Device,
                transformUniformBuffer.Memory,
                offset,
                TransformBufferSizeBytes,
                0,
                &mapped);
            if (mapResult != Result.Success) {
                throw new InvalidOperationException($"Failed to map Vulkan transform buffer: {mapResult}.");
            }

            try {
                StandardMeshShaderData* source = &transformData;
                System.Buffer.MemoryCopy(source, mapped, TransformBufferSizeBytes, TransformBufferSizeBytes);
            } finally {
                context.Api.UnmapMemory(context.Device, transformUniformBuffer.Memory);
            }
        }

        /// <summary>
        /// Writes a legacy matrix payload to the dynamic uniform buffer at the specified offset.
        /// </summary>
        /// <param name="transformData">Legacy world-view-projection matrix payload.</param>
        /// <param name="offset">Byte offset into the dynamic uniform buffer.</param>
        unsafe void UpdateTransformBuffer(float4x4 transformData, uint offset) {
            void* mapped;
            Result mapResult = context.Api.MapMemory(
                context.Device,
                transformUniformBuffer.Memory,
                offset,
                LegacyTransformBufferSizeBytes,
                0,
                &mapped);
            if (mapResult != Result.Success) {
                throw new InvalidOperationException($"Failed to map Vulkan transform buffer: {mapResult}.");
            }

            try {
                float4x4* source = &transformData;
                System.Buffer.MemoryCopy(source, mapped, LegacyTransformBufferSizeBytes, LegacyTransformBufferSizeBytes);
            } finally {
                context.Api.UnmapMemory(context.Device, transformUniformBuffer.Memory);
            }
        }

        /// <summary>
        /// Creates descriptor, pipeline layout, and uniform buffer resources for 3D materials.
        /// </summary>
        unsafe void CreateMaterialResources() {
            PhysicalDeviceProperties properties;
            context.Api.GetPhysicalDeviceProperties(context.PhysicalDevice, out properties);
            ulong alignment = properties.Limits.MinUniformBufferOffsetAlignment;
            if (alignment == 0) {
                alignment = TransformBufferSizeBytes;
            }

            transformBufferStride = AlignUp(TransformBufferSizeBytes, alignment);
            ulong transformBufferSize = transformBufferStride * MaxTransformMatricesPerFrame;

            transformUniformBuffer = new VulkanGpuBuffer(
                context,
                transformBufferSize,
                BufferUsageFlags.BufferUsageUniformBufferBit,
                MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyHostCoherentBit);

            DescriptorSetLayoutBinding transformBinding = new DescriptorSetLayoutBinding {
                Binding = TransformDescriptorBindingSlot,
                DescriptorType = DescriptorType.UniformBufferDynamic,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.ShaderStageVertexBit | ShaderStageFlags.ShaderStageFragmentBit
            };
            DescriptorSetLayoutBinding textureImageBinding = new DescriptorSetLayoutBinding {
                Binding = MaterialTextureDescriptorBindingSlot,
                DescriptorType = DescriptorType.SampledImage,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.ShaderStageFragmentBit
            };
            DescriptorSetLayoutBinding textureSamplerBinding = new DescriptorSetLayoutBinding {
                Binding = MaterialSamplerDescriptorBindingSlot,
                DescriptorType = DescriptorType.Sampler,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.ShaderStageFragmentBit
            };

            DescriptorSetLayoutBinding* materialBindings = stackalloc DescriptorSetLayoutBinding[] {
                transformBinding,
                textureImageBinding,
                textureSamplerBinding
            };
            DescriptorSetLayoutCreateInfo materialLayoutInfo = new DescriptorSetLayoutCreateInfo {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = 3,
                PBindings = materialBindings
            };

            Result materialLayoutResult = context.Api.CreateDescriptorSetLayout(context.Device, materialLayoutInfo, null, out materialDescriptorSetLayout);
            if (materialLayoutResult != Result.Success) {
                throw new InvalidOperationException($"Failed to create Vulkan material descriptor set layout: {materialLayoutResult}.");
            }
            DescriptorSetLayout* layouts = stackalloc DescriptorSetLayout[] { materialDescriptorSetLayout };
            PipelineLayoutCreateInfo pipelineLayoutInfo = new PipelineLayoutCreateInfo {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = 1,
                PSetLayouts = layouts
            };

            Result pipelineLayoutResult = context.Api.CreatePipelineLayout(context.Device, pipelineLayoutInfo, null, out materialPipelineLayout);
            if (pipelineLayoutResult != Result.Success) {
                throw new InvalidOperationException($"Failed to create Vulkan material pipeline layout: {pipelineLayoutResult}.");
            }

            DescriptorPoolSize transformPoolSize = new DescriptorPoolSize {
                Type = DescriptorType.UniformBufferDynamic,
                DescriptorCount = MaxMaterialTextures
            };
            DescriptorPoolSize sampledImagePoolSize = new DescriptorPoolSize {
                Type = DescriptorType.SampledImage,
                DescriptorCount = MaxMaterialTextures
            };
            DescriptorPoolSize samplerPoolSize = new DescriptorPoolSize {
                Type = DescriptorType.Sampler,
                DescriptorCount = MaxMaterialTextures
            };
            DescriptorPoolSize* poolSizes = stackalloc DescriptorPoolSize[] {
                transformPoolSize,
                sampledImagePoolSize,
                samplerPoolSize
            };

            DescriptorPoolCreateInfo poolInfo = new DescriptorPoolCreateInfo {
                SType = StructureType.DescriptorPoolCreateInfo,
                PoolSizeCount = 3,
                PPoolSizes = poolSizes,
                MaxSets = MaxMaterialTextures
            };

            Result poolResult = context.Api.CreateDescriptorPool(context.Device, poolInfo, null, out materialDescriptorPool);
            if (poolResult != Result.Success) {
                throw new InvalidOperationException($"Failed to create Vulkan material descriptor pool: {poolResult}.");
            }

            CreateMaterialTextureSampler();
        }

        /// <summary>
        /// Creates the sampler shared by textured 3D materials.
        /// </summary>
        unsafe void CreateMaterialTextureSampler() {
            SamplerCreateInfo samplerInfo = new SamplerCreateInfo {
                SType = StructureType.SamplerCreateInfo,
                MagFilter = Filter.Nearest,
                MinFilter = Filter.Nearest,
                MipmapMode = SamplerMipmapMode.Nearest,
                AddressModeU = SamplerAddressMode.ClampToEdge,
                AddressModeV = SamplerAddressMode.ClampToEdge,
                AddressModeW = SamplerAddressMode.ClampToEdge,
                MaxAnisotropy = 1.0f,
                MinLod = 0f,
                MaxLod = 0f,
                BorderColor = BorderColor.IntOpaqueBlack
            };

            Result samplerResult = context.Api.CreateSampler(context.Device, samplerInfo, null, out materialTextureSampler);
            if (samplerResult != Result.Success) {
                throw new InvalidOperationException($"Failed to create Vulkan material sampler: {samplerResult}.");
            }
        }

        /// <summary>
        /// Allocates a descriptor set used to bind one material's transform buffer, sampled image, and sampler.
        /// </summary>
        /// <returns>Allocated descriptor set.</returns>
        unsafe DescriptorSet AllocateMaterialDescriptorSet() {
            DescriptorSetLayout descriptorSetLayout = materialDescriptorSetLayout;
            DescriptorSetAllocateInfo allocInfo = new DescriptorSetAllocateInfo {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = materialDescriptorPool,
                DescriptorSetCount = 1,
                PSetLayouts = &descriptorSetLayout
            };

            DescriptorSet descriptorSet;
            Result allocResult = context.Api.AllocateDescriptorSets(context.Device, allocInfo, out descriptorSet);
            if (allocResult != Result.Success) {
                throw new InvalidOperationException($"Failed to allocate Vulkan material descriptor set: {allocResult}.");
            }

            return descriptorSet;
        }

        /// <summary>
        /// Ensures the descriptor set bound for a material references the current transform buffer, texture, and sampler.
        /// </summary>
        /// <param name="material">Concrete Vulkan material that owns the descriptor set.</param>
        /// <param name="runtimeMaterial">Resolved runtime material instance that provides texture values.</param>
        /// <returns>Descriptor set to bind for the material.</returns>
        DescriptorSet EnsureMaterialDescriptorSet(VulkanMaterialResource material, RuntimeMaterial runtimeMaterial) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            } else if (runtimeMaterial == null) {
                throw new ArgumentNullException(nameof(runtimeMaterial));
            }

            if (material.MaterialDescriptorSet.Handle == 0) {
                material.MaterialDescriptorSet = AllocateMaterialDescriptorSet();
            }

            RuntimeTexture runtimeTexture = ResolveDescriptorTexture(runtimeMaterial);
            if (!materialBoundTextures.TryGetValue(material, out RuntimeTexture boundTexture) || !ReferenceEquals(boundTexture, runtimeTexture)) {
                if (runtimeTexture is not VulkanTextureResource textureResource) {
                    throw new InvalidOperationException("3D material textures must use Vulkan texture resources.");
                }

                UpdateMaterialDescriptorSet(material.MaterialDescriptorSet, textureResource);
                materialBoundTextures[material] = runtimeTexture;
            }

            return material.MaterialDescriptorSet;
        }

        /// <summary>
        /// Resolves the texture resource that should be written into the Vulkan material descriptor set.
        /// </summary>
        /// <param name="runtimeMaterial">Resolved runtime material instance that provides texture values.</param>
        /// <returns>Texture resource to bind for the descriptor set.</returns>
        RuntimeTexture ResolveDescriptorTexture(RuntimeMaterial runtimeMaterial) {
            if (runtimeMaterial == null) {
                throw new ArgumentNullException(nameof(runtimeMaterial));
            } else if (runtimeMaterial.Layout.TextureBindings.Length == 0) {
                return TextureUtils.PixelTexture;
            }

            RuntimeTexture runtimeTexture = runtimeMaterial.ResolveTexture();
            if (runtimeTexture != null) {
                return runtimeTexture;
            }

            return TextureUtils.BlackPixelTexture;
        }

        /// <summary>
        /// Writes transform, image, and sampler bindings for a 3D material descriptor set.
        /// </summary>
        /// <param name="descriptorSet">Descriptor set to update.</param>
        /// <param name="texture">Texture resource that will be sampled by the material.</param>
        unsafe void UpdateMaterialDescriptorSet(DescriptorSet descriptorSet, VulkanTextureResource texture) {
            if (texture == null) {
                throw new ArgumentNullException(nameof(texture));
            }

            DescriptorBufferInfo bufferInfo = new DescriptorBufferInfo {
                Buffer = transformUniformBuffer.Handle,
                Offset = 0,
                Range = TransformBufferSizeBytes
            };
            DescriptorImageInfo imageInfo = new DescriptorImageInfo {
                ImageView = texture.ImageView,
                ImageLayout = ImageLayout.ShaderReadOnlyOptimal
            };
            DescriptorImageInfo samplerInfo = new DescriptorImageInfo {
                Sampler = materialTextureSampler
            };

            WriteDescriptorSet* descriptorWrites = stackalloc WriteDescriptorSet[3];
            descriptorWrites[0] = new WriteDescriptorSet {
                SType = StructureType.WriteDescriptorSet,
                DstSet = descriptorSet,
                DstBinding = TransformDescriptorBindingSlot,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.UniformBufferDynamic,
                PBufferInfo = &bufferInfo
            };
            descriptorWrites[1] = new WriteDescriptorSet {
                SType = StructureType.WriteDescriptorSet,
                DstSet = descriptorSet,
                DstBinding = MaterialTextureDescriptorBindingSlot,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.SampledImage,
                PImageInfo = &imageInfo
            };
            descriptorWrites[2] = new WriteDescriptorSet {
                SType = StructureType.WriteDescriptorSet,
                DstSet = descriptorSet,
                DstBinding = MaterialSamplerDescriptorBindingSlot,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.Sampler,
                PImageInfo = &samplerInfo
            };

            context.Api.UpdateDescriptorSets(context.Device, 3, descriptorWrites, 0, null);
        }

        /// <summary>
        /// Rebuilds a material layout from a hot-reloaded shader asset while preserving the material's current render state.
        /// </summary>
        /// <param name="material">Runtime material whose layout is being rebuilt.</param>
        /// <param name="shaderAsset">Updated shader metadata.</param>
        /// <returns>Rebuilt material layout.</returns>
        MaterialLayout BuildMaterialLayout(VulkanMaterialResource material, ShaderAsset shaderAsset) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            } else if (shaderAsset == null) {
                throw new ArgumentNullException(nameof(shaderAsset));
            }

            var materialAsset = new MaterialAsset {
                ShaderAssetId = material.ShaderAssetId,
                VertexProgram = material.VertexProgram,
                PixelProgram = material.PixelProgram,
                Variant = material.Variant,
                RenderState = material.RenderState
            };

            return MaterialLayoutBuilder.Build(materialAsset, shaderAsset);
        }

        /// <summary>
        /// Returns an aligned byte size for dynamic uniform buffer offsets.
        /// </summary>
        /// <param name="value">Requested byte count.</param>
        /// <param name="alignment">Alignment requirement.</param>
        /// <returns>Aligned byte count.</returns>
        ulong AlignUp(ulong value, ulong alignment) {
            if (alignment == 0) {
                return value;
            }

            ulong mask = alignment - 1;
            return (value + mask) & ~mask;
        }

        /// <summary>
        /// Locates a shader program entry for the requested program and stage.
        /// </summary>
        /// <param name="shaderAsset">Shader asset containing program metadata.</param>
        /// <param name="programName">Program name to locate.</param>
        /// <param name="stage">Shader stage to locate.</param>
        /// <returns>Matching shader program metadata.</returns>
        ShaderProgramAsset GetShaderProgram(ShaderAsset shaderAsset, string programName, ShaderStage stage) {
            if (shaderAsset == null) {
                throw new ArgumentNullException(nameof(shaderAsset));
            }

            if (string.IsNullOrWhiteSpace(programName)) {
                throw new InvalidOperationException("Material assets must define a shader program name.");
            }

            if (shaderAsset.Programs == null || shaderAsset.Programs.Length == 0) {
                throw new InvalidOperationException("Shader assets must include shader program metadata.");
            }

            for (int i = 0; i < shaderAsset.Programs.Length; i++) {
                ShaderProgramAsset program = shaderAsset.Programs[i];
                if (program == null) {
                    continue;
                }

                if (!string.Equals(program.Name, programName, StringComparison.Ordinal)) {
                    continue;
                }

                if (program.Stage != stage) {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(program.EntryPoint)) {
                    throw new InvalidOperationException("Shader program metadata must define an entry point.");
                }

                return program;
            }

            throw new InvalidOperationException("Shader program metadata was not found for the requested Vulkan program.");
        }

        /// <summary>
        /// Locates a shader binary entry for the requested program, stage, and variant.
        /// </summary>
        /// <param name="shaderAsset">Shader asset containing binary data.</param>
        /// <param name="programName">Program name to locate.</param>
        /// <param name="stage">Shader stage to locate.</param>
        /// <param name="variant">Variant name to locate.</param>
        /// <returns>Matching shader binary asset.</returns>
        ShaderBinaryAsset GetShaderBinary(ShaderAsset shaderAsset, string programName, ShaderStage stage, string variant) {
            if (shaderAsset == null) {
                throw new ArgumentNullException(nameof(shaderAsset));
            }

            if (string.IsNullOrWhiteSpace(programName)) {
                throw new InvalidOperationException("Material assets must define a shader program name.");
            }

            if (string.IsNullOrWhiteSpace(variant)) {
                throw new InvalidOperationException("Material assets must define a shader variant.");
            }

            if (shaderAsset.Binaries == null || shaderAsset.Binaries.Length == 0) {
                throw new InvalidOperationException("Shader assets must include compiled binaries.");
            }

            string targetName = ShaderTargetNames.GetTargetName(ShaderCompileTarget.Vulkan);
            for (int i = 0; i < shaderAsset.Binaries.Length; i++) {
                ShaderBinaryAsset binary = shaderAsset.Binaries[i];
                if (binary == null) {
                    continue;
                }

                if (!string.Equals(binary.TargetName, targetName, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                if (!string.Equals(binary.ProgramName, programName, StringComparison.Ordinal)) {
                    continue;
                }

                if (binary.Stage != stage) {
                    continue;
                }

                if (!string.Equals(binary.Variant, variant, StringComparison.Ordinal)) {
                    continue;
                }

                if (binary.Bytecode == null || binary.Bytecode.Length == 0) {
                    throw new InvalidOperationException("Shader binary does not include bytecode.");
                }

                return binary;
            }

            throw new InvalidOperationException("Shader binary was not found for the requested Vulkan program.");
        }

        /// <summary>
        /// Registers a runtime material for shader hot reload updates.
        /// </summary>
        /// <param name="material">Material to register.</param>
        void RegisterMaterial(VulkanMaterialResource material) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            string shaderAssetId = material.ShaderAssetId;
            if (!materialsByShaderAssetId.TryGetValue(shaderAssetId, out List<VulkanMaterialResource> materials)) {
                materials = new List<VulkanMaterialResource>();
                materialsByShaderAssetId[shaderAssetId] = materials;
            }

            materials.Add(material);
        }

        /// <summary>
        /// Disposes all tracked runtime materials and clears material registries.
        /// </summary>
        void DisposeMaterials() {
            var visitedMaterials = new HashSet<VulkanMaterialResource>();
            foreach (var pair in materialsByShaderAssetId) {
                List<VulkanMaterialResource> materials = pair.Value;
                for (int i = 0; i < materials.Count; i++) {
                    VulkanMaterialResource material = materials[i];
                    if (material == null) {
                        continue;
                    }

                    if (visitedMaterials.Add(material)) {
                        material.Dispose();
                    }
                }
            }

            materialsByShaderAssetId.Clear();
            materialBoundTextures.Clear();
        }
    }
}
