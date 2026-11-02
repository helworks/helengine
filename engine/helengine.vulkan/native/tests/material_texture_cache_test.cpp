#include "vulkan_material_texture_cache.hpp"
#include <iostream>
#include <vector>

/// <summary>Counts fake GPU retirement through the same shared-owner custom-deleter contract used by the native material adapter.</summary>
class FakeMaterialTexture final {
public:
    /// <summary>Throws with an actionable assertion name even when release-build assertions are disabled.</summary>
    static void Require(bool valid,const char* message) { if (!valid) { throw std::runtime_error(message); } }
    /// <summary>Represents a standalone upload whose retirement is performed by its bridge-style deleter.</summary>
    explicit FakeMaterialTexture() = default;
};

/// <summary>Verifies material-only sharing, source/path isolation, final-user release, cache expiration and failed-construction cleanup without allocating Vulkan resources.</summary>
int main() {
    try {
        int sourceA=0,sourceB=0,loaded=0,retired=0;
        helengine::vulkan::VulkanMaterialTextureCache<FakeMaterialTexture> cache;
        auto load=[&] {
            ++loaded;
            return std::shared_ptr<FakeMaterialTexture>(new FakeMaterialTexture(),[&](FakeMaterialTexture* texture) { ++retired; delete texture; });
        };
        auto first=cache.Acquire(&sourceA,"cooked/imported/shared.hasset",load);
        auto second=cache.Acquire(&sourceA,"cooked/imported/shared.hasset",load);
        FakeMaterialTexture::Require(first==second && loaded==1,"Repeated material texture must reuse one upload.");
        first.reset();
        FakeMaterialTexture::Require(retired==0,"A surviving material must retain its shared texture.");
        second.reset();
        FakeMaterialTexture::Require(retired==1,"The final material must retire the GPU resource exactly once.");
        auto reloaded=cache.Acquire(&sourceA,"cooked/imported/shared.hasset",load);
        FakeMaterialTexture::Require(loaded==2,"Expired weak entries must upload again.");
        auto otherSource=cache.Acquire(&sourceB,"cooked/imported/shared.hasset",load);
        auto otherPath=cache.Acquire(&sourceA,"cooked/imported/other.hasset",load);
        FakeMaterialTexture::Require(loaded==4 && otherSource!=reloaded && otherPath!=reloaded,"Content sources and cooked paths must stay isolated.");
        reloaded.reset(); otherSource.reset(); otherPath.reset();
        FakeMaterialTexture::Require(retired==4,"Each isolated upload must retire once.");

        bool rejected=false;
        try { cache.Acquire(&sourceA,"cooked/imported/failure.hasset",[]()->std::shared_ptr<FakeMaterialTexture> { throw std::runtime_error("decode failed"); }); }
        catch (const std::runtime_error& error) { rejected=std::string(error.what())=="decode failed"; }
        FakeMaterialTexture::Require(rejected,"Loading failures must preserve their original error.");
        auto recovered=cache.Acquire(&sourceA,"cooked/imported/failure.hasset",load);
        FakeMaterialTexture::Require(loaded==5,"Failed loads must not poison the cache.");
        recovered.reset();

        auto survivor=cache.Acquire(&sourceA,"cooked/imported/survivor.hasset",load);
        try {
            std::vector<std::shared_ptr<FakeMaterialTexture>> partialMaterial;
            partialMaterial.push_back(cache.Acquire(&sourceA,"cooked/imported/survivor.hasset",load));
            partialMaterial.push_back(cache.Acquire(&sourceA,"cooked/imported/partial.hasset",load));
            cache.Acquire(&sourceA,"cooked/imported/failure.hasset",[]()->std::shared_ptr<FakeMaterialTexture> { throw std::runtime_error("invalid payload"); });
        } catch (const std::runtime_error& error) { rejected=std::string(error.what())=="invalid payload"; }
        FakeMaterialTexture::Require(rejected && loaded==7 && retired==6,"Failed material assembly must retire its private texture and retain another material's shared texture.");
        survivor.reset();
        FakeMaterialTexture::Require(retired==7,"The surviving material must release its texture after failed assembly cleanup.");

        rejected=false;
        try { cache.Acquire(nullptr,"cooked/imported/shared.hasset",load); } catch (const std::invalid_argument&) { rejected=true; }
        FakeMaterialTexture::Require(rejected && loaded==7,"A missing source must fail before uploading.");
        rejected=false;
        try { cache.Acquire(&sourceA,"",load); } catch (const std::invalid_argument&) { rejected=true; }
        FakeMaterialTexture::Require(rejected && loaded==7,"A missing canonical path must fail before uploading.");
        rejected=false;
        try { cache.Acquire(&sourceA,"cooked/imported/null.hasset",[] { return std::shared_ptr<FakeMaterialTexture>{}; }); } catch (const std::runtime_error&) { rejected=true; }
        FakeMaterialTexture::Require(rejected,"A null upload must not enter the cache.");
        auto afterNull=cache.Acquire(&sourceA,"cooked/imported/null.hasset",load);
        afterNull.reset();
        FakeMaterialTexture::Require(loaded==8 && retired==8,"All successfully uploaded fake resources must retire exactly once.");
        std::cout<<"Native material texture sharing, isolation, final-user retirement and failure cleanup passed\n";
    } catch (const std::exception& error) { std::cerr<<error.what()<<'\n'; return 1; }
}
