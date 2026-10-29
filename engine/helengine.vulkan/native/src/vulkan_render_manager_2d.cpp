#include "vulkan_render_manager_2d.hpp"
#include "vulkan_renderer_2d.hpp"
#include "TextureAsset.hpp"
#include "TextureAssetColorFormat.hpp"
#include "RuntimeTexture.hpp"
#include "RenderCommandList2D.hpp"
#include "RenderCommand2DType.hpp"
#include "ICamera.hpp"
#include "Entity.hpp"
#include <stdexcept>

namespace helengine::vulkan {
VulkanRenderManager2D::VulkanRenderManager2D(VulkanRenderer2D& renderer) : Renderer(renderer), CommandBuilder(std::make_unique<::RenderCommandListBuilder2D>()) { }

VulkanRenderManager2D::~VulkanRenderManager2D() {
    if (!Disposed) { CommandBuilder->Dispose(); }
    for (const auto& owner : TextureOwners) { delete owner.first; }
}

void VulkanRenderManager2D::SetTextCommandReuseMemoryBudgetBytes(int32_t budget) {
    if (budget<0 || Disposed) { throw std::invalid_argument("Linux text command reuse requires a nonnegative budget and a live render bridge."); }
    CommandBuilder->SetTextCommandReuseMemoryBudgetBytes(budget);
}

::RuntimeTexture* VulkanRenderManager2D::BuildTextureFromRaw(::TextureAsset* data) {
    if (Disposed) { throw std::logic_error("Cannot upload texture assets through a disposed Linux 2D bridge."); }
    if (!data || !data->Colors || !data->Colors->Data || data->Colors->Length <= 0 || !data->Width || !data->Height) { throw std::invalid_argument("Linux texture upload requires an initialized asset, nonzero dimensions and RGBA bytes."); }
    if (data->ColorFormat != TextureAssetColorFormat::Rgba32) { throw std::runtime_error("Linux texture asset requires cooked RGBA32 format: " + data->get_Id().raw()); }
    auto texture = std::make_unique<::RuntimeTexture>();
    texture->set_Id(data->get_Id()); texture->set_Width(data->Width); texture->set_Height(data->Height); texture->set_IsEngineOwned(data->IsEngineOwned); texture->set_UsesRgbFontCoverage(false);
    const size_t handle = Renderer.CreateTexture(data->Width,data->Height,std::span<const uint8_t>(data->Colors->Data,static_cast<size_t>(data->Colors->Length)));
    TextureOwners.emplace(texture.get(),handle); return texture.release();
}

size_t VulkanRenderManager2D::ResolveTexture(::RuntimeTexture* texture) const {
    const auto found = TextureOwners.find(texture);
    if (found == TextureOwners.end()) { throw std::runtime_error("Generated 2D command references a missing or released Vulkan texture owner."); }
    return found->second;
}

VkDescriptorImageInfo VulkanRenderManager2D::GetTextureDescriptor(::RuntimeTexture* texture) const { return Renderer.GetTextureDescriptor(ResolveTexture(texture)); }

void VulkanRenderManager2D::ReleaseTexture(::RuntimeTexture* texture) {
    if (!texture) { throw std::invalid_argument("Linux texture release requires a runtime texture owner."); }
    const size_t handle = ResolveTexture(texture); CommandBuilder->InvalidateTextCommandReuse(); Renderer.ReleaseTexture(handle); TextureOwners.erase(texture); ::RenderManager2D::ReleaseTexture(texture);
}

void VulkanRenderManager2D::ReleaseFont(::FontAsset* font) { CommandBuilder->InvalidateTextCommandReuse(); ::RenderManager2D::ReleaseFont(font); }

void VulkanRenderManager2D::UpdateTextureRegionCore(::RuntimeTexture* texture,int32_t x,int32_t y,int32_t width,int32_t height,Array<uint8_t>* rgba,int32_t rowPitch) {
    if (!rgba || !rgba->Data || rgba->Length <= 0 || x < 0 || y < 0 || width <= 0 || height <= 0 || rowPitch <= 0) { throw std::invalid_argument("Linux atlas update requires a validated positive region, row pitch and RGBA source."); }
    Renderer.UpdateTextureRegion(ResolveTexture(texture),x,y,width,height,std::span<const uint8_t>(rgba->Data,static_cast<size_t>(rgba->Length)),rowPitch);
}

void VulkanRenderManager2D::Dispose() {
    if (Disposed) { return; }
    DisposeDefaultTextures();
    while (!TextureOwners.empty()) { ReleaseTexture(TextureOwners.begin()->first); }
    CommandBuilder->Dispose(); Disposed = true;
}

void VulkanRenderManager2D::DrawGeometry(::RuntimeTexture* texture,::SpriteQuad2D geometry,::float4 source,::byte4 color) {
    const std::array<float,4> tint {color.X / 255.0f,color.Y / 255.0f,color.Z / 255.0f,color.W / 255.0f};
    const float u0 = source.X.ToSingle(), v0 = source.Y.ToSingle(), u1 = static_cast<float>(source.X + source.Z), v1 = static_cast<float>(source.Y + source.W);
    const QuadVertex topLeft {{geometry.TopLeft.X.ToSingle(),geometry.TopLeft.Y.ToSingle()},{u0,v0},tint};
    const QuadVertex topRight {{geometry.TopRight.X.ToSingle(),geometry.TopRight.Y.ToSingle()},{u1,v0},tint};
    const QuadVertex bottomRight {{geometry.BottomRight.X.ToSingle(),geometry.BottomRight.Y.ToSingle()},{u1,v1},tint};
    const QuadVertex bottomLeft {{geometry.BottomLeft.X.ToSingle(),geometry.BottomLeft.Y.ToSingle()},{u0,v1},tint};
    Renderer.DrawQuad(ResolveTexture(texture),{{topLeft,topRight,bottomRight,topLeft,bottomRight,bottomLeft}});
}

void VulkanRenderManager2D::RenderCamera(::ICamera* camera) {
    if (!camera) { throw std::invalid_argument("Linux 2D rendering requires an active camera."); }
    if (!camera->get_Parent() || !camera->get_Parent()->get_IsHierarchyEnabled() || !camera->get_RenderQueue2D()) { return; }
    auto* commands = CommandBuilder->Build(camera->get_RenderQueue2D());
    if (!commands) { throw std::runtime_error("Generated 2D command builder returned no command list."); }
    const auto viewport = camera->get_Viewport();
    float x = viewport.X.ToSingle(), y = viewport.Y.ToSingle(), width = viewport.Z.ToSingle(), height = viewport.W.ToSingle();
    if (width > 0 && height > 0 && width <= 1 && height <= 1) { x *= Renderer.GetWidth(); y *= Renderer.GetHeight(); width *= Renderer.GetWidth(); height *= Renderer.GetHeight(); }
    if (width <= 0 || height <= 0) { x = y = 0; width = static_cast<float>(Renderer.GetWidth()); height = static_cast<float>(Renderer.GetHeight()); }
    Renderer.PushClip(x,y,width,height);
    for (int32_t index = 0; index < commands->get_Count(); ++index) {
        const auto type = commands->GetCommandType(index);
        if (type == ::RenderCommand2DType::ClipPush) {
            const auto clip = commands->GetClipPushRect(commands->GetClipPushPayloadIndex(index)); Renderer.PushClip(clip.X.ToSingle(),clip.Y.ToSingle(),clip.Z.ToSingle(),clip.W.ToSingle());
        } else if (type == ::RenderCommand2DType::ClipPop) { Renderer.PopClip(); }
        else if (type == ::RenderCommand2DType::TexturedQuad) {
            const auto payload = commands->GetTexturedQuadPayloadIndex(index); DrawGeometry(commands->GetTexturedQuadTexture(payload),commands->GetTexturedQuadGeometry(payload),commands->GetTexturedQuadSourceRect(payload),commands->GetTexturedQuadColor(payload));
        } else if (type == ::RenderCommand2DType::GlyphQuad) {
            const auto payload = commands->GetGlyphQuadPayloadIndex(index); const auto bounds = commands->GetGlyphQuadBounds(payload);
            ::SpriteQuad2D geometry; geometry.TopLeft = ::float2(bounds.X,bounds.Y); geometry.TopRight = ::float2(bounds.X+bounds.Z,bounds.Y); geometry.BottomRight = ::float2(bounds.X+bounds.Z,bounds.Y+bounds.W); geometry.BottomLeft = ::float2(bounds.X,bounds.Y+bounds.W);
            DrawGeometry(commands->GetGlyphQuadTexture(payload),geometry,commands->GetGlyphQuadSourceRect(payload),commands->GetGlyphQuadColor(payload));
        } else if (type == ::RenderCommand2DType::RoundedRect) {
            const auto payload = commands->GetRoundedRectPayloadIndex(index); const auto bounds = commands->GetRoundedRectBounds(payload);
            const auto fill = commands->GetRoundedRectFillColor(payload), border = commands->GetRoundedRectBorderColor(payload);
            Renderer.DrawRoundedRect(bounds.X.ToSingle(),bounds.Y.ToSingle(),bounds.Z.ToSingle(),bounds.W.ToSingle(),static_cast<float>(commands->GetRoundedRectRadius(payload)),static_cast<float>(commands->GetRoundedRectBorderThickness(payload)),static_cast<uint32_t>(commands->GetRoundedRectCorners(payload)),
                {fill.X/255.0f,fill.Y/255.0f,fill.Z/255.0f,fill.W/255.0f},{border.X/255.0f,border.Y/255.0f,border.Z/255.0f,border.W/255.0f});
        } else { throw std::runtime_error("Unknown generated 2D command type: " + std::to_string(static_cast<int>(type))); }
    }
    Renderer.PopClip();
}

void VulkanRenderManager2D::DrawSprite(::ISpriteDrawable2D*) { throw std::logic_error("Linux sprites must be rendered through RenderCommandListBuilder2D."); }
void VulkanRenderManager2D::DrawText(::ITextDrawable2D*) { throw std::logic_error("Linux text must be rendered through RenderCommandListBuilder2D."); }
void VulkanRenderManager2D::DrawRoundedRect(::IRoundedRectDrawable2D*) { throw std::logic_error("Linux shapes must be rendered through RenderCommandListBuilder2D."); }
}
