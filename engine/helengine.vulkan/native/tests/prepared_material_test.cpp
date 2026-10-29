#include "vulkan_prepared_material.hpp"
#include "vulkan_preparation_generation.hpp"
#include <iostream>
#include <cstdlib>
#include <new>
#include <stdexcept>
using namespace helengine::vulkan;
/// <summary>Counts allocations only during the warmed production lookup probe.</summary>
static size_t Allocations=0;
/// <summary>Enables counting after setup and dependency publication have completed.</summary>
static bool CountAllocations=false;
/// <summary>Observes real standard-library heap allocations without replacing cache behavior.</summary>
void* operator new(size_t bytes) {
    if(CountAllocations) ++Allocations;
    if(void* memory=std::malloc(bytes?bytes:1)) return memory;
    throw std::bad_alloc();
}
/// <summary>Releases memory allocated by the probe's global allocator.</summary>
void operator delete(void* memory) noexcept {std::free(memory);}
/// <summary>Supports sized C++ deletion with the same allocator.</summary>
void operator delete(void* memory,size_t) noexcept {std::free(memory);}

/// <summary>Reports a broken prepared-material lifetime or dependency contract.</summary>
static void Require(bool value,const char* message) { if(!value) throw std::runtime_error(message); }
/// <summary>Exercises production retained snapshots with real dynamic bytes, descriptors and immutable keys.</summary>
int main() {
    try {
        VulkanPreparationGeneration generation;
        Require(generation.Get()==0,"unpublished generation was valid");
        generation.Publish("camera");const auto initial=generation.Get();
        generation.Publish("camera");Require(generation.Get()==initial,"unchanged camera advanced");
        generation.Publish("camerb");Require(generation.Get()==initial+1,"changed camera failed to advance");
        bool overflow=false;
        try {VulkanPreparationGeneration::Next(UINT64_MAX);} catch(const std::overflow_error&) {overflow=true;}
        Require(overflow,"generation wrapped");
        VulkanPreparedMaterial prepared;
        std::vector<VulkanMeshRenderer::BufferBinding> buffers{{0,std::vector<uint8_t>(224,0)},{1,{1,2}},{3,{4,5}}};
        std::vector<VulkanMeshRenderer::BufferBinding> baked{{0,std::vector<uint8_t>(224,0)}};
        std::vector<VulkanMeshRenderer::ImageBinding> images{{4,{VK_NULL_HANDLE,VK_NULL_HANDLE,VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL},VK_IMAGE_VIEW_TYPE_2D}};
        VulkanMeshMaterialSnapshot snapshot(7,8,true,buffers,baked,images,true,true);
        prepared.Publish(std::make_shared<const VulkanMeshMaterialSnapshot>(snapshot));
        auto retained=prepared.Read(true,true,true);
        Require(retained && retained->State==snapshot.State,"unchanged material did not retain completed keys");
        Require(prepared.Read(true,true,true)==retained,"unchanged read reconstructed snapshot");
        const std::string worldKey(128,'w');
        Require(!prepared.MatchesDependencies(worldKey,1,2,3),"unrecorded guard hit");
        prepared.RecordDependencies(worldKey,1,2,3);
        Require(prepared.MatchesDependencies(worldKey,1,2,3),"unchanged guard missed");
        CountAllocations=true;
        for(size_t visit=0;visit<10000;++visit) {
            Require(prepared.Read(true,true,true)==retained && prepared.MatchesDependencies(worldKey,1,2,3),"warm production lookup missed");
        }
        CountAllocations=false;
        Require(Allocations==0,"warmed prepared lookup allocated");
        Require(!prepared.MatchesDependencies(std::string(128,'x'),1,2,3),"world change hit");
        Require(!prepared.MatchesDependencies(worldKey,2,2,3),"camera change hit");
        Require(!prepared.MatchesDependencies(worldKey,1,3,3),"resource release hit");
        Require(!prepared.MatchesDependencies(worldKey,1,2,2),"shadow flag change hit");
        Require(!prepared.MatchesDependencies(worldKey,1,2,1),"emissive flag change hit");
        prepared.InvalidateDependencies();
        Require(!prepared.MatchesDependencies(worldKey,1,2,3),"failed refresh retained guard");
        prepared.RecordDependencies(worldKey,1,2,3);
        prepared.Publish(retained);
        Require(!prepared.MatchesDependencies(worldKey,1,2,3),"publication retained old guard");

        Require(!prepared.Read(false,true,true),"changed properties/layout reused prepared state");
        Require(!prepared.Read(true,false,true),"normal mapping support change reused prepared state");
        Require(!prepared.Read(true,true,false),"batching mode change reused prepared keys");
        Require(VulkanPreparedMaterial::MatchesBuffer(retained->Buffers,1,{1,2}),"unchanged lights missed");
        Require(!VulkanPreparedMaterial::MatchesBuffer(retained->Buffers,1,{2,2}),"changed lights matched");
        auto identity=baked[0].Bytes;identity[16]=7;
        Require(!VulkanPreparedMaterial::MatchesBuffer(retained->BakedBuffers,0,identity),"changed camera matched");
        identity=baked[0].Bytes;identity[208]=1;
        Require(!VulkanPreparedMaterial::MatchesBuffer(retained->BakedBuffers,0,identity),"changed flags matched");
        Require(VulkanPreparedMaterial::MatchesImage(retained->Images,images[0]),"unchanged texture missed");
        auto changed=images[0];changed.Image.imageLayout=VK_IMAGE_LAYOUT_GENERAL;
        Require(!VulkanPreparedMaterial::MatchesImage(retained->Images,changed),"changed texture/shadow descriptor matched");
        auto world=buffers[0].Bytes;world[0]=99;
        auto submission=*retained;VulkanPreparedMaterial::ReplaceOriginalBuffer(submission,0,world);
        Require(submission.Buffers[0].Bytes==world && retained->Buffers[0].Bytes[0]==0,"world update mutated retained snapshot");
        Require(submission.State==retained->State && submission.CacheState==retained->CacheState,"world-only update rebuilt baked keys");
        Require(submission.GetPayloadBytes()==retained->GetPayloadBytes(),"world update corrupted payload accounting");
        auto camera=*retained;
        camera.BakedBuffers[0].Bytes[16]=7;
        VulkanPreparedMaterial::ReplaceOriginalBuffer(camera,1,{9,2});
        camera.RefreshCompatibilityKeys(8,false);
        const VulkanMeshMaterialSnapshot expectedCamera(7,8,true,camera.Buffers,camera.BakedBuffers,camera.Images,true,true);
        Require(camera.State==expectedCamera.State && camera.CacheState==retained->CacheState,"camera/light refresh corrupted pass or persistent geometry compatibility");
        auto dynamic=*retained;
        VulkanPreparedMaterial::ReplaceOriginalBuffer(dynamic,1,{9,2});
        dynamic.BakedBuffers[0].Bytes[16]=7;
        dynamic.BakedBuffers[0].Bytes[208]=1;
        dynamic.Images[0].Image.imageLayout=VK_IMAGE_LAYOUT_GENERAL;
        dynamic.RefreshCompatibilityKeys(8);
        const VulkanMeshMaterialSnapshot expected(7,8,true,dynamic.Buffers,dynamic.BakedBuffers,dynamic.Images,true,true);
        Require(dynamic.State==expected.State && dynamic.CacheState==expected.CacheState,"dynamic refresh retained stale camera/light/flags/shadow compatibility keys");
        Require(dynamic.Buffers[2].Bytes==retained->Buffers[2].Bytes && retained->Buffers[1].Bytes[0]==1,"dynamic refresh changed material constants or prior submission");
        std::vector<VulkanMeshRenderer::BufferBinding> unsupportedBaked{buffers[1],buffers[2]};
        VulkanMeshMaterialSnapshot unsupported(7,8,false,buffers,unsupportedBaked,images,true,false);
        const auto originalUnsupportedCache=unsupported.CacheState;
        VulkanPreparedMaterial::ReplaceOriginalBuffer(unsupported,0,world);
        VulkanPreparedMaterial::ReplaceOriginalBuffer(unsupported,1,{9,2});
        VulkanPreparedMaterial::ReplaceBuffer(unsupported.BakedBuffers,1,{9,2});
        unsupported.RefreshCompatibilityKeys(8,false);
        const VulkanMeshMaterialSnapshot expectedUnsupported(7,8,false,unsupported.Buffers,unsupported.BakedBuffers,unsupported.Images,true,false);
        Require(unsupported.State==expectedUnsupported.State && unsupported.CacheState==expectedUnsupported.CacheState && unsupported.CacheState==originalUnsupportedCache,"non-sparse unsupported dynamic refresh diverged from fresh construction");
        Require(unsupported.CreateBakedBindings()[0].Bytes==std::vector<uint8_t>({9,2}),"non-sparse refresh left stale baked light bytes");
        VulkanPreparedMaterial::ReplaceImage(unsupported,changed);
        unsupported.RefreshCompatibilityKeys(8,true);
        const VulkanMeshMaterialSnapshot expectedUnsupportedImage(7,8,false,unsupported.Buffers,unsupported.BakedBuffers,unsupported.Images,true,false);
        Require(unsupported.State==expectedUnsupportedImage.State && unsupported.CacheState==expectedUnsupportedImage.CacheState,"non-sparse descriptor refresh diverged from fresh construction");
        bool failed=false;
        try {VulkanPreparedMaterial::ReplaceOriginalBuffer(submission,0,{1});} catch(const std::invalid_argument&) {failed=true;}
        Require(failed && submission.Buffers[0].Bytes==world,"failed replacement changed completed payload");
        failed=false;
        try {prepared.Publish(nullptr);} catch(const std::invalid_argument&) {failed=true;}
        Require(failed && prepared.Read(true,true,true)==retained,"failed publication replaced completed preparation");
        std::weak_ptr<const VulkanMeshMaterialSnapshot> lifetime=retained;
        prepared.Clear();Require(!prepared.MatchesDependencies(worldKey,1,2,3),"clear retained guard");Require(!prepared.Read(true,true,true),"release/invalidation retained prepared entry");
        Require(!lifetime.expired(),"cache release invalidated a shared queued submission");
        retained.reset();Require(lifetime.expired(),"last owner did not release retained material preparation");
        Require(submission.Buffers[0].Bytes==world,"release invalidated queued immutable payload");
        std::cout << "PASS prepared material reuse invalidation dynamic dependencies failure lifetime\n";
    } catch(const std::exception& error) {std::cerr << error.what() << '\n';return 1;}
}
