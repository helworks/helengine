#pragma once
#include "RenderManager2D.hpp"
#include "RenderCommandListBuilder2D.hpp"
#include "SpriteQuad2D.hpp"
#include "float4.hpp"
#include "byte4.hpp"
#include <memory>
#include <unordered_map>
#include <vulkan/vulkan.h>

class ICamera;
namespace helengine::vulkan {
class VulkanRenderer2D;
/// <summary>Consumes the generated engine's retained 2D command list without updating components or altering sprite pivots and glyph layout.</summary>
class VulkanRenderManager2D final : public ::RenderManager2D {
public:
    /// <summary>Borrows the native renderer and creates one reusable generated command builder.</summary>
    explicit VulkanRenderManager2D(VulkanRenderer2D& renderer);
    /// <summary>Releases remaining runtime texture wrappers; native GPU resources remain owned by the renderer until synchronized shutdown.</summary>
    ~VulkanRenderManager2D() override;
    /// <summary>Uploads exactly RGBA32 bytes from a validated engine asset and returns an engine runtime texture owner.</summary>
    ::RuntimeTexture* BuildTextureFromRaw(::TextureAsset* data) override;
    /// <summary>Selects grayscale font coverage until an explicit RGB coverage shader contract is implemented.</summary>
    bool get_SupportsRgbFontCoverage() override { return false; }
    /// <summary>Releases one texture and its Vulkan descriptor after pending GPU work completes.</summary>
    void ReleaseTexture(::RuntimeTexture* texture) override;
    /// <summary>Invalidates retained glyph references before the generated font release path retires its atlas and metrics.</summary>
    void ReleaseFont(::FontAsset* font) override;
    /// <summary>Disposes default textures and all remaining runtime texture owners outside an active frame.</summary>
    void Dispose() override;
    /// <summary>Builds and consumes one camera's generated command list in retained order during an active native frame.</summary>
    void RenderCamera(::ICamera* camera);
    /// <summary>Applies the build-selected retained text metadata budget before any generated camera commands are built.</summary>
    void SetTextCommandReuseMemoryBudgetBytes(int32_t budget);
    /// <summary>Borrows a live engine texture's descriptor for the same-device 3D material pass without transferring texture ownership.</summary>
    VkDescriptorImageInfo GetTextureDescriptor(::RuntimeTexture* texture) const;
    /// <summary>Rejects bypassing the generated command builder, whose geometry already incorporates sprite pivots and transforms.</summary>
    void DrawSprite(::ISpriteDrawable2D* sprite) override;
    /// <summary>Rejects bypassing generated text layout and glyph atlas commands.</summary>
    void DrawText(::ITextDrawable2D* text) override;
    /// <summary>Rejects bypassing generated shape and clipping commands.</summary>
    void DrawRoundedRect(::IRoundedRectDrawable2D* shape) override;
protected:
    /// <summary>Preserves the generated dynamic atlas region and row-pitch contract when uploading to Vulkan.</summary>
    void UpdateTextureRegionCore(::RuntimeTexture* texture,int32_t x,int32_t y,int32_t width,int32_t height,Array<uint8_t>* rgba,int32_t rowPitch) override;
private:
    /// <summary>Resolves an engine texture owner to a live native image without a placeholder fallback.</summary>
    size_t ResolveTexture(::RuntimeTexture* texture) const;
    /// <summary>Maps generated absolute sprite geometry and normalized UV source rectangle to the fixed float32 GPU vertex ABI.</summary>
    void DrawGeometry(::RuntimeTexture* texture,::SpriteQuad2D geometry,::float4 source,::byte4 color);
    /// <summary>Borrows the renderer for uploads and ordered frame commands.</summary>
    VulkanRenderer2D& Renderer;
    /// <summary>Owns the reusable generated command builder and its retained text metadata.</summary>
    std::unique_ptr<::RenderCommandListBuilder2D> CommandBuilder;
    /// <summary>Maps each engine-owned wrapper to its stable native texture handle.</summary>
    std::unordered_map<::RuntimeTexture*,size_t> TextureOwners;
    /// <summary>Prevents duplicate explicit disposal.</summary>
    bool Disposed = false;
};
}
