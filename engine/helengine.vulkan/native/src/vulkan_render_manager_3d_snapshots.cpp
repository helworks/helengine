#include "vulkan_render_manager_3d.hpp"
#include "vulkan_frame_trace_scope.hpp"
#include "vulkan_camera_preparation_key.hpp"
#include "ShaderRuntimeMaterial.hpp"
#include "MaterialLayout.hpp"
#include "MaterialPropertyBlock.hpp"
#include "vulkan_renderer_2d.hpp"
#include "vulkan_render_manager_2d.hpp"
#include <stdexcept>
#include <algorithm>

namespace helengine::vulkan {
std::shared_ptr<const VulkanMeshMaterialSnapshot> VulkanRenderManager3D::BuildMaterialSnapshot(::ShaderRuntimeMaterial* material,::float4x4 world,const std::string& worldKey,std::array<std::vector<uint8_t>,4>& worldTransforms,std::array<std::vector<uint8_t>,4>& identityTransforms) {
        VulkanFrameTraceScope SnapshotTrace("Material.Snapshot");
        VulkanFrameTraceScope LookupTrace("Material.RootProperties");
        const auto found=Materials.find(VulkanInheritedMaterialCache::ResolveRoot(material));
        if (found==Materials.end()) { throw std::runtime_error("Linux drawable root material has no live authored Vulkan pipeline."); }
        const bool inherited=material->get_ParentMaterial()!=nullptr;
        auto* inheritedCache=inherited?&MaterialPreparations[material]:nullptr;
        auto& preparedCache=inherited?inheritedCache->Prepared:found->second.Prepared;
        VulkanRootMaterialCache::Identity propertyIdentity{nullptr,nullptr,0,0,false};
        const VulkanRootMaterialCache::Payload* cachedProperties=nullptr;
        try {cachedProperties=inherited?FindRootMaterialProperties(material,*inheritedCache):FindRootMaterialProperties(material,found->second,propertyIdentity);}
        catch(...) {preparedCache.InvalidateDependencies();throw;}
        LookupTrace.Finish();
        const bool captureProperties=!cachedProperties;
        VulkanRootMaterialCache::Payload propertyCopies;
        if(captureProperties) { propertyCopies.Constants.reserve(found->second.UniformBindingCount); propertyCopies.Textures.reserve(found->second.Bindings.size()-found->second.UniformBindingCount); }
        const bool supported=found->second.SupportsBatching&&!material->get_SupportsNormalMapping();
        const auto prepared=preparedCache.Read(cachedProperties!=nullptr,supported,Batcher!=nullptr);
        const size_t flags=(material->get_ReceivesShadows()?1:0)|(material->get_SupportsEmissive()?2:0);
        const auto cameraGeneration=CameraPreparation.Get();
        const auto resourceRevision=Renderer.GetContext().GetSampledResourceRevision();
        if(prepared && preparedCache.MatchesDependencies(worldKey,cameraGeneration,resourceRevision,flags)) {
            VulkanFrameTraceScope DependencyHitTrace("Material.PreparedDependencyHit");DependencyHitTrace.AddItems(1);
            return prepared;
        }
        preparedCache.InvalidateDependencies();
        if(prepared) {
            VulkanFrameTraceScope ValidateTrace("Material.ValidatePrepared");
            bool keysChanged=false,cacheChanged=false;
            std::shared_ptr<VulkanMeshMaterialSnapshot> replacement;
            for(const auto& binding:found->second.Bindings) {
                if(binding.Layout.Type!=VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER) {
                    const VulkanMeshRenderer::ImageBinding image{binding.Layout.Slot,ResolveSnapshotImage(material,binding,cachedProperties),binding.Layout.ViewType};
                    if(!VulkanPreparedMaterial::MatchesImage(prepared->Images,image)) {
                        if(!replacement) replacement=std::make_shared<VulkanMeshMaterialSnapshot>(*prepared);
                        VulkanPreparedMaterial::ReplaceImage(*replacement,image);keysChanged=true;cacheChanged=true;
                    }
                } else if(binding.Name=="TransformBuffer") {
                    if(worldTransforms[flags].empty()) worldTransforms[flags]=BuildUniform(material,binding,world);
                    if(!VulkanPreparedMaterial::MatchesBuffer(prepared->Buffers,binding.Layout.Slot,worldTransforms[flags])) {
                        if(!replacement) replacement=std::make_shared<VulkanMeshMaterialSnapshot>(*prepared);
                        VulkanPreparedMaterial::ReplaceOriginalBuffer(*replacement,binding.Layout.Slot,worldTransforms[flags]);
                    }
                    if(Batcher && supported) {
                        if(identityTransforms[flags].empty()) identityTransforms[flags]=BuildUniform(material,binding,::float4x4::get_Identity());
                        if(!VulkanPreparedMaterial::MatchesBuffer(prepared->BakedBuffers,binding.Layout.Slot,identityTransforms[flags])) {
                            if(!replacement) replacement=std::make_shared<VulkanMeshMaterialSnapshot>(*prepared);
                            if(binding.Layout.Slot>2) cacheChanged=true;
                            else if(binding.Layout.Slot==0) {
                                for(const auto& buffer:prepared->BakedBuffers) if(buffer.Slot==0 && !std::equal(buffer.Bytes.begin()+208,buffer.Bytes.end(),identityTransforms[flags].begin()+208)) cacheChanged=true;
                            }
                            VulkanPreparedMaterial::ReplaceBuffer(replacement->BakedBuffers,binding.Layout.Slot,identityTransforms[flags]);keysChanged=true;
                        }
                    }
                } else if(binding.Name=="ForwardLightBuffer" || binding.Name=="ShadowBuffer") {
                    const auto& bytes=binding.Name=="ForwardLightBuffer"?CurrentForwardLights:CurrentShadows;
                    const size_t required=binding.Name=="ForwardLightBuffer"?288:400;
                    if(binding.Layout.MinimumBytes!=required || bytes.size()!=required) throw std::runtime_error("Linux dynamic material uniform must preserve its authored GPU contract.");
                    if(!VulkanPreparedMaterial::MatchesBuffer(prepared->Buffers,binding.Layout.Slot,bytes)) {
                        if(!replacement) replacement=std::make_shared<VulkanMeshMaterialSnapshot>(*prepared);
                        VulkanPreparedMaterial::ReplaceOriginalBuffer(*replacement,binding.Layout.Slot,bytes);
                        if(Batcher && !supported) VulkanPreparedMaterial::ReplaceBuffer(replacement->BakedBuffers,binding.Layout.Slot,bytes);
                        keysChanged=true;if(binding.Layout.Slot>2) cacheChanged=true;
                    }
                }
            }
            ValidateTrace.Finish();
            VulkanFrameTraceScope HitTrace("Material.PreparedHit");HitTrace.AddItems(1);
            if(!replacement) {
                preparedCache.RecordDependencies(worldKey,cameraGeneration,resourceRevision,flags);
                return prepared;
            }
            VulkanFrameTraceScope RefreshTrace(keysChanged?"Material.PreparedDynamicRefresh":"Material.PreparedWorldCopy");RefreshTrace.AddCounters(1,replacement->GetPayloadBytes());
            if(keysChanged && Batcher) replacement->RefreshCompatibilityKeys(found->second.BatchPipelineKey,cacheChanged);
            preparedCache.Publish(replacement,Batcher!=nullptr);
            preparedCache.RecordDependencies(worldKey,cameraGeneration,resourceRevision,flags);
            return replacement;
        }
        VulkanFrameTraceScope RebuildTrace("Material.PreparedRebuild");RebuildTrace.AddItems(1);
        std::vector<VulkanMeshRenderer::BufferBinding> buffers,bakedBuffers; std::vector<VulkanMeshRenderer::ImageBinding> images;
        buffers.reserve(found->second.UniformBindingCount);
        if(Batcher) bakedBuffers.reserve(found->second.UniformBindingCount-(supported?0:found->second.TransformBindingCount));
        images.reserve(found->second.Bindings.size()-found->second.UniformBindingCount);
        VulkanFrameTraceScope BindingsTrace("Material.BuildBindings");
        BindingsTrace.AddItems(found->second.Bindings.size());
        for (const auto& binding : found->second.Bindings) {
            if (binding.Layout.Type==VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER) {
                if (Batcher && binding.Name=="TransformBuffer") {
                    if(worldTransforms[flags].empty()) worldTransforms[flags]=BuildUniform(material,binding,world);
                    if(supported && identityTransforms[flags].empty()) identityTransforms[flags]=BuildUniform(material,binding,::float4x4::get_Identity());
                    buffers.push_back({binding.Layout.Slot,worldTransforms[flags]}); if(supported) bakedBuffers.push_back({binding.Layout.Slot,identityTransforms[flags]});
                } else {
                    auto bytes=cachedProperties && IsMaterialConstant(binding)?cachedProperties->Constant(binding.Layout.Slot):BuildUniform(material,binding,world);
                    if(captureProperties && IsMaterialConstant(binding)) propertyCopies.Constants.push_back({binding.Layout.Slot,bytes});
                    if(Batcher && !supported) bakedBuffers.push_back({binding.Layout.Slot,bytes});
                    buffers.push_back({binding.Layout.Slot,std::move(bytes)});
                }
            }
            else {
                const auto descriptor=ResolveSnapshotImage(material,binding,cachedProperties);
                if(captureProperties && binding.Layout.Type!=VK_DESCRIPTOR_TYPE_SAMPLER && binding.Name!="shadowAtlasTexture" && binding.Name!="shadowAtlasSampler" && binding.Name!="pointShadowSampler" && binding.Name!="pointShadowTexture0" && binding.Name!="pointShadowTexture1" && binding.Name!="pointShadowTexture2" && binding.Name!="pointShadowTexture3") propertyCopies.Textures.push_back({binding.Layout.Slot,ResolveMaterialTexture(material,binding.Name)});
                images.push_back({binding.Layout.Slot,descriptor,binding.Layout.ViewType});
            }
        }
        BindingsTrace.Finish();
        VulkanFrameTraceScope ConstructTrace("Material.ConstructSnapshot");
        auto snapshot=std::make_shared<const VulkanMeshMaterialSnapshot>(found->second.Pipeline,found->second.BatchPipelineKey,supported,std::move(buffers),std::move(bakedBuffers),std::move(images),Batcher!=nullptr,Batcher && supported);
        ConstructTrace.Finish();
        VulkanFrameTraceScope PublishTrace("Material.PublishProperties");
        if(captureProperties) {
            if(inherited) PublishRootMaterialProperties(material,*inheritedCache,std::move(propertyCopies));
            else PublishRootMaterialProperties(material,found->second,propertyIdentity,std::move(propertyCopies));
        }
        preparedCache.Publish(snapshot,Batcher!=nullptr);
        preparedCache.RecordDependencies(worldKey,cameraGeneration,resourceRevision,flags);
        return snapshot;
}

void VulkanRenderManager3D::PublishCameraPreparation() {
    CameraPreparation.Publish(VulkanCameraPreparationKey::Create(CurrentViewProjection,CurrentCameraPosition,CurrentForwardLights,CurrentShadows,CurrentShadowAtlas,CurrentPointShadows,NeutralPointShadow));
}

VkDescriptorImageInfo VulkanRenderManager3D::ResolveSnapshotImage(::ShaderRuntimeMaterial* material,const VulkanMaterialResource::Binding& binding,const VulkanRootMaterialCache::Payload* properties) {
    if(binding.Name=="shadowAtlasTexture" || binding.Name=="shadowAtlasSampler") return MeshRenderer.GetShadowDescriptor(CurrentShadowAtlas);
    if(binding.Name=="pointShadowSampler") return MeshRenderer.GetShadowDescriptor(NeutralPointShadow);
    if(binding.Name=="pointShadowTexture0" || binding.Name=="pointShadowTexture1" || binding.Name=="pointShadowTexture2" || binding.Name=="pointShadowTexture3") return MeshRenderer.GetShadowDescriptor(CurrentPointShadows[static_cast<size_t>(binding.Name.back()-'0')]);
    if(binding.Layout.Type==VK_DESCRIPTOR_TYPE_SAMPLER) return Renderer.GetTextureDescriptor(0);
    auto* texture=properties?properties->Texture(binding.Layout.Slot):ResolveMaterialTexture(material,binding.Name);
    if(!texture) throw std::runtime_error("Linux material has no required inherited sampled texture: "+binding.Name);
    return Bridge.GetTextureDescriptor(texture);
}
}
