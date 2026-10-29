#include "vulkan_render_manager_3d.hpp"
#include "vulkan_render_manager_2d.hpp"
#include "vulkan_renderer_2d.hpp"
#include "ShaderAsset.hpp"
#include "ShaderProgramAsset.hpp"
#include "ShaderBinaryAsset.hpp"
#include "ShaderBindingAsset.hpp"
#include "ShaderMaterialAsset.hpp"
#include "ShaderRuntimeMaterial.hpp"
#include "ShaderRuntimeMaterialLoader.hpp"
#include "ShaderRuntimeMaterialAccess.hpp"
#include "ShaderRuntimeContentRegistration.hpp"
#include "ShaderAssetBinarySerializer.hpp"
#include "ShaderMaterialAssetBinarySerializer.hpp"
#include "AssetSerializer.hpp"
#include "vulkan_material_asset_loader.hpp"
#include "vulkan_mesh_batch_eligibility.hpp"
#include "RuntimeContentManagerConfiguration.hpp"
#include "ContentManager.hpp"
#include "Core.hpp"
#include "TextureAsset.hpp"
#include "RuntimeTexture.hpp"
#include "MaterialLayout.hpp"
#include "MaterialLayoutBuilder.hpp"
#include "MaterialPropertyBlock.hpp"
#include "MaterialRenderState.hpp"
#include "StandardMaterialTextureBindingDefaults.hpp"
#include "generated_disposable_deleter.hpp"
#include <algorithm>
#include <cstring>
#include <cstdlib>
#include <iostream>
#include <stdexcept>

