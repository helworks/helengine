#pragma once
#include "vulkan_context.hpp"
#include "vulkan_indirect_range.hpp"
#include <array>
#include <memory>
#include <span>
#include <vector>

namespace helengine::vulkan {
/// <summary>Owns indexed Vulkan meshes and authored SPIR-V pipelines while borrowing the 2D owner's RGBA8 target for synchronous depth-tested rendering.</summary>
class VulkanMeshRenderer final {
public:
    /// <summary>Selects the existing engine's forward, atlas-depth or radial point-shadow attachment contract.</summary>
    enum class PassKind { Color,DepthShadow,PointShadow };
    /// <summary>Preserves the fixed float32 position, normal and UV vertex contract independently of configurable engine arithmetic.</summary>
    struct Vertex {
        /// <summary>Stores three position components without changing engine matrix conventions.</summary>
        float Position[3];
        /// <summary>Stores the authored normal used by the shader.</summary>
        float Normal[3];
        /// <summary>Stores authored normalized texture coordinates.</summary>
        float Uv[2];
    };
    /// <summary>Describes one set-zero descriptor reflected by the engine's ShaderBinary.Bindings contract; no slot guessing is performed.</summary>
    struct BindingLayout {
        /// <summary>Uses the exact unified engine binding slot, including texture and sampler shifts.</summary>
        uint32_t Slot;
        /// <summary>Selects a uniform buffer, separate sampled image or separate sampler.</summary>
        VkDescriptorType Type;
        /// <summary>Identifies the vertex and fragment stages which consume the binding.</summary>
        VkShaderStageFlags Stages;
        /// <summary>Requires the reflected minimum uniform byte length; image and sampler bindings require zero.</summary>
        size_t MinimumBytes;
        /// <summary>Rejects descriptor arrays explicitly until their engine runtime contract supplies every array element.</summary>
        uint32_t Count = 1;
        /// <summary>Preserves the reflected register space and rejects unsupported sets instead of silently binding them as set zero.</summary>
        uint32_t Set = 0;
        /// <summary>Preserves the reflected sampled texture dimension; samplers and buffers do not consume this field.</summary>
        VkImageViewType ViewType = VK_IMAGE_VIEW_TYPE_2D;
    };
    /// <summary>Supplies one immutable uniform snapshot, copied when the draw is queued.</summary>
    struct BufferBinding {
        /// <summary>Identifies the reflected uniform descriptor slot.</summary>
        uint32_t Slot;
        /// <summary>Owns the fixed-layout GPU bytes without converting configurable math implicitly.</summary>
        std::vector<uint8_t> Bytes;
    };
    /// <summary>Borrows an existing texture owner's descriptor until RenderFrame completes or DiscardFrame cancels the draw.</summary>
    struct ImageBinding {
        /// <summary>Identifies an exact sampled-image or sampler slot from the reflected layout.</summary>
        uint32_t Slot;
        /// <summary>Supplies live same-device image/sampler handles in shader-read layout, never transferring ownership.</summary>
        VkDescriptorImageInfo Image;
        /// <summary>Matches the owner's actual view dimension to the reflected 2D or cube resource type.</summary>
        VkImageViewType ViewType = VK_IMAGE_VIEW_TYPE_2D;
    };
    /// <summary>Translates the existing MaterialRenderState enum values without changing asset formats or silently substituting renderer defaults.</summary>
    struct MaterialState {
        /// <summary>Selects engine Opaque zero or AlphaBlend one.</summary>
        uint32_t BlendMode=0;
        /// <summary>Selects engine None zero, Front one or Back two.</summary>
        uint32_t CullMode=2;
        /// <summary>Controls the existing less-than depth comparison.</summary>
        bool DepthTestEnabled=true;
        /// <summary>Controls depth-buffer writes independently of the comparison.</summary>
        bool DepthWriteEnabled=true;
    };
    /// <summary>Creates the shared render passes without an independent color target; engine-authored SPIR-V requires the host's Vulkan 1.2 context.</summary>
    explicit VulkanMeshRenderer(VulkanContext& context,std::string shaderRoot={});
    /// <summary>Releases all owned GPU resources after every synchronous submission has completed.</summary>
    ~VulkanMeshRenderer();
    /// <summary>Prevents duplicated native resource ownership.</summary>
    VulkanMeshRenderer(const VulkanMeshRenderer&) = delete;
    /// <summary>Prevents copying pipeline and mesh handles.</summary>
    VulkanMeshRenderer& operator=(const VulkanMeshRenderer&) = delete;
    /// <summary>Uploads validated finite vertices and triangle indices into device-local buffers using bounded staging allocations.</summary>
    size_t UploadMesh(std::span<const Vertex> vertices,std::span<const uint32_t> indices);
    /// <summary>Uploads persistent static geometry and derives conservative bounds for every ordered original range.</summary>
    size_t UploadIndirectPage(std::span<const Vertex> vertices,std::span<const uint32_t> indices,std::span<const VulkanIndirectRange> ranges);
    /// <summary>Shares immutable source GPU geometry; the supplied CPU spans must be the exact original upload data, as guaranteed by the manager's immutable BatchSource.</summary>
    size_t UploadIndirectPageForMesh(size_t source,std::span<const Vertex> sourceVertices,std::span<const uint32_t> sourceIndices,std::span<const VulkanIndirectRange> ranges);
    /// <summary>Returns direct API calls used by page fallbacks in the last color pass.</summary>
    size_t GetLastPageDirectCallCount() const;
    /// <summary>Queues one compatible page with exact serialized baked WVP bytes; repeated page use falls back to direct ranges.</summary>
    void QueueIndirectPage(size_t page,size_t material,std::span<const BufferBinding> buffers,std::span<const ImageBinding> images,std::span<const float,16> worldViewProjection,std::span<const int> frameSubjects={});
    /// <summary>Reports whether a live mesh owns indirect page metadata.</summary>
    bool IsIndirectPage(size_t mesh) const;
    /// <summary>Reports retained bounds/command bytes plus vertex/index payload, excluding allocator overhead.</summary>
    size_t GetIndirectPageBytes(size_t mesh) const;
    /// <summary>Returns cumulative successfully published page payload uploads; unchanged frames leave this counter unchanged.</summary>
    uint64_t GetIndirectUploadBytes() const;
    /// <summary>Reads the last GPU-selected slot count for explicit diagnostics, rejecting pending work and introducing a synchronous readback.</summary>
    size_t ReadIndirectVisibleCommandCount(size_t mesh) const;
    /// <summary>Returns logical page commands from the last completed color pass, not visible draw counts.</summary>
    size_t GetLastIndirectCommandCount() const;
    /// <summary>Returns actual indirect draw API calls recorded for the last color pass.</summary>
    size_t GetLastIndirectCallCount() const;
    /// <summary>Returns actual GPU visibility dispatches recorded for the last color pass.</summary>
    size_t GetLastIndirectDispatchCount() const;
    /// <summary>Allocates or rewrites pass-idle coherent transient geometry, reusing fixed vertex/index capacities; SIZE_MAX requests a fresh handle.</summary>
    size_t UpdateDynamicMesh(size_t mesh,std::span<const Vertex> vertices,std::span<const uint32_t> indices);
    /// <summary>Reports queued binding packets; a page packet can contain multiple logical commands or fallback API calls.</summary>
    size_t GetQueuedDrawCount() const;
    /// <summary>Creates a pipeline using the supplied authored entry points and exact engine-reflected set-zero descriptor layout.</summary>
    size_t UploadMaterial(std::span<const uint32_t> vertex,std::string vertexEntry,std::span<const uint32_t> fragment,std::string fragmentEntry,std::span<const BindingLayout> bindings);
    /// <summary>Creates an authored pipeline preserving the material's explicit blend, cull and depth state.</summary>
    size_t UploadMaterial(std::span<const uint32_t> vertex,std::string vertexEntry,std::span<const uint32_t> fragment,std::string fragmentEntry,std::span<const BindingLayout> bindings,MaterialState renderState);
    /// <summary>Creates a real builtin atlas-depth or R32 radial-cube pipeline using the unchanged authored SPIR-V programs and uniform layouts.</summary>
    size_t UploadMaterial(std::span<const uint32_t> vertex,std::string vertexEntry,std::span<const uint32_t> fragment,std::string fragmentEntry,std::span<const BindingLayout> bindings,MaterialState renderState,PassKind pass);
    /// <summary>Validates all reflected resources and submesh bounds before copying one draw's uniform snapshots into the queue.</summary>
    void QueueDraw(size_t mesh,size_t material,std::span<const BufferBinding> buffers,std::span<const ImageBinding> images,uint32_t firstIndex=0,uint32_t indexCount=0);
    /// <summary>Clears or loads a borrowed RGBA8 image, renders queued indexed geometry with a fresh depth attachment and leaves color in COLOR_ATTACHMENT_OPTIMAL for the 2D LOAD pass.</summary>
    /// <remarks>Loaded color must enter in TRANSFER_SRC_OPTIMAL after the previous 2D/readback pass. The caller keeps the target and every borrowed texture alive until return.</remarks>
    void RenderFrame(VkImageView target,uint32_t width,uint32_t height,std::array<float,4> clear,bool loadExistingColor=false);
    /// <summary>Renders one camera within a validated physical-pixel viewport and matching scissor while preserving the whole target's clear/load policy.</summary>
    void RenderFrame(VkImageView target,uint32_t width,uint32_t height,std::array<float,4> clear,bool loadExistingColor,VkRect2D viewport);
    /// <summary>Creates a sampleable D32 atlas initialized to depth one; requested shadows still require actual caster passes.</summary>
    size_t CreateDepthShadow(uint32_t width,uint32_t height);
    /// <summary>Creates an R32_FLOAT cube with six renderable faces initialized to radial depth one, matching EditorPointShadowDepth.hlsl.</summary>
    size_t CreatePointShadow(uint32_t resolution);
    /// <summary>Returns live owned shadow sampling handles without transferring their lifetime to the caller.</summary>
    VkDescriptorImageInfo GetShadowDescriptor(size_t texture) const;
    /// <summary>Returns the actual two-dimensional or cube view type for explicit reflected dimension validation.</summary>
    VkImageViewType GetShadowViewType(size_t texture) const;
    /// <summary>Renders queued caster draws into one complete atlas or cube face, then synchronizes and makes the result shader-readable.</summary>
    void RenderShadowPass(size_t texture,uint32_t face=0,bool clear=true);
    /// <summary>Renders one atlas allocation without disturbing other tiles when clear is false; matrices and per-caster uniforms remain the provider's existing engine contract.</summary>
    void RenderShadowPass(size_t texture,uint32_t face,bool clear,VkRect2D viewport);
    /// <summary>Releases an owned shadow texture only when no queued draw borrows its image or sampler.</summary>
    void ReleaseShadowTexture(size_t texture);
    /// <summary>Rejects releasing a mesh referenced by queued work.</summary>
    void ReleaseMesh(size_t mesh);
    /// <summary>Rejects releasing a material referenced by queued work.</summary>
    void ReleaseMaterial(size_t material);
    /// <summary>Lets the texture owner defer resource retirement while a queued descriptor borrows its view.</summary>
    bool ReferencesImage(VkImageView image) const;
    /// <summary>Reports whether resources remain borrowed by a queued frame.</summary>
    bool HasPendingDraws() const;
    /// <summary>Cancels unsent CPU draw packets and releases their borrowed resource references.</summary>
    void DiscardFrame();
    /// <summary>Returns the hardware occlusion result of the latest color mesh pass; zero distinguishes all-culled geometry from a HUD-only frame.</summary>
    uint64_t GetLastVisibleSamples() const;
    /// <summary>Returns the hardware occlusion result of the latest atlas or cube caster pass.</summary>
    uint64_t GetLastShadowSamples() const;
private:
    /// <summary>Hides the owned mesh, descriptor, render-pass and pipeline state from generated engine headers.</summary>
    class State;
    /// <summary>Owns the native implementation for this renderer's lifetime.</summary>
    std::unique_ptr<State> Resources;
};
}
