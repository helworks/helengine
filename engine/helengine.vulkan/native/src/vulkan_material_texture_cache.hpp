#pragma once
#include <map>
#include <memory>
#include <stdexcept>
#include <string>

namespace helengine::vulkan {
/// <summary>Shares only immutable imported material textures on the render thread, without extending GPU residency beyond their material users.</summary>
template<class TTexture> class VulkanMaterialTextureCache final {
public:
    /// <summary>Reuses a live texture for the same borrowed content-source identity and canonical cooked path, loading again after final-user retirement. Loader failures leave no resident entry.</summary>
    template<class TLoader> std::shared_ptr<TTexture> Acquire(const void* source,const std::string& path,TLoader load) {
        if (!source || path.empty()) { throw std::invalid_argument("Material textures require a content source and canonical cooked path."); }
        auto& textures=TexturesBySource[source];
        const auto found=textures.find(path);
        if (found!=textures.end()) {
            if (auto texture=found->second.lock()) { return texture; }
            textures.erase(found);
        }
        auto texture=load();
        if (!texture) { throw std::runtime_error("Material texture loader returned no resource."); }
        textures.emplace(path,texture);
        return texture;
    }
private:
    /// <summary>Stores weak references only; owning materials release the texture through the native 2D bridge when the final shared owner leaves.</summary>
    std::map<const void*,std::map<std::string,std::weak_ptr<TTexture>>> TexturesBySource;
};
}
