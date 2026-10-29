#include "vulkan_indirect_culler.hpp"
#include <array>
#include <fstream>
#include <vector>
#include <stdexcept>
namespace helengine::vulkan {
VulkanIndirectCuller::VulkanIndirectCuller(VulkanContext& context,const std::string& shaderRoot):Device(context.GetDevice()) {
    VkShaderModule shader=VK_NULL_HANDLE;
    try {
        std::ifstream input(shaderRoot+"/frustum_cull.comp.spv",std::ios::binary|std::ios::ate);
        if(!input) throw std::runtime_error("Missing packaged Vulkan frustum culling shader.");
        const auto length=input.tellg();
        if(length<20 || length%4) throw std::runtime_error("Invalid Vulkan frustum shader length.");
        std::vector<uint32_t> words(static_cast<size_t>(length)/4);input.seekg(0);
        if(!input.read(reinterpret_cast<char*>(words.data()),length) || words[0]!=0x07230203u) throw std::runtime_error("Invalid Vulkan frustum shader module.");
        VkShaderModuleCreateInfo module{VK_STRUCTURE_TYPE_SHADER_MODULE_CREATE_INFO};module.codeSize=words.size()*4;module.pCode=words.data();
        VulkanContext::Require(vkCreateShaderModule(Device,&module,nullptr,&shader),"vkCreateShaderModule(frustum)");
        const std::array<VkDescriptorSetLayoutBinding,2> bindings{{{0,VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,1,VK_SHADER_STAGE_COMPUTE_BIT,nullptr},{1,VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,1,VK_SHADER_STAGE_COMPUTE_BIT,nullptr}}};
        VkDescriptorSetLayoutCreateInfo descriptors{VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO};descriptors.bindingCount=2;descriptors.pBindings=bindings.data();
        VulkanContext::Require(vkCreateDescriptorSetLayout(Device,&descriptors,nullptr,&Descriptors),"vkCreateDescriptorSetLayout(frustum)");
        const VkPushConstantRange push{VK_SHADER_STAGE_COMPUTE_BIT,0,68};
        VkPipelineLayoutCreateInfo layout{VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO};layout.setLayoutCount=1;layout.pSetLayouts=&Descriptors;layout.pushConstantRangeCount=1;layout.pPushConstantRanges=&push;
        VulkanContext::Require(vkCreatePipelineLayout(Device,&layout,nullptr,&Layout),"vkCreatePipelineLayout(frustum)");
        VkComputePipelineCreateInfo pipeline{VK_STRUCTURE_TYPE_COMPUTE_PIPELINE_CREATE_INFO};pipeline.layout=Layout;pipeline.stage.sType=VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO;pipeline.stage.stage=VK_SHADER_STAGE_COMPUTE_BIT;pipeline.stage.module=shader;pipeline.stage.pName="main";
        VulkanContext::Require(vkCreateComputePipelines(Device,VK_NULL_HANDLE,1,&pipeline,nullptr,&Pipeline),"vkCreateComputePipelines(frustum)");
        vkDestroyShaderModule(Device,shader,nullptr);
    } catch(...) {if(shader) vkDestroyShaderModule(Device,shader,nullptr);Release();throw;}
}
VulkanIndirectCuller::~VulkanIndirectCuller(){Release();}
void VulkanIndirectCuller::Release() noexcept {
    if(Pipeline) vkDestroyPipeline(Device,Pipeline,nullptr);
    if(Layout) vkDestroyPipelineLayout(Device,Layout,nullptr);
    if(Descriptors) vkDestroyDescriptorSetLayout(Device,Descriptors,nullptr);
}
}
