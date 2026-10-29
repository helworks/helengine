#include "vulkan_render_manager_3d.hpp"
#include "vulkan_runtime_model.hpp"
#include "vulkan_mesh_batch_geometry.hpp"
#include "vulkan_renderer_2d.hpp"
#include "Core.hpp"
#include "ObjectManager.hpp"
#include "Entity.hpp"
#include "IDrawable3D.hpp"
#include "RuntimeMaterial.hpp"
#include "ShaderRuntimeMaterial.hpp"
#include "RuntimeSubmesh.hpp"
#include "ICamera.hpp"
#include "LightComponent.hpp"
#include "DirectionalLightComponent.hpp"
#include "PointLightComponent.hpp"
#include "SpotLightComponent.hpp"
#include "LightDirectionUtility.hpp"
#include "RealMath.hpp"
#include <algorithm>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <numbers>
#include <stdexcept>

namespace helengine::vulkan {
std::vector<uint32_t> VulkanRenderManager3D::LoadBackendShader(const char* name) const {
    const auto path=std::filesystem::path(Renderer.GetShaderRoot())/name;
    std::ifstream input(path,std::ios::binary|std::ios::ate);
    if (!input || input.tellg()<20 || input.tellg()%4) { throw std::runtime_error("Missing or malformed native shadow shader: "+path.string()); }
    std::vector<uint32_t> words(static_cast<size_t>(input.tellg())/4); input.seekg(0);
    if (!input.read(reinterpret_cast<char*>(words.data()),static_cast<std::streamsize>(words.size()*4))) { throw std::runtime_error("Incomplete native shadow shader: "+path.string()); }
    return words;
}

void VulkanRenderManager3D::EnsureShadowPipelines() {
    const VulkanMeshRenderer::MaterialState state {0,2,true,true};
    if (DepthShadowPipeline==SIZE_MAX) {
        const std::array<VulkanMeshRenderer::BindingLayout,1> bindings {{{0,VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,VK_SHADER_STAGE_VERTEX_BIT,80}}};
        DepthShadowPipeline=MeshRenderer.UploadMaterial(LoadBackendShader("shadow_depth.vert.spv"),"VS",LoadBackendShader("shadow_depth.frag.spv"),"PS",bindings,state,VulkanMeshRenderer::PassKind::DepthShadow);
    }
    if (PointShadowPipeline==SIZE_MAX) {
        const std::array<VulkanMeshRenderer::BindingLayout,1> bindings {{{0,VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,VK_SHADER_STAGE_VERTEX_BIT|VK_SHADER_STAGE_FRAGMENT_BIT,144}}};
        PointShadowPipeline=MeshRenderer.UploadMaterial(LoadBackendShader("point_shadow_depth.vert.spv"),"VS",LoadBackendShader("point_shadow_depth.frag.spv"),"PS",bindings,state,VulkanMeshRenderer::PassKind::PointShadow);
    }
}

::float4x4 VulkanRenderManager3D::BuildShadowViewProjection(::ICamera* camera,::LightComponent* light,int face) const {
    if (!camera || !camera->get_Parent() || !light || !light->get_Parent()) { throw std::invalid_argument("Linux shadow transforms require attached camera and light entities."); }
    ::float4x4 view,projection;
    if (auto* point=dynamic_cast<::PointLightComponent*>(light)) {
        if (face<0 || face>=6 || point->get_Range()<=0.1f) { throw std::invalid_argument("Point shadow requires a valid cube face and a range beyond its existing 0.1 near plane."); }
        const std::array<::float3,6> directions={::float3(1,0,0),::float3(-1,0,0),::float3(0,1,0),::float3(0,-1,0),::float3(0,0,1),::float3(0,0,-1)};
        const std::array<::float3,6> ups={::float3(0,-1,0),::float3(0,-1,0),::float3(0,0,1),::float3(0,0,-1),::float3(0,1,0),::float3(0,1,0)};
        auto position=point->get_Parent()->get_Position(),target=position+directions[face],up=ups[face];
        ::float4x4::CreateLookAt__ref0_ref1_ref2_out3(position,target,up,view);
        ::float4x4::CreatePerspectiveFieldOfView__out4(std::numbers::pi_v<float>*0.5f,1.0f,0.1f,point->get_Range(),projection);
    } else if (auto* directional=dynamic_cast<::DirectionalLightComponent*>(light)) {
        const auto rotated=::LightDirectionUtility::GetEntityForwardDirection(light->get_Parent());
        const auto direction=::float3::Normalize(::float3(-rotated.X,-rotated.Y,-rotated.Z));
        const float distance=std::max(1.0f,directional->get_ShadowDistance());
        const auto forward=::float4::RotateVector(::float3(0,0,-1),camera->get_Parent()->get_Orientation());
        auto target=camera->get_Parent()->get_Position()+forward*(distance*0.5f),position=target+direction*distance;
        auto up=::RealMath::Abs(::float3::Dot(direction,::float3(0,1,0)))>0.99f ? ::float3(0,0,1) : ::float3(0,1,0);
        ::float4x4::CreateLookAt__ref0_ref1_ref2_out3(position,target,up,view);
        const float half=distance*0.5f;
        ::float4x4::CreateOrthographicOffCenter__out6(-half,half,-half,half,0.1f,distance*2,projection);
    } else if (auto* spot=dynamic_cast<::SpotLightComponent*>(light)) {
        const auto direction=::float3::Normalize(::LightDirectionUtility::GetEntityForwardDirection(light->get_Parent()));
        auto position=light->get_Parent()->get_Position(),target=position+direction;
        auto up=::RealMath::Abs(::float3::Dot(direction,::float3(0,1,0)))>0.99f ? ::float3(0,0,1) : ::float3(0,1,0);
        ::float4x4::CreateLookAt__ref0_ref1_ref2_out3(position,target,up,view);
        const float fov=std::min(spot->get_OuterConeAngleDegrees()*std::numbers::pi_v<float>/90.0f,std::numbers::pi_v<float>-0.0001f),range=std::max(0.1f,spot->get_Range());
        ::float4x4::CreatePerspectiveFieldOfView__out4(fov,1.0f,0.1f,range+std::max(2.0f,range*0.15f),projection);
    } else { throw std::invalid_argument("Linux shadow transform received an unsupported authored light kind."); }
    ::float4x4 result; ::float4x4::Multiply__ref0_ref1_out2(view,projection,result); return result;
}

void VulkanRenderManager3D::QueueShadowCasters(::LightComponent* light,::float4x4 viewProjection) {
    auto* drawables=get_OwnerCore()->get_ObjectManager()->get_Drawables3D(); const bool point=light->get_LightType()==::LightType::Point;
    for (int32_t index=0;drawables && index<drawables->get_Count();++index) {
        auto* drawable=(*drawables)[index];
        if (!drawable || !drawable->get_Parent() || !drawable->get_Parent()->get_IsHierarchyEnabled() || drawable->get_Parent()->get_IsRenderHierarchySuppressed()) { continue; }
        if (!(drawable->get_Parent()->get_LayerMask()&light->get_Parent()->get_LayerMask()&CurrentCamera->get_LayerMask())) { continue; }
        auto* model=dynamic_cast<VulkanRuntimeModel*>(drawable->get_Model());
        if (!model || !Models.contains(model)) { throw std::invalid_argument("Linux shadow caster references a foreign or released native model."); }
        auto* submeshes=model->get_Submeshes(); auto* materials=drawable->get_Materials();
        if (!materials || materials->Length<=0 || (materials->Length!=1 && materials->Length<submeshes->Length)) { throw std::invalid_argument("Linux shadow caster requires its authored submesh materials."); }
        auto world=drawable->get_Parent()->get_WorldTransformMatrix(); ::float4x4 transform; ::float4x4::Multiply__ref0_ref1_out2(world,viewProjection,transform);
        std::shared_ptr<const VulkanMeshBatchGeometry> batchGeometry;
        for (int32_t submeshIndex=0;submeshIndex<submeshes->Length;++submeshIndex) {
            auto* material=(*materials)[materials->Length==1?0:submeshIndex]; if (!material) { throw std::invalid_argument("Linux shadow caster material is null."); } if (!material->get_CastsShadows()) { continue; }
            std::vector<uint8_t> bytes;
            if (point) { AppendShaderMatrix(bytes,world); }
            AppendShaderMatrix(bytes,transform);
            if (point) { auto* pointLight=static_cast<::PointLightComponent*>(light); const auto position=pointLight->get_Parent()->get_Position(); const float values[] {position.X.ToSingle(),position.Y.ToSingle(),position.Z.ToSingle(),pointLight->get_Range()}; const auto* source=reinterpret_cast<const uint8_t*>(values); bytes.insert(bytes.end(),source,source+sizeof(values)); }
            else { bytes.resize(80,0); }
            std::vector<VulkanMeshRenderer::BufferBinding> bindings {{0,std::move(bytes)}},bakedBindings;
            if(Batcher) {
                std::vector<uint8_t> baked;
                if(point) AppendShaderMatrix(baked,::float4x4::get_Identity());
                AppendShaderMatrix(baked,viewProjection);
                if(point) { auto* pointLight=static_cast<::PointLightComponent*>(light); const auto position=pointLight->get_Parent()->get_Position(); const float values[] {position.X.ToSingle(),position.Y.ToSingle(),position.Z.ToSingle(),pointLight->get_Range()}; const auto* source=reinterpret_cast<const uint8_t*>(values); baked.insert(baked.end(),source,source+sizeof(values)); }
                else { baked.resize(80,0); }
                bakedBindings.push_back({0,std::move(baked)});
            }
            bool supported=false;
            if(auto* shader=dynamic_cast<::ShaderRuntimeMaterial*>(material)) { const auto found=Materials.find(VulkanInheritedMaterialCache::ResolveRoot(shader)); supported=found!=Materials.end() && found->second.SupportsBatching && !shader->get_SupportsNormalMapping(); }
            if(Batcher && supported && !batchGeometry) batchGeometry=std::make_shared<VulkanMeshBatchGeometry>(world);
            const size_t pipeline=point?PointShadowPipeline:DepthShadowPipeline;
            const size_t pipelineKey=(size_t(1)<<(sizeof(size_t)*8-1))|(point?2:1);
            auto* submesh=(*submeshes)[submeshIndex]; QueueMeshSubmission(model,drawable->get_Parent()->get_Static(),batchGeometry,pipeline,pipelineKey,supported,submesh->get_IndexStart(),submesh->get_IndexCount(),std::move(bindings),std::move(bakedBindings),{});
        }
    }
    FlushMeshSubmissions();
}

void VulkanRenderManager3D::PrepareCameraShadows(::ICamera* camera) {
    CurrentShadowAtlas=NeutralShadowAtlas; CurrentPointShadows.fill(NeutralPointShadow); std::array<float,100> values {};
    std::vector<size_t> selected; size_t nonPoint=0;
    if (get_ShadowsEnabled()) { for (size_t index=0;index<CurrentLights.size();++index) { auto* light=CurrentLights[index]; if (light->get_ShadowsEnabled() && light->get_ShadowStrength()>0 && light->get_ShadowMapMode()!=::ShadowMapMode::Disabled) { selected.push_back(index); if (light->get_LightType()!=::LightType::Point) { ++nonPoint; } } } }
    if (!selected.empty()) { EnsureShadowPipelines(); }
    const uint32_t grid=nonPoint>1?2:1,tile=2048/grid;
    values[0]=nonPoint?1.0f:0.0f; values[1]=values[2]=1.0f/2048; values[3]=static_cast<float>(nonPoint);
    if (nonPoint) { if (ShadowAtlas==SIZE_MAX) { ShadowAtlas=MeshRenderer.CreateDepthShadow(2048,2048); } CurrentShadowAtlas=ShadowAtlas; MeshRenderer.RenderShadowPass(ShadowAtlas); }
    size_t atlasIndex=0,pointIndex=0;
    for (size_t index : selected) {
        auto* light=CurrentLights[index]; const size_t offset=4+index*24; values[offset+4]=1; values[offset+5]=light->get_ShadowStrength();
        if (light->get_LightType()==::LightType::Point) {
            if (PointShadows[pointIndex]==SIZE_MAX) { PointShadows[pointIndex]=MeshRenderer.CreatePointShadow(512); }
            CurrentPointShadows[pointIndex]=PointShadows[pointIndex]; values[offset+6]=2; values[offset+7]=static_cast<float>(pointIndex);
            for (int face=0;face<6;++face) { const auto projection=BuildShadowViewProjection(camera,light,face); QueueShadowCasters(light,projection); MeshRenderer.RenderShadowPass(PointShadows[pointIndex],face,true); LastVisibleShadowSamples+=MeshRenderer.GetLastShadowSamples(); }
            ++pointIndex;
        } else {
            const VkRect2D region {{static_cast<int32_t>(atlasIndex%grid)*static_cast<int32_t>(tile),static_cast<int32_t>(atlasIndex/grid)*static_cast<int32_t>(tile)},{tile,tile}};
            values[offset]=static_cast<float>(region.offset.x)/2048; values[offset+1]=static_cast<float>(region.offset.y)/2048; values[offset+2]=values[offset+3]=static_cast<float>(tile)/2048; values[offset+6]=1;
            const auto projection=BuildShadowViewProjection(camera,light,-1); std::vector<uint8_t> matrix; AppendShaderMatrix(matrix,projection); std::memcpy(values.data()+offset+8,matrix.data(),64);
            QueueShadowCasters(light,projection); MeshRenderer.RenderShadowPass(ShadowAtlas,0,false,region); LastVisibleShadowSamples+=MeshRenderer.GetLastShadowSamples(); ++atlasIndex;
        }
    }
    CurrentShadows.resize(sizeof(values)); std::memcpy(CurrentShadows.data(),values.data(),sizeof(values));
}
}
