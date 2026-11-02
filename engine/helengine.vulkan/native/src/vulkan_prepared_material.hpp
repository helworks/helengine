#pragma once
#include "vulkan_mesh_material_snapshot.hpp"
#include <memory>

namespace helengine::vulkan {
/// <summary>Retains one completed root-material preparation; callers validate mutable layout, live resources and dynamic dependencies before reuse.</summary>
class VulkanPreparedMaterial final {
public:
    /// <summary>Returns shared immutable preparation only after property validation and matching shader/batching behavior.</summary>
    std::shared_ptr<const VulkanMeshMaterialSnapshot> Read(bool propertiesValid,bool supported,bool buildKeys) const {
        return propertiesValid && Snapshot && Snapshot->Supported==supported && BuildKeys==buildKeys?Snapshot:nullptr;
    }
    /// <summary>Checks the successful dynamic guard without allocating; callers must validate properties and preparation mode first.</summary>
    bool MatchesDependencies(const std::string& world,uint64_t camera,uint64_t resources,size_t flags) const {
        return DependenciesValid && Snapshot && WorldKey==world && CameraGeneration==camera && ResourceRevision==resources && Flags==flags;
    }
    /// <summary>Records dependencies only after the full binding validation or publication has completed successfully.</summary>
    void RecordDependencies(const std::string& world,uint64_t camera,uint64_t resources,size_t flags) {
        InvalidateDependencies();
        if(!Snapshot || camera==0) throw std::invalid_argument("Prepared dependencies require a completed snapshot and camera state.");
        WorldKey=world;CameraGeneration=camera;ResourceRevision=resources;Flags=flags;DependenciesValid=true;
    }
    /// <summary>Prevents an attempted or failed refresh from reusing its predecessor's dynamic guard.</summary>
    void InvalidateDependencies() {DependenciesValid=false;}
    /// <summary>Publishes a fully constructed immutable preparation; failed construction leaves ownership unchanged.</summary>
    void Publish(std::shared_ptr<const VulkanMeshMaterialSnapshot> snapshot,bool buildKeys=true) {
        InvalidateDependencies();
        if(!snapshot) throw std::invalid_argument("Prepared material requires a completed snapshot.");
        Snapshot=std::move(snapshot);BuildKeys=buildKeys;
    }
    /// <summary>Invalidates retained preparation while queued submissions preserve their own immutable ownership.</summary>
    void Clear() {InvalidateDependencies();Snapshot.reset();}
    /// <summary>Compares a current dynamic uniform against the retained bytes without constructing bindings or binary keys.</summary>
    static bool MatchesBuffer(const std::vector<VulkanMeshRenderer::BufferBinding>& buffers,uint32_t slot,const std::vector<uint8_t>& bytes) {
        for(const auto& buffer:buffers) if(buffer.Slot==slot) return buffer.Bytes==bytes;
        return false;
    }
    /// <summary>Compares each Vulkan descriptor field explicitly, excluding padding and validating view type alongside live handles.</summary>
    static bool MatchesImage(const std::vector<VulkanMeshRenderer::ImageBinding>& images,const VulkanMeshRenderer::ImageBinding& current) {
        for(const auto& image:images) if(image.Slot==current.Slot) return image.ViewType==current.ViewType && image.Image.sampler==current.Image.sampler && image.Image.imageView==current.Image.imageView && image.Image.imageLayout==current.Image.imageLayout;
        return false;
    }
    /// <summary>Updates only a copied original-world buffer after baked dynamic dependencies matched, preserving completed keys and payload accounting.</summary>
    static void ReplaceOriginalBuffer(VulkanMeshMaterialSnapshot& snapshot,uint32_t slot,const std::vector<uint8_t>& bytes) {
        ReplaceBuffer(snapshot.Buffers,slot,bytes);
    }
    /// <summary>Replaces a dynamic original or baked uniform without altering reflected size or completed payload accounting.</summary>
    static void ReplaceBuffer(std::vector<VulkanMeshRenderer::BufferBinding>& buffers,uint32_t slot,const std::vector<uint8_t>& bytes) {
        for(auto& buffer:buffers) if(buffer.Slot==slot) {
            if(buffer.Bytes.size()!=bytes.size()) throw std::invalid_argument("Prepared world replacement must preserve reflected uniform size.");
            buffer.Bytes=bytes;return;
        }
        throw std::invalid_argument("Prepared world replacement requires an existing uniform slot.");
    }
    /// <summary>Replaces a changed live descriptor on an unpublished snapshot while preserving authored binding order.</summary>
    static void ReplaceImage(VulkanMeshMaterialSnapshot& snapshot,const VulkanMeshRenderer::ImageBinding& image) {
        for(auto& current:snapshot.Images) if(current.Slot==image.Slot) {current=image;return;}
        throw std::invalid_argument("Prepared descriptor replacement requires an existing binding slot.");
    }
private:
    /// <summary>Stores the exact native world scalar bytes associated with the successful snapshot.</summary>
    std::string WorldKey;
    /// <summary>Stores the exact camera/light/shadow state's published generation.</summary>
    uint64_t CameraGeneration=0;
    /// <summary>Stores the sampled-resource lifetime revision checked on every visit.</summary>
    uint64_t ResourceRevision=0;
    /// <summary>Stores explicit shadow and emissive flags, which are not material property revisions.</summary>
    size_t Flags=0;
    /// <summary>Becomes true only after all dependency recording completed successfully.</summary>
    bool DependenciesValid=false;
    /// <summary>Owns at most the latest successful material/world preparation, bounded by live native root materials.</summary>
    std::shared_ptr<const VulkanMeshMaterialSnapshot> Snapshot;
    /// <summary>Distinguishes keyless direct drawing from keyed batch preparation.</summary>
    bool BuildKeys=false;
};
}
