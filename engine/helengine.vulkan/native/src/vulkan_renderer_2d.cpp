#include "vulkan_frame_trace_scope.hpp"
#include "vulkan_renderer_2d.hpp"
#include <algorithm>
#include <cmath>
#include <fstream>
#include <stdexcept>

namespace helengine::vulkan {
VulkanRenderer2D::VulkanRenderer2D(VulkanContext& context, uint32_t width, uint32_t height, std::string shaderRoot) : Context(context), ShaderRoot(shaderRoot.empty() ? HELENGINE_VULKAN_SHADER_ROOT : std::move(shaderRoot)) {
    try { InitializePipeline(); if (!Resize(width, height)) { throw std::invalid_argument("Initial Vulkan target extent must be nonzero."); } const uint8_t white[] {255,255,255,255}; WhiteTexture = CreateTexture(1,1,white); }
    catch (...) { Release(); throw; }
}

VulkanRenderer2D::~VulkanRenderer2D() { Release(); }

void VulkanRenderer2D::Release() noexcept {
    VkDevice device = Context.GetDevice(); vkDeviceWaitIdle(device);
    if (Framebuffer) { vkDestroyFramebuffer(device, Framebuffer, nullptr); Framebuffer = VK_NULL_HANDLE; }
    Target.reset();
    for (const auto& output : OutputFramebuffers) { if (output.second) { vkDestroyFramebuffer(device,output.second,nullptr); } }
    OutputFramebuffers.clear(); OutputImages.clear();
    if (Pipeline) { vkDestroyPipeline(device, Pipeline, nullptr); Pipeline = VK_NULL_HANDLE; }
    if (PipelineLayout) { vkDestroyPipelineLayout(device, PipelineLayout, nullptr); PipelineLayout = VK_NULL_HANDLE; }
    if (LoadRenderPass) { vkDestroyRenderPass(device, LoadRenderPass, nullptr); LoadRenderPass = VK_NULL_HANDLE; }
    if (RenderPass) { vkDestroyRenderPass(device, RenderPass, nullptr); RenderPass = VK_NULL_HANDLE; }
    if (DescriptorPool) { vkDestroyDescriptorPool(device, DescriptorPool, nullptr); DescriptorPool = VK_NULL_HANDLE; }
    Textures.clear(); TextureSets.clear();
    if (Sampler) { vkDestroySampler(device, Sampler, nullptr); Sampler = VK_NULL_HANDLE; }
    if (DescriptorLayout) { vkDestroyDescriptorSetLayout(device, DescriptorLayout, nullptr); DescriptorLayout = VK_NULL_HANDLE; }
}

VkShaderModule VulkanRenderer2D::LoadShader(const char* name) {
    const std::string path = ShaderRoot + "/" + name + ".spv";
    std::ifstream stream(path, std::ios::binary | std::ios::ate);
    if (!stream) { throw std::runtime_error("Required SPIR-V artifact is unavailable: " + path); }
    const auto size = stream.tellg();
    if (size < 20 || size % 4 != 0) { throw std::runtime_error("Invalid SPIR-V byte count: " + path); }
    std::vector<uint32_t> code(static_cast<size_t>(size) / 4); stream.seekg(0); stream.read(reinterpret_cast<char*>(code.data()), size);
    if (!stream || code[0] != 0x07230203U) { throw std::runtime_error("Invalid SPIR-V header/read: " + path); }
    VkShaderModuleCreateInfo create {VK_STRUCTURE_TYPE_SHADER_MODULE_CREATE_INFO}; create.codeSize = static_cast<size_t>(size); create.pCode = code.data();
    VkShaderModule module = VK_NULL_HANDLE; VulkanContext::Require(vkCreateShaderModule(Context.GetDevice(), &create, nullptr, &module), "vkCreateShaderModule"); return module;
}

void VulkanRenderer2D::InitializePipeline() {
    VkDevice device = Context.GetDevice();
    VkFormatProperties format {}; vkGetPhysicalDeviceFormatProperties(Context.GetPhysicalDevice(), VK_FORMAT_R8G8B8A8_UNORM, &format);
    const VkFormatFeatureFlags required = VK_FORMAT_FEATURE_COLOR_ATTACHMENT_BIT | VK_FORMAT_FEATURE_COLOR_ATTACHMENT_BLEND_BIT | VK_FORMAT_FEATURE_SAMPLED_IMAGE_BIT | VK_FORMAT_FEATURE_TRANSFER_SRC_BIT | VK_FORMAT_FEATURE_TRANSFER_DST_BIT;
    if ((format.optimalTilingFeatures & required) != required) { throw std::runtime_error("Vulkan RGBA8 optimal image lacks required sampled/color/blend/transfer features."); }
    VkDescriptorSetLayoutBinding binding {}; binding.binding = 0; binding.descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER; binding.descriptorCount = 1; binding.stageFlags = VK_SHADER_STAGE_FRAGMENT_BIT;
    VkDescriptorSetLayoutCreateInfo descriptor {VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO}; descriptor.bindingCount = 1; descriptor.pBindings = &binding;
    VulkanContext::Require(vkCreateDescriptorSetLayout(device, &descriptor, nullptr, &DescriptorLayout), "vkCreateDescriptorSetLayout");
    VkDescriptorPoolSize poolSize {VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER, 4096};
    VkDescriptorPoolCreateInfo pool {VK_STRUCTURE_TYPE_DESCRIPTOR_POOL_CREATE_INFO}; pool.flags = VK_DESCRIPTOR_POOL_CREATE_FREE_DESCRIPTOR_SET_BIT; pool.maxSets = 4096; pool.poolSizeCount = 1; pool.pPoolSizes = &poolSize;
    VulkanContext::Require(vkCreateDescriptorPool(device, &pool, nullptr, &DescriptorPool), "vkCreateDescriptorPool");
    VkSamplerCreateInfo sampler {VK_STRUCTURE_TYPE_SAMPLER_CREATE_INFO}; sampler.magFilter = VK_FILTER_NEAREST; sampler.minFilter = VK_FILTER_NEAREST;
    sampler.mipmapMode = VK_SAMPLER_MIPMAP_MODE_NEAREST; sampler.addressModeU = sampler.addressModeV = sampler.addressModeW = VK_SAMPLER_ADDRESS_MODE_CLAMP_TO_EDGE;
    VulkanContext::Require(vkCreateSampler(device, &sampler, nullptr, &Sampler), "vkCreateSampler");
    VkAttachmentDescription color {}; color.format = VK_FORMAT_R8G8B8A8_UNORM; color.samples = VK_SAMPLE_COUNT_1_BIT;
    color.loadOp = VK_ATTACHMENT_LOAD_OP_CLEAR; color.storeOp = VK_ATTACHMENT_STORE_OP_STORE; color.stencilLoadOp = VK_ATTACHMENT_LOAD_OP_DONT_CARE; color.stencilStoreOp = VK_ATTACHMENT_STORE_OP_DONT_CARE;
    color.initialLayout = VK_IMAGE_LAYOUT_UNDEFINED; color.finalLayout = VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL;
    VkAttachmentReference reference {0, VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL};
    VkSubpassDescription subpass {}; subpass.pipelineBindPoint = VK_PIPELINE_BIND_POINT_GRAPHICS; subpass.colorAttachmentCount = 1; subpass.pColorAttachments = &reference;
    VkSubpassDependency dependencies[2] {};
    dependencies[0].srcSubpass = VK_SUBPASS_EXTERNAL; dependencies[0].dstSubpass = 0;
    dependencies[0].srcStageMask = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT; dependencies[0].dstStageMask = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;
    dependencies[0].srcAccessMask=VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT;
    dependencies[0].dstAccessMask = VK_ACCESS_COLOR_ATTACHMENT_READ_BIT | VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT;
    dependencies[1].srcSubpass = 0; dependencies[1].dstSubpass = VK_SUBPASS_EXTERNAL;
    dependencies[1].srcStageMask = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT; dependencies[1].dstStageMask = VK_PIPELINE_STAGE_TRANSFER_BIT;
    dependencies[1].srcAccessMask = VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT; dependencies[1].dstAccessMask = VK_ACCESS_TRANSFER_READ_BIT;
    VkRenderPassCreateInfo pass {VK_STRUCTURE_TYPE_RENDER_PASS_CREATE_INFO}; pass.attachmentCount = 1; pass.pAttachments = &color; pass.subpassCount = 1; pass.pSubpasses = &subpass; pass.dependencyCount = 2; pass.pDependencies = dependencies;
    VulkanContext::Require(vkCreateRenderPass(device, &pass, nullptr, &RenderPass), "vkCreateRenderPass");
    color.loadOp=VK_ATTACHMENT_LOAD_OP_LOAD; color.initialLayout=VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
    VulkanContext::Require(vkCreateRenderPass(device,&pass,nullptr,&LoadRenderPass),"vkCreateRenderPass(UI load)");
    VkPushConstantRange push {VK_SHADER_STAGE_VERTEX_BIT | VK_SHADER_STAGE_FRAGMENT_BIT, 0, sizeof(QuadPushConstants)};
    VkPipelineLayoutCreateInfo layout {VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO}; layout.setLayoutCount = 1; layout.pSetLayouts = &DescriptorLayout; layout.pushConstantRangeCount = 1; layout.pPushConstantRanges = &push;
    VulkanContext::Require(vkCreatePipelineLayout(device, &layout, nullptr, &PipelineLayout), "vkCreatePipelineLayout");
    VkShaderModule vertex = VK_NULL_HANDLE, fragment = VK_NULL_HANDLE;
    try {
        vertex = LoadShader("quad.vert"); fragment = LoadShader("quad.frag");
        VkPipelineShaderStageCreateInfo stages[2] {};
        stages[0].sType = stages[1].sType = VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO;
        stages[0].stage = VK_SHADER_STAGE_VERTEX_BIT; stages[0].module = vertex; stages[0].pName = "main";
        stages[1].stage = VK_SHADER_STAGE_FRAGMENT_BIT; stages[1].module = fragment; stages[1].pName = "main";
        VkVertexInputBindingDescription input {0, sizeof(QuadVertex), VK_VERTEX_INPUT_RATE_VERTEX};
        VkVertexInputAttributeDescription attributes[] {{0,0,VK_FORMAT_R32G32_SFLOAT,offsetof(QuadVertex,Position)}, {1,0,VK_FORMAT_R32G32_SFLOAT,offsetof(QuadVertex,Uv)}, {2,0,VK_FORMAT_R32G32B32A32_SFLOAT,offsetof(QuadVertex,Color)}};
        VkPipelineVertexInputStateCreateInfo vertexInput {VK_STRUCTURE_TYPE_PIPELINE_VERTEX_INPUT_STATE_CREATE_INFO}; vertexInput.vertexBindingDescriptionCount = 1; vertexInput.pVertexBindingDescriptions = &input; vertexInput.vertexAttributeDescriptionCount = 3; vertexInput.pVertexAttributeDescriptions = attributes;
        VkPipelineInputAssemblyStateCreateInfo assembly {VK_STRUCTURE_TYPE_PIPELINE_INPUT_ASSEMBLY_STATE_CREATE_INFO}; assembly.topology = VK_PRIMITIVE_TOPOLOGY_TRIANGLE_LIST;
        VkPipelineViewportStateCreateInfo viewport {VK_STRUCTURE_TYPE_PIPELINE_VIEWPORT_STATE_CREATE_INFO}; viewport.viewportCount = 1; viewport.scissorCount = 1;
        VkPipelineRasterizationStateCreateInfo raster {VK_STRUCTURE_TYPE_PIPELINE_RASTERIZATION_STATE_CREATE_INFO}; raster.polygonMode = VK_POLYGON_MODE_FILL; raster.cullMode = VK_CULL_MODE_NONE; raster.frontFace = VK_FRONT_FACE_COUNTER_CLOCKWISE; raster.lineWidth = 1;
        VkPipelineMultisampleStateCreateInfo samples {VK_STRUCTURE_TYPE_PIPELINE_MULTISAMPLE_STATE_CREATE_INFO}; samples.rasterizationSamples = VK_SAMPLE_COUNT_1_BIT;
        VkPipelineColorBlendAttachmentState blend {}; blend.blendEnable = VK_TRUE; blend.srcColorBlendFactor = VK_BLEND_FACTOR_SRC_ALPHA; blend.dstColorBlendFactor = VK_BLEND_FACTOR_ONE_MINUS_SRC_ALPHA; blend.colorBlendOp = VK_BLEND_OP_ADD; blend.srcAlphaBlendFactor = VK_BLEND_FACTOR_ONE; blend.dstAlphaBlendFactor = VK_BLEND_FACTOR_ONE_MINUS_SRC_ALPHA; blend.alphaBlendOp = VK_BLEND_OP_ADD; blend.colorWriteMask = 15;
        VkPipelineColorBlendStateCreateInfo blending {VK_STRUCTURE_TYPE_PIPELINE_COLOR_BLEND_STATE_CREATE_INFO}; blending.attachmentCount = 1; blending.pAttachments = &blend;
        VkDynamicState dynamicStates[] {VK_DYNAMIC_STATE_VIEWPORT, VK_DYNAMIC_STATE_SCISSOR};
        VkPipelineDynamicStateCreateInfo dynamic {VK_STRUCTURE_TYPE_PIPELINE_DYNAMIC_STATE_CREATE_INFO}; dynamic.dynamicStateCount = 2; dynamic.pDynamicStates = dynamicStates;
        VkGraphicsPipelineCreateInfo pipeline {VK_STRUCTURE_TYPE_GRAPHICS_PIPELINE_CREATE_INFO}; pipeline.stageCount = 2; pipeline.pStages = stages; pipeline.pVertexInputState = &vertexInput; pipeline.pInputAssemblyState = &assembly; pipeline.pViewportState = &viewport; pipeline.pRasterizationState = &raster; pipeline.pMultisampleState = &samples; pipeline.pColorBlendState = &blending; pipeline.pDynamicState = &dynamic; pipeline.layout = PipelineLayout; pipeline.renderPass = RenderPass;
        VulkanContext::Require(vkCreateGraphicsPipelines(device, VK_NULL_HANDLE, 1, &pipeline, nullptr, &Pipeline), "vkCreateGraphicsPipelines(2D)");
    } catch (...) { if (vertex) { vkDestroyShaderModule(device, vertex, nullptr); } if (fragment) { vkDestroyShaderModule(device, fragment, nullptr); } throw; }
    vkDestroyShaderModule(device, vertex, nullptr); vkDestroyShaderModule(device, fragment, nullptr);
}

void VulkanRenderer2D::AddOutput(size_t handle,uint32_t width,uint32_t height) {
    if (!handle || handle==ActiveOutput || OutputImages.contains(handle)) { throw std::invalid_argument("Output handle must be nonzero and unique."); }
    if (FrameActive) { throw std::logic_error("Cannot register an output during a frame."); }
    const size_t previous=ActiveOutput;
    OutputImages.emplace(previous,std::move(Target)); OutputFramebuffers.emplace(previous,Framebuffer);
    Framebuffer=VK_NULL_HANDLE; Width=Height=0; ActiveOutput=handle;
    try { if (!Resize(width,height)) { throw std::invalid_argument("Output extent must be nonzero."); } }
    catch (...) { Target=std::move(OutputImages.at(previous)); OutputImages.erase(previous); Framebuffer=OutputFramebuffers.at(previous); OutputFramebuffers.erase(previous); ActiveOutput=previous; Width=Target->GetWidth(); Height=Target->GetHeight(); throw; }
    SelectOutput(previous);
}

void VulkanRenderer2D::SelectOutput(size_t handle) {
    if (FrameActive) { throw std::logic_error("Cannot select an output during a frame."); }
    if (handle==ActiveOutput) { return; }
    const auto found=OutputImages.find(handle);
    if (found==OutputImages.end()) { throw std::invalid_argument("Output handle is not registered."); }
    auto image=std::move(found->second); OutputImages.erase(found);
    const auto framebuffer=OutputFramebuffers.at(handle); OutputFramebuffers.erase(handle);
    OutputImages.emplace(ActiveOutput,std::move(Target)); OutputFramebuffers.emplace(ActiveOutput,Framebuffer);
    Target=std::move(image); Framebuffer=framebuffer; Width=Target->GetWidth(); Height=Target->GetHeight(); ActiveOutput=handle;
}

bool VulkanRenderer2D::Resize(uint32_t width, uint32_t height) {
    if (FrameActive) { throw std::logic_error("Cannot resize during a Vulkan frame."); }
    if (!width || !height) { return false; }
    if (width > Context.GetProperties().limits.maxImageDimension2D || height > Context.GetProperties().limits.maxImageDimension2D) { throw std::invalid_argument("Requested drawable exceeds Vulkan maxImageDimension2D."); }
    if (width == Width && height == Height) { return true; }
    auto replacement = std::make_unique<GpuImage>(Context, width, height, VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT | VK_IMAGE_USAGE_TRANSFER_SRC_BIT);
    VkImageView view = replacement->GetView();
    VkFramebufferCreateInfo create {VK_STRUCTURE_TYPE_FRAMEBUFFER_CREATE_INFO}; create.renderPass = RenderPass; create.attachmentCount = 1; create.pAttachments = &view; create.width = width; create.height = height; create.layers = 1;
    VkFramebuffer framebuffer = VK_NULL_HANDLE;
    VulkanContext::Require(vkCreateFramebuffer(Context.GetDevice(), &create, nullptr, &framebuffer), "vkCreateFramebuffer");
    VulkanContext::Require(vkDeviceWaitIdle(Context.GetDevice()), "vkDeviceWaitIdle(resize)");
    if (Framebuffer) { vkDestroyFramebuffer(Context.GetDevice(), Framebuffer, nullptr); }
    Framebuffer = framebuffer; Target = std::move(replacement); Width = width; Height = height; return true;
}

void VulkanRenderer2D::Transition(VkCommandBuffer commands, VkImage image, VkImageLayout oldLayout, VkImageLayout newLayout, VkPipelineStageFlags sourceStage, VkPipelineStageFlags destinationStage, VkAccessFlags sourceAccess, VkAccessFlags destinationAccess) {
    VkImageMemoryBarrier barrier {VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER}; barrier.srcAccessMask = sourceAccess; barrier.dstAccessMask = destinationAccess; barrier.oldLayout = oldLayout; barrier.newLayout = newLayout;
    barrier.srcQueueFamilyIndex = barrier.dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED; barrier.image = image; barrier.subresourceRange = {VK_IMAGE_ASPECT_COLOR_BIT,0,1,0,1};
    vkCmdPipelineBarrier(commands, sourceStage, destinationStage, 0, 0, nullptr, 0, nullptr, 1, &barrier);
}

size_t VulkanRenderer2D::CreateTexture(uint32_t width, uint32_t height, std::span<const uint8_t> rgba) {
    if (!width || !height || static_cast<uint64_t>(width) * height * 4 != rgba.size()) { throw std::invalid_argument("RGBA8 upload dimensions do not match source byte count."); }
    if (FrameActive) { throw std::logic_error("Textures must be uploaded before BeginFrame."); }
    Context.InvalidateSampledResources();
    auto image = std::make_unique<GpuImage>(Context, width, height, VK_IMAGE_USAGE_TRANSFER_DST_BIT | VK_IMAGE_USAGE_SAMPLED_BIT);
    GpuBuffer staging(Context, rgba.size(), VK_BUFFER_USAGE_TRANSFER_SRC_BIT, VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT | VK_MEMORY_PROPERTY_HOST_COHERENT_BIT);
    std::memcpy(staging.Map(), rgba.data(), rgba.size()); staging.Unmap();
    VkCommandBuffer commands = Context.BeginCommands();
    Transition(commands, image->Get(), VK_IMAGE_LAYOUT_UNDEFINED, VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, VK_PIPELINE_STAGE_TOP_OF_PIPE_BIT, VK_PIPELINE_STAGE_TRANSFER_BIT, 0, VK_ACCESS_TRANSFER_WRITE_BIT);
    VkBufferImageCopy copy {}; copy.imageSubresource = {VK_IMAGE_ASPECT_COLOR_BIT,0,0,1}; copy.imageExtent = {width,height,1};
    vkCmdCopyBufferToImage(commands, staging.Get(), image->Get(), VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, 1, &copy);
    Transition(commands, image->Get(), VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL, VK_PIPELINE_STAGE_TRANSFER_BIT, VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT, VK_ACCESS_TRANSFER_WRITE_BIT, VK_ACCESS_SHADER_READ_BIT);
    Context.SubmitAndWait(commands);
    VkDescriptorSetAllocateInfo allocate {VK_STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO}; allocate.descriptorPool = DescriptorPool; allocate.descriptorSetCount = 1; allocate.pSetLayouts = &DescriptorLayout;
    VkDescriptorSet descriptor = VK_NULL_HANDLE; VulkanContext::Require(vkAllocateDescriptorSets(Context.GetDevice(), &allocate, &descriptor), "vkAllocateDescriptorSets(texture)");
    VkDescriptorImageInfo imageInfo {Sampler, image->GetView(), VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL};
    VkWriteDescriptorSet write {VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET}; write.dstSet = descriptor; write.dstBinding = 0; write.descriptorCount = 1; write.descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER; write.pImageInfo = &imageInfo;
    vkUpdateDescriptorSets(Context.GetDevice(), 1, &write, 0, nullptr);
    const size_t handle = Textures.size(); Textures.push_back(std::move(image)); TextureSets.push_back(descriptor); return handle;
}

void VulkanRenderer2D::UpdateTextureRegion(size_t texture, uint32_t x, uint32_t y, uint32_t width, uint32_t height, std::span<const uint8_t> rgba, uint32_t rowPitch) {
    if (FrameActive) { throw std::logic_error("Texture regions must be updated before BeginFrame."); }
    if (texture >= Textures.size() || !Textures[texture]) { throw std::out_of_range("Cannot update a missing or released runtime texture."); }
    const auto& image = *Textures[texture];
    const uint64_t rowBytes = static_cast<uint64_t>(width) * 4;
    if (!width || !height || x >= image.GetWidth() || y >= image.GetHeight() || width > image.GetWidth() - x || height > image.GetHeight() - y
        || rowPitch < rowBytes || static_cast<uint64_t>(height - 1) * rowPitch + rowBytes > rgba.size()) {
        throw std::invalid_argument("Runtime texture region dimensions, bounds or source row pitch are invalid.");
    }
    GpuBuffer staging(Context, rowBytes * height, VK_BUFFER_USAGE_TRANSFER_SRC_BIT, VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT | VK_MEMORY_PROPERTY_HOST_COHERENT_BIT);
    auto* destination = static_cast<uint8_t*>(staging.Map());
    for (uint32_t row = 0; row < height; ++row) { std::memcpy(destination + row * rowBytes, rgba.data() + static_cast<size_t>(row) * rowPitch, rowBytes); }
    staging.Unmap();
    VkCommandBuffer commands = Context.BeginCommands();
    Transition(commands,image.Get(),VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT,VK_PIPELINE_STAGE_TRANSFER_BIT,VK_ACCESS_SHADER_READ_BIT,VK_ACCESS_TRANSFER_WRITE_BIT);
    VkBufferImageCopy copy {}; copy.imageSubresource = {VK_IMAGE_ASPECT_COLOR_BIT,0,0,1}; copy.imageOffset = {static_cast<int32_t>(x),static_cast<int32_t>(y),0}; copy.imageExtent = {width,height,1};
    vkCmdCopyBufferToImage(commands,staging.Get(),image.Get(),VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,1,&copy);
    Transition(commands,image.Get(),VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,VK_PIPELINE_STAGE_TRANSFER_BIT,VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT,VK_ACCESS_TRANSFER_WRITE_BIT,VK_ACCESS_SHADER_READ_BIT);
    Context.SubmitAndWait(commands);
}

void VulkanRenderer2D::ReleaseTexture(size_t texture) {
    if (texture == WhiteTexture) { throw std::invalid_argument("The renderer-internal white texture is released only at renderer shutdown."); }
    if (FrameActive) { throw std::logic_error("Runtime textures must be released outside an active frame."); }
    if (texture >= Textures.size() || !Textures[texture]) { throw std::out_of_range("Cannot release a missing or already released runtime texture."); }
    Context.InvalidateSampledResources();
    VulkanContext::Require(vkDeviceWaitIdle(Context.GetDevice()),"vkDeviceWaitIdle(texture release)");
    VulkanContext::Require(vkFreeDescriptorSets(Context.GetDevice(),DescriptorPool,1,&TextureSets[texture]),"vkFreeDescriptorSets(texture release)");
    TextureSets[texture] = VK_NULL_HANDLE; Textures[texture].reset();
}

VkDescriptorImageInfo VulkanRenderer2D::GetTextureDescriptor(size_t texture) const {
    if (texture>=Textures.size() || !Textures[texture]) { throw std::out_of_range("Cannot borrow a missing or released Vulkan runtime texture."); }
    return {Sampler,Textures[texture]->GetView(),VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL};
}

void VulkanRenderer2D::BeginFrame(std::array<float,4> clear,bool loadExistingColor) {
    if (FrameActive) { throw std::logic_error("Vulkan frame is already active."); }
    std::copy(clear.begin(), clear.end(), Clear.color.float32); Vertices.clear(); DrawTextures.clear(); DrawClips.clear(); DrawConstants.clear(); Clips.clear(); FrameActive = true; LoadExistingColor=loadExistingColor;
}

void VulkanRenderer2D::DrawQuad(size_t texture, const std::array<QuadVertex,6>& vertices) {
    if (!FrameActive) { throw std::logic_error("DrawQuad requires BeginFrame."); }
    if (texture >= TextureSets.size() || !Textures[texture]) { throw std::out_of_range("Required runtime texture handle is not uploaded."); }
    const VkRect2D clip = Clips.empty() ? VkRect2D{{0,0},{Width,Height}} : Clips.back();
    if (!clip.extent.width || !clip.extent.height) { return; }
    Vertices.insert(Vertices.end(), vertices.begin(), vertices.end()); DrawTextures.push_back(texture); DrawClips.push_back(clip);
    QuadPushConstants constants; constants.TargetSize = {static_cast<float>(Width),static_cast<float>(Height)}; DrawConstants.push_back(constants);
}

void VulkanRenderer2D::DrawRect(size_t texture, float x, float y, float width, float height, std::array<float,4> color) {
    DrawQuad(texture, {{{{x,y},{0,0},color}, {{x+width,y},{1,0},color}, {{x+width,y+height},{1,1},color}, {{x,y},{0,0},color}, {{x+width,y+height},{1,1},color}, {{x,y+height},{0,1},color}}});
}

void VulkanRenderer2D::DrawRoundedRect(float x,float y,float width,float height,float radius,float borderThickness,uint32_t corners,std::array<float,4> fill,std::array<float,4> border) {
    if (!std::isfinite(width) || !std::isfinite(height) || !std::isfinite(radius) || !std::isfinite(borderThickness)) { throw std::invalid_argument("Rounded rectangle dimensions must be finite."); }
    if (width <= 0 || height <= 0) { return; }
    const size_t count = DrawTextures.size(); DrawRect(WhiteTexture,x,y,width,height,fill);
    if (count == DrawTextures.size()) { return; }
    auto& constants = DrawConstants.back(); constants.ShapeSize = {width,height}; constants.Radius = std::clamp(radius,0.0f,std::min(width,height)*0.5f);
    constants.BorderThickness = std::clamp(borderThickness,0.0f,std::min(width,height)*0.5f); constants.Corners = corners; constants.Mode = 1; constants.BorderColor = border;
}

void VulkanRenderer2D::PushClip(float x, float y, float width, float height) {
    if (!FrameActive) { throw std::logic_error("PushClip requires BeginFrame."); }
    if (!std::isfinite(x) || !std::isfinite(y) || !std::isfinite(width) || !std::isfinite(height)) { throw std::invalid_argument("Clip coordinates must be finite."); }
    const auto parent = Clips.empty() ? VkRect2D{{0,0},{Width,Height}} : Clips.back();
    const float left = std::clamp(std::floor(x), static_cast<float>(parent.offset.x), static_cast<float>(parent.offset.x + parent.extent.width));
    const float top = std::clamp(std::floor(y), static_cast<float>(parent.offset.y), static_cast<float>(parent.offset.y + parent.extent.height));
    const float right = std::clamp(std::ceil(x + std::max(width,0.0f)), left, static_cast<float>(parent.offset.x + parent.extent.width));
    const float bottom = std::clamp(std::ceil(y + std::max(height,0.0f)), top, static_cast<float>(parent.offset.y + parent.extent.height));
    Clips.push_back({{static_cast<int32_t>(left),static_cast<int32_t>(top)},{static_cast<uint32_t>(right-left),static_cast<uint32_t>(bottom-top)}});
}

void VulkanRenderer2D::PopClip() { if (!FrameActive || Clips.empty()) { throw std::logic_error("Unmatched ClipPop in generated 2D command stream."); } Clips.pop_back(); }

std::vector<uint8_t> VulkanRenderer2D::EndFrameAndReadback() { return EndFrame(true); }

std::vector<uint8_t> VulkanRenderer2D::EndFrame(bool readbackEnabled) {
    VulkanFrameTraceScope renderTrace("Overlay.Render");
    if (!FrameActive) { throw std::logic_error("EndFrame requires BeginFrame."); }
    if (!Clips.empty()) { throw std::logic_error("Unmatched ClipPush at end of generated 2D command stream."); }
    const VkDeviceSize byteCount = static_cast<VkDeviceSize>(Width) * Height * 4;
    std::unique_ptr<GpuBuffer> readback;
    if (readbackEnabled) {
        VulkanFrameTraceScope AllocateTrace("Overlay.AllocateReadback");
        AllocateTrace.AddCounters(1,byteCount);
        readback=std::make_unique<GpuBuffer>(Context,byteCount,VK_BUFFER_USAGE_TRANSFER_DST_BIT,VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT | VK_MEMORY_PROPERTY_HOST_COHERENT_BIT);
    }
    VulkanFrameTraceScope VertexTrace("Overlay.UploadVertices");
    VertexTrace.AddCounters(Vertices.size(),Vertices.size()*sizeof(QuadVertex));
    std::unique_ptr<GpuBuffer> vertices;
    if (!Vertices.empty()) {
        vertices = std::make_unique<GpuBuffer>(Context, Vertices.size() * sizeof(QuadVertex), VK_BUFFER_USAGE_VERTEX_BUFFER_BIT, VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT | VK_MEMORY_PROPERTY_HOST_COHERENT_BIT);
        std::memcpy(vertices->Map(), Vertices.data(), Vertices.size() * sizeof(QuadVertex)); vertices->Unmap();
    }
    VertexTrace.Finish();
    VulkanFrameTraceScope RecordTrace("Overlay.Record");
    VkCommandBuffer commands = Context.BeginCommands();
    VkRenderPassBeginInfo begin {VK_STRUCTURE_TYPE_RENDER_PASS_BEGIN_INFO}; begin.renderPass = LoadExistingColor ? LoadRenderPass : RenderPass; begin.framebuffer = Framebuffer; begin.renderArea.extent = {Width,Height}; begin.clearValueCount = 1; begin.pClearValues = &Clear;
    vkCmdBeginRenderPass(commands, &begin, VK_SUBPASS_CONTENTS_INLINE);
    vkCmdBindPipeline(commands, VK_PIPELINE_BIND_POINT_GRAPHICS, Pipeline);
    VkViewport viewport {0,0,static_cast<float>(Width),static_cast<float>(Height),0,1}; vkCmdSetViewport(commands,0,1,&viewport);
    if (vertices) {
        VkBuffer buffer = vertices->Get(); VkDeviceSize offset = 0; vkCmdBindVertexBuffers(commands,0,1,&buffer,&offset);
        for (uint32_t index = 0; index < DrawTextures.size(); ++index) {
            vkCmdPushConstants(commands,PipelineLayout,VK_SHADER_STAGE_VERTEX_BIT | VK_SHADER_STAGE_FRAGMENT_BIT,0,sizeof(QuadPushConstants),&DrawConstants[index]);
            vkCmdSetScissor(commands,0,1,&DrawClips[index]); vkCmdBindDescriptorSets(commands,VK_PIPELINE_BIND_POINT_GRAPHICS,PipelineLayout,0,1,&TextureSets[DrawTextures[index]],0,nullptr); vkCmdDraw(commands,6,1,index*6,0);
        }
    }
    vkCmdEndRenderPass(commands);
    RecordTrace.Finish();
    renderTrace.Finish();
    if (!readbackEnabled) {
        Context.SubmitAndWait(commands);
        FrameActive=false;
        return {};
    }
    VulkanFrameTraceScope readbackTrace("Overlay.Readback");
    VkBufferImageCopy copy {}; copy.imageSubresource = {VK_IMAGE_ASPECT_COLOR_BIT,0,0,1}; copy.imageExtent = {Width,Height,1};
    vkCmdCopyImageToBuffer(commands, Target->Get(), VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL, readback->Get(), 1, &copy);
    VkBufferMemoryBarrier barrier {VK_STRUCTURE_TYPE_BUFFER_MEMORY_BARRIER}; barrier.srcAccessMask = VK_ACCESS_TRANSFER_WRITE_BIT; barrier.dstAccessMask = VK_ACCESS_HOST_READ_BIT;
    barrier.srcQueueFamilyIndex = barrier.dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED; barrier.buffer = readback->Get(); barrier.size = VK_WHOLE_SIZE;
    vkCmdPipelineBarrier(commands,VK_PIPELINE_STAGE_TRANSFER_BIT,VK_PIPELINE_STAGE_HOST_BIT,0,0,nullptr,1,&barrier,0,nullptr);
    Context.SubmitAndWait(commands);
    VulkanFrameTraceScope CopyTrace("Overlay.CopyReadbackToCpu");
    CopyTrace.AddCounters(1,byteCount);
    std::vector<uint8_t> pixels(static_cast<size_t>(byteCount)); std::memcpy(pixels.data(),readback->Map(),pixels.size()); readback->Unmap(); FrameActive = false; return pixels;
}
}
