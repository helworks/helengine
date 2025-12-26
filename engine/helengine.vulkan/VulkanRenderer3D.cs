using Silk.NET.Vulkan;
using System.Collections.Generic;

namespace helengine.vulkan {
    /// <summary>
    /// Vulkan-backed renderer responsible for swapchain management and 3D rendering.
    /// </summary>
    public class VulkanRenderer3D : RenderManager3D, IRenderVisitor3D {
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

            VulkanGpuBuffer? indexBuffer = null;
            int indexCount = 0;
            if (data.Indices16 != null && data.Indices16.Length > 0) {
                indexCount = data.Indices16.Length;
                ulong indexBufferSize = (ulong)(data.Indices16.Length * sizeof(ushort));
                indexBuffer = new VulkanGpuBuffer(
                    context,
                    indexBufferSize,
                    BufferUsageFlags.BufferUsageIndexBufferBit,
                    MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyHostCoherentBit);
                indexBuffer.Update(data.Indices16);
            }

            var model = new VulkanModelResource {
                VertexBuffer = vertexBuffer,
                IndexBuffer = indexBuffer,
                VertexCount = vertices.Length,
                IndexCount = indexCount
            };

            return model;
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
        public override void Dispose() {
            if (disposed) {
                return;
            }

            disposed = true;

            WindowResized -= OnWindowResized;

            renderer2D.Dispose();

            for (int i = 0; i < surfaces.Count; i++) {
                surfaces[i].Dispose();
            }

            surfaces.Clear();
            surfacesByHandle.Clear();
            context.Dispose();

            base.Dispose();
        }

        /// <summary>
        /// Draws a single 3D drawable encountered during queue traversal.
        /// </summary>
        /// <param name="drawable">Drawable to render.</param>
        public void Visit(IDrawable3D drawable) {
            if (drawable?.Parent == null || !drawable.Parent.Enabled) {
                return;
            }

            // 3D rendering pipeline will be added in a future pass.
        }

        /// <summary>
        /// Responds to window resize events and rebuilds the swapchain.
        /// </summary>
        /// <param name="handle">Window handle.</param>
        /// <param name="width">New width.</param>
        /// <param name="height">New height.</param>
        void OnWindowResized(IntPtr handle, int width, int height) {
            if (!surfacesByHandle.TryGetValue(handle, out var surface)) {
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

            surface.BeginRenderPass(commandBuffer, imageIndex, 1f, 0.5f, 0f, 1f);

            renderer2D.BeginFrame(surface, commandBuffer);

            for (int i = 0; i < cameras.Count; i++) {
                RenderCamera(cameras[i]);
            }

            renderer2D.EndFrame();
            surface.EndRenderPass(commandBuffer);

            surface.EndFrame(commandBuffer, imageIndex);
        }

        /// <summary>
        /// Renders the 3D queue followed by the 2D overlay for a camera.
        /// </summary>
        /// <param name="camera">Camera to render.</param>
        void RenderCamera(ICamera camera) {
            IRenderQueue3D renderQueue = camera.RenderQueue3D;
            renderQueue.VisitOrdered(this);

            renderer2D.RenderCamera(camera);
        }
    }
}