namespace helengine::vulkan {
::RuntimeMaterial* VulkanRenderManager3D::BuildMaterialFromRawAsset(::ContentManager* content,HeCppString path) {
    if (Disposed || !content || String::IsNullOrWhiteSpace(path)) { throw std::invalid_argument("Linux material loading requires a live renderer, content manager and cooked material path."); }
    if (!OwnerCore || OwnerCore->get_RenderManager2D()!=&Bridge) { throw std::logic_error("Linux material loading requires its owning core and paired 2D renderer."); }
    ::RuntimeContentManagerConfiguration::ConfigureSharedAssetContentManager(content,&Bridge);
    ::ShaderRuntimeContentRegistration::Register(content);
    auto material=VulkanMaterialAssetLoader::Read<::ShaderMaterialAsset>(content->get_ContentStreamSource(),path,[](::Stream* stream) { return ::ShaderMaterialAssetBinarySerializer::Deserialize(stream); });
    if (!material) { throw std::runtime_error("Linux cooked material processor returned no material asset."); }
    const auto shaderPath=::ShaderRuntimeMaterialLoader::ResolveShaderPackagePath(material->ShaderAssetId,get_ShaderCompileTarget());
    auto shader=VulkanMaterialAssetLoader::Read<::ShaderAsset>(content->get_ContentStreamSource(),shaderPath,[](::Stream* stream) { return ::ShaderAssetBinarySerializer::Deserialize(stream); });
    auto* runtime=BuildMaterialFromRaw(material.get(),shader.get());
    try {
        auto* shaderRuntime=::ShaderRuntimeMaterialAccess::Require(runtime);
        ApplyImportedMaterialTexture(content,material->DiffuseTextureAssetId,shaderRuntime,::StandardMaterialTextureBindingDefaults::DiffuseTextureBindingName());
        ApplyImportedMaterialTexture(content,material->EmissiveTextureAssetId,shaderRuntime,::StandardMaterialTextureBindingDefaults::EmissiveTextureBindingName());
        ApplyImportedMaterialTexture(content,material->RoughnessTextureAssetId,shaderRuntime,::StandardMaterialTextureBindingDefaults::RoughnessTextureBindingName());
        return runtime;
    } catch (...) { ReleaseMaterial(runtime); throw; }
}

void VulkanRenderManager3D::ApplyImportedMaterialTexture(::ContentManager* content,HeCppString assetId,::ShaderRuntimeMaterial* material,HeCppString bindingName) {
    if (String::IsNullOrWhiteSpace(assetId)) { return; }
    const int32_t bindingIndex=material->get_Layout()->FindTextureBindingIndex(bindingName);
    if (bindingIndex<0) { return; }
    const auto path=::ShaderRuntimeMaterialLoader::ResolveImportedTexturePackagePath(assetId);
    auto texture=MaterialTextures.Acquire(content->get_ContentStreamSource(),path.raw(),[this,content,path] {
        auto asset=VulkanMaterialAssetLoader::Read<::TextureAsset>(content->get_ContentStreamSource(),path,[](::Stream* stream) { return ::AssetSerializer::Deserialize(stream); });
        return std::shared_ptr<::RuntimeTexture>(Bridge.BuildTextureFromRaw(asset.get()),[this](::RuntimeTexture* value) { Bridge.ReleaseTexture(value); });
    });
    Materials.at(material).Textures.push_back(texture);
    material->get_Properties()->SetTexture(bindingIndex,texture.get());
}

::ShaderCompileTarget VulkanRenderManager3D::get_ShaderCompileTarget() { return ::ShaderCompileTarget::Vulkan; }

::ShaderProgramAsset* VulkanRenderManager3D::FindProgram(::ShaderAsset* shader,HeCppString name,::ShaderStage stage) {
    if (shader && shader->Programs) { for (int32_t index=0;index<shader->Programs->Length;++index) { auto* program=(*shader->Programs)[index]; if (program && program->Name==name && program->Stage==stage) { return program; } } }
    throw std::runtime_error("Linux material has no authored shader program for the requested stage: "+name.raw());
}

std::vector<uint32_t> VulkanRenderManager3D::ReadShader(::ShaderAsset* shader,HeCppString name,::ShaderStage stage,HeCppString variant) {
    if (!shader || shader->TargetName!="vulkan" || !shader->Binaries) { throw std::invalid_argument("Linux materials require the cooked Vulkan shader package."); }
    for (int32_t index=0;index<shader->Binaries->Length;++index) {
        auto* binary=(*shader->Binaries)[index];
        if (!binary || binary->TargetName!=shader->TargetName || binary->ProgramName!=name || binary->Stage!=stage || binary->Variant!=variant) { continue; }
        if (!binary->Bytecode || binary->Bytecode->Length<20 || binary->Bytecode->Length%4) { throw std::runtime_error("Authored Vulkan shader binary has an invalid SPIR-V byte length."); }
        std::vector<uint32_t> words(binary->Bytecode->Length/4); std::memcpy(words.data(),binary->Bytecode->Data,binary->Bytecode->Length); return words;
    }
    throw std::runtime_error("Linux material has no exact cooked shader binary for program/variant: "+name.raw()+"/"+variant.raw());
}

void VulkanRenderManager3D::AddBindings(VulkanMaterialResource& resource,::ShaderProgramAsset* program,VkShaderStageFlags stage) {
    if (!program->Bindings) { return; }
    for (int32_t index=0;index<program->Bindings->Length;++index) {
        auto* binding=(*program->Bindings)[index];
        if (!binding || binding->Name.empty() || binding->Slot<0 || binding->Set!=0 || binding->Size<0) { throw std::runtime_error("Linux Vulkan shader reflection requires named set-zero resources with nonnegative slots and sizes."); }
        VkDescriptorType type;
        switch (binding->Type) {
            case ::ShaderResourceType::ConstantBuffer: type=VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER; break;
            case ::ShaderResourceType::Texture2D: type=VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE; break;
            case ::ShaderResourceType::TextureCube: type=VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE; break;
            case ::ShaderResourceType::Sampler: type=VK_DESCRIPTOR_TYPE_SAMPLER; break;
            default: throw std::runtime_error("Linux authored material resource type is not implemented: "+binding->Name.raw());
        }
        const auto found=std::find_if(resource.Bindings.begin(),resource.Bindings.end(),[binding](const auto& value) { return value.Layout.Slot==static_cast<uint32_t>(binding->Slot); });
        if (found!=resource.Bindings.end()) {
            if (found->Name!=binding->Name.raw() || found->Layout.Type!=type || found->Layout.MinimumBytes!=static_cast<size_t>(binding->Size) || found->Layout.ViewType!=(binding->Type==::ShaderResourceType::TextureCube?VK_IMAGE_VIEW_TYPE_CUBE:VK_IMAGE_VIEW_TYPE_2D)) { throw std::runtime_error("Authored shader stages disagree about a shared Vulkan descriptor."); }
            found->Layout.Stages|=stage;
        } else { resource.Bindings.push_back({binding->Name.raw(),{static_cast<uint32_t>(binding->Slot),type,stage,static_cast<size_t>(binding->Size),1,static_cast<uint32_t>(binding->Set),binding->Type==::ShaderResourceType::TextureCube?VK_IMAGE_VIEW_TYPE_CUBE:VK_IMAGE_VIEW_TYPE_2D}}); }
    }
}

::RuntimeMaterial* VulkanRenderManager3D::BuildMaterialFromRaw(::ShaderMaterialAsset* material,::ShaderAsset* shader) {
    if (Disposed || !material || !shader || material->ShaderAssetId.empty() || material->ShaderAssetId!=shader->get_Id() || !material->RenderState) { throw std::invalid_argument("Linux material upload requires matching authored shader identity, render state and a live renderer."); }
    auto* vertex=FindProgram(shader,material->VertexProgram,::ShaderStage::Vertex);
    auto* pixel=FindProgram(shader,material->PixelProgram,::ShaderStage::Pixel);
    VulkanMaterialResource resource; resource.ShaderId=shader->get_Id().raw(); AddBindings(resource,vertex,VK_SHADER_STAGE_VERTEX_BIT); AddBindings(resource,pixel,VK_SHADER_STAGE_FRAGMENT_BIT);
    std::vector<VulkanMeshRenderer::BindingLayout> bindings;
    for(const auto& binding:resource.Bindings) {
        bindings.push_back(binding.Layout);
        if(binding.Layout.Type==VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER) { ++resource.UniformBindingCount; if(binding.Name=="TransformBuffer") ++resource.TransformBindingCount; }
    }
    VulkanMeshRenderer::MaterialState state {static_cast<uint32_t>(material->RenderState->get_BlendMode()),static_cast<uint32_t>(material->RenderState->get_CullMode()),material->RenderState->get_DepthTestEnabled(),material->RenderState->get_DepthWriteEnabled()};
    GeneratedDisposableOwner<::ShaderRuntimeMaterial> runtime(new ::ShaderRuntimeMaterial());
    runtime->set_Id(material->get_Id()); runtime->SetLayout(::MaterialLayoutBuilder::Build(material,shader)); runtime->SetRenderState(material->RenderState); runtime->ApplyConstantBufferDefaults(material->ConstantBuffers);
    runtime->set_CastsShadows(material->CastsShadows); runtime->set_ReceivesShadows(material->ReceivesShadows); runtime->set_SupportsNormalMapping(!material->NormalTextureAssetId.empty()); runtime->set_SupportsEmissive(!material->EmissiveTextureAssetId.empty());
    ::StandardMaterialTextureBindingDefaults::Apply(runtime.get(),&Bridge);
    const auto vertexCode=ReadShader(shader,material->VertexProgram,::ShaderStage::Vertex,material->Variant),pixelCode=ReadShader(shader,material->PixelProgram,::ShaderStage::Pixel,material->Variant);
    resource.BatchPipelineKey=InternBatchPipeline(resource,vertexCode,vertex->EntryPoint.raw(),pixelCode,pixel->EntryPoint.raw(),state);
    resource.SupportsBatching=VulkanMeshBatchEligibility::Supports(resource.ShaderId,material->VertexProgram.raw(),material->PixelProgram.raw(),material->Variant.raw(),state.BlendMode);
    resource.Pipeline=MeshRenderer.UploadMaterial(vertexCode,vertex->EntryPoint.raw(),pixelCode,pixel->EntryPoint.raw(),bindings,state);
    try { Materials.emplace(runtime.get(),resource); } catch (...) { MeshRenderer.ReleaseMaterial(resource.Pipeline); throw; }
    if (std::getenv("HELENGINE_LINUX_DIAGNOSTICS") && Materials.size()%1024==0) { std::cout << "material_instances=" << Materials.size() << std::endl; }
    return runtime.release();
}

void VulkanRenderManager3D::InvalidateShaderResources(HeCppString shaderId,::ShaderAsset*) {
    for (const auto& resource : Materials) { if (resource.second.ShaderId==shaderId.raw()) { throw std::runtime_error("Release live Linux materials before invalidating their authored shader pipelines."); } }
}

::RuntimeTexture* VulkanRenderManager3D::ResolveMaterialTexture(::ShaderRuntimeMaterial* material,const std::string& name) const {
    while (material) {
        const int32_t index=material->get_Layout()->FindTextureBindingIndex(name);
        if (index>=0) { auto* texture=material->get_Properties()->GetTexture(index); if (texture) { return texture; } }
        material=dynamic_cast<::ShaderRuntimeMaterial*>(material->get_ParentMaterial());
    }
    return nullptr;
}

void VulkanRenderManager3D::AppendShaderMatrix(std::vector<uint8_t>& output,::float4x4 matrix) {
    const float values[] {matrix.M11.ToSingle(),matrix.M21.ToSingle(),matrix.M31.ToSingle(),matrix.M41.ToSingle(),matrix.M12.ToSingle(),matrix.M22.ToSingle(),matrix.M32.ToSingle(),matrix.M42.ToSingle(),matrix.M13.ToSingle(),matrix.M23.ToSingle(),matrix.M33.ToSingle(),matrix.M43.ToSingle(),matrix.M14.ToSingle(),matrix.M24.ToSingle(),matrix.M34.ToSingle(),matrix.M44.ToSingle()};
    const auto* bytes=reinterpret_cast<const uint8_t*>(values); output.insert(output.end(),bytes,bytes+sizeof(values));
}

std::vector<uint8_t> VulkanRenderManager3D::BuildUniform(::ShaderRuntimeMaterial* material,const VulkanMaterialResource::Binding& binding,::float4x4 world) {
    if (binding.Name=="TransformBuffer") {
        if (binding.Layout.MinimumBytes!=224) { throw std::runtime_error("Linux generated transform upload requires the existing 224-byte TransformBuffer shader contract."); }
        ::float4x4 wvp,normal; ::float4x4::Multiply__ref0_ref1_out2(world,CurrentViewProjection,wvp); ::float4x4::InverseTranspose__ref0_out1(world,normal);
        std::vector<uint8_t> bytes; bytes.reserve(224); AppendShaderMatrix(bytes,world); AppendShaderMatrix(bytes,wvp); AppendShaderMatrix(bytes,normal);
        const float tail[] {CurrentCameraPosition.X.ToSingle(),CurrentCameraPosition.Y.ToSingle(),CurrentCameraPosition.Z.ToSingle(),0,material->get_ReceivesShadows()?1.0f:0.0f,material->get_SupportsEmissive()?1.0f:0.0f,0,0};
        const auto* source=reinterpret_cast<const uint8_t*>(tail); bytes.insert(bytes.end(),source,source+sizeof(tail)); return bytes;
    }
    if (binding.Name=="ForwardLightBuffer") {
        if (binding.Layout.MinimumBytes!=288 || CurrentForwardLights.size()!=288) { throw std::runtime_error("Linux forward lights require the existing 288-byte authored GPU contract."); }
        return CurrentForwardLights;
    }
    if (binding.Name=="ShadowBuffer") {
        if (binding.Layout.MinimumBytes!=400) { throw std::runtime_error("Linux shadow metadata requires the existing 400-byte authored GPU contract."); }
        if (CurrentShadows.size()!=400) { throw std::runtime_error("Linux shadow metadata must be prepared from this actual camera's caster passes."); }
        return CurrentShadows;
    }
    Array<uint8_t>* data=nullptr;
    if (!material->TryResolveConstantBufferData__out1(binding.Name,data) || !data) { throw std::runtime_error("Linux material has no required inherited constant buffer: "+binding.Name); }
    std::unique_ptr<Array<uint8_t>> owned(data);
    if (data->Length<0 || static_cast<size_t>(data->Length)<binding.Layout.MinimumBytes) { throw std::runtime_error("Linux authored material buffer is smaller than its reflected GPU layout: "+binding.Name); }
    return std::vector<uint8_t>(data->Data,data->Data+data->Length);
}
}
