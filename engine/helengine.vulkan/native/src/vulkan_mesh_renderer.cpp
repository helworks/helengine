#include "vulkan_frame_trace_scope.hpp"
#include <cstdlib>
#include <chrono>
#include <iostream>
#include "vulkan_mesh_renderer.hpp"
#include "gpu_buffer.hpp"
#include "mesh_buffer_upload.hpp"
#include "vulkan_prepared_bindings.hpp"
#include "vulkan_indirect_page.hpp"
#include <algorithm>
#include <cmath>
#include <cstring>
#include <limits>
#include <stdexcept>
#include <unordered_map>

namespace helengine::vulkan {
static_assert(sizeof(VulkanMeshRenderer::Vertex)==32,"The native mesh vertex ABI must remain float32 position/normal/UV.");

/// <summary>Holds native resources under deterministic ownership; color targets and sampled textures remain borrowed from the host.</summary>
class VulkanMeshRenderer::State final {
public:
    /// <summary>Owns staged device-local geometry and immutable triangle count.</summary>
    struct Mesh {
        /// <summary>Owns the uploaded fixed-layout vertex buffer.</summary>
        std::unique_ptr<GpuBuffer> Vertices;
        /// <summary>Owns the uploaded uint32 index buffer.</summary>
        std::unique_ptr<GpuBuffer> Indices;
        /// <summary>Bounds every queued submesh draw.</summary>
        uint32_t IndexCount;
        /// <summary>Bounds pass-idle transient vertex writes independently from current index count.</summary>
        size_t VertexCapacity=0;
        /// <summary>Bounds pass-idle transient index writes.</summary>
        size_t IndexCapacity=0;
        /// <summary>Distinguishes reusable coherent geometry from immutable device-local source meshes.</summary>
        bool Dynamic=false;
        /// <summary>Owns persistent per-range bounds and visibility commands for a static page.</summary>
        std::unique_ptr<VulkanIndirectPage> Indirect;
        /// <summary>Records payload bytes for renderer-side resident budget accounting.</summary>
        size_t GeometryBytes=0;
        /// <summary>Marks a successfully queued page until frame completion, avoiding per-page linear duplicate searches.</summary>
        bool PageQueued=false;
        /// <summary>Keeps immutable original GPU geometry alive when a page shares its buffers after source-handle retirement.</summary>
        std::shared_ptr<Mesh> GeometryOwner;
    };
    /// <summary>Owns one authored pipeline and its exact reflected descriptor contract.</summary>
    struct Material {
        /// <summary>Borrows the device which outlives this material.</summary>
        VkDevice Device;
        /// <summary>Owns the descriptor layout for engine register space zero.</summary>
        VkDescriptorSetLayout Descriptors=VK_NULL_HANDLE;
        /// <summary>Owns the compatible pipeline layout.</summary>
        VkPipelineLayout Layout=VK_NULL_HANDLE;
        /// <summary>Owns the native authored graphics pipeline.</summary>
        VkPipeline Pipeline=VK_NULL_HANDLE;
        /// <summary>Stores validated combined-stage descriptor requirements.</summary>
        std::vector<BindingLayout> Bindings;
        /// <summary>Selects the exact compatible native attachment contract for this pipeline.</summary>
        PassKind Pass=PassKind::Color;
        /// <summary>Initializes the borrowing relationship before Vulkan handle creation.</summary>
        explicit Material(VkDevice device):Device(device) {}
        /// <summary>Releases handles in dependency order, including partial construction failures.</summary>
        ~Material() {
            if(Pipeline) vkDestroyPipeline(Device,Pipeline,nullptr);
            if(Layout) vkDestroyPipelineLayout(Device,Layout,nullptr);
            if(Descriptors) vkDestroyDescriptorSetLayout(Device,Descriptors,nullptr);
        }
    };
    /// <summary>Owns temporary shader modules until pipeline creation finishes or fails.</summary>
    struct Modules {
        /// <summary>Borrows the logical device.</summary>
        VkDevice Device;
        /// <summary>Owns the authored vertex module.</summary>
        VkShaderModule Vertex=VK_NULL_HANDLE;
        /// <summary>Owns the authored fragment module.</summary>
        VkShaderModule Fragment=VK_NULL_HANDLE;
        /// <summary>Releases both modules after pipeline compilation.</summary>
        ~Modules() { if(Vertex) vkDestroyShaderModule(Device,Vertex,nullptr); if(Fragment) vkDestroyShaderModule(Device,Fragment,nullptr); }
    };
    /// <summary>Owns a correctly formatted depth attachment independently of borrowed color.</summary>
    struct Depth {
        /// <summary>Borrows the owning context.</summary>
        VulkanContext& Context;
        /// <summary>Owns the optimal-tiled depth image.</summary>
        VkImage Image=VK_NULL_HANDLE;
        /// <summary>Owns device-local depth memory.</summary>
        VkDeviceMemory Memory=VK_NULL_HANDLE;
        /// <summary>Owns the depth-only view.</summary>
        VkImageView View=VK_NULL_HANDLE;
        /// <summary>Creates a D32 depth image with explicit capability checks.</summary>
        Depth(VulkanContext& context,uint32_t width,uint32_t height):Context(context) {
            VkFormatProperties properties{}; vkGetPhysicalDeviceFormatProperties(context.GetPhysicalDevice(),VK_FORMAT_D32_SFLOAT,&properties);
            if(!(properties.optimalTilingFeatures&VK_FORMAT_FEATURE_DEPTH_STENCIL_ATTACHMENT_BIT)) throw std::runtime_error("Vulkan mesh target requires D32_SFLOAT depth attachment support");
            VkImageCreateInfo create{VK_STRUCTURE_TYPE_IMAGE_CREATE_INFO}; create.imageType=VK_IMAGE_TYPE_2D; create.format=VK_FORMAT_D32_SFLOAT;
            create.extent={width,height,1}; create.mipLevels=1; create.arrayLayers=1; create.samples=VK_SAMPLE_COUNT_1_BIT; create.tiling=VK_IMAGE_TILING_OPTIMAL; create.usage=VK_IMAGE_USAGE_DEPTH_STENCIL_ATTACHMENT_BIT; create.sharingMode=VK_SHARING_MODE_EXCLUSIVE;
            try {
                VulkanContext::Require(vkCreateImage(context.GetDevice(),&create,nullptr,&Image),"vkCreateImage(mesh depth)");
                VkMemoryRequirements requirements{}; vkGetImageMemoryRequirements(context.GetDevice(),Image,&requirements);
                VkMemoryAllocateInfo allocate{VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO}; allocate.allocationSize=requirements.size; allocate.memoryTypeIndex=context.FindMemoryType(requirements.memoryTypeBits,VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT);
                VulkanContext::Require(vkAllocateMemory(context.GetDevice(),&allocate,nullptr,&Memory),"vkAllocateMemory(mesh depth)");
                VulkanContext::Require(vkBindImageMemory(context.GetDevice(),Image,Memory,0),"vkBindImageMemory(mesh depth)");
                VkImageViewCreateInfo view{VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO}; view.image=Image; view.viewType=VK_IMAGE_VIEW_TYPE_2D; view.format=VK_FORMAT_D32_SFLOAT; view.subresourceRange={VK_IMAGE_ASPECT_DEPTH_BIT,0,1,0,1};
                VulkanContext::Require(vkCreateImageView(context.GetDevice(),&view,nullptr,&View),"vkCreateImageView(mesh depth)");
            } catch(...) { Release(); throw; }
        }
        /// <summary>Releases all initialized native depth handles.</summary>
        void Release() noexcept { if(View) vkDestroyImageView(Context.GetDevice(),View,nullptr); if(Image) vkDestroyImage(Context.GetDevice(),Image,nullptr); if(Memory) vkFreeMemory(Context.GetDevice(),Memory,nullptr); }
        /// <summary>Releases depth resources after synchronous submission finishes.</summary>
        ~Depth() { Release(); }
    };
    /// <summary>Owns a queued draw and its copied uniform bytes while borrowing material, mesh and texture handles.</summary>
    struct Draw {
        /// <summary>Retains direct frame-local mesh ownership through deferred descriptor and command recording.</summary>
        int TraceSubject=-1;
        /// <summary>Retains shared participants without assigning a batch duration to individual meshes.</summary>
        int TraceBatch=-1;
        /// <summary>Selects the owned mesh.</summary>
        size_t MeshId;
        /// <summary>Selects the owned authored pipeline.</summary>
        size_t MaterialId;
        /// <summary>Stores the validated start of the authored submesh.</summary>
        uint32_t FirstIndex;
        /// <summary>Stores the validated number of triangle indices.</summary>
        uint32_t IndexCount;
        /// <summary>Owns the exact per-draw uniform snapshots.</summary>
        std::vector<BufferBinding> Buffers;
        /// <summary>Borrows same-device native texture descriptors until submission completes.</summary>
        std::vector<ImageBinding> Images;
        /// <summary>Identifies an explicit page packet independently of ordinary draws from that same geometry.</summary>
        bool Page=false;
        /// <summary>Enables compute for the first page use; repeated uses keep their own direct geometry ordering.</summary>
        bool CullPage=false;
        /// <summary>Copies exact authored float32 WVP columns for current-frame culling only.</summary>
        std::array<float,16> CullMatrix{};
    };
    /// <summary>Owns a sampleable shadow atlas or radial cube, including per-face render views and a non-comparison linear sampler.</summary>
    struct ShadowTexture {
        /// <summary>Borrows the context which outlives every native handle.</summary>
        VulkanContext& Context;
        /// <summary>Owns the optimal-tiled sampleable image.</summary>
        VkImage Image=VK_NULL_HANDLE;
        /// <summary>Owns the image's device-local memory.</summary>
        VkDeviceMemory Memory=VK_NULL_HANDLE;
        /// <summary>Owns the two-dimensional or cube sampling view.</summary>
        VkImageView View=VK_NULL_HANDLE;
        /// <summary>Owns one two-dimensional rendering view per atlas or cube face.</summary>
        std::vector<VkImageView> Faces;
        /// <summary>Owns a linear clamp sampler because existing HLSL compares raw sampled shadow depths manually.</summary>
        VkSampler Sampler=VK_NULL_HANDLE;
        /// <summary>Stores the exact engine shadow attachment kind.</summary>
        PassKind Pass;
        /// <summary>Stores the physical shadow target extent.</summary>
        uint32_t Width,Height;
        /// <summary>Creates either sampled D32 depth or an R32 radial cube using verified native format capabilities.</summary>
        ShadowTexture(VulkanContext& context,uint32_t width,uint32_t height,PassKind pass):Context(context),Pass(pass),Width(width),Height(height) {
            const bool cube=pass==PassKind::PointShadow; const uint32_t layers=cube ? 6 : 1;
            const auto& limits=context.GetProperties().limits;
            if(!width || !height || width>limits.maxImageDimension2D || height>limits.maxImageDimension2D || width>limits.maxFramebufferWidth || height>limits.maxFramebufferHeight || (cube && (width!=height || width>limits.maxImageDimensionCube || limits.maxImageArrayLayers<6))) throw std::invalid_argument("Vulkan shadow extent exceeds the device's framebuffer/2D/cube/layer limits");
            const VkFormat format=cube ? VK_FORMAT_R32_SFLOAT : VK_FORMAT_D32_SFLOAT;
            VkFormatProperties properties{}; vkGetPhysicalDeviceFormatProperties(context.GetPhysicalDevice(),format,&properties);
            const VkFormatFeatureFlags required=VK_FORMAT_FEATURE_SAMPLED_IMAGE_BIT|VK_FORMAT_FEATURE_SAMPLED_IMAGE_FILTER_LINEAR_BIT|(cube ? VK_FORMAT_FEATURE_COLOR_ATTACHMENT_BIT : VK_FORMAT_FEATURE_DEPTH_STENCIL_ATTACHMENT_BIT);
            if((properties.optimalTilingFeatures&required)!=required) throw std::runtime_error("Vulkan shadow format requires native render attachment and linear raw-depth sampling capabilities");
            try {
                VkImageCreateInfo create{VK_STRUCTURE_TYPE_IMAGE_CREATE_INFO}; create.flags=cube ? VK_IMAGE_CREATE_CUBE_COMPATIBLE_BIT : 0; create.imageType=VK_IMAGE_TYPE_2D; create.format=format; create.extent={width,height,1}; create.mipLevels=1; create.arrayLayers=layers; create.samples=VK_SAMPLE_COUNT_1_BIT; create.tiling=VK_IMAGE_TILING_OPTIMAL;
                create.usage=VK_IMAGE_USAGE_SAMPLED_BIT|VK_IMAGE_USAGE_TRANSFER_DST_BIT|(cube ? VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT : VK_IMAGE_USAGE_DEPTH_STENCIL_ATTACHMENT_BIT); create.sharingMode=VK_SHARING_MODE_EXCLUSIVE;
                VulkanContext::Require(vkCreateImage(context.GetDevice(),&create,nullptr,&Image),"vkCreateImage(sampled shadow)");
                VkMemoryRequirements requirements{}; vkGetImageMemoryRequirements(context.GetDevice(),Image,&requirements);
                VkMemoryAllocateInfo allocate{VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO}; allocate.allocationSize=requirements.size; allocate.memoryTypeIndex=context.FindMemoryType(requirements.memoryTypeBits,VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT);
                VulkanContext::Require(vkAllocateMemory(context.GetDevice(),&allocate,nullptr,&Memory),"vkAllocateMemory(sampled shadow)"); VulkanContext::Require(vkBindImageMemory(context.GetDevice(),Image,Memory,0),"vkBindImageMemory(sampled shadow)");
                const VkImageAspectFlags aspect=cube ? VK_IMAGE_ASPECT_COLOR_BIT : VK_IMAGE_ASPECT_DEPTH_BIT;
                VkImageViewCreateInfo view{VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO}; view.image=Image; view.viewType=cube ? VK_IMAGE_VIEW_TYPE_CUBE : VK_IMAGE_VIEW_TYPE_2D; view.format=format; view.subresourceRange={aspect,0,1,0,layers};
                VulkanContext::Require(vkCreateImageView(context.GetDevice(),&view,nullptr,&View),"vkCreateImageView(shadow sampling)");
                Faces.resize(layers,VK_NULL_HANDLE);
                for(uint32_t face=0;face<layers;++face) { view.viewType=VK_IMAGE_VIEW_TYPE_2D; view.subresourceRange={aspect,0,1,face,1}; VulkanContext::Require(vkCreateImageView(context.GetDevice(),&view,nullptr,&Faces[face]),"vkCreateImageView(shadow face)"); }
                VkSamplerCreateInfo sampler{VK_STRUCTURE_TYPE_SAMPLER_CREATE_INFO}; sampler.minFilter=sampler.magFilter=VK_FILTER_LINEAR; sampler.mipmapMode=VK_SAMPLER_MIPMAP_MODE_NEAREST; sampler.addressModeU=sampler.addressModeV=sampler.addressModeW=VK_SAMPLER_ADDRESS_MODE_CLAMP_TO_EDGE; sampler.maxLod=0;
                VulkanContext::Require(vkCreateSampler(context.GetDevice(),&sampler,nullptr,&Sampler),"vkCreateSampler(raw shadow depth)");
                const auto commands=context.BeginCommands(); VkImageMemoryBarrier barrier{VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER}; barrier.srcQueueFamilyIndex=barrier.dstQueueFamilyIndex=VK_QUEUE_FAMILY_IGNORED; barrier.image=Image; barrier.subresourceRange={aspect,0,1,0,layers}; barrier.oldLayout=VK_IMAGE_LAYOUT_UNDEFINED; barrier.newLayout=VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL; barrier.dstAccessMask=VK_ACCESS_TRANSFER_WRITE_BIT;
                vkCmdPipelineBarrier(commands,VK_PIPELINE_STAGE_TOP_OF_PIPE_BIT,VK_PIPELINE_STAGE_TRANSFER_BIT,0,0,nullptr,0,nullptr,1,&barrier);
                if(cube) { VkClearColorValue clear{{1,0,0,1}}; vkCmdClearColorImage(commands,Image,VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,&clear,1,&barrier.subresourceRange); }
                else { VkClearDepthStencilValue clear{1,0}; vkCmdClearDepthStencilImage(commands,Image,VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,&clear,1,&barrier.subresourceRange); }
                barrier.oldLayout=VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL; barrier.newLayout=VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL; barrier.srcAccessMask=VK_ACCESS_TRANSFER_WRITE_BIT; barrier.dstAccessMask=VK_ACCESS_SHADER_READ_BIT;
                vkCmdPipelineBarrier(commands,VK_PIPELINE_STAGE_TRANSFER_BIT,VK_PIPELINE_STAGE_VERTEX_SHADER_BIT|VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT,0,0,nullptr,0,nullptr,1,&barrier); context.SubmitAndWait(commands);
            } catch(...) { Release(); throw; }
        }
        /// <summary>Releases views and sampler before their backing image and allocation.</summary>
        void Release() noexcept { if(Sampler) vkDestroySampler(Context.GetDevice(),Sampler,nullptr); for(auto view:Faces) if(view) vkDestroyImageView(Context.GetDevice(),view,nullptr); if(View) vkDestroyImageView(Context.GetDevice(),View,nullptr); if(Image) vkDestroyImage(Context.GetDevice(),Image,nullptr); if(Memory) vkFreeMemory(Context.GetDevice(),Memory,nullptr); }
        /// <summary>Releases resources after every synchronous pass or borrow has ended.</summary>
        ~ShadowTexture() { Release(); }
    };
    /// <summary>Owns temporary attachments until the synchronous submission fence completes.</summary>
    struct Frame {
        /// <summary>Borrows the device.</summary>
        VkDevice Device;
        /// <summary>Owns the temporary framebuffer referencing borrowed color and owned depth.</summary>
        VkFramebuffer Framebuffer=VK_NULL_HANDLE;
        /// <summary>Owns the depth attachment, released after the framebuffer.</summary>
        std::unique_ptr<Depth> DepthTarget;
        /// <summary>Releases frame handles after the context synchronizes successful or failed submission.</summary>
        ~Frame() { if(Framebuffer) vkDestroyFramebuffer(Device,Framebuffer,nullptr); }
    };
    /// <summary>Borrows the context which outlives all resource state.</summary>
    VulkanContext& Context;
    /// <summary>Locates packaged backend shaders independently of authored material binaries.</summary>
    std::string ShaderRoot;
    /// <summary>Disables only GPU selection for explicit differential diagnostics while preserving identical uploaded page geometry.</summary>
    bool GpuCullingEnabled=true;
    /// <summary>Owns one lazily initialized visibility pipeline shared by all static pages.</summary>
    std::unique_ptr<VulkanIndirectCuller> Culler;
    /// <summary>Counts logical command slots in the most recent color pass.</summary>
    size_t LastIndirectCommands=0;
    /// <summary>Counts actual indirect Vulkan draw calls in the most recent color pass.</summary>
    size_t LastIndirectCalls=0;
    /// <summary>Counts actual ordinary indexed API calls used by page fallbacks.</summary>
    size_t LastPageDirectCalls=0;
    /// <summary>Counts GPU visibility dispatches in the most recent color pass.</summary>
    size_t LastIndirectDispatches=0;
    /// <summary>Accumulates payload bytes published by successful page uploads for dirty-upload diagnostics.</summary>
    uint64_t IndirectUploadBytes=0;
    /// <summary>Owns the pass which explicitly discards and clears the previous color contents.</summary>
    VkRenderPass ClearPass=VK_NULL_HANDLE;
    /// <summary>Owns the compatible pass which loads color from the previous transfer-source pass.</summary>
    VkRenderPass LoadPass=VK_NULL_HANDLE;
    /// <summary>Owns clear/load variants of the existing atlas-depth and point radial-shadow render-pass contracts.</summary>
    std::array<VkRenderPass,4> ShadowPasses{};
    /// <summary>Owns uploaded meshes by stable identity.</summary>
    std::unordered_map<size_t,std::shared_ptr<Mesh>> Meshes;
    /// <summary>Owns compiled authored materials by stable identity.</summary>
    std::unordered_map<size_t,std::unique_ptr<Material>> Materials;
    /// <summary>Owns sampleable shadow atlases and six-face radial cube resources by stable identity.</summary>
    std::unordered_map<size_t,std::unique_ptr<ShadowTexture>> Shadows;
    /// <summary>Stores not-yet-submitted draw snapshots in authored order.</summary>
    std::vector<Draw> Draws;
    /// <summary>Retains at most one compatible descriptor arena for each color/depth/radial pass kind.</summary>
    std::array<std::unique_ptr<VulkanPreparedBindings>,3> PreparedBindings;
    /// <summary>Owns separate synchronous hardware occlusion queries for color and shadow passes.</summary>
    VkQueryPool VisibilityQueries=VK_NULL_HANDLE;
    /// <summary>Retains actual last-pass visibility results independently of later UI draws or CPU draw counts.</summary>
    std::array<uint64_t,2> VisibleSamples {};
    /// <summary>Allocates monotonic nonzero mesh identities.</summary>
    size_t NextMesh=1;
    /// <summary>Allocates monotonic nonzero material identities.</summary>
    size_t NextMaterial=1;
    /// <summary>Allocates monotonic shadow texture identities independently of model or material handles.</summary>
    size_t NextShadow=1;

