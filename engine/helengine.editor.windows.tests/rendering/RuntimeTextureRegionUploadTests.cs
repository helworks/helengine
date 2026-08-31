using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using SharpDX;
using SharpDX.Direct3D11;
using helengine;
using helengine.directx11;
using helengine.vulkan;
using Silk.NET.Vulkan;
using Xunit.Sdk;
using D3DDevice = SharpDX.Direct3D11.Device;
using VkImage = Silk.NET.Vulkan.Image;
using VkBuffer = Silk.NET.Vulkan.Buffer;
using VkDevice = Silk.NET.Vulkan.Device;
using VkImageLayout = Silk.NET.Vulkan.ImageLayout;

namespace helengine.editor.windows.tests.rendering {
    /// <summary>
    /// Verifies byte-exact RGBA8 sub-rectangle uploads on the desktop renderers.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class RuntimeTextureRegionUploadTests {
        /// <summary>
        /// Ensures Direct3D11 uploads honor tight and padded source rows and preserve texture/view identity.
        /// </summary>
        [Theory]
        [InlineData(8)]
        [InlineData(12)]
        public void DirectX11_uploads_region_without_replacing_texture_resources(int sourceRowPitch) {
            using DirectX11Renderer3D renderer = CreateDirectX11RendererOrSkip();
            TextureAsset asset = CreateBlackTextureAsset();
            DirectX11TextureResource texture = Assert.IsType<DirectX11TextureResource>(renderer.Render2D.BuildTextureFromRaw(asset));
            RuntimeTexture originalRuntimeTexture = texture;
            Texture2D originalTexture = texture.Texture;
            ShaderResourceView originalResource = texture.Resource;

            try {
                renderer.Render2D.UpdateTextureRegion(texture, 1, 1, 2, 2, CreateRegion(sourceRowPitch), sourceRowPitch);

                Assert.Same(originalRuntimeTexture, texture);
                Assert.Same(originalTexture, texture.Texture);
                Assert.Same(originalResource, texture.Resource);
                Assert.Equal(ExpectedTextureBytes(), ReadDirectX11Texture(texture, renderer.Device));
            } finally {
                renderer.Render2D.ReleaseTexture(texture);
            }
        }

        /// <summary>
        /// Ensures Direct3D11 rejects a runtime texture owned by another renderer before touching GPU state.
        /// </summary>
        [Fact]
        public void DirectX11_rejects_foreign_runtime_texture() {
            using DirectX11Renderer3D renderer = CreateDirectX11RendererOrSkip();
            ManagedRuntimeTexture foreignTexture = new ManagedRuntimeTexture { Width = 4, Height = 4 };

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                renderer.Render2D.UpdateTextureRegion(foreignTexture, 0, 0, 1, 1, new byte[4], 4));

            Assert.Equal("texture", exception.ParamName);

            ArgumentException releaseException = Assert.Throws<ArgumentException>(() =>
                renderer.Render2D.ReleaseTexture(foreignTexture));

