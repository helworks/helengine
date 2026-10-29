#pragma once
#include "gpu_buffer.hpp"
#include "uniform_arena_layout.hpp"
#include <array>
#include <limits>
#include <memory>
#include <vector>

namespace helengine::vulkan {
/// <summary>Retains one compatible synchronous pass's descriptor sets and coherent uniforms; writes occur only after the preceding pass has completed.</summary>
class VulkanPreparedBindings final {
public:
    /// <summary>Allocates a complete replacement before publication; partial allocation failures release all acquired resources.</summary>
    VulkanPreparedBindings(VulkanContext& context,const std::vector<VkDescriptorSetLayout>& layouts,const std::vector<size_t>& sizes,const std::array<uint32_t,3>& counts)
        :Context(context),Layouts(layouts),Sizes(sizes),ResourceRevision(context.GetSampledResourceRevision()) {
        if(layouts.empty() || layouts.size()>UINT32_MAX || counts[0]!=sizes.size()) throw std::invalid_argument("Prepared bindings require nonempty layouts and exact uniform counts.");
        const auto& limits=context.GetProperties().limits;
        UniformArenaLayout plan(limits.minUniformBufferOffsetAlignment,limits.maxUniformBufferRange,std::numeric_limits<size_t>::max());
        for(size_t size:sizes) Offsets.push_back(plan.Add(size));
        Shadow.resize(static_cast<size_t>(plan.GetSize()));BufferWritten.resize(sizes.size(),false);
        Images.resize(static_cast<size_t>(counts[1])+counts[2]);ImageWritten.resize(Images.size(),false);
        Sets.resize(layouts.size());
        try {
            if(plan.GetSize()) {
                Arena=std::make_unique<GpuBuffer>(Context,plan.GetSize(),VK_BUFFER_USAGE_UNIFORM_BUFFER_BIT,VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT|VK_MEMORY_PROPERTY_HOST_COHERENT_BIT);
                Mapping=Arena->Map();
            }
            const VkDescriptorType types[]={VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE,VK_DESCRIPTOR_TYPE_SAMPLER};
            std::vector<VkDescriptorPoolSize> poolSizes;
            for(size_t index=0;index<counts.size();++index) if(counts[index]) poolSizes.push_back({types[index],counts[index]});
            VkDescriptorPoolCreateInfo pool{VK_STRUCTURE_TYPE_DESCRIPTOR_POOL_CREATE_INFO};pool.maxSets=static_cast<uint32_t>(layouts.size());pool.poolSizeCount=static_cast<uint32_t>(poolSizes.size());pool.pPoolSizes=poolSizes.data();
            VulkanContext::Require(vkCreateDescriptorPool(Context.GetDevice(),&pool,nullptr,&Pool),"vkCreateDescriptorPool(prepared bindings)");
            VkDescriptorSetAllocateInfo allocate{VK_STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO};allocate.descriptorPool=Pool;allocate.descriptorSetCount=static_cast<uint32_t>(layouts.size());allocate.pSetLayouts=layouts.data();
            VulkanContext::Require(vkAllocateDescriptorSets(Context.GetDevice(),&allocate,Sets.data()),"vkAllocateDescriptorSets(prepared bindings)");
        }catch(...){Release();throw;}
    }
    /// <summary>Releases descriptors before their backing uniform allocation at a caller-established idle boundary.</summary>
    ~VulkanPreparedBindings(){Release();}
    /// <summary>Prevents copying the descriptor pool and persistent mapping ownership.</summary>
    VulkanPreparedBindings(const VulkanPreparedBindings&)=delete;
    /// <summary>Prevents replacing live resource handles through copy assignment.</summary>
    VulkanPreparedBindings& operator=(const VulkanPreparedBindings&)=delete;
    /// <summary>Rejects schema changes and sampled-handle reuse after any renderer resource retirement.</summary>
    bool Matches(const std::vector<VkDescriptorSetLayout>& layouts,const std::vector<size_t>& sizes) const {
        return ResourceRevision==Context.GetSampledResourceRevision() && Layouts==layouts && Sizes==sizes;
    }
    /// <summary>Uploads only changed payloads and writes each stable buffer descriptor once for the retained schema.</summary>
    bool WriteBuffer(size_t uniformIndex,size_t drawIndex,uint32_t slot,std::span<const uint8_t> bytes) {
        if(uniformIndex>=Sizes.size() || bytes.size()!=Sizes[uniformIndex] || drawIndex>=Sets.size()) throw std::invalid_argument("Prepared uniform does not match its planned slice.");
        const size_t offset=static_cast<size_t>(Offsets[uniformIndex]);
        const bool changed=!BufferWritten[uniformIndex] || std::memcmp(Shadow.data()+offset,bytes.data(),bytes.size())!=0;
        if(changed) {
            std::memcpy(static_cast<uint8_t*>(Mapping)+offset,bytes.data(),bytes.size());
            std::memcpy(Shadow.data()+offset,bytes.data(),bytes.size());
        }
        if(!BufferWritten[uniformIndex]) {
            VkDescriptorBufferInfo info{Arena->Get(),Offsets[uniformIndex],static_cast<VkDeviceSize>(bytes.size())};
            VkWriteDescriptorSet write{VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET};write.dstSet=Sets[drawIndex];write.dstBinding=slot;write.descriptorCount=1;write.descriptorType=VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER;write.pBufferInfo=&info;
            vkUpdateDescriptorSets(Context.GetDevice(),1,&write,0,nullptr);BufferWritten[uniformIndex]=true;
        }
        return changed;
    }
    /// <summary>Rewrites only changed sampled bindings; exact field comparison avoids padding and ignores unused descriptor fields.</summary>
    bool WriteImage(size_t imageIndex,size_t drawIndex,uint32_t slot,VkDescriptorType type,const VkDescriptorImageInfo& image) {
        if(imageIndex>=Images.size() || drawIndex>=Sets.size() || (type!=VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE && type!=VK_DESCRIPTOR_TYPE_SAMPLER)) throw std::invalid_argument("Prepared image does not match its planned descriptor.");
        const auto& previous=Images[imageIndex];
        const bool same=type==VK_DESCRIPTOR_TYPE_SAMPLER ? previous.sampler==image.sampler : previous.imageView==image.imageView && previous.imageLayout==image.imageLayout;
        if(ImageWritten[imageIndex] && same) return false;
        VkWriteDescriptorSet write{VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET};write.dstSet=Sets[drawIndex];write.dstBinding=slot;write.descriptorCount=1;write.descriptorType=type;write.pImageInfo=&image;
        vkUpdateDescriptorSets(Context.GetDevice(),1,&write,0,nullptr);Images[imageIndex]=image;ImageWritten[imageIndex]=true;return true;
    }
    /// <summary>Borrows prepared descriptors until this retained pass is replaced or released.</summary>
    const std::vector<VkDescriptorSet>& GetSets() const{return Sets;}
    /// <summary>Reports logical GPU uniform capacity independently of driver allocation granularity.</summary>
    size_t GetUniformBytes() const{return Shadow.size();}
private:
    /// <summary>Borrows the device owner, which outlives the persistent mapping.</summary>
    VulkanContext& Context;
    /// <summary>Stores exact ordered layouts; material retirement clears the owner's prepared passes.</summary>
    std::vector<VkDescriptorSetLayout> Layouts;
    /// <summary>Stores exact ordered uniform ranges that define stable offsets.</summary>
    std::vector<size_t> Sizes;
    /// <summary>Detects sampled resource retirement even if the driver recycles native handles.</summary>
    uint64_t ResourceRevision;
    /// <summary>Owns every descriptor set retained for this pass.</summary>
    VkDescriptorPool Pool=VK_NULL_HANDLE;
    /// <summary>Owns the coherent GPU uniform buffer.</summary>
    std::unique_ptr<GpuBuffer> Arena;
    /// <summary>Retains a persistent mapping written only between synchronous submissions.</summary>
    void* Mapping=nullptr;
    /// <summary>Stores aligned uniform offsets in reflected traversal order.</summary>
    std::vector<VkDeviceSize> Offsets;
    /// <summary>Retains CPU comparison bytes so changed detection never reads write-combined GPU memory.</summary>
    std::vector<uint8_t> Shadow;
    /// <summary>Distinguishes an unwritten all-zero uniform from initialized zero data.</summary>
    std::vector<bool> BufferWritten;
    /// <summary>Stores the previous descriptor fields without owning borrowed textures or samplers.</summary>
    std::vector<VkDescriptorImageInfo> Images;
    /// <summary>Distinguishes uninitialized sampled bindings from previously written descriptors.</summary>
    std::vector<bool> ImageWritten;
    /// <summary>Owns descriptor handles through Pool in authored draw order.</summary>
    std::vector<VkDescriptorSet> Sets;
    /// <summary>Cleans up a partial or complete allocation without touching borrowed layouts or sampled resources.</summary>
    void Release() noexcept {
        if(Pool){vkDestroyDescriptorPool(Context.GetDevice(),Pool,nullptr);Pool=VK_NULL_HANDLE;}
        if(Mapping){Arena->Unmap();Mapping=nullptr;}
        Arena.reset();
    }
};
}
