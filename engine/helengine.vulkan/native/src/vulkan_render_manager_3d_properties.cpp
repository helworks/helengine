#include "vulkan_render_manager_3d.hpp"
#include "ShaderRuntimeMaterial.hpp"
#include "MaterialLayout.hpp"
#include "MaterialLayoutBinding.hpp"
#include "MaterialLayoutBindings.hpp"
#include "MaterialPropertyBlock.hpp"
#include <algorithm>

namespace helengine::vulkan {
bool VulkanRenderManager3D::IsMaterialConstant(const VulkanMaterialResource::Binding& binding) {
    return binding.Layout.Type==VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER && binding.Name!="TransformBuffer" && binding.Name!="ForwardLightBuffer" && binding.Name!="ShadowBuffer";
}

const VulkanRootMaterialCache::Payload* VulkanRenderManager3D::FindRootMaterialProperties(::ShaderRuntimeMaterial* material,VulkanInheritedMaterialCache& cache) {
    const auto* result=cache.Read(material);
    if(result) ++RootCacheHits;else ++RootCacheMisses;
    return result;
}

void VulkanRenderManager3D::PublishRootMaterialProperties(::ShaderRuntimeMaterial* material,VulkanInheritedMaterialCache& cache,VulkanRootMaterialCache::Payload values) {
    const size_t oldBytes=cache.GetRetainedBytes(),oldPayload=cache.GetPayloadBytes();
    cache.Publish(material,std::move(values));
    const size_t newBytes=cache.GetRetainedBytes(),newPayload=cache.GetPayloadBytes();
    RootCachePeakBytes=std::max(RootCachePeakBytes,RootCacheRetainedBytes+newBytes);
    RootCacheRetainedBytes=RootCacheRetainedBytes-oldBytes+newBytes;
    RootCachePayloadBytes=RootCachePayloadBytes-oldPayload+newPayload;
    if(!oldBytes) ++RootCacheEntries;
}

const VulkanRootMaterialCache::Payload* VulkanRenderManager3D::FindRootMaterialProperties(::ShaderRuntimeMaterial* material,VulkanMaterialResource& resource,VulkanRootMaterialCache::Identity& identity) {
    identity={nullptr,nullptr,0,0,false};
    if(material->get_ParentMaterial()) return nullptr;
    auto* layout=material->get_Layout();auto* properties=material->get_Properties();
    if(!layout || !properties) return nullptr;
    identity={layout,properties,properties->get_Revision(),material->get_LayoutRevision(),true};
    const auto* result=resource.RootProperties.Read(identity,layout->get_TextureBindings(),layout->get_ConstantBufferBindings());
    if(result) ++RootCacheHits;else ++RootCacheMisses;
    return result;
}

void VulkanRenderManager3D::PublishRootMaterialProperties(::ShaderRuntimeMaterial* material,VulkanMaterialResource& resource,const VulkanRootMaterialCache::Identity& identity,VulkanRootMaterialCache::Payload values) {
    auto* layout=material->get_Layout();auto* properties=material->get_Properties();
    // Resolution invokes no authored callbacks, but refuse publication if its invalidation identity changed before completion.
    if(material->get_ParentMaterial() || layout!=identity.Layout || properties!=identity.Properties || properties->get_Revision()!=identity.PropertyRevision || material->get_LayoutRevision()!=identity.LayoutRevision) return;
    const size_t oldBytes=resource.RootProperties.GetRetainedBytes(),oldPayload=resource.RootProperties.GetPayloadBytes();
    resource.RootProperties.Publish(identity,layout->get_TextureBindings(),layout->get_ConstantBufferBindings(),std::move(values));
    const size_t newBytes=resource.RootProperties.GetRetainedBytes(),newPayload=resource.RootProperties.GetPayloadBytes();
    RootCachePeakBytes=std::max(RootCachePeakBytes,RootCacheRetainedBytes+newBytes);
    RootCacheRetainedBytes=RootCacheRetainedBytes-oldBytes+newBytes;
    RootCachePayloadBytes=RootCachePayloadBytes-oldPayload+newPayload;
    if(!oldBytes) ++RootCacheEntries;
}
}