            Assert.Equal("texture", releaseException.ParamName);
        }

        /// <summary>
        /// Ensures Vulkan uploads honor tight and padded source rows and preserve image/view/descriptor identity.
        /// </summary>
        [Theory]
        [InlineData(8)]
        [InlineData(12)]
        public void Vulkan_uploads_region_without_replacing_texture_resources(int sourceRowPitch) {
            using VulkanRenderer3D renderer = CreateVulkanRendererOrSkip();
            TextureAsset asset = CreateBlackTextureAsset();
            VulkanTextureResource texture = Assert.IsType<VulkanTextureResource>(renderer.Render2D.BuildTextureFromRaw(asset));
            RuntimeTexture originalRuntimeTexture = texture;
            VkImage originalImage = texture.Image;
            DeviceMemory originalMemory = texture.Memory;
            ImageView originalImageView = texture.ImageView;
            DescriptorSet originalDescriptorSet = texture.DescriptorSet;

            try {
                renderer.Render2D.UpdateTextureRegion(texture, 1, 1, 2, 2, CreateRegion(sourceRowPitch), sourceRowPitch);

                Assert.Same(originalRuntimeTexture, texture);
                Assert.Equal(originalImage, texture.Image);
                Assert.Equal(originalMemory, texture.Memory);
                Assert.Equal(originalImageView, texture.ImageView);
                Assert.Equal(originalDescriptorSet, texture.DescriptorSet);
                Assert.Equal(ExpectedTextureBytes(), ReadVulkanTexture(renderer, texture));
            } finally {
                renderer.Render2D.ReleaseTexture(texture);
            }
        }

        /// <summary>
        /// Ensures Vulkan rejects a runtime texture owned by another renderer before touching GPU state.
        /// </summary>
        [Fact]
        public void Vulkan_rejects_foreign_runtime_texture() {
            using VulkanRenderer3D renderer = CreateVulkanRendererOrSkip();
            ManagedRuntimeTexture foreignTexture = new ManagedRuntimeTexture { Width = 4, Height = 4 };

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                renderer.Render2D.UpdateTextureRegion(foreignTexture, 0, 0, 1, 1, new byte[4], 4));

            Assert.Equal("texture", exception.ParamName);

            ArgumentException releaseException = Assert.Throws<ArgumentException>(() =>
                renderer.Render2D.ReleaseTexture(foreignTexture));

            Assert.Equal("texture", releaseException.ParamName);
        }

        /// <summary>
        /// Ensures Direct3D11 rejects a same-backend texture owned by another renderer and leaves its rightful owner usable.
        /// </summary>
        [Fact]
        public void DirectX11_rejects_same_backend_texture_from_another_renderer() {
            using DirectX11Renderer3D rightfulOwner = CreateDirectX11RendererOrSkip();
            using DirectX11Renderer3D foreignOwner = CreateDirectX11RendererOrSkip();
            DirectX11TextureResource texture = Assert.IsType<DirectX11TextureResource>(
                rightfulOwner.Render2D.BuildTextureFromRaw(CreateBlackTextureAsset()));

            try {
                ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                    foreignOwner.Render2D.UpdateTextureRegion(texture, 0, 0, 1, 1, new byte[4], 4));

                Assert.Equal("texture", exception.ParamName);
                rightfulOwner.Render2D.ReleaseTexture(texture);
                Assert.True(texture.IsDisposed);
            } finally {
                if (!texture.IsDisposed) {
                    rightfulOwner.Render2D.ReleaseTexture(texture);
                }
            }
        }

        /// <summary>
        /// Ensures Direct3D11 rejects release by another renderer and allows the rightful owner to release exactly once.
        /// </summary>
        [Fact]
        public void DirectX11_rejects_release_by_another_renderer() {
            using DirectX11Renderer3D rightfulOwner = CreateDirectX11RendererOrSkip();
            using DirectX11Renderer3D foreignOwner = CreateDirectX11RendererOrSkip();
            DirectX11TextureResource texture = Assert.IsType<DirectX11TextureResource>(
                rightfulOwner.Render2D.BuildTextureFromRaw(CreateBlackTextureAsset()));

            try {
                ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                    foreignOwner.Render2D.ReleaseTexture(texture));

                Assert.Equal("texture", exception.ParamName);
                rightfulOwner.Render2D.ReleaseTexture(texture);
                Assert.True(texture.IsDisposed);
                Assert.Null(texture.Texture);
                Assert.Null(texture.Resource);
            } finally {
                if (!texture.IsDisposed) {
                    rightfulOwner.Render2D.ReleaseTexture(texture);
                }
            }
        }

        /// <summary>
        /// Ensures Vulkan rejects a same-backend texture owned by another renderer and leaves its rightful owner usable.
        /// </summary>
        [Fact]
        public void Vulkan_rejects_same_backend_texture_from_another_renderer() {
            using VulkanRenderer3D rightfulOwner = CreateVulkanRendererOrSkip();
            using VulkanRenderer3D foreignOwner = CreateVulkanRendererOrSkip();
            VulkanTextureResource texture = Assert.IsType<VulkanTextureResource>(
                rightfulOwner.Render2D.BuildTextureFromRaw(CreateBlackTextureAsset()));

            try {
                ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                    foreignOwner.Render2D.UpdateTextureRegion(texture, 0, 0, 1, 1, new byte[4], 4));

                Assert.Equal("texture", exception.ParamName);
                rightfulOwner.Render2D.ReleaseTexture(texture);
                Assert.True(texture.IsDisposed);
            } finally {
                if (!texture.IsDisposed) {
                    rightfulOwner.Render2D.ReleaseTexture(texture);
                }
            }
        }

        /// <summary>
        /// Ensures Vulkan rejects release by another renderer and allows the rightful owner to release exactly once.
        /// </summary>
        [Fact]
        public void Vulkan_rejects_release_by_another_renderer() {
            using VulkanRenderer3D rightfulOwner = CreateVulkanRendererOrSkip();
            using VulkanRenderer3D foreignOwner = CreateVulkanRendererOrSkip();
            VulkanTextureResource texture = Assert.IsType<VulkanTextureResource>(
                rightfulOwner.Render2D.BuildTextureFromRaw(CreateBlackTextureAsset()));

            try {
                ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                    foreignOwner.Render2D.ReleaseTexture(texture));

                Assert.Equal("texture", exception.ParamName);
                rightfulOwner.Render2D.ReleaseTexture(texture);
                Assert.True(texture.IsDisposed);
                Assert.Equal(0ul, texture.Image.Handle);
                Assert.Equal(0ul, texture.Memory.Handle);
                Assert.Equal(0ul, texture.ImageView.Handle);
                Assert.Equal(0ul, texture.DescriptorSet.Handle);
            } finally {
                if (!texture.IsDisposed) {
                    rightfulOwner.Render2D.ReleaseTexture(texture);
                }
            }
        }

        /// <summary>
        /// Ensures Direct3D11 renderer disposal drains explicit textures that were not released by the caller.
        /// </summary>
        [Fact]
        public void DirectX11_disposal_drains_unreleased_texture_resources() {
            using DirectX11Renderer3D renderer = CreateDirectX11RendererOrSkip();
            DirectX11TextureResource texture = Assert.IsType<DirectX11TextureResource>(
                renderer.Render2D.BuildTextureFromRaw(CreateBlackTextureAsset()));

            renderer.Dispose();

            Assert.True(texture.IsDisposed);
            Assert.Null(texture.Texture);
            Assert.Null(texture.Resource);
        }

        /// <summary>
        /// Ensures Vulkan renderer disposal drains explicit textures and clears all native handles.
        /// </summary>
        [Fact]
        public void Vulkan_disposal_drains_unreleased_texture_resources() {
            using VulkanRenderer3D renderer = CreateVulkanRendererOrSkip();
            VulkanTextureResource texture = Assert.IsType<VulkanTextureResource>(
                renderer.Render2D.BuildTextureFromRaw(CreateBlackTextureAsset()));

            renderer.Dispose();

            Assert.True(texture.IsDisposed);
            Assert.Equal(0ul, texture.Image.Handle);
            Assert.Equal(0ul, texture.Memory.Handle);
            Assert.Equal(0ul, texture.ImageView.Handle);
            Assert.Equal(0ul, texture.DescriptorSet.Handle);
        }

        /// <summary>
        /// Ensures Vulkan preserves a texture when release is attempted while a 2D frame is recording.
        /// </summary>
        [Fact]
        public void Vulkan_release_during_active_frame_preserves_texture_resources() {
            using VulkanRenderer3D renderer = CreateVulkanRendererOrSkip();
            VulkanTextureResource texture = Assert.IsType<VulkanTextureResource>(
                renderer.Render2D.BuildTextureFromRaw(CreateBlackTextureAsset()));
            FieldInfo frameActiveField = typeof(VulkanRenderer2D).GetField(
                "frameActive",
                BindingFlags.Instance | BindingFlags.NonPublic);

            try {
                frameActiveField.SetValue(renderer.Render2D, true);

                Assert.Throws<InvalidOperationException>(() => renderer.Render2D.ReleaseTexture(texture));
                Assert.False(texture.IsDisposed);
                Assert.NotEqual(0ul, texture.Image.Handle);
                Assert.NotEqual(0ul, texture.Memory.Handle);
                Assert.NotEqual(0ul, texture.ImageView.Handle);
                Assert.NotEqual(0ul, texture.DescriptorSet.Handle);
            } finally {
                frameActiveField.SetValue(renderer.Render2D, false);
                if (!texture.IsDisposed) {
                    renderer.Render2D.ReleaseTexture(texture);
                }
            }
        }

        /// <summary>
        /// Ensures Vulkan disposal rejects an active frame without committing disposed state, then can be retried safely.
        /// </summary>
        [Fact]
        public void Vulkan_disposal_during_active_frame_can_retry_after_frame_completion() {
            using VulkanRenderer3D renderer = CreateVulkanRendererOrSkip();
            VulkanTextureResource texture = Assert.IsType<VulkanTextureResource>(
                renderer.Render2D.BuildTextureFromRaw(CreateBlackTextureAsset()));
            FieldInfo frameActiveField = typeof(VulkanRenderer2D).GetField(
                "frameActive",
                BindingFlags.Instance | BindingFlags.NonPublic);

            try {
                frameActiveField.SetValue(renderer.Render2D, true);

                Assert.Throws<InvalidOperationException>(() => renderer.Render2D.Dispose());
                Assert.False(texture.IsDisposed);
                Assert.NotEqual(0ul, texture.Image.Handle);
                Assert.NotEqual(0ul, texture.Memory.Handle);
                Assert.NotEqual(0ul, texture.ImageView.Handle);
                Assert.NotEqual(0ul, texture.DescriptorSet.Handle);

                frameActiveField.SetValue(renderer.Render2D, false);
                renderer.Render2D.Dispose();
                Assert.True(texture.IsDisposed);
                Assert.Equal(0ul, texture.Image.Handle);
                Assert.Equal(0ul, texture.Memory.Handle);
                Assert.Equal(0ul, texture.ImageView.Handle);
                Assert.Equal(0ul, texture.DescriptorSet.Handle);
            } finally {
                frameActiveField.SetValue(renderer.Render2D, false);
            }
        }

        /// <summary>
        /// Ensures an exception during surface recording/submission still ends the Vulkan 2D frame.
        /// </summary>
        [Fact]
        public void Vulkan_surface_submission_exception_ends_2d_frame() {
            using VulkanRenderer3D renderer = CreateVulkanRendererOrSkip();
            FieldInfo frameActiveField = typeof(VulkanRenderer2D).GetField(
                "frameActive",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo executeSurfaceFrameMethod = typeof(VulkanRenderer3D).GetMethod(
                "ExecuteSurfaceFrame",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(executeSurfaceFrameMethod);

            try {
                frameActiveField.SetValue(renderer.Render2D, true);
                Action throwingSurfaceFrame = () => throw new InvalidOperationException("surface submission failed");

                TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
                    executeSurfaceFrameMethod.Invoke(renderer, new object[] { throwingSurfaceFrame }));

                Assert.IsType<InvalidOperationException>(exception.InnerException);
                Assert.False((bool)frameActiveField.GetValue(renderer.Render2D));
            } finally {
                frameActiveField.SetValue(renderer.Render2D, false);
            }
        }

        /// <summary>
        /// Ensures Direct3D11 rejects texture release while the renderer is traversing a frame.
        /// </summary>
        [Fact]
        public void DirectX11_release_during_active_frame_preserves_texture_resources() {
            using DirectX11Renderer3D renderer = CreateDirectX11RendererOrSkip();
            DirectX11TextureResource texture = Assert.IsType<DirectX11TextureResource>(
                renderer.Render2D.BuildTextureFromRaw(CreateBlackTextureAsset()));
            FieldInfo frameActiveField = typeof(DirectX11Renderer3D).GetField(
                "frameActive",
                BindingFlags.Instance | BindingFlags.NonPublic);

            try {
                frameActiveField.SetValue(renderer, true);

                Assert.Throws<InvalidOperationException>(() => renderer.Render2D.ReleaseTexture(texture));
                Assert.False(texture.IsDisposed);
                Assert.NotNull(texture.Texture);
                Assert.NotNull(texture.Resource);
            } finally {
                frameActiveField.SetValue(renderer, false);
                if (!texture.IsDisposed) {
                    renderer.Render2D.ReleaseTexture(texture);
                }
            }
        }

        /// <summary>
        /// Ensures releasing texture descriptors permits reuse of the fixed Vulkan descriptor pool.
        /// </summary>
        [Fact]
        public void Vulkan_reuses_texture_descriptor_pool_after_release() {
            using VulkanRenderer3D renderer = CreateVulkanRendererOrSkip();
            for (int index = 0; index < 2050; index++) {
                VulkanTextureResource texture = Assert.IsType<VulkanTextureResource>(
                    renderer.Render2D.BuildTextureFromRaw(CreateBlackTextureAsset()));
                renderer.Render2D.ReleaseTexture(texture);
            }
        }

        /// <summary>
        /// Ensures aborting a transient command buffer removes only an actively recording allocation.
        /// </summary>
        [Fact]
        public void Vulkan_abort_single_time_command_tracks_recording_ownership() {
            using VulkanRenderer3D renderer = CreateVulkanRendererOrSkip();
            VulkanContext context = GetVulkanContext(renderer);
            FieldInfo recordingCommandBuffersField = typeof(VulkanContext).GetField(
                "RecordingCommandBufferHandles",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(recordingCommandBuffersField);
            var recordingCommandBufferHandles = Assert.IsAssignableFrom<ICollection<ulong>>(
                recordingCommandBuffersField.GetValue(context));

            CommandBuffer commandBuffer = BeginSingleTimeCommands(context);
            Assert.True(recordingCommandBufferHandles.Contains((ulong)commandBuffer.Handle));

            context.AbortSingleTimeCommands(commandBuffer);

            Assert.False(recordingCommandBufferHandles.Contains((ulong)commandBuffer.Handle));
        }

        static TextureAsset CreateBlackTextureAsset() {
            byte[] colors = new byte[4 * 4 * 4];
            for (int index = 3; index < colors.Length; index += 4) {
                colors[index] = 255;
            }

            return new TextureAsset {
                Width = 4,
                Height = 4,
                Colors = colors
            };
        }

        static byte[] CreatePaddedRegion() {
            return new byte[] {
                255, 0, 0, 255, 0, 255, 0, 255, 0xA1, 0xA2, 0xA3, 0xA4,
                0, 0, 255, 255, 255, 255, 255, 255, 0xB1, 0xB2, 0xB3, 0xB4
            };
        }

        static byte[] CreateRegion(int sourceRowPitch) {
            if (sourceRowPitch == 8) {
                return new byte[] {
                    255, 0, 0, 255, 0, 255, 0, 255,
                    0, 0, 255, 255, 255, 255, 255, 255
                };
            }

            return CreatePaddedRegion();
        }

        static byte[] ExpectedTextureBytes() {
            return new byte[] {
                0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255,
                0, 0, 0, 255, 255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 0, 255,
                0, 0, 0, 255, 0, 0, 255, 255, 255, 255, 255, 255, 0, 0, 0, 255,
                0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255
            };
        }

        static VulkanContext GetVulkanContext(VulkanRenderer3D renderer) {
            FieldInfo contextField = typeof(VulkanRenderer3D).GetField("context", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(contextField);
            return Assert.IsType<VulkanContext>(contextField.GetValue(renderer));
        }

        static DirectX11Renderer3D CreateDirectX11RendererOrSkip() {
            try {
                return new DirectX11Renderer3D();
            } catch (SharpDXException exception) {
                throw SkipException.ForSkip($"Direct3D11 capability is unavailable: {exception.Message}");
            } catch (DllNotFoundException exception) {
                throw SkipException.ForSkip($"Direct3D11 capability is unavailable: {exception.Message}");
            }
        }

        static VulkanRenderer3D CreateVulkanRendererOrSkip() {
            try {
                return new VulkanRenderer3D();
            } catch (PlatformNotSupportedException exception) {
                throw SkipException.ForSkip($"Vulkan capability is unavailable: {exception.Message}");
            } catch (DllNotFoundException exception) {
                throw SkipException.ForSkip($"Vulkan capability is unavailable: {exception.Message}");
            } catch (FileNotFoundException exception) when (
                string.Equals(exception.FileName, "vulkan-1.dll", StringComparison.OrdinalIgnoreCase)) {
                throw SkipException.ForSkip($"Vulkan capability is unavailable: {exception.Message}");
            }
        }

        static byte[] ReadDirectX11Texture(DirectX11TextureResource texture, D3DDevice device) {
            Texture2DDescription description = texture.Texture.Description;
            description.BindFlags = BindFlags.None;
            description.Usage = ResourceUsage.Staging;
            description.CpuAccessFlags = CpuAccessFlags.Read;
            description.OptionFlags = ResourceOptionFlags.None;

            using Texture2D stagingTexture = new Texture2D(device, description);
            DeviceContext context = device.ImmediateContext;
            context.CopyResource(texture.Texture, stagingTexture);
            DataBox dataBox = context.MapSubresource(stagingTexture, 0, MapMode.Read, MapFlags.None);
            try {
                byte[] bytes = new byte[texture.Width * texture.Height * 4];
                for (int y = 0; y < texture.Height; y++) {
                    for (int x = 0; x < texture.Width; x++) {
                        int sourceOffset = y * dataBox.RowPitch + x * 4;
                        int destinationOffset = (y * texture.Width + x) * 4;
                        for (int channel = 0; channel < 4; channel++) {
                            bytes[destinationOffset + channel] = Marshal.ReadByte(dataBox.DataPointer, sourceOffset + channel);
                        }
                    }
                }

                return bytes;
            } finally {
                context.UnmapSubresource(stagingTexture, 0);
            }
        }

        static byte[] ReadVulkanTexture(VulkanRenderer3D renderer, VulkanTextureResource texture) {
            FieldInfo contextField = typeof(VulkanRenderer3D).GetField("context", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(contextField);
            VulkanContext context = Assert.IsType<VulkanContext>(contextField.GetValue(renderer));

            using VulkanGpuBuffer readbackBuffer = new VulkanGpuBuffer(
                context,
                4 * 4 * 4,
                BufferUsageFlags.BufferUsageTransferDstBit,
                MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyHostCoherentBit);

            ImageMemoryBarrier toTransferSource = CreateImageBarrier(
                texture.Image,
                VkImageLayout.ShaderReadOnlyOptimal,
                VkImageLayout.TransferSrcOptimal,
                AccessFlags.AccessShaderReadBit,
                AccessFlags.AccessTransferReadBit);
            CommandBuffer commandBuffer = BeginSingleTimeCommands(context);
            InvokePipelineBarrier(
                renderer.Api,
                commandBuffer,
                PipelineStageFlags.PipelineStageFragmentShaderBit,
                PipelineStageFlags.PipelineStageTransferBit,
                toTransferSource);

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
                ImageExtent = new Extent3D(4, 4, 1)
            };
            InvokeCopyImageToBuffer(renderer.Api, commandBuffer, texture.Image, VkImageLayout.TransferSrcOptimal, readbackBuffer.Handle, region);

            ImageMemoryBarrier toShaderRead = CreateImageBarrier(
                texture.Image,
                VkImageLayout.TransferSrcOptimal,
                VkImageLayout.ShaderReadOnlyOptimal,
                AccessFlags.AccessTransferReadBit,
                AccessFlags.AccessShaderReadBit);
            InvokePipelineBarrier(
                renderer.Api,
                commandBuffer,
                PipelineStageFlags.PipelineStageTransferBit,
                PipelineStageFlags.PipelineStageFragmentShaderBit,
                toShaderRead);
            EndSingleTimeCommands(context, commandBuffer);

            byte[] bytes = new byte[4 * 4 * 4];
            IntPtr mapped = InvokeMapMemory(renderer.Api, context.Device, readbackBuffer.Memory, (ulong)bytes.Length);
            if (mapped == IntPtr.Zero || mapped.ToInt64() > -4096 && mapped.ToInt64() < 4096) {
                throw new InvalidOperationException($"Failed to map Vulkan readback memory; result code: {mapped.ToInt64()}.");
            }
            try {
                Marshal.Copy(mapped, bytes, 0, bytes.Length);
            } finally {
                renderer.Api.UnmapMemory(context.Device, readbackBuffer.Memory);
            }

            return bytes;
        }

        static ImageMemoryBarrier CreateImageBarrier(
            VkImage image,
            VkImageLayout oldLayout,
            VkImageLayout newLayout,
            AccessFlags sourceAccess,
            AccessFlags destinationAccess) {
            return new ImageMemoryBarrier {
                SType = StructureType.ImageMemoryBarrier,
                OldLayout = oldLayout,
                NewLayout = newLayout,
                SrcAccessMask = sourceAccess,
                DstAccessMask = destinationAccess,
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
        }

        static CommandBuffer BeginSingleTimeCommands(VulkanContext context) {
            MethodInfo method = typeof(VulkanContext).GetMethod("BeginSingleTimeCommands", BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(method);
            return (CommandBuffer)method.Invoke(context, null);
        }

        static void EndSingleTimeCommands(VulkanContext context, CommandBuffer commandBuffer) {
            MethodInfo method = typeof(VulkanContext).GetMethod("EndSingleTimeCommands", BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(method);
            method.Invoke(context, new object[] { commandBuffer });
        }

        static void InvokePipelineBarrier(
            Vk api,
            CommandBuffer commandBuffer,
            PipelineStageFlags sourceStage,
            PipelineStageFlags destinationStage,
            ImageMemoryBarrier barrier) {
            MethodInfo method = typeof(Vk).GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(candidate => candidate.Name == "CmdPipelineBarrier")
                .Where(candidate => candidate.GetParameters().Length == 10)
                .Where(candidate => candidate.GetParameters()[5].ParameterType.IsByRef)
                .Where(candidate => candidate.GetParameters()[7].ParameterType.IsByRef)
                .Where(candidate => candidate.GetParameters()[9].ParameterType.IsByRef)
                .Single();
            method.Invoke(api, new object[] {
                commandBuffer,
                sourceStage,
                destinationStage,
                DependencyFlags.None,
                0u,
                default(MemoryBarrier),
                0u,
                default(BufferMemoryBarrier),
                1u,
                barrier
            });
        }

        static void InvokeCopyImageToBuffer(
            Vk api,
            CommandBuffer commandBuffer,
            VkImage image,
            VkImageLayout imageLayout,
            VkBuffer buffer,
            BufferImageCopy region) {
            MethodInfo method = typeof(Vk).GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(candidate => candidate.Name == "CmdCopyImageToBuffer")
                .Where(candidate => candidate.GetParameters().Length == 6)
                .Where(candidate => candidate.GetParameters()[5].ParameterType.IsByRef)
                .Single();
            method.Invoke(api, new object[] {
                commandBuffer,
                image,
                imageLayout,
                buffer,
                1u,
                region
            });
        }

        static IntPtr InvokeMapMemory(Vk api, VkDevice device, DeviceMemory memory, ulong size) {
            Type pointerType = typeof(void).MakePointerType();
            Type pointerToPointerType = pointerType.MakePointerType();
            MethodInfo method = typeof(Vk).GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(candidate => candidate.Name == "MapMemory")
                .Where(candidate => candidate.GetParameters().Length == 6)
                .Single(candidate => candidate.GetParameters()[5].ParameterType == pointerToPointerType);

            DynamicMethod dynamicMethod = new DynamicMethod(
                "InvokeMapMemory",
                typeof(IntPtr),
                new[] { typeof(Vk), typeof(VkDevice), typeof(DeviceMemory), typeof(ulong) },
                typeof(RuntimeTextureRegionUploadTests).Module,
                true);
            ILGenerator il = dynamicMethod.GetILGenerator();
            LocalBuilder mappedLocal = il.DeclareLocal(pointerType);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Ldc_I8, 0L);
            il.Emit(OpCodes.Ldarg_3);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ldloca, mappedLocal);
            il.Emit(OpCodes.Call, method);
            System.Reflection.Emit.Label mappedSuccessfully = il.DefineLabel();
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Brfalse, mappedSuccessfully);
            il.Emit(OpCodes.Conv_I);
            il.Emit(OpCodes.Ret);
            il.MarkLabel(mappedSuccessfully);
            il.Emit(OpCodes.Pop);
            il.Emit(OpCodes.Ldloc, mappedLocal);
            il.Emit(OpCodes.Conv_I);
            il.Emit(OpCodes.Ret);

            var invoker = (MapMemoryInvoker)dynamicMethod.CreateDelegate(typeof(MapMemoryInvoker));
            return invoker(api, device, memory, size);
        }

        delegate IntPtr MapMemoryInvoker(Vk api, VkDevice device, DeviceMemory memory, ulong size);
    }
}
