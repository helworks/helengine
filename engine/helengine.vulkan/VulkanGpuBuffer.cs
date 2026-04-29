using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using VulkanBufferHandle = Silk.NET.Vulkan.Buffer;

namespace helengine.vulkan {
    /// <summary>
    /// Wraps a Vulkan buffer with its allocated memory.
    /// </summary>
    public sealed unsafe class VulkanGpuBuffer : IDisposable {
        /// <summary>
        /// Vulkan context used for buffer operations.
        /// </summary>
        readonly VulkanContext context;
        /// <summary>
        /// Size of the buffer in bytes.
        /// </summary>
        readonly ulong size;
        /// <summary>
        /// Buffer handle.
        /// </summary>
        VulkanBufferHandle buffer;
        /// <summary>
        /// Device memory bound to the buffer.
        /// </summary>
        DeviceMemory memory;
        /// <summary>
        /// Tracks whether the buffer has been disposed.
        /// </summary>
        bool disposed;

        /// <summary>
        /// Initializes a Vulkan buffer with the specified usage and memory properties.
        /// </summary>
        /// <param name="context">Shared Vulkan context.</param>
        /// <param name="size">Buffer size in bytes.</param>
        /// <param name="usage">Buffer usage flags.</param>
        /// <param name="memoryProperties">Memory property requirements.</param>
        public VulkanGpuBuffer(VulkanContext context, ulong size, BufferUsageFlags usage, MemoryPropertyFlags memoryProperties) {
            this.context = context;
            this.size = size;
            CreateBuffer(usage, memoryProperties);
        }

        /// <summary>
        /// Gets the Vulkan buffer handle.
        /// </summary>
        public VulkanBufferHandle Handle { get { return buffer; } }

        /// <summary>
        /// Gets the device memory bound to the buffer.
        /// </summary>
        public DeviceMemory Memory { get { return memory; } }

        /// <summary>
        /// Gets the size of the buffer in bytes.
        /// </summary>
        public ulong Size { get { return size; } }

        /// <summary>
        /// Uploads structured data into the buffer.
        /// </summary>
        /// <typeparam name="T">Struct type of the data.</typeparam>
        /// <param name="data">Array of data elements to copy.</param>
        public unsafe void Update<T>(T[] data) where T : unmanaged {
            Update(data, 0);
        }

        /// <summary>
        /// Uploads structured data into the buffer at a specific byte offset.
        /// </summary>
        /// <typeparam name="T">Struct type of the data.</typeparam>
        /// <param name="data">Array of data elements to copy.</param>
        /// <param name="destinationOffsetBytes">Destination byte offset in the target buffer.</param>
        public unsafe void Update<T>(T[] data, ulong destinationOffsetBytes) where T : unmanaged {
            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            ulong byteCount = (ulong)(data.Length * sizeof(T));
            if (byteCount == 0) {
                return;
            }

            if (destinationOffsetBytes > size) {
                throw new ArgumentOutOfRangeException(nameof(destinationOffsetBytes), "Destination offset exceeds the size of the Vulkan buffer.");
            }

            ulong remaining = size - destinationOffsetBytes;
            if (byteCount > remaining) {
                throw new ArgumentOutOfRangeException(nameof(data), "Data exceeds the available Vulkan buffer range at the requested destination offset.");
            }

            void* mapped;
            Result mapResult = context.Api.MapMemory(context.Device, memory, destinationOffsetBytes, byteCount, 0, &mapped);
            if (mapResult != Result.Success) {
                throw new InvalidOperationException($"Failed to map Vulkan buffer memory: {mapResult}.");
            }

            fixed (T* source = data) {
                System.Buffer.MemoryCopy(source, mapped, byteCount, byteCount);
            }
            context.Api.UnmapMemory(context.Device, memory);
        }

        /// <summary>
        /// Releases Vulkan resources owned by the buffer.
        /// </summary>
        public void Dispose() {
            if (disposed) {
                return;
            }

            disposed = true;

            if (buffer.Handle != 0) {
                context.Api.DestroyBuffer(context.Device, buffer, null);
            }

            if (memory.Handle != 0) {
                context.Api.FreeMemory(context.Device, memory, null);
            }
        }

        /// <summary>
        /// Creates the Vulkan buffer and allocates memory for it.
        /// </summary>
        /// <param name="usage">Buffer usage flags.</param>
        /// <param name="memoryProperties">Memory property requirements.</param>
        unsafe void CreateBuffer(BufferUsageFlags usage, MemoryPropertyFlags memoryProperties) {
            BufferCreateInfo bufferInfo = new BufferCreateInfo {
                SType = StructureType.BufferCreateInfo,
                Size = size,
                Usage = usage,
                SharingMode = SharingMode.Exclusive
            };

            Result bufferResult = context.Api.CreateBuffer(context.Device, bufferInfo, null, out buffer);
            if (bufferResult != Result.Success) {
                throw new InvalidOperationException($"Failed to create Vulkan buffer: {bufferResult}.");
            }

            MemoryRequirements memoryRequirements;
            context.Api.GetBufferMemoryRequirements(context.Device, buffer, out memoryRequirements);

            MemoryAllocateInfo allocInfo = new MemoryAllocateInfo {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = memoryRequirements.Size,
                MemoryTypeIndex = context.FindMemoryType(memoryRequirements.MemoryTypeBits, memoryProperties)
            };

            Result allocResult = context.Api.AllocateMemory(context.Device, allocInfo, null, out memory);
            if (allocResult != Result.Success) {
                throw new InvalidOperationException($"Failed to allocate Vulkan buffer memory: {allocResult}.");
            }

            context.Api.BindBufferMemory(context.Device, buffer, memory, 0);
        }
    }
}
