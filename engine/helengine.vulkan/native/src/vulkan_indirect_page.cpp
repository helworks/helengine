#include "vulkan_indirect_page.hpp"
#include "vulkan_indirect_range_plan.hpp"
#include <algorithm>
#include <cstring>
#include <stdexcept>
namespace helengine::vulkan {
VulkanIndirectPage::VulkanIndirectPage(VulkanContext& context,VulkanIndirectCuller* culler,std::span<const VulkanIndirectRange> ranges,std::span<const VulkanIndirectBounds> bounds):Context(context),Culler(culler),Ranges(ranges.begin(),ranges.end()) {
    const auto& limits=context.GetProperties().limits;
    if(ranges.empty() || ranges.size()>UINT32_MAX || ranges.size()!=bounds.size()) throw std::invalid_argument("Indirect page requires matching nonempty ranges and bounds.");
    for(size_t index=0;index<ranges.size();++index)if(bounds[index].IndexCount!=ranges[index].IndexCount || bounds[index].FirstIndex!=ranges[index].FirstIndex)throw std::invalid_argument("Indirect immutable metadata must match its original ranges.");
    DirectIndexCount=VulkanIndirectRangePlan::ContiguousIndexCount(ranges);
    if(!Culler) return;
    if(bounds.size_bytes()>limits.maxStorageBufferRange || ranges.size()*sizeof(VkDrawIndexedIndirectCommand)>limits.maxStorageBufferRange || (ranges.size()+63)/64>limits.maxComputeWorkGroupCount[0]) throw std::length_error("Indirect page exceeds storage or compute dispatch limits.");
    std::vector<VkDrawIndexedIndirectCommand> templates;templates.reserve(ranges.size());
    for(const auto& range:ranges) templates.push_back({range.IndexCount,1,range.FirstIndex,0,0});
    Bounds=Upload(bounds.data(),bounds.size_bytes(),VK_BUFFER_USAGE_STORAGE_BUFFER_BIT,VK_ACCESS_SHADER_READ_BIT);
    Commands=Upload(templates.data(),templates.size()*sizeof(templates[0]),VK_BUFFER_USAGE_STORAGE_BUFFER_BIT|VK_BUFFER_USAGE_INDIRECT_BUFFER_BIT|VK_BUFFER_USAGE_TRANSFER_SRC_BIT,VK_ACCESS_SHADER_READ_BIT|VK_ACCESS_SHADER_WRITE_BIT);
    try {
        const VkDescriptorPoolSize size{VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,2};
        VkDescriptorPoolCreateInfo pool{VK_STRUCTURE_TYPE_DESCRIPTOR_POOL_CREATE_INFO};pool.maxSets=1;pool.poolSizeCount=1;pool.pPoolSizes=&size;
        VulkanContext::Require(vkCreateDescriptorPool(context.GetDevice(),&pool,nullptr,&Pool),"vkCreateDescriptorPool(indirect page)");
        const auto layout=culler->GetDescriptors();VkDescriptorSetAllocateInfo allocate{VK_STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO};allocate.descriptorPool=Pool;allocate.descriptorSetCount=1;allocate.pSetLayouts=&layout;
        VulkanContext::Require(vkAllocateDescriptorSets(context.GetDevice(),&allocate,&Set),"vkAllocateDescriptorSets(indirect page)");
        const std::array<VkDescriptorBufferInfo,2> buffers{{{Bounds->Get(),0,bounds.size_bytes()},{Commands->Get(),0,templates.size()*sizeof(templates[0])}}};
        std::array<VkWriteDescriptorSet,2> writes{};
        for(uint32_t i=0;i<2;++i){writes[i].sType=VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET;writes[i].dstSet=Set;writes[i].dstBinding=i;writes[i].descriptorCount=1;writes[i].descriptorType=VK_DESCRIPTOR_TYPE_STORAGE_BUFFER;writes[i].pBufferInfo=&buffers[i];}
        vkUpdateDescriptorSets(context.GetDevice(),2,writes.data(),0,nullptr);
    }catch(...){if(Pool) vkDestroyDescriptorPool(context.GetDevice(),Pool,nullptr);throw;}
}
VulkanIndirectPage::~VulkanIndirectPage(){if(Pool) vkDestroyDescriptorPool(Context.GetDevice(),Pool,nullptr);}
std::unique_ptr<GpuBuffer> VulkanIndirectPage::Upload(const void* data,size_t bytes,VkBufferUsageFlags usage,VkAccessFlags access) {
    GpuBuffer staging(Context,bytes,VK_BUFFER_USAGE_TRANSFER_SRC_BIT,VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT|VK_MEMORY_PROPERTY_HOST_COHERENT_BIT);
    std::memcpy(staging.Map(),data,bytes);staging.Unmap();
    auto result=std::make_unique<GpuBuffer>(Context,bytes,usage|VK_BUFFER_USAGE_TRANSFER_DST_BIT,VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT);
    const auto commands=Context.BeginCommands();const VkBufferCopy copy{0,0,bytes};vkCmdCopyBuffer(commands,staging.Get(),result->Get(),1,&copy);
    VkPipelineStageFlags destinationStages=VK_PIPELINE_STAGE_COMPUTE_SHADER_BIT;
    if(usage&VK_BUFFER_USAGE_INDIRECT_BUFFER_BIT){destinationStages|=VK_PIPELINE_STAGE_DRAW_INDIRECT_BIT;access|=VK_ACCESS_INDIRECT_COMMAND_READ_BIT;}
    VkBufferMemoryBarrier barrier{VK_STRUCTURE_TYPE_BUFFER_MEMORY_BARRIER};barrier.srcAccessMask=VK_ACCESS_TRANSFER_WRITE_BIT;barrier.dstAccessMask=access;barrier.srcQueueFamilyIndex=barrier.dstQueueFamilyIndex=VK_QUEUE_FAMILY_IGNORED;barrier.buffer=result->Get();barrier.size=VK_WHOLE_SIZE;
    vkCmdPipelineBarrier(commands,VK_PIPELINE_STAGE_TRANSFER_BIT,destinationStages,0,0,nullptr,1,&barrier,0,nullptr);Context.SubmitAndWait(commands);return result;
}
void VulkanIndirectPage::Cull(VkCommandBuffer commands,std::span<const float,16> matrix) const {
    if(!Culler)return;
    vkCmdBindDescriptorSets(commands,VK_PIPELINE_BIND_POINT_COMPUTE,Culler->GetLayout(),0,1,&Set,0,nullptr);
    std::array<uint32_t,17> push{};std::memcpy(push.data(),matrix.data(),64);push[16]=static_cast<uint32_t>(Ranges.size());
    vkCmdPushConstants(commands,Culler->GetLayout(),VK_SHADER_STAGE_COMPUTE_BIT,0,sizeof(push),push.data());
    vkCmdDispatch(commands,(push[16]+63)/64,1,1);
}
size_t VulkanIndirectPage::ReadVisibleCommandCount() const {
    if(!Culler) return Ranges.size();
    const auto bytes=Ranges.size()*sizeof(VkDrawIndexedIndirectCommand);
    GpuBuffer staging(Context,bytes,VK_BUFFER_USAGE_TRANSFER_DST_BIT,VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT|VK_MEMORY_PROPERTY_HOST_COHERENT_BIT);
    const auto commands=Context.BeginCommands();
    VkBufferMemoryBarrier barrier{VK_STRUCTURE_TYPE_BUFFER_MEMORY_BARRIER};barrier.srcAccessMask=VK_ACCESS_SHADER_WRITE_BIT;barrier.dstAccessMask=VK_ACCESS_TRANSFER_READ_BIT;barrier.srcQueueFamilyIndex=barrier.dstQueueFamilyIndex=VK_QUEUE_FAMILY_IGNORED;barrier.buffer=Commands->Get();barrier.size=VK_WHOLE_SIZE;
    vkCmdPipelineBarrier(commands,VK_PIPELINE_STAGE_COMPUTE_SHADER_BIT,VK_PIPELINE_STAGE_TRANSFER_BIT,0,0,nullptr,1,&barrier,0,nullptr);
    const VkBufferCopy copy{0,0,bytes};vkCmdCopyBuffer(commands,Commands->Get(),staging.Get(),1,&copy);
    barrier.srcAccessMask=VK_ACCESS_TRANSFER_WRITE_BIT;barrier.dstAccessMask=VK_ACCESS_HOST_READ_BIT;barrier.buffer=staging.Get();
    vkCmdPipelineBarrier(commands,VK_PIPELINE_STAGE_TRANSFER_BIT,VK_PIPELINE_STAGE_HOST_BIT,0,0,nullptr,1,&barrier,0,nullptr);Context.SubmitAndWait(commands);
    const auto* values=static_cast<const VkDrawIndexedIndirectCommand*>(staging.Map());size_t count=0;
    for(size_t index=0;index<Ranges.size();++index)count+=values[index].instanceCount!=0;
    staging.Unmap();return count;
}
size_t VulkanIndirectPage::Draw(VkCommandBuffer commands,bool indirect) const {
    if(!Culler || !indirect){if(DirectIndexCount){vkCmdDrawIndexed(commands,DirectIndexCount,1,Ranges.front().FirstIndex,0,0);return 1;}for(const auto& range:Ranges)vkCmdDrawIndexed(commands,range.IndexCount,1,range.FirstIndex,0,0);return Ranges.size();}
    const uint32_t limit=Context.IsMultiDrawIndirectEnabled()?Context.GetProperties().limits.maxDrawIndirectCount:1;
    size_t calls=0;
    for(size_t first=0;first<Ranges.size();){const auto count=static_cast<uint32_t>(std::min<size_t>(limit,Ranges.size()-first));vkCmdDrawIndexedIndirect(commands,Commands->Get(),first*sizeof(VkDrawIndexedIndirectCommand),count,sizeof(VkDrawIndexedIndirectCommand));first+=count;++calls;}
    return calls;
}
}
