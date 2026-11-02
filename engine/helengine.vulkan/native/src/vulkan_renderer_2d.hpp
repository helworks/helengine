#pragma once
#include "gpu_buffer.hpp"
#include "gpu_image.hpp"
#include "quad_vertex.hpp"
#include "quad_push_constants.hpp"
#include <array>
#include <memory>
#include <span>
#include <vector>
#include <unordered_map>

namespace helengine::vulkan {
/// <summary>Renders ordered textured quads into a synchronized RGBA8 target; this low-level renderer has no simulation or generated-core dependency.</summary>
class VulkanRenderer2D final {
public:
    /// <summary>Creates render-pass, texture descriptors and the Vulkan 1.1 quad pipeline.</summary>
    VulkanRenderer2D(VulkanContext& context, uint32_t width, uint32_t height, std::string shaderRoot = {});
    /// <summary>Synchronizes and releases framebuffer, pipeline and descriptor resources in dependency order.</summary>
    ~VulkanRenderer2D();
    /// <summary>Prevents duplicating ownership of descriptors and pipeline state.</summary>
    VulkanRenderer2D(const VulkanRenderer2D&) = delete;
    /// <summary>Prevents copying GPU state through assignment.</summary>
    VulkanRenderer2D& operator=(const VulkanRenderer2D&) = delete;
    /// <summary>Uploads a validated RGBA8 texture, returning a renderer-local stable handle.</summary>
    size_t CreateTexture(uint32_t width, uint32_t height, std::span<const uint8_t> rgba);
    /// <summary>Uploads a bounded RGBA8 region with explicit source pitch, waiting before old texture memory can be modified.</summary>
    void UpdateTextureRegion(size_t texture, uint32_t x, uint32_t y, uint32_t width, uint32_t height, std::span<const uint8_t> rgba, uint32_t rowPitch);
    /// <summary>Frees a runtime texture and its descriptor after GPU completion while keeping other texture handles stable.</summary>
    void ReleaseTexture(size_t texture);
    /// <summary>Recreates a target only for a nonzero drawable size; minimized windows retain their last valid target.</summary>
    bool Resize(uint32_t width, uint32_t height);
    /// <summary>Allocates an independent attachment on this device while retaining shared pipelines and texture handles.</summary>
    void AddOutput(size_t handle,uint32_t width,uint32_t height);
    /// <summary>Selects a previously registered attachment outside an active frame without changing any other output.</summary>
    void SelectOutput(size_t handle);
    /// <summary>Begins a frame with an explicit clear color and an empty clipping stack.</summary>
    void BeginFrame(std::array<float, 4> clear, bool loadExistingColor = false);
    /// <summary>Appends six ordered vertices with normalized UVs and the currently active clip rectangle.</summary>
    void DrawQuad(size_t texture, const std::array<QuadVertex, 6>& vertices);
    /// <summary>Appends one axis-aligned rectangle, primarily for glyph bounds, shapes and GPU contract tests.</summary>
    void DrawRect(size_t texture, float x, float y, float width, float height, std::array<float,4> color);
    /// <summary>Appends a rounded rectangle using engine corner flags and a signed-distance fragment shader with inward border coverage.</summary>
    void DrawRoundedRect(float x,float y,float width,float height,float radius,float borderThickness,uint32_t corners,std::array<float,4> fill,std::array<float,4> border);
    /// <summary>Intersects a new pixel-space scissor with the active stack and framebuffer bounds.</summary>
    void PushClip(float x, float y, float width, float height);
    /// <summary>Restores the previous scissor; malformed command streams fail rather than leaking clip state.</summary>
    void PopClip();
    /// <summary>Submits one render pass, synchronizes GPU writes and returns tightly packed RGBA8 bytes for offscreen verification.</summary>
    std::vector<uint8_t> EndFrameAndReadback();
    /// <summary>Completes the GPU frame in transfer-source layout; disabled readback returns no CPU pixels and performs no staging allocation or copy.</summary>
    std::vector<uint8_t> EndFrame(bool readbackEnabled);
    /// <summary>Returns the final transfer-source image for presentation on the same device.</summary>
    VkImage GetOutputImage() const { return Target->Get(); }
    /// <summary>Borrows the RGBA8 target view for a same-device native pass; the renderer retains image ownership.</summary>
    VkImageView GetOutputImageView() const { return Target->GetView(); }
    /// <summary>Borrows the device context so sibling native passes share the exact same GPU and synchronization owner.</summary>
    VulkanContext& GetContext() const { return Context; }
    /// <summary>Returns the explicit packaged shader directory shared by backend-internal Vulkan passes.</summary>
    const std::string& GetShaderRoot() const { return ShaderRoot; }
    /// <summary>Borrows a live texture's sampled image/sampler binding while this renderer retains ownership and synchronization.</summary>
    VkDescriptorImageInfo GetTextureDescriptor(size_t texture) const;
    /// <summary>Returns the valid target pixel width.</summary>
    uint32_t GetWidth() const { return Width; }
    /// <summary>Returns the valid target pixel height.</summary>
    uint32_t GetHeight() const { return Height; }
    /// <summary>Returns the actual ordered draw count recorded for the active or latest completed frame.</summary>
    size_t GetQueuedDrawCount() const { return DrawTextures.size(); }
private:
    /// <summary>Creates descriptor layout, sampler, render pass and shader pipeline.</summary>
    void InitializePipeline();
    /// <summary>Releases successfully initialized resources after pending submissions complete.</summary>
    void Release() noexcept;
    /// <summary>Loads compiled SPIR-V and creates a shader module, rejecting absent or malformed artifacts.</summary>
    VkShaderModule LoadShader(const char* name);
    /// <summary>Records one image layout and memory-dependency transition.</summary>
    static void Transition(VkCommandBuffer commands, VkImage image, VkImageLayout oldLayout, VkImageLayout newLayout, VkPipelineStageFlags sourceStage, VkPipelineStageFlags destinationStage, VkAccessFlags sourceAccess, VkAccessFlags destinationAccess);
    /// <summary>Borrows the graphics context, which must outlive the renderer.</summary>
    VulkanContext& Context;
    /// <summary>Stores the explicit diagnostic build shader root or packaged player shader directory.</summary>
    std::string ShaderRoot;
    /// <summary>Owns the current RGBA8 output attachment.</summary>
    std::unique_ptr<GpuImage> Target;
    /// <summary>Owns images for inactive outputs; the selected image remains in Target.</summary>
    std::unordered_map<size_t,std::unique_ptr<GpuImage>> OutputImages;
    /// <summary>Owns compatible framebuffers for inactive outputs.</summary>
    std::unordered_map<size_t,VkFramebuffer> OutputFramebuffers;
    /// <summary>Identifies the selected attachment; the initial single output retains handle 1.</summary>
    size_t ActiveOutput = 1;
    /// <summary>Owns sampled textures in stable handle order.</summary>
    std::vector<std::unique_ptr<GpuImage>> Textures;
    /// <summary>Stores one descriptor set per texture; the descriptor pool owns their lifetime.</summary>
    std::vector<VkDescriptorSet> TextureSets;
    /// <summary>Stores ordered quad vertices before their single frame upload.</summary>
    std::vector<QuadVertex> Vertices;
    /// <summary>Stores descriptor indices for ordered draws.</summary>
    std::vector<size_t> DrawTextures;
    /// <summary>Stores the effective scissor associated with each ordered draw.</summary>
    std::vector<VkRect2D> DrawClips;
    /// <summary>Stores immutable shader constants associated with each ordered draw.</summary>
    std::vector<QuadPushConstants> DrawConstants;
    /// <summary>Stores nested clip state without changing engine command ordering.</summary>
    std::vector<VkRect2D> Clips;
    /// <summary>Stores the explicit frame clear value.</summary>
    VkClearValue Clear {};
    /// <summary>Owns the target's compatible render pass.</summary>
    VkRenderPass RenderPass = VK_NULL_HANDLE;
    /// <summary>Owns the compatible UI pass that preserves preceding 3D color in COLOR_ATTACHMENT_OPTIMAL layout.</summary>
    VkRenderPass LoadRenderPass = VK_NULL_HANDLE;
    /// <summary>Owns the framebuffer bound to Target.</summary>
    VkFramebuffer Framebuffer = VK_NULL_HANDLE;
    /// <summary>Owns the sampled-image binding layout.</summary>
    VkDescriptorSetLayout DescriptorLayout = VK_NULL_HANDLE;
    /// <summary>Owns bounded sampled-image descriptor allocation.</summary>
    VkDescriptorPool DescriptorPool = VK_NULL_HANDLE;
    /// <summary>Owns the nearest/clamped sampler used by UI atlases.</summary>
    VkSampler Sampler = VK_NULL_HANDLE;
    /// <summary>Owns the vertex/fragment pipeline layout and viewport push constants.</summary>
    VkPipelineLayout PipelineLayout = VK_NULL_HANDLE;
    /// <summary>Owns the graphics pipeline for ordered transparent UI draws.</summary>
    VkPipeline Pipeline = VK_NULL_HANDLE;
    /// <summary>Stores current target geometry in physical pixels.</summary>
    uint32_t Width = 0, Height = 0;
    /// <summary>Rejects invalid Begin/Draw/End lifecycle transitions.</summary>
    bool FrameActive = false;
    /// <summary>Selects the preserving render pass when a preceding same-device pass supplied color content.</summary>
    bool LoadExistingColor = false;
    /// <summary>Owns a renderer-internal white texture so solid SDF shapes retain a valid sampler binding.</summary>
    size_t WhiteTexture = SIZE_MAX;
};
}
