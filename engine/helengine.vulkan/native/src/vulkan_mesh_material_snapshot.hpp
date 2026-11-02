#pragma once
#include "vulkan_mesh_renderer.hpp"
#include "vulkan_frame_trace_scope.hpp"
#include <string>
#include <utility>
#include <stdexcept>

namespace helengine::vulkan {
/// <summary>Owns completed binding payloads and exact compatibility keys; submissions publish this value through const shared ownership.</summary>
class VulkanMeshMaterialSnapshot final {
public:
    /// <summary>Selects this material owner's live native pipeline independently of equivalent pipeline identity.</summary>
    size_t Pipeline;
    /// <summary>Restricts geometry baking to the supported standard opaque contract.</summary>
    bool Supported;
    /// <summary>Retains original world uniforms for unchanged source-range drawing.</summary>
    std::vector<VulkanMeshRenderer::BufferBinding> Buffers;
    /// <summary>Retains identity-world uniforms for baked geometry drawing.</summary>
    std::vector<VulkanMeshRenderer::BufferBinding> BakedBuffers;
    /// <summary>Borrows image and sampler handles until synchronous pass completion.</summary>
    std::vector<VulkanMeshRenderer::ImageBinding> Images;
    /// <summary>Compares effective shader, pipeline state, uniforms and sampled resources within a pass.</summary>
    std::string State;
    /// <summary>Excludes camera/light/shadow bytes from persistent geometry identity while retaining material flags and properties.</summary>
    std::string CacheState;
    /// <summary>Stores only changed baked uniforms; unchanged bytes remain owned by Buffers until a selected baked draw materializes its bindings.</summary>
    bool SparseBaked;
    /// <summary>Finishes exact binary keys before the owner can publish immutable bindings to ordered submissions.</summary>
    VulkanMeshMaterialSnapshot(size_t pipeline,size_t pipelineKey,bool supported,std::vector<VulkanMeshRenderer::BufferBinding> buffers,std::vector<VulkanMeshRenderer::BufferBinding> bakedBuffers,std::vector<VulkanMeshRenderer::ImageBinding> images,bool buildKeys=true,bool sparseBaked=false)
        :Pipeline(pipeline),Supported(supported),Buffers(std::move(buffers)),BakedBuffers(std::move(bakedBuffers)),Images(std::move(images)),SparseBaked(sparseBaked),PayloadBytesValue(0) {
        for(const auto& buffer:Buffers) PayloadBytesValue+=buffer.Bytes.size();
        for(const auto& buffer:BakedBuffers) PayloadBytesValue+=buffer.Bytes.size();
        if(SparseBaked) {
            for(size_t i=0;i<BakedBuffers.size();++i) {
                const auto& replacement=BakedBuffers[i];bool found=false;
                for(const auto& original:Buffers) if(original.Slot==replacement.Slot && original.Bytes.size()==replacement.Bytes.size()) found=true;
                if(!found) throw std::invalid_argument("Sparse baked uniform requires a matching original slot and byte size.");
                for(size_t j=0;j<i;++j) if(BakedBuffers[j].Slot==replacement.Slot) throw std::invalid_argument("Sparse baked uniforms repeat a replacement slot.");
            }
        }
        if(buildKeys) BuildKeys(pipelineKey);
    }
    /// <summary>Refreshes pass keys after dynamic bindings change on an unpublished copy; preserves geometry keys when only camera/light bytes changed.</summary>
    void RefreshCompatibilityKeys(size_t pipelineKey,bool refreshCache=true) {
        State.clear();if(refreshCache) CacheState.clear();BuildKeys(pipelineKey,refreshCache);
    }
    /// <summary>Counts retained CPU uniform payload bytes once per immutable snapshot, excluding vector/control overhead and GPU resources.</summary>
    size_t GetPayloadBytes() const { return PayloadBytesValue; }
    /// <summary>Materializes only the selected baked draw from immutable captured bytes, without consulting mutable engine state again.</summary>
    std::vector<VulkanMeshRenderer::BufferBinding> CreateBakedBindings() const {
        VulkanFrameTraceScope BakedTrace("Material.MaterializeBakedBindings");
        if(!SparseBaked) return BakedBuffers;
        std::vector<VulkanMeshRenderer::BufferBinding> result;result.reserve(Buffers.size());
        for(const auto& buffer:Buffers) result.push_back(EffectiveBaked(buffer));
        return result;
    }
private:
    /// <summary>Counts completed retained uniform bytes once while their metadata is hot, avoiding per-frame diagnostic heap traversal.</summary>
    size_t PayloadBytesValue;
    /// <summary>Appends length-prefixed fields without delimiters, padding structs or lossy resource identities.</summary>
    static void Append(std::string& key,const void* data,size_t size) {
        const uint64_t count=size;key.append(reinterpret_cast<const char*>(&count),sizeof(count));
        if(size) key.append(static_cast<const char*>(data),size);
    }
    /// <summary>Uses an identity-world replacement when present, otherwise retaining the original immutable uniform payload.</summary>
    const VulkanMeshRenderer::BufferBinding& EffectiveBaked(const VulkanMeshRenderer::BufferBinding& original) const {
        if(SparseBaked) for(const auto& replacement:BakedBuffers) if(replacement.Slot==original.Slot) return replacement;
        return original;
    }
    /// <summary>Builds both exact compatibility encodings in one traversal, without allocating duplicate baked payloads.</summary>
    void BuildKeys(size_t pipelineKey,bool buildCache=true) {
        VulkanFrameTraceScope KeysTrace("Material.BuildKeys");
        size_t stateBytes=sizeof(uint64_t)+sizeof(pipelineKey),cacheBytes=stateBytes;
        const auto& bindings=SparseBaked?Buffers:BakedBuffers;
        for(const auto& original:bindings) {
            const auto& buffer=EffectiveBaked(original);
            const size_t bytes=2*sizeof(uint64_t)+sizeof(buffer.Slot)+buffer.Bytes.size();stateBytes+=bytes;
            if(buffer.Slot==0 && buffer.Bytes.size()==224) cacheBytes+=sizeof(uint64_t)+16;
            else if(buffer.Slot>2) cacheBytes+=bytes;
        }
        for(const auto& image:Images) {
            const size_t bytes=5*sizeof(uint64_t)+sizeof(image.Slot)+sizeof(image.ViewType)+sizeof(image.Image.sampler)+sizeof(image.Image.imageView)+sizeof(image.Image.imageLayout);
            stateBytes+=bytes;cacheBytes+=bytes;
        }
        KeysTrace.AddCounters(1,stateBytes+(buildCache?cacheBytes:0));
        State.reserve(stateBytes);Append(State,&pipelineKey,sizeof(pipelineKey));
        if(buildCache) {CacheState.reserve(cacheBytes);Append(CacheState,&pipelineKey,sizeof(pipelineKey));}
        for(const auto& original:bindings) {
            const auto& buffer=EffectiveBaked(original);
            Append(State,&buffer.Slot,sizeof(buffer.Slot));Append(State,buffer.Bytes.data(),buffer.Bytes.size());
            if(buildCache && buffer.Slot==0 && buffer.Bytes.size()==224) Append(CacheState,buffer.Bytes.data()+208,16);
            else if(buildCache && buffer.Slot>2) { Append(CacheState,&buffer.Slot,sizeof(buffer.Slot));Append(CacheState,buffer.Bytes.data(),buffer.Bytes.size()); }
        }
        for(const auto& image:Images) {
            Append(State,&image.Slot,sizeof(image.Slot));Append(State,&image.ViewType,sizeof(image.ViewType));Append(State,&image.Image.sampler,sizeof(image.Image.sampler));Append(State,&image.Image.imageView,sizeof(image.Image.imageView));Append(State,&image.Image.imageLayout,sizeof(image.Image.imageLayout));
            if(buildCache) {Append(CacheState,&image.Slot,sizeof(image.Slot));Append(CacheState,&image.ViewType,sizeof(image.ViewType));Append(CacheState,&image.Image.sampler,sizeof(image.Image.sampler));Append(CacheState,&image.Image.imageView,sizeof(image.Image.imageView));Append(CacheState,&image.Image.imageLayout,sizeof(image.Image.imageLayout));}
        }
    }
};
}