    /// <summary>Initializes compatible render passes with explicit inter-pass color dependencies.</summary>
    explicit State(VulkanContext& context,std::string shaderRoot):Context(context),ShaderRoot(shaderRoot.empty()?HELENGINE_VULKAN_SHADER_ROOT:std::move(shaderRoot)) {
        const char* culling=std::getenv("HELENGINE_VULKAN_GPU_CULLING");
        if(culling) {
            if(std::strcmp(culling,"0")==0)GpuCullingEnabled=false;
            else if(std::strcmp(culling,"1")!=0)throw std::invalid_argument("HELENGINE_VULKAN_GPU_CULLING must be 0 or 1.");
        }
        try {
            ClearPass=CreatePass(false); LoadPass=CreatePass(true); ShadowPasses[0]=CreateShadowPass(PassKind::DepthShadow,true); ShadowPasses[1]=CreateShadowPass(PassKind::DepthShadow,false); ShadowPasses[2]=CreateShadowPass(PassKind::PointShadow,true); ShadowPasses[3]=CreateShadowPass(PassKind::PointShadow,false);
            VkQueryPoolCreateInfo query {VK_STRUCTURE_TYPE_QUERY_POOL_CREATE_INFO}; query.queryType=VK_QUERY_TYPE_OCCLUSION; query.queryCount=2;
            VulkanContext::Require(vkCreateQueryPool(context.GetDevice(),&query,nullptr,&VisibilityQueries),"vkCreateQueryPool(mesh visibility)");
        }
        catch(...) { if(VisibilityQueries) vkDestroyQueryPool(context.GetDevice(),VisibilityQueries,nullptr); for(auto pass:ShadowPasses) if(pass) vkDestroyRenderPass(context.GetDevice(),pass,nullptr); if(LoadPass) vkDestroyRenderPass(context.GetDevice(),LoadPass,nullptr); if(ClearPass) vkDestroyRenderPass(context.GetDevice(),ClearPass,nullptr); throw; }
    }
    /// <summary>Releases owned resources after all synchronous rendering has finished.</summary>
    ~State() {
        vkDeviceWaitIdle(Context.GetDevice());
        for(auto& prepared:PreparedBindings) prepared.reset();
        Materials.clear(); Meshes.clear(); Shadows.clear();
        if(VisibilityQueries) vkDestroyQueryPool(Context.GetDevice(),VisibilityQueries,nullptr);
        for(auto pass:ShadowPasses) if(pass) vkDestroyRenderPass(Context.GetDevice(),pass,nullptr);
        if(LoadPass) vkDestroyRenderPass(Context.GetDevice(),LoadPass,nullptr);
        if(ClearPass) vkDestroyRenderPass(Context.GetDevice(),ClearPass,nullptr);
    }
    /// <summary>Creates one clear/load color pass with independent depth and a color-attachment final layout for UI composition.</summary>
    VkRenderPass CreatePass(bool load) {
        std::array<VkAttachmentDescription,2> attachments{};
        attachments[0].format=VK_FORMAT_R8G8B8A8_UNORM; attachments[0].samples=VK_SAMPLE_COUNT_1_BIT;
        attachments[0].loadOp=load ? VK_ATTACHMENT_LOAD_OP_LOAD : VK_ATTACHMENT_LOAD_OP_CLEAR; attachments[0].storeOp=VK_ATTACHMENT_STORE_OP_STORE;
        attachments[0].stencilLoadOp=VK_ATTACHMENT_LOAD_OP_DONT_CARE; attachments[0].stencilStoreOp=VK_ATTACHMENT_STORE_OP_DONT_CARE;
        attachments[0].initialLayout=load ? VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL : VK_IMAGE_LAYOUT_UNDEFINED; attachments[0].finalLayout=VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
        attachments[1].format=VK_FORMAT_D32_SFLOAT; attachments[1].samples=VK_SAMPLE_COUNT_1_BIT; attachments[1].loadOp=VK_ATTACHMENT_LOAD_OP_CLEAR; attachments[1].storeOp=VK_ATTACHMENT_STORE_OP_DONT_CARE;
        attachments[1].stencilLoadOp=VK_ATTACHMENT_LOAD_OP_DONT_CARE; attachments[1].stencilStoreOp=VK_ATTACHMENT_STORE_OP_DONT_CARE; attachments[1].initialLayout=VK_IMAGE_LAYOUT_UNDEFINED; attachments[1].finalLayout=VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL;
        VkAttachmentReference color{0,VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL},depth{1,VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL};
        VkSubpassDescription subpass{}; subpass.pipelineBindPoint=VK_PIPELINE_BIND_POINT_GRAPHICS; subpass.colorAttachmentCount=1; subpass.pColorAttachments=&color; subpass.pDepthStencilAttachment=&depth;
        std::array<VkSubpassDependency,2> dependencies{};
        dependencies[0].srcSubpass=VK_SUBPASS_EXTERNAL; dependencies[0].dstSubpass=0; dependencies[0].srcStageMask=VK_PIPELINE_STAGE_TRANSFER_BIT|VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;
        dependencies[0].srcAccessMask=VK_ACCESS_TRANSFER_READ_BIT|VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT; dependencies[0].dstStageMask=VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT|VK_PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT;
        dependencies[0].dstAccessMask=VK_ACCESS_COLOR_ATTACHMENT_READ_BIT|VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT|VK_ACCESS_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT;
        dependencies[1].srcSubpass=0; dependencies[1].dstSubpass=VK_SUBPASS_EXTERNAL; dependencies[1].srcStageMask=VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT; dependencies[1].srcAccessMask=VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT;
        dependencies[1].dstStageMask=VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT; dependencies[1].dstAccessMask=VK_ACCESS_COLOR_ATTACHMENT_READ_BIT|VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT;
        VkRenderPassCreateInfo create{VK_STRUCTURE_TYPE_RENDER_PASS_CREATE_INFO}; create.attachmentCount=attachments.size(); create.pAttachments=attachments.data(); create.subpassCount=1; create.pSubpasses=&subpass; create.dependencyCount=dependencies.size(); create.pDependencies=dependencies.data();
        VkRenderPass pass=VK_NULL_HANDLE; VulkanContext::Require(vkCreateRenderPass(Context.GetDevice(),&create,nullptr,&pass),"vkCreateRenderPass(mesh)"); return pass;
    }
    /// <summary>Creates compatible clear/load passes which preserve the engine's D32 atlas or R32 radial cube shader contracts.</summary>
    VkRenderPass CreateShadowPass(PassKind kind,bool clear) {
        const bool point=kind==PassKind::PointShadow; std::array<VkAttachmentDescription,2> attachments{};
        auto& sampled=attachments[0]; sampled.format=point ? VK_FORMAT_R32_SFLOAT : VK_FORMAT_D32_SFLOAT; sampled.samples=VK_SAMPLE_COUNT_1_BIT; sampled.loadOp=clear ? VK_ATTACHMENT_LOAD_OP_CLEAR : VK_ATTACHMENT_LOAD_OP_LOAD; sampled.storeOp=VK_ATTACHMENT_STORE_OP_STORE; sampled.stencilLoadOp=VK_ATTACHMENT_LOAD_OP_DONT_CARE; sampled.stencilStoreOp=VK_ATTACHMENT_STORE_OP_DONT_CARE; sampled.initialLayout=sampled.finalLayout=VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
        if(point) { auto& depth=attachments[1]; depth.format=VK_FORMAT_D32_SFLOAT; depth.samples=VK_SAMPLE_COUNT_1_BIT; depth.loadOp=VK_ATTACHMENT_LOAD_OP_CLEAR; depth.storeOp=VK_ATTACHMENT_STORE_OP_DONT_CARE; depth.stencilLoadOp=VK_ATTACHMENT_LOAD_OP_DONT_CARE; depth.stencilStoreOp=VK_ATTACHMENT_STORE_OP_DONT_CARE; depth.initialLayout=VK_IMAGE_LAYOUT_UNDEFINED; depth.finalLayout=VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL; }
        const VkAttachmentReference color{0,VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL},depth{point ? 1u : 0u,VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL};
        VkSubpassDescription subpass{}; subpass.pipelineBindPoint=VK_PIPELINE_BIND_POINT_GRAPHICS; subpass.colorAttachmentCount=point ? 1 : 0; subpass.pColorAttachments=point ? &color : nullptr; subpass.pDepthStencilAttachment=&depth;
        std::array<VkSubpassDependency,2> dependencies{};
        dependencies[0].srcSubpass=VK_SUBPASS_EXTERNAL; dependencies[0].dstSubpass=0; dependencies[0].srcStageMask=VK_PIPELINE_STAGE_VERTEX_SHADER_BIT|VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT|VK_PIPELINE_STAGE_TRANSFER_BIT|VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT|VK_PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT|VK_PIPELINE_STAGE_LATE_FRAGMENT_TESTS_BIT; dependencies[0].srcAccessMask=VK_ACCESS_SHADER_READ_BIT|VK_ACCESS_TRANSFER_WRITE_BIT|VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT|VK_ACCESS_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT; dependencies[0].dstStageMask=VK_PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT|VK_PIPELINE_STAGE_LATE_FRAGMENT_TESTS_BIT|VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT; dependencies[0].dstAccessMask=VK_ACCESS_DEPTH_STENCIL_ATTACHMENT_READ_BIT|VK_ACCESS_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT|VK_ACCESS_COLOR_ATTACHMENT_READ_BIT|VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT;
        dependencies[1].srcSubpass=0; dependencies[1].dstSubpass=VK_SUBPASS_EXTERNAL; dependencies[1].srcStageMask=VK_PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT|VK_PIPELINE_STAGE_LATE_FRAGMENT_TESTS_BIT|VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT; dependencies[1].srcAccessMask=VK_ACCESS_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT|VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT; dependencies[1].dstStageMask=VK_PIPELINE_STAGE_VERTEX_SHADER_BIT|VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT; dependencies[1].dstAccessMask=VK_ACCESS_SHADER_READ_BIT;
        VkRenderPassCreateInfo create{VK_STRUCTURE_TYPE_RENDER_PASS_CREATE_INFO}; create.attachmentCount=point ? 2 : 1; create.pAttachments=attachments.data(); create.subpassCount=1; create.pSubpasses=&subpass; create.dependencyCount=dependencies.size(); create.pDependencies=dependencies.data();
        VkRenderPass pass=VK_NULL_HANDLE; VulkanContext::Require(vkCreateRenderPass(Context.GetDevice(),&create,nullptr,&pass),"vkCreateRenderPass(sampled shadow)"); return pass;
    }
    /// <summary>Uploads caller-owned bytes using a temporary coherent staging buffer and a vertex/index visibility barrier.</summary>
    std::unique_ptr<GpuBuffer> Upload(const void* data,size_t bytes,VkBufferUsageFlags usage,VkAccessFlags access) {
        GpuBuffer staging(Context,bytes,VK_BUFFER_USAGE_TRANSFER_SRC_BIT,VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT|VK_MEMORY_PROPERTY_HOST_COHERENT_BIT);
        std::memcpy(staging.Map(),data,bytes); staging.Unmap();
        auto target=std::make_unique<GpuBuffer>(Context,bytes,usage|VK_BUFFER_USAGE_TRANSFER_DST_BIT,VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT);
        auto commands=Context.BeginCommands(); VkBufferCopy copy{0,0,bytes}; vkCmdCopyBuffer(commands,staging.Get(),target->Get(),1,&copy);
        VkBufferMemoryBarrier barrier{VK_STRUCTURE_TYPE_BUFFER_MEMORY_BARRIER}; barrier.srcAccessMask=VK_ACCESS_TRANSFER_WRITE_BIT; barrier.dstAccessMask=access;
        barrier.srcQueueFamilyIndex=barrier.dstQueueFamilyIndex=VK_QUEUE_FAMILY_IGNORED; barrier.buffer=target->Get(); barrier.size=VK_WHOLE_SIZE;
        vkCmdPipelineBarrier(commands,VK_PIPELINE_STAGE_TRANSFER_BIT,VK_PIPELINE_STAGE_VERTEX_INPUT_BIT,0,0,nullptr,1,&barrier,0,nullptr);
        Context.SubmitAndWait(commands); return target;
    }
    /// <summary>Creates a shader module from an intact SPIR-V word stream without source or binary rewriting.</summary>
    VkShaderModule CreateModule(std::span<const uint32_t> words) {
        if(words.size()<5 || words[0]!=0x07230203u) throw std::invalid_argument("Authored Vulkan mesh program requires an intact SPIR-V module");
        const uint32_t api=Context.GetApiVersion();
        const uint32_t maximumSpirv=api>=VK_API_VERSION_1_3 ? 0x00010600u : (api>=VK_API_VERSION_1_2 ? 0x00010500u : 0x00010300u);
        if(words[1]<0x00010000u || words[1]>maximumSpirv) throw std::invalid_argument("Authored mesh SPIR-V version exceeds the requested Vulkan context API; engine shaders require Vulkan 1.2 or newer");
        VkShaderModuleCreateInfo create{VK_STRUCTURE_TYPE_SHADER_MODULE_CREATE_INFO}; create.codeSize=words.size_bytes(); create.pCode=words.data();
        VkShaderModule module=VK_NULL_HANDLE; VulkanContext::Require(vkCreateShaderModule(Context.GetDevice(),&create,nullptr,&module),"vkCreateShaderModule(authored mesh)"); return module;
    }
    /// <summary>Normalizes same-slot stage declarations and validates exactly the supported reflected descriptor contract against physical device limits.</summary>
    std::vector<BindingLayout> ValidateLayout(std::span<const BindingLayout> input) const {
        std::vector<BindingLayout> layout;
        const auto& limits=Context.GetProperties().limits;
        for(const auto& binding:input) {
            if(!binding.Stages || (binding.Stages&~(VK_SHADER_STAGE_VERTEX_BIT|VK_SHADER_STAGE_FRAGMENT_BIT)) || binding.Count!=1 || binding.Set!=0) throw std::invalid_argument("Vulkan mesh reflection requires scalar set-zero descriptors in vertex/fragment stages");
            if(binding.Type==VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER) {
                if(!binding.MinimumBytes || binding.MinimumBytes>limits.maxUniformBufferRange) throw std::invalid_argument("Vulkan mesh uniform reflection exceeds the device byte range");
            } else if(binding.Type==VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE || binding.Type==VK_DESCRIPTOR_TYPE_SAMPLER) {
                if(binding.MinimumBytes) throw std::invalid_argument("Vulkan image/sampler reflection cannot require uniform bytes");
                if(binding.Type==VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE && binding.ViewType!=VK_IMAGE_VIEW_TYPE_2D && binding.ViewType!=VK_IMAGE_VIEW_TYPE_CUBE) throw std::invalid_argument("Vulkan mesh sampled texture reflection requires a 2D or cube view");
            } else throw std::invalid_argument("Vulkan mesh reflection supports only uniform buffers, separate textures and separate samplers");
            auto previous=std::find_if(layout.begin(),layout.end(),[&binding](const auto& item){return item.Slot==binding.Slot;});
            if(previous==layout.end()) layout.push_back(binding);
            else {
                if(previous->Type!=binding.Type || (binding.Type==VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE && previous->ViewType!=binding.ViewType)) throw std::invalid_argument("Vulkan shader reflection assigns conflicting resource kinds or dimensions to one unified descriptor slot");
                previous->Stages|=binding.Stages; previous->MinimumBytes=std::max(previous->MinimumBytes,binding.MinimumBytes);
            }
        }
        for(const auto stage:{VK_SHADER_STAGE_VERTEX_BIT,VK_SHADER_STAGE_FRAGMENT_BIT}) {
            uint32_t uniforms=0,images=0,samplers=0;
            for(const auto& binding:layout) if(binding.Stages&stage) { if(binding.Type==VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER) ++uniforms; else if(binding.Type==VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE) ++images; else ++samplers; }
            if(uniforms>limits.maxPerStageDescriptorUniformBuffers || images>limits.maxPerStageDescriptorSampledImages || samplers>limits.maxPerStageDescriptorSamplers || uniforms+images+samplers>limits.maxPerStageResources) throw std::invalid_argument("Vulkan mesh reflected descriptors exceed per-stage device limits");
        }
        uint32_t uniforms=0,images=0,samplers=0;
        for(const auto& binding:layout) { if(binding.Type==VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER) ++uniforms; else if(binding.Type==VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE) ++images; else ++samplers; }
        if(uniforms>limits.maxDescriptorSetUniformBuffers || images>limits.maxDescriptorSetSampledImages || samplers>limits.maxDescriptorSetSamplers) throw std::invalid_argument("Vulkan mesh descriptors exceed set-zero device limits");
        return layout;
    }
    /// <summary>Rejects mixing color, depth-only and radial cube pipelines before any command buffer is recorded.</summary>
    void ValidatePass(PassKind pass) const {
        for(const auto& draw:Draws) if(Materials.at(draw.MaterialId)->Pass!=pass) throw std::invalid_argument("Vulkan queued pipeline does not match the requested color/depth/radial shadow pass");
    }
    /// <summary>Bounds camera and shadow atlas tiles by target geometry and physical-device viewport limits.</summary>
    void ValidateViewport(uint32_t width,uint32_t height,VkRect2D viewport) const {
        const auto& limits=Context.GetProperties().limits;
        if(viewport.offset.x<0 || viewport.offset.y<0 || !viewport.extent.width || !viewport.extent.height || static_cast<uint32_t>(viewport.offset.x)>=width || static_cast<uint32_t>(viewport.offset.y)>=height || viewport.extent.width>width-static_cast<uint32_t>(viewport.offset.x) || viewport.extent.height>height-static_cast<uint32_t>(viewport.offset.y) || viewport.extent.width>limits.maxViewportDimensions[0] || viewport.extent.height>limits.maxViewportDimensions[1]) throw std::invalid_argument("Vulkan pass viewport must remain inside its target and device limits");
    }
    /// <summary>Reuses compatible descriptor sets and uploads only changed bytes after the previous synchronous pass has completed.</summary>
    std::span<const VkDescriptorSet> PrepareBindings(PassKind pass) {
        if(Draws.size()>UINT32_MAX) throw std::length_error("Vulkan mesh frame exceeds descriptor set allocation limits");
        auto& prepared=PreparedBindings.at(static_cast<size_t>(pass));
        if(Draws.empty()){prepared.reset();return {};}
        std::vector<size_t> sizes;
        std::array<uint32_t,3> counts{};std::vector<VkDescriptorSetLayout> layouts;layouts.reserve(Draws.size());
        VulkanFrameTraceScope LayoutTrace("Bindings.PlanLayout");
        LayoutTrace.AddCounters(Draws.size());
        for(const auto& draw:Draws) {
            const auto& material=*Materials.at(draw.MaterialId);layouts.push_back(material.Descriptors);
            for(const auto& binding:material.Bindings) {
                auto& count=counts[binding.Type==VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER ? 0 : (binding.Type==VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE ? 1 : 2)];
                if(count==UINT32_MAX) throw std::length_error("Vulkan mesh frame exceeds descriptor allocation limits");
                ++count;
                if(binding.Type==VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER) {
                    const auto buffer=std::find_if(draw.Buffers.begin(),draw.Buffers.end(),[&binding](const auto& item){return item.Slot==binding.Slot;});
                    if(buffer==draw.Buffers.end() || buffer->Bytes.size()<binding.MinimumBytes) throw std::invalid_argument("Vulkan frame is missing required reflected uniform bytes");
                    sizes.push_back(buffer->Bytes.size());
                }
            }
        }
        LayoutTrace.Finish();
        if(!prepared || !prepared->Matches(layouts,sizes)) {
            VulkanFrameTraceScope RebuildTrace("Bindings.RebuildPrepared");
            auto replacement=std::make_unique<VulkanPreparedBindings>(Context,layouts,sizes,counts);
            RebuildTrace.AddCounters(layouts.size(),replacement->GetUniformBytes());prepared=std::move(replacement);
        }else {
            VulkanFrameTraceScope ReuseTrace("Bindings.ReusePrepared");ReuseTrace.AddCounters(layouts.size(),prepared->GetUniformBytes());
        }
        VulkanFrameTraceScope UploadTrace("Bindings.UploadAndWrite");
        size_t uniformIndex=0,imageIndex=0,updatedUniforms=0,updatedImages=0,uploadedBytes=0;
        for(size_t drawIndex=0;drawIndex<Draws.size();++drawIndex) {
            VulkanFrameTraceScope DrawTrace("Bindings.Draw");
            DrawTrace.SetContext(Draws[drawIndex].TraceSubject,Draws[drawIndex].TraceBatch);
            const auto& draw=Draws[drawIndex];const auto& material=*Materials.at(draw.MaterialId);
            for(const auto& binding:material.Bindings) {
                if(binding.Type==VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER) {
                    const auto& buffer=*std::find_if(draw.Buffers.begin(),draw.Buffers.end(),[&binding](const auto& item){return item.Slot==binding.Slot;});
                    if(prepared->WriteBuffer(uniformIndex++,drawIndex,binding.Slot,buffer.Bytes)) {
                        DrawTrace.AddCounters(1,buffer.Bytes.size());++updatedUniforms;uploadedBytes+=buffer.Bytes.size();
                    }
                }else {
                    const auto& image=std::find_if(draw.Images.begin(),draw.Images.end(),[&binding](const auto& item){return item.Slot==binding.Slot;})->Image;
                    if(prepared->WriteImage(imageIndex++,drawIndex,binding.Slot,binding.Type,image)){DrawTrace.AddCounters(1);++updatedImages;}
                }
            }
        }
        if(std::getenv("HELENGINE_LINUX_FRAME_TIMINGS")) std::cout<<"bindings_updates uniforms="<<updatedUniforms<<" images="<<updatedImages<<" bytes="<<uploadedBytes<<" retained_gpu_bytes="<<prepared->GetUniformBytes()<<std::endl;
        return prepared->GetSets();
    }
    /// <summary>Derives exact conservative bounds from the caller's immutable original upload data and validates every referenced triangle.</summary>
    static std::vector<VulkanIndirectBounds> BuildIndirectBounds(std::span<const Vertex> vertices,std::span<const uint32_t> indices,std::span<const VulkanIndirectRange> ranges) {
    if(ranges.empty() || ranges.size()>UINT32_MAX) throw std::invalid_argument("Indirect page requires nonempty bounded ranges.");
    std::vector<VulkanIndirectBounds> bounds;bounds.reserve(ranges.size());
    for(const auto& range:ranges) {
        if(!range.IndexCount || range.IndexCount%3 || range.FirstIndex>indices.size() || range.IndexCount>indices.size()-range.FirstIndex) throw std::invalid_argument("Indirect range exceeds page triangle indices.");
        VulkanIndirectBounds bound{{INFINITY,INFINITY,INFINITY},range.IndexCount,{-INFINITY,-INFINITY,-INFINITY},range.FirstIndex};
        for(size_t index=range.FirstIndex;index<size_t(range.FirstIndex)+range.IndexCount;++index) {
            if(indices[index]>=vertices.size()) throw std::invalid_argument("Indirect vertex index exceeds page geometry.");
            for(size_t axis=0;axis<3;++axis){const float value=vertices[indices[index]].Position[axis];if(!std::isfinite(value))throw std::invalid_argument("Indirect bounds require finite uploaded positions.");bound.Minimum[axis]=std::min(bound.Minimum[axis],value);bound.Maximum[axis]=std::max(bound.Maximum[axis],value);}
        }
        bounds.push_back(bound);
    }
        return bounds;
    }
    /// <summary>Binds the shared compute pipeline once and synchronizes all page commands with one pre/post barrier pair.</summary>
    void PrepareIndirectCommands(VkCommandBuffer commands) {
        LastIndirectCommands=LastIndirectCalls=LastIndirectDispatches=LastPageDirectCalls=0;
        if(!Culler || std::none_of(Draws.begin(),Draws.end(),[](const auto& draw){return draw.Page && draw.CullPage;}))return;
        VulkanFrameTraceScope passTrace("Mesh.GpuFrustumPass");
        VkMemoryBarrier before{VK_STRUCTURE_TYPE_MEMORY_BARRIER};
        before.srcAccessMask=VK_ACCESS_INDIRECT_COMMAND_READ_BIT|VK_ACCESS_SHADER_WRITE_BIT|VK_ACCESS_TRANSFER_READ_BIT|VK_ACCESS_TRANSFER_WRITE_BIT;
        before.dstAccessMask=VK_ACCESS_SHADER_READ_BIT|VK_ACCESS_SHADER_WRITE_BIT;
        vkCmdPipelineBarrier(commands,VK_PIPELINE_STAGE_DRAW_INDIRECT_BIT|VK_PIPELINE_STAGE_COMPUTE_SHADER_BIT|VK_PIPELINE_STAGE_TRANSFER_BIT,VK_PIPELINE_STAGE_COMPUTE_SHADER_BIT,0,1,&before,0,nullptr,0,nullptr);
        vkCmdBindPipeline(commands,VK_PIPELINE_BIND_POINT_COMPUTE,Culler->GetPipeline());
        for(const auto& draw:Draws) if(draw.Page && draw.CullPage) {
            VulkanFrameTraceScope cullTrace("Mesh.GpuFrustumCull");cullTrace.SetContext(draw.TraceSubject,draw.TraceBatch);
            const auto& page=*Meshes.at(draw.MeshId)->Indirect;page.Cull(commands,draw.CullMatrix);
            ++LastIndirectDispatches;cullTrace.AddCounters(page.GetCommandCount());
        }
        VkMemoryBarrier after{VK_STRUCTURE_TYPE_MEMORY_BARRIER};after.srcAccessMask=VK_ACCESS_SHADER_WRITE_BIT;after.dstAccessMask=VK_ACCESS_INDIRECT_COMMAND_READ_BIT;
        vkCmdPipelineBarrier(commands,VK_PIPELINE_STAGE_COMPUTE_SHADER_BIT,VK_PIPELINE_STAGE_DRAW_INDIRECT_BIT,0,1,&after,0,nullptr,0,nullptr);
        passTrace.AddCounters(LastIndirectDispatches);
    }
    /// <summary>Ends frame-local page borrowing without allocating a separate set of active page handles.</summary>
    void ClearDraws() {
        for(const auto& draw:Draws) if(draw.Page) Meshes.at(draw.MeshId)->PageQueued=false;
        Draws.clear();
    }
    /// <summary>Records already validated indexed draw packets without allocating or modifying their resource ownership.</summary>
    void RecordDraws(VkCommandBuffer commands,std::span<const VkDescriptorSet> sets) {
        for(size_t index=0;index<Draws.size();++index) {
            VulkanFrameTraceScope RecordTrace("Mesh.RecordDraw");
            RecordTrace.AddCounters(1);
            RecordTrace.SetContext(Draws[index].TraceSubject,Draws[index].TraceBatch,"emission");
            const auto& draw=Draws[index]; const auto& mesh=*Meshes.at(draw.MeshId); const auto& material=*Materials.at(draw.MaterialId);
            const auto& geometry=mesh.GeometryOwner?*mesh.GeometryOwner:mesh;
            const auto vertex=geometry.Vertices->Get(); const VkDeviceSize offset=0;
            vkCmdBindPipeline(commands,VK_PIPELINE_BIND_POINT_GRAPHICS,material.Pipeline); vkCmdBindVertexBuffers(commands,0,1,&vertex,&offset); vkCmdBindIndexBuffer(commands,geometry.Indices->Get(),0,VK_INDEX_TYPE_UINT32);
            vkCmdBindDescriptorSets(commands,VK_PIPELINE_BIND_POINT_GRAPHICS,material.Layout,0,1,&sets[index],0,nullptr);
            if(draw.Page) {
                const auto calls=mesh.Indirect->Draw(commands,draw.CullPage);
                LastIndirectCommands+=mesh.Indirect->GetCommandCount();
                if(draw.CullPage && mesh.Indirect->UsesCompute()) LastIndirectCalls+=calls;else LastPageDirectCalls+=calls;
            } else vkCmdDrawIndexed(commands,draw.IndexCount,1,draw.FirstIndex,0,0);
        }
    }
};

VulkanMeshRenderer::VulkanMeshRenderer(VulkanContext& context,std::string shaderRoot):Resources(std::make_unique<State>(context,std::move(shaderRoot))) {}
VulkanMeshRenderer::~VulkanMeshRenderer()=default;

size_t VulkanMeshRenderer::UploadMesh(std::span<const Vertex> vertices,std::span<const uint32_t> indices) {
    if(vertices.empty() || indices.empty() || indices.size()%3 || indices.size()>UINT32_MAX || vertices.size()>UINT32_MAX) throw std::invalid_argument("Vulkan mesh upload requires nonempty indexed triangles within uint32 limits");
    for(const auto& vertex:vertices) {
        for(float value:vertex.Position) if(!std::isfinite(value)) throw std::invalid_argument("Vulkan mesh positions must be finite float32 values");
        for(float value:vertex.Normal) if(!std::isfinite(value)) throw std::invalid_argument("Vulkan mesh normals must be finite float32 values");
        for(float value:vertex.Uv) if(!std::isfinite(value)) throw std::invalid_argument("Vulkan mesh UVs must be finite float32 values");
    }
    for(uint32_t index:indices) if(index>=vertices.size()) throw std::invalid_argument("Vulkan mesh index exceeds its authored vertex range");
    auto mesh=std::make_unique<State::Mesh>();
    MeshBufferUpload::Initialize(mesh->Vertices,mesh->Indices,
        [this,vertices] { return Resources->Upload(vertices.data(),vertices.size_bytes(),VK_BUFFER_USAGE_VERTEX_BUFFER_BIT,VK_ACCESS_VERTEX_ATTRIBUTE_READ_BIT); },
        [this,indices] { return Resources->Upload(indices.data(),indices.size_bytes(),VK_BUFFER_USAGE_INDEX_BUFFER_BIT,VK_ACCESS_INDEX_READ_BIT); });
    mesh->IndexCount=static_cast<uint32_t>(indices.size());mesh->GeometryBytes=vertices.size_bytes()+indices.size_bytes();mesh->VertexCapacity=vertices.size();
    const size_t identity=Resources->NextMesh++; Resources->Meshes.emplace(identity,std::move(mesh)); return identity;
}

size_t VulkanMeshRenderer::UploadIndirectPage(std::span<const Vertex> vertices,std::span<const uint32_t> indices,std::span<const VulkanIndirectRange> ranges) {
    auto bounds=State::BuildIndirectBounds(vertices,indices,ranges);
    auto& state=*Resources;
    if(state.Context.IsGraphicsComputeEnabled() && !state.Culler) state.Culler=std::make_unique<VulkanIndirectCuller>(state.Context,state.ShaderRoot);
    auto indirect=std::make_unique<VulkanIndirectPage>(state.Context,state.Culler.get(),ranges,bounds);
    const auto bytes=vertices.size_bytes()+indices.size_bytes()+indirect->GetBytes();
    if(bytes>UINT64_MAX-state.IndirectUploadBytes)throw std::overflow_error("Indirect upload byte counter exhausted.");
    const auto identity=UploadMesh(vertices,indices);
    state.Meshes.at(identity)->Indirect=std::move(indirect);state.IndirectUploadBytes+=bytes;
    return identity;
}

size_t VulkanMeshRenderer::UploadIndirectPageForMesh(size_t source,std::span<const Vertex> vertices,std::span<const uint32_t> indices,std::span<const VulkanIndirectRange> ranges) {
    auto& state=*Resources;const auto found=state.Meshes.find(source);
    if(found==state.Meshes.end() || found->second->Dynamic || found->second->Indirect || found->second->GeometryOwner)throw std::invalid_argument("Shared indirect pages require an original immutable mesh.");
    if(vertices.size()!=found->second->VertexCapacity || indices.size()!=found->second->IndexCount)throw std::invalid_argument("Indirect source spans must match the original uploaded geometry counts.");
    auto bounds=State::BuildIndirectBounds(vertices,indices,ranges);
    if(state.Context.IsGraphicsComputeEnabled() && !state.Culler)state.Culler=std::make_unique<VulkanIndirectCuller>(state.Context,state.ShaderRoot);
    auto mesh=std::make_shared<State::Mesh>();mesh->GeometryOwner=found->second;mesh->IndexCount=found->second->IndexCount;
    mesh->Indirect=std::make_unique<VulkanIndirectPage>(state.Context,state.Culler.get(),ranges,bounds);
    const auto bytes=mesh->Indirect->GetBytes();if(bytes>UINT64_MAX-state.IndirectUploadBytes)throw std::overflow_error("Indirect upload byte counter exhausted.");
    const auto identity=state.NextMesh++;state.Meshes.emplace(identity,std::move(mesh));state.IndirectUploadBytes+=bytes;return identity;
}

void VulkanMeshRenderer::QueueIndirectPage(size_t page,size_t material,std::span<const BufferBinding> buffers,std::span<const ImageBinding> images,std::span<const float,16> matrix,std::span<const int> subjects) {
    auto& state=*Resources;const auto found=state.Meshes.find(page);
    if(found==state.Meshes.end() || !found->second->Indirect) throw std::invalid_argument("Indirect draw requires a live static page.");
    if(!subjects.empty() && subjects.size()!=found->second->Indirect->GetCommandCount()) throw std::invalid_argument("Indirect trace subjects must match original ranges.");
    if(state.Materials.at(material)->Pass!=PassKind::Color) throw std::invalid_argument("Indirect page selection currently supports color passes only.");
    const bool repeated=found->second->PageQueued;
    int traceBatch=-1;
    if(!subjects.empty() && VulkanFrameTrace::IsRecording()) traceBatch=VulkanFrameTrace::RegisterBatch(std::vector<int>(subjects.begin(),subjects.end()));
    QueueDraw(page,material,buffers,images);
    auto& draw=state.Draws.back();draw.Page=true;draw.CullPage=state.GpuCullingEnabled&&!repeated;found->second->PageQueued=true;
    std::copy(matrix.begin(),matrix.end(),draw.CullMatrix.begin());
    if(traceBatch!=-1){draw.TraceSubject=-1;draw.TraceBatch=traceBatch;}
}
bool VulkanMeshRenderer::IsIndirectPage(size_t mesh) const {const auto found=Resources->Meshes.find(mesh);return found!=Resources->Meshes.end() && found->second->Indirect!=nullptr;}
size_t VulkanMeshRenderer::GetIndirectPageBytes(size_t mesh) const {const auto& value=*Resources->Meshes.at(mesh);if(!value.Indirect)throw std::invalid_argument("Mesh is not an indirect page.");return value.GeometryBytes+value.Indirect->GetBytes();}
uint64_t VulkanMeshRenderer::GetIndirectUploadBytes() const {return Resources->IndirectUploadBytes;}
size_t VulkanMeshRenderer::ReadIndirectVisibleCommandCount(size_t mesh) const {
    if(HasPendingDraws())throw std::logic_error("Indirect diagnostic readback requires no pending draws.");
    const auto& value=*Resources->Meshes.at(mesh);if(!value.Indirect)throw std::invalid_argument("Mesh is not an indirect page.");
    return value.Indirect->ReadVisibleCommandCount();
}
size_t VulkanMeshRenderer::GetLastPageDirectCallCount() const {return Resources->LastPageDirectCalls;}
size_t VulkanMeshRenderer::GetLastIndirectCommandCount() const {return Resources->LastIndirectCommands;}
size_t VulkanMeshRenderer::GetLastIndirectCallCount() const {return Resources->LastIndirectCalls;}
size_t VulkanMeshRenderer::GetLastIndirectDispatchCount() const {return Resources->LastIndirectDispatches;}

size_t VulkanMeshRenderer::UpdateDynamicMesh(size_t identity,std::span<const Vertex> vertices,std::span<const uint32_t> indices) {
    if(vertices.empty() || indices.empty() || indices.size()%3 || indices.size()>UINT32_MAX || vertices.size()>UINT32_MAX) throw std::invalid_argument("Vulkan transient mesh requires nonempty uint32 indexed triangles");
    for(const auto& vertex:vertices) {
        for(float value:vertex.Position) if(!std::isfinite(value)) throw std::invalid_argument("Vulkan transient position is not finite");
        for(float value:vertex.Normal) if(!std::isfinite(value)) throw std::invalid_argument("Vulkan transient normal is not finite");
        for(float value:vertex.Uv) if(!std::isfinite(value)) throw std::invalid_argument("Vulkan transient UV is not finite");
    }
    for(uint32_t index:indices) if(index>=vertices.size()) throw std::invalid_argument("Vulkan transient index exceeds vertex range");
    auto& state=*Resources;
    if(identity!=SIZE_MAX && std::any_of(state.Draws.begin(),state.Draws.end(),[identity](const auto& draw) { return draw.MeshId==identity; })) throw std::logic_error("Transient mesh is still borrowed by pending GPU draws");
    std::unique_ptr<State::Mesh> fresh; State::Mesh* mesh;
    if(identity==SIZE_MAX) {
        fresh=std::make_unique<State::Mesh>(); fresh->Dynamic=true; fresh->VertexCapacity=vertices.size(); fresh->IndexCapacity=indices.size();
        MeshBufferUpload::Initialize(fresh->Vertices,fresh->Indices,
            [&state,vertices] { return std::make_unique<GpuBuffer>(state.Context,vertices.size_bytes(),VK_BUFFER_USAGE_VERTEX_BUFFER_BIT,VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT|VK_MEMORY_PROPERTY_HOST_COHERENT_BIT); },
            [&state,indices] { return std::make_unique<GpuBuffer>(state.Context,indices.size_bytes(),VK_BUFFER_USAGE_INDEX_BUFFER_BIT,VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT|VK_MEMORY_PROPERTY_HOST_COHERENT_BIT); });
        mesh=fresh.get();
    } else {
        const auto found=state.Meshes.find(identity); if(found==state.Meshes.end() || !found->second->Dynamic) throw std::invalid_argument("Transient update requires a live transient mesh owner");
        mesh=found->second.get(); if(vertices.size()>mesh->VertexCapacity || indices.size()>mesh->IndexCapacity) throw std::length_error("Transient geometry exceeds retained capacity");
    }
    void* vertexMapping=mesh->Vertices->Map(); std::memcpy(vertexMapping,vertices.data(),vertices.size_bytes()); mesh->Vertices->Unmap();
    void* indexMapping=mesh->Indices->Map(); std::memcpy(indexMapping,indices.data(),indices.size_bytes()); mesh->Indices->Unmap(); mesh->IndexCount=static_cast<uint32_t>(indices.size());
    if(fresh) { identity=state.NextMesh++; state.Meshes.emplace(identity,std::move(fresh)); }
    return identity;
}

size_t VulkanMeshRenderer::GetQueuedDrawCount() const { return Resources->Draws.size(); }

size_t VulkanMeshRenderer::UploadMaterial(std::span<const uint32_t> vertex,std::string vertexEntry,std::span<const uint32_t> fragment,std::string fragmentEntry,std::span<const BindingLayout> bindings) {
    return UploadMaterial(vertex,std::move(vertexEntry),fragment,std::move(fragmentEntry),bindings,MaterialState{});
}

size_t VulkanMeshRenderer::UploadMaterial(std::span<const uint32_t> vertex,std::string vertexEntry,std::span<const uint32_t> fragment,std::string fragmentEntry,std::span<const BindingLayout> bindings,MaterialState renderState) {
    return UploadMaterial(vertex,std::move(vertexEntry),fragment,std::move(fragmentEntry),bindings,renderState,PassKind::Color);
}

size_t VulkanMeshRenderer::UploadMaterial(std::span<const uint32_t> vertex,std::string vertexEntry,std::span<const uint32_t> fragment,std::string fragmentEntry,std::span<const BindingLayout> bindings,MaterialState renderState,PassKind pass) {
    if(vertexEntry.empty() || fragmentEntry.empty() || vertexEntry.find('\0')!=std::string::npos || fragmentEntry.find('\0')!=std::string::npos) throw std::invalid_argument("Vulkan mesh shader programs require explicit intact authored entry points");
    if(renderState.BlendMode>1 || renderState.CullMode>2) throw std::invalid_argument("Vulkan authored material has an unsupported engine blend or cull enum value");
    if(pass!=PassKind::Color && pass!=PassKind::DepthShadow && pass!=PassKind::PointShadow) throw std::invalid_argument("Vulkan material has an unknown attachment pass kind");
    auto& state=*Resources; const auto device=state.Context.GetDevice(); auto material=std::make_unique<State::Material>(device); material->Bindings=state.ValidateLayout(bindings); material->Pass=pass;
    std::vector<VkDescriptorSetLayoutBinding> nativeBindings;
    for(const auto& binding:material->Bindings) nativeBindings.push_back({binding.Slot,binding.Type,1,binding.Stages,nullptr});
    VkDescriptorSetLayoutCreateInfo descriptors{VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO}; descriptors.bindingCount=static_cast<uint32_t>(nativeBindings.size()); descriptors.pBindings=nativeBindings.data();
    VulkanContext::Require(vkCreateDescriptorSetLayout(device,&descriptors,nullptr,&material->Descriptors),"vkCreateDescriptorSetLayout(authored mesh reflection)");
    VkPipelineLayoutCreateInfo pipelineLayout{VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO}; pipelineLayout.setLayoutCount=1; pipelineLayout.pSetLayouts=&material->Descriptors;
    VulkanContext::Require(vkCreatePipelineLayout(device,&pipelineLayout,nullptr,&material->Layout),"vkCreatePipelineLayout(authored mesh reflection)");
    State::Modules modules{device}; modules.Vertex=state.CreateModule(vertex); modules.Fragment=state.CreateModule(fragment);
    std::array<VkPipelineShaderStageCreateInfo,2> stages{};
    stages[0].sType=stages[1].sType=VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO; stages[0].stage=VK_SHADER_STAGE_VERTEX_BIT; stages[0].module=modules.Vertex; stages[0].pName=vertexEntry.c_str(); stages[1].stage=VK_SHADER_STAGE_FRAGMENT_BIT; stages[1].module=modules.Fragment; stages[1].pName=fragmentEntry.c_str();
    VkVertexInputBindingDescription vertexBinding{0,sizeof(Vertex),VK_VERTEX_INPUT_RATE_VERTEX};
    const std::array<VkVertexInputAttributeDescription,3> attributes={{{0,0,VK_FORMAT_R32G32B32_SFLOAT,offsetof(Vertex,Position)},{1,0,VK_FORMAT_R32G32B32_SFLOAT,offsetof(Vertex,Normal)},{2,0,VK_FORMAT_R32G32_SFLOAT,offsetof(Vertex,Uv)}}};
    // Builtin shadow vertex programs consume POSITION only; their fixed mesh stride remains unchanged.
    VkPipelineVertexInputStateCreateInfo input{VK_STRUCTURE_TYPE_PIPELINE_VERTEX_INPUT_STATE_CREATE_INFO}; input.vertexBindingDescriptionCount=1; input.pVertexBindingDescriptions=&vertexBinding; input.vertexAttributeDescriptionCount=pass==PassKind::Color ? static_cast<uint32_t>(attributes.size()) : 1; input.pVertexAttributeDescriptions=attributes.data();
    VkPipelineInputAssemblyStateCreateInfo assembly{VK_STRUCTURE_TYPE_PIPELINE_INPUT_ASSEMBLY_STATE_CREATE_INFO}; assembly.topology=VK_PRIMITIVE_TOPOLOGY_TRIANGLE_LIST;
    VkPipelineViewportStateCreateInfo viewport{VK_STRUCTURE_TYPE_PIPELINE_VIEWPORT_STATE_CREATE_INFO}; viewport.viewportCount=1; viewport.scissorCount=1;
    VkPipelineRasterizationStateCreateInfo raster{VK_STRUCTURE_TYPE_PIPELINE_RASTERIZATION_STATE_CREATE_INFO}; raster.polygonMode=VK_POLYGON_MODE_FILL; raster.cullMode=renderState.CullMode==0 ? VK_CULL_MODE_NONE : (renderState.CullMode==1 ? VK_CULL_MODE_FRONT_BIT : VK_CULL_MODE_BACK_BIT); raster.frontFace=VK_FRONT_FACE_COUNTER_CLOCKWISE; raster.lineWidth=1;
    VkPipelineMultisampleStateCreateInfo multisample{VK_STRUCTURE_TYPE_PIPELINE_MULTISAMPLE_STATE_CREATE_INFO}; multisample.rasterizationSamples=VK_SAMPLE_COUNT_1_BIT;
    VkPipelineDepthStencilStateCreateInfo depth{VK_STRUCTURE_TYPE_PIPELINE_DEPTH_STENCIL_STATE_CREATE_INFO}; depth.depthTestEnable=renderState.DepthTestEnabled; depth.depthWriteEnable=renderState.DepthWriteEnabled; depth.depthCompareOp=VK_COMPARE_OP_LESS;
    VkPipelineColorBlendAttachmentState color{}; color.colorWriteMask=VK_COLOR_COMPONENT_R_BIT|VK_COLOR_COMPONENT_G_BIT|VK_COLOR_COMPONENT_B_BIT|VK_COLOR_COMPONENT_A_BIT;
    color.blendEnable=renderState.BlendMode==1; color.srcColorBlendFactor=VK_BLEND_FACTOR_SRC_ALPHA; color.dstColorBlendFactor=VK_BLEND_FACTOR_ONE_MINUS_SRC_ALPHA; color.colorBlendOp=VK_BLEND_OP_ADD;
    color.srcAlphaBlendFactor=VK_BLEND_FACTOR_ONE; color.dstAlphaBlendFactor=VK_BLEND_FACTOR_ZERO; color.alphaBlendOp=VK_BLEND_OP_ADD;
    VkPipelineColorBlendStateCreateInfo blend{VK_STRUCTURE_TYPE_PIPELINE_COLOR_BLEND_STATE_CREATE_INFO}; blend.attachmentCount=pass==PassKind::DepthShadow ? 0 : 1; blend.pAttachments=pass==PassKind::DepthShadow ? nullptr : &color;
    const VkDynamicState dynamicStates[]={VK_DYNAMIC_STATE_VIEWPORT,VK_DYNAMIC_STATE_SCISSOR};
    VkPipelineDynamicStateCreateInfo dynamic{VK_STRUCTURE_TYPE_PIPELINE_DYNAMIC_STATE_CREATE_INFO}; dynamic.dynamicStateCount=2; dynamic.pDynamicStates=dynamicStates;
    VkGraphicsPipelineCreateInfo create{VK_STRUCTURE_TYPE_GRAPHICS_PIPELINE_CREATE_INFO}; create.stageCount=stages.size(); create.pStages=stages.data(); create.pVertexInputState=&input; create.pInputAssemblyState=&assembly; create.pViewportState=&viewport; create.pRasterizationState=&raster; create.pMultisampleState=&multisample; create.pDepthStencilState=&depth; create.pColorBlendState=&blend; create.pDynamicState=&dynamic; create.layout=material->Layout; create.renderPass=pass==PassKind::Color ? state.ClearPass : state.ShadowPasses[pass==PassKind::DepthShadow ? 0 : 2];
    VulkanContext::Require(vkCreateGraphicsPipelines(device,VK_NULL_HANDLE,1,&create,nullptr,&material->Pipeline),"vkCreateGraphicsPipelines(authored mesh; check engine reflection, entry points and SPIR-V interface)");
    const size_t identity=state.NextMaterial++; state.Materials.emplace(identity,std::move(material)); return identity;
}

void VulkanMeshRenderer::QueueDraw(size_t meshId,size_t materialId,std::span<const BufferBinding> buffers,std::span<const ImageBinding> images,uint32_t firstIndex,uint32_t indexCount) {
    VulkanFrameTraceScope QueueTrace("Mesh.ValidateAndCopyDraw");
    QueueTrace.AddCounters(1);
    auto& state=*Resources; auto mesh=state.Meshes.find(meshId); auto material=state.Materials.find(materialId);
    if(mesh==state.Meshes.end() || material==state.Materials.end()) throw std::invalid_argument("Vulkan indexed draw requires live mesh and material owners");
    if(!indexCount && firstIndex<mesh->second->IndexCount) indexCount=mesh->second->IndexCount-firstIndex;
    if(!indexCount || indexCount%3 || firstIndex%3 || firstIndex>mesh->second->IndexCount || indexCount>mesh->second->IndexCount-firstIndex) throw std::invalid_argument("Vulkan submesh draw exceeds its indexed triangle range");
    const auto& layout=material->second->Bindings;
    for(size_t index=0;index<buffers.size();++index) {
        const auto& buffer=buffers[index]; auto required=std::find_if(layout.begin(),layout.end(),[&buffer](const auto& item){return item.Slot==buffer.Slot;});
        if(required==layout.end() || required->Type!=VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER || buffer.Bytes.size()<required->MinimumBytes || buffer.Bytes.size()>state.Context.GetProperties().limits.maxUniformBufferRange) throw std::invalid_argument("Vulkan draw has unknown, short or oversized reflected uniform bytes");
        for(size_t previous=0;previous<index;++previous) if(buffers[previous].Slot==buffer.Slot) throw std::invalid_argument("Vulkan draw repeats a uniform binding slot");
    }
    for(size_t index=0;index<images.size();++index) {
        const auto& image=images[index]; auto required=std::find_if(layout.begin(),layout.end(),[&image](const auto& item){return item.Slot==image.Slot;});
        if(required==layout.end() || required->Type==VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER) throw std::invalid_argument("Vulkan draw has an unknown reflected image/sampler slot");
        if(required->Type==VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE && (!image.Image.imageView || image.Image.imageLayout!=VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL)) throw std::invalid_argument("Vulkan draw requires a live sampled image in shader-read layout");
        if(required->Type==VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE && image.ViewType!=required->ViewType) throw std::invalid_argument("Vulkan sampled texture view dimension contradicts the engine shader reflection");
        if(required->Type==VK_DESCRIPTOR_TYPE_SAMPLER && !image.Image.sampler) throw std::invalid_argument("Vulkan draw requires a live reflected sampler");
        for(size_t previous=0;previous<index;++previous) if(images[previous].Slot==image.Slot) throw std::invalid_argument("Vulkan draw repeats an image/sampler binding slot");
    }
    for(const auto& required:layout) {
        const bool present=required.Type==VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER ? std::any_of(buffers.begin(),buffers.end(),[&required](const auto& item){return item.Slot==required.Slot;}) : std::any_of(images.begin(),images.end(),[&required](const auto& item){return item.Slot==required.Slot;});
        if(!present) throw std::invalid_argument("Vulkan draw is missing an engine-reflected required descriptor");
    }
    State::Draw draw{VulkanFrameTrace::CurrentSubject(),VulkanFrameTrace::CurrentBatch(),meshId,materialId,firstIndex,indexCount,{},{}}; draw.Buffers.assign(buffers.begin(),buffers.end()); draw.Images.assign(images.begin(),images.end()); state.Draws.push_back(std::move(draw));
}

void VulkanMeshRenderer::RenderFrame(VkImageView target,uint32_t width,uint32_t height,std::array<float,4> clear,bool loadExistingColor) {
    RenderFrame(target,width,height,clear,loadExistingColor,VkRect2D{{0,0},{width,height}});
}

void VulkanMeshRenderer::RenderFrame(VkImageView target,uint32_t width,uint32_t height,std::array<float,4> clear,bool loadExistingColor,VkRect2D cameraViewport) {
    auto& state=*Resources; const auto device=state.Context.GetDevice(); const auto& limits=state.Context.GetProperties().limits;
    if(!target || !width || !height || width>limits.maxFramebufferWidth || height>limits.maxFramebufferHeight || state.Draws.size()>UINT32_MAX) throw std::invalid_argument("Vulkan mesh pass requires a valid borrowed RGBA8 target within device framebuffer limits");
    state.ValidateViewport(width,height,cameraViewport); state.ValidatePass(PassKind::Color);
    for(float value:clear) if(!std::isfinite(value)) throw std::invalid_argument("Vulkan mesh clear color must be finite");
    VulkanFrameTraceScope setupTrace("Mesh.Setup");
    const auto frameStart=std::chrono::steady_clock::now();
    State::Frame frame{device}; frame.DepthTarget=std::make_unique<State::Depth>(state.Context,width,height);
    const std::array<VkImageView,2> attachments={target,frame.DepthTarget->View};
    VkFramebufferCreateInfo framebuffer{VK_STRUCTURE_TYPE_FRAMEBUFFER_CREATE_INFO}; framebuffer.renderPass=state.ClearPass; framebuffer.attachmentCount=attachments.size(); framebuffer.pAttachments=attachments.data(); framebuffer.width=width; framebuffer.height=height; framebuffer.layers=1;
    VulkanContext::Require(vkCreateFramebuffer(device,&framebuffer,nullptr,&frame.Framebuffer),"vkCreateFramebuffer(mesh borrowed target)");
    setupTrace.Finish();
    VulkanFrameTraceScope bindingsTrace("Mesh.Bindings");
    const auto bindingsStart=std::chrono::steady_clock::now();
    const auto sets=state.PrepareBindings(PassKind::Color);
    bindingsTrace.Finish();
    VulkanFrameTraceScope recordTrace("Mesh.Record");
    const auto recordStart=std::chrono::steady_clock::now();
    const auto commands=state.Context.BeginCommands(); std::array<VkClearValue,2> values{}; std::copy(clear.begin(),clear.end(),values[0].color.float32); values[1].depthStencil={1,0};
    VkRenderPassBeginInfo begin{VK_STRUCTURE_TYPE_RENDER_PASS_BEGIN_INFO}; begin.renderPass=loadExistingColor ? state.LoadPass : state.ClearPass; begin.framebuffer=frame.Framebuffer; begin.renderArea.extent={width,height}; begin.clearValueCount=values.size(); begin.pClearValues=values.data();
    state.PrepareIndirectCommands(commands);
    vkCmdResetQueryPool(commands,state.VisibilityQueries,0,1);
    vkCmdBeginRenderPass(commands,&begin,VK_SUBPASS_CONTENTS_INLINE);
    vkCmdBeginQuery(commands,state.VisibilityQueries,0,0);
    // Engine-authored camera matrices retain clip-space +Y upward; the 2D pass already consumes physical top-left pixel positions.
    const VkViewport viewport{static_cast<float>(cameraViewport.offset.x),static_cast<float>(cameraViewport.offset.y)+cameraViewport.extent.height,static_cast<float>(cameraViewport.extent.width),-static_cast<float>(cameraViewport.extent.height),0,1}; vkCmdSetViewport(commands,0,1,&viewport); vkCmdSetScissor(commands,0,1,&cameraViewport);
    state.RecordDraws(commands,sets);
    vkCmdEndQuery(commands,state.VisibilityQueries,0);
    vkCmdEndRenderPass(commands);
    recordTrace.Finish();
    VulkanFrameTraceScope submitTrace("Mesh.SubmitAndWait");
    const auto submitStart=std::chrono::steady_clock::now();
    state.Context.SubmitAndWait(commands);
    const auto submitEnd=std::chrono::steady_clock::now();
    submitTrace.Finish();
    state.ClearDraws();
    if(std::getenv("HELENGINE_LINUX_FRAME_TIMINGS")) { std::cout << "mesh_ms setup=" << std::chrono::duration<double,std::milli>(bindingsStart-frameStart).count() << " bindings=" << std::chrono::duration<double,std::milli>(recordStart-bindingsStart).count() << " record=" << std::chrono::duration<double,std::milli>(submitStart-recordStart).count() << " submit_wait=" << std::chrono::duration<double,std::milli>(submitEnd-submitStart).count() << std::endl; }
    VulkanContext::Require(vkGetQueryPoolResults(device,state.VisibilityQueries,0,1,sizeof(uint64_t),&state.VisibleSamples[0],sizeof(uint64_t),VK_QUERY_RESULT_64_BIT|VK_QUERY_RESULT_WAIT_BIT),"vkGetQueryPoolResults(mesh visibility)");
}

size_t VulkanMeshRenderer::CreateDepthShadow(uint32_t width,uint32_t height) {
    Resources->Context.InvalidateSampledResources();
    auto resource=std::make_unique<State::ShadowTexture>(Resources->Context,width,height,PassKind::DepthShadow); const size_t identity=Resources->NextShadow++; Resources->Shadows.emplace(identity,std::move(resource)); return identity;
}
size_t VulkanMeshRenderer::CreatePointShadow(uint32_t resolution) {
    Resources->Context.InvalidateSampledResources();
    auto resource=std::make_unique<State::ShadowTexture>(Resources->Context,resolution,resolution,PassKind::PointShadow); const size_t identity=Resources->NextShadow++; Resources->Shadows.emplace(identity,std::move(resource)); return identity;
}
VkDescriptorImageInfo VulkanMeshRenderer::GetShadowDescriptor(size_t texture) const {
    const auto found=Resources->Shadows.find(texture); if(found==Resources->Shadows.end()) throw std::invalid_argument("Vulkan shadow descriptor requires a live owned texture");
    return {found->second->Sampler,found->second->View,VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL};
}
VkImageViewType VulkanMeshRenderer::GetShadowViewType(size_t texture) const {
    const auto found=Resources->Shadows.find(texture); if(found==Resources->Shadows.end()) throw std::invalid_argument("Vulkan shadow dimension requires a live owned texture");
    return found->second->Pass==PassKind::PointShadow ? VK_IMAGE_VIEW_TYPE_CUBE : VK_IMAGE_VIEW_TYPE_2D;
}
void VulkanMeshRenderer::RenderShadowPass(size_t texture,uint32_t face,bool clear) {
    const auto found=Resources->Shadows.find(texture); if(found==Resources->Shadows.end()) throw std::invalid_argument("Vulkan caster pass requires a live owned shadow target");
    RenderShadowPass(texture,face,clear,VkRect2D{{0,0},{found->second->Width,found->second->Height}});
}
void VulkanMeshRenderer::RenderShadowPass(size_t texture,uint32_t face,bool clear,VkRect2D viewport) {
    auto& state=*Resources; const auto found=state.Shadows.find(texture); if(found==state.Shadows.end()) throw std::invalid_argument("Vulkan caster pass requires a live owned shadow target");
    const auto& target=*found->second; const auto device=state.Context.GetDevice(); const bool point=target.Pass==PassKind::PointShadow;
    if(face>=target.Faces.size() || state.Draws.size()>UINT32_MAX) throw std::invalid_argument("Vulkan shadow caster face or draw count is out of range");
    state.ValidateViewport(target.Width,target.Height,viewport); state.ValidatePass(target.Pass);
    if(ReferencesImage(target.View)) throw std::invalid_argument("Vulkan shadow pass cannot sample its own render target");
    State::Frame frame{device}; if(point) frame.DepthTarget=std::make_unique<State::Depth>(state.Context,target.Width,target.Height);
    const std::array<VkImageView,2> attachments={target.Faces[face],point ? frame.DepthTarget->View : VK_NULL_HANDLE};
    const auto pass=state.ShadowPasses[(point ? 2 : 0)+(clear ? 0 : 1)];
    VkFramebufferCreateInfo framebuffer{VK_STRUCTURE_TYPE_FRAMEBUFFER_CREATE_INFO}; framebuffer.renderPass=pass; framebuffer.attachmentCount=point ? 2 : 1; framebuffer.pAttachments=attachments.data(); framebuffer.width=target.Width; framebuffer.height=target.Height; framebuffer.layers=1;
    VulkanContext::Require(vkCreateFramebuffer(device,&framebuffer,nullptr,&frame.Framebuffer),"vkCreateFramebuffer(shadow face)");
    const auto sets=state.PrepareBindings(target.Pass); std::array<VkClearValue,2> values{};
    if(point) { values[0].color.float32[0]=1; values[0].color.float32[3]=1; values[1].depthStencil={1,0}; } else values[0].depthStencil={1,0};
    const auto commands=state.Context.BeginCommands(); VkRenderPassBeginInfo begin{VK_STRUCTURE_TYPE_RENDER_PASS_BEGIN_INFO}; begin.renderPass=pass; begin.framebuffer=frame.Framebuffer; begin.renderArea.extent={target.Width,target.Height}; begin.clearValueCount=point ? 2 : 1; begin.pClearValues=values.data();
    vkCmdResetQueryPool(commands,state.VisibilityQueries,1,1);
    vkCmdBeginRenderPass(commands,&begin,VK_SUBPASS_CONTENTS_INLINE);
    vkCmdBeginQuery(commands,state.VisibilityQueries,1,0);
    // Existing shadow shaders map +clip Y to the top of both atlas UVs and cube faces. Vulkan 1.1 negative-height viewports preserve that contract without rewriting matrices or shaders.
    const VkViewport nativeViewport{static_cast<float>(viewport.offset.x),static_cast<float>(viewport.offset.y)+viewport.extent.height,static_cast<float>(viewport.extent.width),-static_cast<float>(viewport.extent.height),0,1}; vkCmdSetViewport(commands,0,1,&nativeViewport); vkCmdSetScissor(commands,0,1,&viewport);
    state.RecordDraws(commands,sets); vkCmdEndQuery(commands,state.VisibilityQueries,1); vkCmdEndRenderPass(commands); state.Context.SubmitAndWait(commands); state.ClearDraws();
    VulkanContext::Require(vkGetQueryPoolResults(device,state.VisibilityQueries,1,1,sizeof(uint64_t),&state.VisibleSamples[1],sizeof(uint64_t),VK_QUERY_RESULT_64_BIT|VK_QUERY_RESULT_WAIT_BIT),"vkGetQueryPoolResults(shadow visibility)");
}
void VulkanMeshRenderer::ReleaseShadowTexture(size_t texture) {
    const auto found=Resources->Shadows.find(texture); if(found==Resources->Shadows.end()) throw std::invalid_argument("Vulkan shadow release requires a live owned texture");
    const auto view=found->second->View; const auto sampler=found->second->Sampler;
    if(std::any_of(Resources->Draws.begin(),Resources->Draws.end(),[view,sampler](const auto& draw){return std::any_of(draw.Images.begin(),draw.Images.end(),[view,sampler](const auto& image){return image.Image.imageView==view || image.Image.sampler==sampler;});})) throw std::logic_error("Vulkan shadow image or sampler remains borrowed by queued draws");
    Resources->Context.InvalidateSampledResources();
    Resources->Shadows.erase(found);
}

void VulkanMeshRenderer::ReleaseMesh(size_t mesh) {
    if(std::any_of(Resources->Draws.begin(),Resources->Draws.end(),[mesh](const auto& draw){return draw.MeshId==mesh;})) throw std::logic_error("Vulkan mesh is still referenced by queued draws");
    if(!Resources->Meshes.erase(mesh)) throw std::invalid_argument("Vulkan mesh release references an absent owner");
}
void VulkanMeshRenderer::ReleaseMaterial(size_t material) {
    if(std::any_of(Resources->Draws.begin(),Resources->Draws.end(),[material](const auto& draw){return draw.MaterialId==material;})) throw std::logic_error("Vulkan material is still referenced by queued draws");
    if(!Resources->Materials.contains(material)) throw std::invalid_argument("Vulkan material release references an absent owner");
    // Layout handles cannot outlive their material or be confused with a recycled driver handle.
    for(auto& prepared:Resources->PreparedBindings) prepared.reset();
    Resources->Materials.erase(material);
}
bool VulkanMeshRenderer::ReferencesImage(VkImageView image) const {
    return image && std::any_of(Resources->Draws.begin(),Resources->Draws.end(),[image](const auto& draw){return std::any_of(draw.Images.begin(),draw.Images.end(),[image](const auto& binding){return binding.Image.imageView==image;});});
}
bool VulkanMeshRenderer::HasPendingDraws() const { return !Resources->Draws.empty(); }
uint64_t VulkanMeshRenderer::GetLastVisibleSamples() const { return Resources->VisibleSamples[0]; }
uint64_t VulkanMeshRenderer::GetLastShadowSamples() const { return Resources->VisibleSamples[1]; }
void VulkanMeshRenderer::DiscardFrame() { Resources->ClearDraws(); }
}
