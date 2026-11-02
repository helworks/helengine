#include "vulkan_render_manager_3d.hpp"
#include "vulkan_frame_trace_scope.hpp"
#include "vulkan_render_manager_2d.hpp"
#include "vulkan_renderer_2d.hpp"
#include "vulkan_runtime_model.hpp"
#include "vulkan_mesh_batch_geometry.hpp"
#include "ModelSubmeshResolver.hpp"
#include "RuntimeSubmesh.hpp"
#include "generated_disposable_deleter.hpp"
#include "RuntimeMaterial.hpp"
#include "ShaderRuntimeMaterial.hpp"
#include "IDrawable3D.hpp"
#include <numeric>
#include <chrono>
#include <cstdlib>
#include <cstring>
#include <iostream>
#include "Core.hpp"
#include "SceneManager.hpp"
#include "LoadedSceneRecord.hpp"
#include "ObjectManager.hpp"
#include "ICamera.hpp"
#include "Entity.hpp"
#include "ModelAsset.hpp"
#include "IRenderQueue3D.hpp"
#include <algorithm>
#include <stdexcept>

namespace helengine::vulkan {
VulkanRenderManager3D::VulkanRenderManager3D(VulkanRenderer2D& renderer,VulkanRenderManager2D& bridge) : Renderer(renderer), Bridge(bridge), MeshRenderer(renderer.GetContext(),renderer.GetShaderRoot()), NeutralShadowAtlas(MeshRenderer.CreateDepthShadow(1,1)), NeutralPointShadow(MeshRenderer.CreatePointShadow(1)) { }

VulkanRenderManager3D::~VulkanRenderManager3D() {
    MeshRenderer.DiscardFrame(); ClearMeshSubmissions(); RetainedRuns.clear(); Batcher.reset();
    MaterialPreparations.clear();
    for (auto* model : Models) { model->Dispose(); delete model; }
    for (const auto& material : Materials) { material.first->Dispose(); delete material.first; }
}

::RuntimeModel* VulkanRenderManager3D::BuildModelFromRaw(::ModelAsset* data) {
    if (Disposed) { throw std::logic_error("Cannot upload a model through a disposed Linux renderer."); }
    if (!data || !data->Positions || !data->Normals || !data->TexCoords || data->Positions->Length<=0 || data->Positions->Length!=data->Normals->Length || data->Positions->Length!=data->TexCoords->Length) { throw std::invalid_argument("Linux model upload requires matching nonempty position, normal and UV attributes."); }
    std::vector<VulkanMeshRenderer::Vertex> vertices(static_cast<size_t>(data->Positions->Length));
    for (int32_t index=0;index<data->Positions->Length;++index) {
        const auto position=(*data->Positions)[index],normal=(*data->Normals)[index]; const auto uv=(*data->TexCoords)[index];
        vertices[index]={{position.X.ToSingle(),position.Y.ToSingle(),position.Z.ToSingle()},{normal.X.ToSingle(),normal.Y.ToSingle(),normal.Z.ToSingle()},{uv.X.ToSingle(),uv.Y.ToSingle()}};
    }
    std::vector<uint32_t> indices;
    if (data->Indices32 && data->Indices32->Length>0) { indices.assign(data->Indices32->Data,data->Indices32->Data+data->Indices32->Length); }
    else if (data->Indices16 && data->Indices16->Length>0) { indices.assign(data->Indices16->Data,data->Indices16->Data+data->Indices16->Length); }
    else { indices.resize(vertices.size()); std::iota(indices.begin(),indices.end(),0U); }
    std::unique_ptr<Array<::RuntimeSubmesh*>> submeshes(::ModelSubmeshResolver::BuildRuntimeSubmeshes(data));
    std::vector<std::unique_ptr<::RuntimeSubmesh>> temporarySubmeshes;
    for (int32_t index=0;index<submeshes->Length;++index) { temporarySubmeshes.emplace_back((*submeshes)[index]); }
    for (int32_t index=0;index<submeshes->Length;++index) { if ((*submeshes)[index]->get_PrimitiveTopology()!=::ModelPrimitiveTopology::TriangleList) { throw std::runtime_error("Linux indexed model pipeline does not yet support authored line-list topology."); } }
    const size_t handle=MeshRenderer.UploadMesh(vertices,indices);
    GeneratedDisposableOwner<VulkanRuntimeModel> model(new VulkanRuntimeModel(handle));
    try {
    if (Batcher) {
        auto source=std::make_shared<VulkanMeshBatcher::Geometry>(); source->Vertices=std::move(vertices); source->Indices=std::move(indices);
        model->BatchSource=std::move(source);
    }
        model->set_Id(data->get_Id()); model->SetBounds(data->BoundsMin,data->BoundsMax); model->SetSubmeshes(submeshes.get()); for (auto& submesh : temporarySubmeshes) { submesh.release(); } Models.emplace(model.get()); if(model->BatchSource) BatchSourceBytes+=uint64_t(model->BatchSource->Vertices.size())*sizeof(VulkanMeshRenderer::Vertex)+uint64_t(model->BatchSource->Indices.size())*sizeof(uint32_t); }
    catch (...) { MeshRenderer.ReleaseMesh(handle); throw; }
    return model.release();
}

void VulkanRenderManager3D::ReleaseModel(::RuntimeModel* model) {
    auto* native=dynamic_cast<VulkanRuntimeModel*>(model);
    if (!native || !Models.contains(model)) { throw std::invalid_argument("Linux model release requires a live model owned by this renderer."); }
    if (MeshRenderer.HasPendingDraws() || !MeshSubmissions.empty()) { throw std::logic_error("Cannot release a source model while a mesh pass is pending."); }
    RetainedRuns.clear();
    if (Batcher) Batcher->Clear();
    if (native->BatchSource) BatchSourceBytes-=uint64_t(native->BatchSource->Vertices.size())*sizeof(VulkanMeshRenderer::Vertex)+uint64_t(native->BatchSource->Indices.size())*sizeof(uint32_t);
    MeshRenderer.ReleaseMesh(native->Mesh); Models.erase(model); ::RenderManager3D::ReleaseModel(model);
}

void VulkanRenderManager3D::ReleaseMaterial(::RuntimeMaterial* material) {
    if (!material) { throw std::invalid_argument("Linux material release requires a generated runtime material."); }
    if (MeshRenderer.HasPendingDraws() || !MeshSubmissions.empty()) { throw std::logic_error("Cannot release a material while a mesh pass is pending."); }
    RetainedRuns.clear();
    if (Batcher) Batcher->Clear();
    for(auto entry=MaterialPreparations.begin();entry!=MaterialPreparations.end();) {
        if(entry->first==material || entry->second.DependsOn(material)) {
            const auto bytes=entry->second.GetRetainedBytes();
            RootCacheRetainedBytes-=bytes;RootCachePayloadBytes-=entry->second.GetPayloadBytes();if(bytes) --RootCacheEntries;
            entry=MaterialPreparations.erase(entry);
        } else ++entry;
    }
    const auto found=Materials.find(material);
    if (found!=Materials.end()) {
        const auto bytes=found->second.RootProperties.GetRetainedBytes();
        RootCacheRetainedBytes-=bytes;RootCachePayloadBytes-=found->second.RootProperties.GetPayloadBytes();if(bytes) --RootCacheEntries;
        MeshRenderer.ReleaseMaterial(found->second.Pipeline); Materials.erase(found);
    }
    ::RenderManager3D::ReleaseMaterial(material);
}

void VulkanRenderManager3D::Dispose() {
    if (Disposed) { return; }
    MeshRenderer.DiscardFrame(); ClearMeshSubmissions(); RetainedRuns.clear(); Batcher.reset();
    // Discard all dependency retention once before bulk release, avoiding a full cache scan for every root.
    MaterialPreparations.clear();
    while (!Models.empty()) { ReleaseModel(*Models.begin()); }
    while (!Materials.empty()) { ReleaseMaterial(Materials.begin()->first); }
    RootCacheRetainedBytes=0;RootCachePayloadBytes=0;RootCacheEntries=0;
    ::RenderManager3D::Dispose(); Disposed=true;
}

void VulkanRenderManager3D::Visit(::IDrawable3D* drawable) {
    VulkanFrameTraceScope drawableTrace("Drawable.Visit");
    if (!CurrentCamera) { throw std::logic_error("Linux generated draw visitation requires an active camera."); }
    if (!drawable) { return; }
    auto* parent=drawable->get_Parent();
    if (!parent || !parent->get_IsHierarchyEnabled()) { return; }
    auto* sourceModel=drawable->get_Model();
    if (!sourceModel) { return; }
    auto* model=dynamic_cast<VulkanRuntimeModel*>(sourceModel);
    if (!model || !Models.contains(model)) { throw std::runtime_error("Generated drawable references a model owned by another renderer or already released."); }
    auto* submeshes=model->get_Submeshes(); auto* materials=drawable->get_Materials();
    if (!materials || materials->Length<=0 || (materials->Length!=1 && materials->Length<submeshes->Length)) { throw std::runtime_error("Linux drawable requires authored runtime materials for every submesh."); }
    // Capture interface-derived ownership before constructing reusable material payloads.
    const auto world=parent->get_WorldTransformMatrix();
    const auto worldKey=VulkanMatrixKey::Create(world);
    const bool isStatic=parent->get_Static();
    std::array<std::vector<uint8_t>,4> worldTransforms,identityTransforms;
    std::shared_ptr<const VulkanMeshBatchGeometry> batchGeometry;
    VulkanMeshVisitSnapshots visit;
    RetainedRun* retained=nullptr;
    if(Batcher && isStatic) {
        auto& run=RetainedRuns[drawable];
        const auto cameraGeneration=CameraPreparation.Get();
        const auto resourceGeneration=Renderer.GetContext().GetSampledResourceRevision();
        if(run.WorldKey!=worldKey || run.CameraGeneration!=cameraGeneration || run.ResourceGeneration!=resourceGeneration) {
            run.Submissions.clear();run.WorldKey=worldKey;run.CameraGeneration=cameraGeneration;run.ResourceGeneration=resourceGeneration;
        }
        run.Submissions.resize(submeshes->Length);run.Epoch=SubmissionEpoch;retained=&run;
    } else RetainedRuns.erase(drawable);
    bool reuseMaterials=Batcher && materials->Length==1 && submeshes->Length>1;
    if(Batcher && materials->Length>1) {
        std::vector<::RuntimeMaterial*> identities;identities.reserve(submeshes->Length);
        for(int32_t index=0;index<submeshes->Length;++index) identities.push_back((*materials)[index]);
        std::sort(identities.begin(),identities.end(),std::less<::RuntimeMaterial*>{});
        reuseMaterials=std::adjacent_find(identities.begin(),identities.end())!=identities.end();
    }
    for(int32_t index=0;index<submeshes->Length;++index) {
        VulkanFrameTraceScope SubmeshTrace("Drawable.Submesh");
        SubmeshTrace.AddCounters(1);
        auto* material=dynamic_cast<::ShaderRuntimeMaterial*>((*materials)[materials->Length==1?0:index]);
        if(!material) throw std::runtime_error("Vulkan drawable requires a shader-backed material.");
        auto* submesh=(*submeshes)[index];
        if(VulkanFrameTrace::IsRecording()) SubmeshTrace.SetContext(VulkanFrameTrace::RegisterSubject(parent,parent->get_SceneEntityRuntimeId(),model->get_Id().raw(),material->get_Id().raw(),model->Mesh,index,submesh->get_IndexCount()));
        if(retained) {
            const auto snapshot=reuseMaterials?visit.GetOrCreate(material,[&] { return BuildMaterialSnapshot(material,world,worldKey,worldTransforms,identityTransforms); }):BuildMaterialSnapshot(material,world,worldKey,worldTransforms,identityTransforms);
            const auto& previous=retained->Submissions[index];
            const bool unchanged=previous && previous->SharedMaterial==snapshot && previous->MaterialIdentity==material && previous->Input.Mesh==model->Mesh && previous->Input.Source==model->BatchSource && previous->Input.First==submesh->get_IndexStart() && previous->Input.Count==submesh->get_IndexCount();
            if(!unchanged && snapshot->Supported && !batchGeometry) batchGeometry=std::make_shared<VulkanMeshBatchGeometry>(world,worldKey);
            QueueRetainedMeshSubmission(*retained,index,model,material,snapshot,batchGeometry,submesh->get_IndexStart(),submesh->get_IndexCount());
        } else if(reuseMaterials) {
            const auto snapshot=visit.GetOrCreate(material,[&] { return BuildMaterialSnapshot(material,world,worldKey,worldTransforms,identityTransforms); });
            if(snapshot->Supported && !batchGeometry) batchGeometry=std::make_shared<VulkanMeshBatchGeometry>(world,worldKey);
            QueueMeshSubmission(model,isStatic,batchGeometry,snapshot,submesh->get_IndexStart(),submesh->get_IndexCount());
        } else if(Batcher) {
            auto snapshot=BuildMaterialSnapshot(material,world,worldKey,worldTransforms,identityTransforms);
            if(snapshot->Supported && !batchGeometry) batchGeometry=std::make_shared<VulkanMeshBatchGeometry>(world,worldKey);
            QueueMeshSubmission(model,isStatic,batchGeometry,std::move(snapshot),submesh->get_IndexStart(),submesh->get_IndexCount());
        } else {
            const auto snapshot=BuildMaterialSnapshot(material,world,worldKey,worldTransforms,identityTransforms);
            MeshRenderer.QueueDraw(model->Mesh,snapshot->Pipeline,snapshot->Buffers,snapshot->Images,submesh->get_IndexStart(),submesh->get_IndexCount());++LastDrawCalls;
        }
    }
}

void VulkanRenderManager3D::AddWindow(intptr_t handle,int32_t width,int32_t height) {
    if (handle<=0 || width<=0 || height<=0 || TransparentOutputs.contains(handle)) { throw std::invalid_argument("Native output requires a unique positive handle and dimensions."); }
    if (Outputs.empty()) { if (handle!=1) { throw std::invalid_argument("Initial native output handle must be 1."); } Renderer.SelectOutput(1); Renderer.Resize(width,height); }
    else { Renderer.AddOutput(static_cast<size_t>(handle),width,height); }
    ::RenderManager3D::AddWindow(handle,width,height); Outputs.push_back(handle); TransparentOutputs.emplace(handle,false); OutputReadiness.emplace(handle,false); OutputPixels.emplace(handle,std::vector<uint8_t>{});
}

void VulkanRenderManager3D::SetOutputTransparent(intptr_t handle,bool transparent) { TransparentOutputs.at(handle)=transparent; }
void VulkanRenderManager3D::OnWindowResize(intptr_t handle,int32_t width,int32_t height) {
    OutputReadiness.at(handle)=false;
    ::RenderManager3D::OnWindowResize(handle,width,height);
}
bool VulkanRenderManager3D::IsOutputReady(intptr_t handle) const { return OutputReadiness.at(handle); }
void VulkanRenderManager3D::SetReadbackEnabled(bool enabled) {
    ReadbackEnabled=enabled;
    if (!enabled) {
        LastPixels.clear();
        for (auto& output : OutputPixels) { output.second.clear(); }
    }
}

const std::vector<uint8_t>& VulkanRenderManager3D::GetOutputPixels(intptr_t handle) const { return OutputPixels.at(handle); }

bool VulkanRenderManager3D::IsCompletedSceneCamera(::ICamera* camera,const std::unordered_set<::Entity*>& completedRoots) {
    for (auto* entity=camera->get_Parent();entity;entity=entity->get_Parent()) { if (completedRoots.contains(entity)) { return true; } }
    return false;
}

void VulkanRenderManager3D::Draw() {
    const char* geometryDiagnostic=std::getenv("HELENGINE_VULKAN_GEOMETRY_CHECKSUM");
    if(geometryDiagnostic && std::strcmp(geometryDiagnostic,"0")!=0 && std::strcmp(geometryDiagnostic,"1")!=0) throw std::invalid_argument("HELENGINE_VULKAN_GEOMETRY_CHECKSUM must be 0 or 1.");
    const bool measureGeometry=geometryDiagnostic && std::strcmp(geometryDiagnostic,"1")==0;
    if (Disposed || !get_OwnerCore() || !get_OwnerCore()->get_ObjectManager()) { throw std::logic_error("Linux camera dispatch requires initialized generated Core/ObjectManager and a live renderer."); }
    auto* scenes=get_OwnerCore()->get_SceneManager(); auto* loaded=scenes->get_LoadedScenes();
    std::unordered_set<::Entity*> completedRoots;
    if (loaded && !scenes->get_IsSceneTransitionActive() && !scenes->get_HasPendingSceneOperations()) {
        for (int32_t index=0;index<loaded->get_Count();++index) {
            auto* roots=loaded->get_Item(index)->get_RootEntities();
            for (int32_t root=0;roots && root<roots->get_Count();++root) { if (auto* entity=roots->get_Item(root)) { completedRoots.insert(entity); } }
        }
    }
    LastDrawCalls=0; LastVisibleMeshSamples=0; LastVisibleShadowSamples=0; RootCacheHits=0; RootCacheMisses=0;
    if(SubmissionEpoch==UINT64_MAX) throw std::overflow_error("Retained submission frame generation exhausted.");
    ++SubmissionEpoch;
    if(Batcher) Batcher->BeginFrame();
    for (const auto handle : Outputs) {
        Renderer.SelectOutput(static_cast<size_t>(handle)); SelectRenderWindow(handle);
        const auto size=GetWindowSize(handle); Renderer.Resize(static_cast<uint32_t>(size.X),static_cast<uint32_t>(size.Y));
        std::vector<::ICamera*> cameras;
        auto* registered = get_OwnerCore()->get_ObjectManager()->get_Cameras();
        if (registered) { for (int32_t index=0;index<registered->get_Count();++index) { auto* camera = (*registered)[index]; if (camera && camera->get_Parent() && camera->get_Parent()->get_IsHierarchyEnabled() && IsCameraVisibleInWindow(camera,handle)) { cameras.push_back(camera); } } }
        std::stable_sort(cameras.begin(),cameras.end(),[](auto* left,auto* right) { return left->get_CameraDrawOrder() < right->get_CameraDrawOrder(); });
        bool initialized=false,completeSceneCameraRendered=false;
        for (auto* camera : cameras) {
            if (camera->get_RenderTarget()) { throw std::runtime_error("Linux native offscreen camera RenderTarget resources are not implemented."); }
            auto settings=camera->get_ClearSettings(); auto color=settings.ClearColor;
            const float alpha=TransparentOutputs.at(handle)?std::clamp(color.W.ToSingle(),0.0f,1.0f):1.0f;
            const std::array<float,4> clear {color.X.ToSingle()*alpha,color.Y.ToSingle()*alpha,color.Z.ToSingle()*alpha,alpha};
            auto viewport=camera->get_Viewport(); float x=viewport.X.ToSingle(),y=viewport.Y.ToSingle(),w=viewport.Z.ToSingle(),h=viewport.W.ToSingle();
            if (w>0 && h>0 && w<=1 && h<=1) { x*=Renderer.GetWidth(); y*=Renderer.GetHeight(); w*=Renderer.GetWidth(); h*=Renderer.GetHeight(); }
            if (w<=0 || h<=0) { x=y=0; w=Renderer.GetWidth(); h=Renderer.GetHeight(); }
            auto* queue=camera->get_RenderQueue3D(); const bool has3d=queue && queue->get_Count()>0;
            if (has3d) {
                if (!settings.ClearDepthEnabled || settings.ClearDepth!=1.0f) { throw std::runtime_error("Linux 3D camera requires explicit depth clear 1 until authored depth retention is implemented."); }
                auto position=camera->get_Parent()->get_Position(); auto orientation=camera->get_Parent()->get_Orientation();
                auto forward=::float4::RotateVector(::float3(0.0f,0.0f,-1.0f),orientation); auto up=::float4::RotateVector(::float3(0.0f,1.0f,0.0f),orientation); auto target=position+forward;
                ::float4x4 view,projection; ::float4x4::CreateLookAt__ref0_ref1_ref2_out3(position,target,up,view);
                ::float4x4::CreatePerspectiveFieldOfView__out4(camera->get_FieldOfView(),w/h,camera->get_NearPlaneDistance(),camera->get_FarPlaneDistance(),projection);
                ::float4x4::Multiply__ref0_ref1_out2(view,projection,CurrentViewProjection); CurrentCameraPosition=position; CurrentCamera=camera;
                if(std::getenv("HELENGINE_LINUX_DIAGNOSTICS")) {
                    std::vector<uint8_t> cameraBytes; cameraBytes.reserve(64); AppendShaderMatrix(cameraBytes,CurrentViewProjection);
                    uint64_t cameraChecksum=1469598103934665603ULL;
                    for(uint8_t byte:cameraBytes) cameraChecksum=(cameraChecksum^byte)*1099511628211ULL;
                    std::cout << "camera_vp_checksum=" << cameraChecksum << " camera_output=" << handle
                        << " camera_position=" << position.X.ToSingle() << ',' << position.Y.ToSingle() << ',' << position.Z.ToSingle()
                        << " camera_orientation=" << orientation.X.ToSingle() << ',' << orientation.Y.ToSingle() << ',' << orientation.Z.ToSingle() << ',' << orientation.W.ToSingle() << std::endl;
                }
                const bool traceQueue=std::getenv("HELENGINE_LINUX_FRAME_TIMINGS")!=nullptr;
                const auto queueStart=traceQueue?std::chrono::steady_clock::now():std::chrono::steady_clock::time_point{};
                try { VulkanFrameTraceScope prepareTrace("Queue.Prepare");
                    PrepareCameraLights(camera); PrepareCameraShadows(camera); PublishCameraPreparation();
                    prepareTrace.Finish();
                    const auto snapshotStart=traceQueue?std::chrono::steady_clock::now():std::chrono::steady_clock::time_point{};
                    VulkanFrameTraceScope snapshotsTrace("Queue.Snapshots");
                    queue->VisitOrdered(this);
                    snapshotsTrace.Finish();
                    const auto flushStart=traceQueue?std::chrono::steady_clock::now():std::chrono::steady_clock::time_point{};
                    VulkanFrameTraceScope flushTrace("Queue.Flush");
                    FlushMeshSubmissions(); CurrentCamera=nullptr;
                    flushTrace.Finish();
                    if(traceQueue) {
                        const auto queueEnd=std::chrono::steady_clock::now();
                        std::cout << "queue_ms=" << std::chrono::duration<double,std::milli>(queueEnd-queueStart).count()
                            << " queue_prepare_ms=" << std::chrono::duration<double,std::milli>(snapshotStart-queueStart).count()
                            << " queue_snapshot_ms=" << std::chrono::duration<double,std::milli>(flushStart-snapshotStart).count()
                            << " queue_flush_ms=" << std::chrono::duration<double,std::milli>(queueEnd-flushStart).count() << std::endl;
                    }
                    MeshRenderer.RenderFrame(Renderer.GetOutputImageView(),Renderer.GetWidth(),Renderer.GetHeight(),clear,initialized&&!settings.ClearColorEnabled,{{static_cast<int32_t>(x),static_cast<int32_t>(y)},{static_cast<uint32_t>(w),static_cast<uint32_t>(h)}}); }
                catch (...) { CurrentCamera=nullptr; MeshRenderer.DiscardFrame(); ClearMeshSubmissions(); RetainedRuns.clear(); if(Batcher) Batcher->Clear(); throw; }
                LastVisibleMeshSamples+=MeshRenderer.GetLastVisibleSamples();
                Renderer.BeginFrame(clear,true);
            } else { MeshRenderer.RenderFrame(Renderer.GetOutputImageView(),Renderer.GetWidth(),Renderer.GetHeight(),clear,initialized&&!settings.ClearColorEnabled); Renderer.BeginFrame(clear,true); }
            if(measureGeometry) {
                // Explicit diagnostic readback excludes the current camera's variable FPS overlay and never persists pixels.
                const auto geometryPixels=Renderer.EndFrame(true);uint64_t checksum=14695981039346656037ULL;
                for(const uint8_t value:geometryPixels) { checksum^=value;checksum*=1099511628211ULL; }
                std::cout << "geometry_fnv1a64=" << checksum << " geometry_bytes=" << geometryPixels.size() << " output=" << handle << std::endl;
                auto& context=Renderer.GetContext();const auto restore=context.BeginCommands();
                VkImageMemoryBarrier barrier{};barrier.sType=VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER;
                barrier.oldLayout=VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL;barrier.newLayout=VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
                barrier.srcQueueFamilyIndex=VK_QUEUE_FAMILY_IGNORED;barrier.dstQueueFamilyIndex=VK_QUEUE_FAMILY_IGNORED;
                barrier.srcAccessMask=VK_ACCESS_TRANSFER_READ_BIT;barrier.dstAccessMask=VK_ACCESS_COLOR_ATTACHMENT_READ_BIT|VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT;
                barrier.image=Renderer.GetOutputImage();barrier.subresourceRange={VK_IMAGE_ASPECT_COLOR_BIT,0,1,0,1};
                vkCmdPipelineBarrier(restore,VK_PIPELINE_STAGE_TRANSFER_BIT,VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT,0,0,nullptr,0,nullptr,1,&barrier);
                context.SubmitAndWait(restore);Renderer.BeginFrame(clear,true);
            }
            const auto overlayStart=std::chrono::steady_clock::now();
            VulkanFrameTraceScope overlayBuildTrace("Overlay.Build");
            Bridge.RenderCamera(camera); LastDrawCalls+=static_cast<int32_t>(Renderer.GetQueuedDrawCount());
            overlayBuildTrace.Finish();
            LastPixels=Renderer.EndFrame(ReadbackEnabled); initialized=true;
            if(std::getenv("HELENGINE_LINUX_FRAME_TIMINGS")) { std::cout << "overlay_readback_ms=" << std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-overlayStart).count() << std::endl; }
            completeSceneCameraRendered=completeSceneCameraRendered || IsCompletedSceneCamera(camera,completedRoots);
        }
        if (!initialized) { Renderer.BeginFrame({0,0,0,TransparentOutputs.at(handle)?0.0f:1.0f}); LastPixels=Renderer.EndFrame(ReadbackEnabled); }
        OutputPixels.at(handle)=LastPixels;
        OutputReadiness.at(handle)=completeSceneCameraRendered;
    }
    if(Batcher) {
        Batcher->EndFrame();
        for(auto run=RetainedRuns.begin();run!=RetainedRuns.end();) {
            if(run->second.Epoch!=SubmissionEpoch) run=RetainedRuns.erase(run);
            else ++run;
        }
        if(std::getenv("HELENGINE_LINUX_FRAME_TIMINGS")) { const auto& c=Batcher->GetCounters(); std::cout << "batch_inputs=" << c.Inputs << " batch_draws=" << c.Draws << " batch_plan_replays=" << c.PlanReplays << " gpu_static_pages=" << Batcher->GetStaticPageCount() << " batch_coalesced=" << c.Coalesced << " batch_static=" << c.StaticBatches << " batch_hits=" << c.CacheHits << " batch_misses=" << c.CacheMisses << " batch_dynamic=" << c.DynamicBatches << " batch_index_visits=" << c.ReferencedIndexVisits << " batch_upload_bytes=" << c.UploadedBytes << " batch_static_resident_bytes=" << Batcher->GetStaticBytes() << " batch_dynamic_resident_bytes=" << Batcher->GetDynamicBytes() << " batch_source_cpu_bytes=" << BatchSourceBytes << std::endl; }
    }
    if(std::getenv("HELENGINE_LINUX_FRAME_TIMINGS")) {
        std::cout << "gpu_last_pass_commands=" << MeshRenderer.GetLastIndirectCommandCount() << " gpu_last_pass_indirect_calls=" << MeshRenderer.GetLastIndirectCallCount() << " gpu_last_pass_dispatches=" << MeshRenderer.GetLastIndirectDispatchCount() << std::endl;
        std::cout << "root_cache_hits=" << RootCacheHits << " root_cache_misses=" << RootCacheMisses << " root_cache_entries=" << RootCacheEntries
            << " root_cache_retained_bytes=" << RootCacheRetainedBytes << " root_cache_constant_bytes=" << RootCachePayloadBytes << " root_cache_peak_bytes=" << RootCachePeakBytes << std::endl;
    }
    Renderer.SelectOutput(1); SelectRenderWindow(1); LastPixels=OutputPixels.at(1);
}
}
