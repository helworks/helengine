#include "vulkan_frame_trace_scope.hpp"
#include "vulkan_render_manager_3d.hpp"
#include "vulkan_renderer_2d.hpp"
#include "vulkan_runtime_model.hpp"
#include "vulkan_mesh_batch_geometry.hpp"
#include <stdexcept>
#include <chrono>
#include <cstdlib>
#include <iostream>
#include <type_traits>
#include <cstring>

namespace helengine::vulkan {
void VulkanRenderManager3D::ConfigureMeshBatching(const VulkanMeshBatchingSettings& settings) {
    settings.Validate();
    const char* gpuPages=std::getenv("HELENGINE_VULKAN_GPU_STATIC_PAGES");
    if(gpuPages && std::strcmp(gpuPages,"0")!=0 && std::strcmp(gpuPages,"1")!=0) throw std::invalid_argument("HELENGINE_VULKAN_GPU_STATIC_PAGES must be 0 or 1.");
    const bool useGpuPages=Renderer.GetContext().IsGraphicsComputeEnabled() && (!gpuPages || std::strcmp(gpuPages,"0")!=0);
    if(Disposed || !Models.empty() || !Materials.empty() || MeshRenderer.HasPendingDraws() || !MeshSubmissions.empty()) throw std::logic_error("Configure Linux mesh batching before uploading models/materials or initializing Core.");
    RetainedRuns.clear();
    Batcher.reset(); BatchingSettings=settings;
    if(!settings.StaticEnabled && !settings.DynamicEnabled) return;
    const VulkanMeshBatcher::Settings config{settings.StaticEnabled,settings.StaticBudgetBytes,settings.DynamicEnabled,settings.DynamicMaxVertices,settings.DynamicBudgetBytes};
    Batcher=std::make_unique<VulkanMeshBatcher>(config,VulkanMeshBatcher::Backend{
        [this](const auto& geometry) {
            if(geometry.BorrowedSource) return MeshRenderer.UploadIndirectPageForMesh(geometry.BorrowedMesh,geometry.BorrowedSource->Vertices,geometry.BorrowedSource->Indices,geometry.Ranges);
            return geometry.Ranges.empty()?MeshRenderer.UploadMesh(geometry.Vertices,geometry.Indices):MeshRenderer.UploadIndirectPage(geometry.Vertices,geometry.Indices,geometry.Ranges);
        },
        [this](size_t mesh,const auto& geometry) { return MeshRenderer.UpdateDynamicMesh(mesh,geometry.Vertices,geometry.Indices); },
        [this](size_t mesh) { MeshRenderer.ReleaseMesh(mesh); },useGpuPages,Renderer.GetContext().GetProperties().limits.maxMemoryAllocationCount/8});
}

void VulkanRenderManager3D::AppendBatchKey(std::string& key,const void* data,size_t size) {
    const uint64_t count=size; key.append(reinterpret_cast<const char*>(&count),sizeof(count));
    if(size) key.append(static_cast<const char*>(data),size);
}

size_t VulkanRenderManager3D::InternBatchPipeline(const VulkanMaterialResource& resource,const std::vector<uint32_t>& vertex,const std::string& vertexEntry,const std::vector<uint32_t>& pixel,const std::string& pixelEntry,VulkanMeshRenderer::MaterialState state) {
    std::string key; AppendBatchKey(key,vertex.data(),vertex.size()*sizeof(uint32_t)); AppendBatchKey(key,vertexEntry.data(),vertexEntry.size()); AppendBatchKey(key,pixel.data(),pixel.size()*sizeof(uint32_t)); AppendBatchKey(key,pixelEntry.data(),pixelEntry.size());
    AppendBatchKey(key,&state.BlendMode,sizeof(state.BlendMode)); AppendBatchKey(key,&state.CullMode,sizeof(state.CullMode)); AppendBatchKey(key,&state.DepthTestEnabled,sizeof(state.DepthTestEnabled)); AppendBatchKey(key,&state.DepthWriteEnabled,sizeof(state.DepthWriteEnabled));
    for(const auto& binding:resource.Bindings) {
        AppendBatchKey(key,binding.Name.data(),binding.Name.size()); const auto& layout=binding.Layout;
        AppendBatchKey(key,&layout.Slot,sizeof(layout.Slot)); AppendBatchKey(key,&layout.Type,sizeof(layout.Type)); AppendBatchKey(key,&layout.Stages,sizeof(layout.Stages)); AppendBatchKey(key,&layout.MinimumBytes,sizeof(layout.MinimumBytes)); AppendBatchKey(key,&layout.Count,sizeof(layout.Count)); AppendBatchKey(key,&layout.Set,sizeof(layout.Set)); AppendBatchKey(key,&layout.ViewType,sizeof(layout.ViewType));
    }
    const auto found=BatchPipelineKeys.find(key); if(found!=BatchPipelineKeys.end()) return found->second;
    const auto identity=BatchPipelineKeys.size()+1; BatchPipelineKeys.emplace(std::move(key),identity); return identity;
}

void VulkanRenderManager3D::QueueMeshSubmission(VulkanRuntimeModel* model,bool isStatic,const std::shared_ptr<const VulkanMeshBatchGeometry>& geometry,size_t pipeline,size_t pipelineKey,bool supported,uint32_t first,uint32_t count,std::vector<VulkanMeshRenderer::BufferBinding> buffers,std::vector<VulkanMeshRenderer::BufferBinding> bakedBuffers,std::vector<VulkanMeshRenderer::ImageBinding> images) {
    if(!Batcher) { MeshRenderer.QueueDraw(model->Mesh,pipeline,buffers,images,first,count); ++LastDrawCalls; return; }
    QueueMeshSubmission(model,isStatic,geometry,VulkanMeshMaterialSnapshot(pipeline,pipelineKey,supported,std::move(buffers),std::move(bakedBuffers),std::move(images)),first,count);
}

void VulkanRenderManager3D::QueueMeshSubmission(VulkanRuntimeModel* model,bool isStatic,const std::shared_ptr<const VulkanMeshBatchGeometry>& geometry,const std::shared_ptr<const VulkanMeshMaterialSnapshot>& material,uint32_t first,uint32_t count) {
    if(!Batcher || !material) throw std::logic_error("Shared mesh submission requires an active batcher and completed material snapshot.");
    auto input=CreateMeshInput(model,isStatic,geometry,material->Supported,first,count);
    input.SharedState={material,&material->State};input.SharedCacheState={material,&material->CacheState};
    MeshSubmissions.push_back(std::make_shared<MeshSubmission>(std::move(input),material));
    MeshSubjects.push_back(VulkanFrameTrace::CurrentSubject());MeshTokens.push_back(0);
}

void VulkanRenderManager3D::QueueMeshSubmission(VulkanRuntimeModel* model,bool isStatic,const std::shared_ptr<const VulkanMeshBatchGeometry>& geometry,VulkanMeshMaterialSnapshot&& material,uint32_t first,uint32_t count) {
    if(!Batcher) throw std::logic_error("Owned mesh submission requires an active batcher.");
    auto input=CreateMeshInput(model,isStatic,geometry,material.Supported,first,count);
    input.State=std::move(material.State);input.CacheState=std::move(material.CacheState);
    MeshSubmissions.push_back(std::make_shared<MeshSubmission>(std::move(input),std::move(material)));
    MeshSubjects.push_back(VulkanFrameTrace::CurrentSubject());MeshTokens.push_back(0);
}

VulkanMeshBatcher::Input VulkanRenderManager3D::CreateMeshInput(VulkanRuntimeModel* model,bool isStatic,const std::shared_ptr<const VulkanMeshBatchGeometry>& geometry,bool supported,uint32_t first,uint32_t count) {
    VulkanMeshBatcher::Input input;input.Mesh=model->Mesh;input.Source=model->BatchSource;input.First=first;input.Count=count;input.Static=isStatic;input.Supported=supported;
    if(supported) { if(!geometry) throw std::logic_error("Supported batching input requires its engine transform snapshot.");input.SharedTransform={geometry,&geometry->Key()};input.Bake=[geometry](const auto& vertex) { return geometry->Bake(vertex); }; }
    return input;
}

void VulkanRenderManager3D::ClearMeshSubmissions() {
    MeshSubmissions.clear();MeshSubjects.clear();MeshTokens.clear();
}

void VulkanRenderManager3D::QueueRetainedMeshSubmission(RetainedRun& run,size_t index,VulkanRuntimeModel* model,::ShaderRuntimeMaterial* material,const std::shared_ptr<const VulkanMeshMaterialSnapshot>& snapshot,const std::shared_ptr<const VulkanMeshBatchGeometry>& geometry,uint32_t first,uint32_t count) {
    auto& previous=run.Submissions.at(index);
    if(previous && previous->SharedMaterial==snapshot && previous->MaterialIdentity==material && previous->Input.Mesh==model->Mesh && previous->Input.Source==model->BatchSource && previous->Input.First==first && previous->Input.Count==count) {
        VulkanFrameTraceScope hit("Batcher.RetainedSubmissionHit");hit.AddCounters(1);
    } else {
        if(NextSubmissionGeneration==UINT64_MAX) throw std::overflow_error("Retained submission identity exhausted.");
        auto input=CreateMeshInput(model,true,geometry,snapshot->Supported,first,count);
        input.SharedState={snapshot,&snapshot->State};input.SharedCacheState={snapshot,&snapshot->CacheState};
        auto replacement=std::make_shared<MeshSubmission>(std::move(input),snapshot);
        replacement->Generation=++NextSubmissionGeneration;replacement->MaterialIdentity=material;
        previous=std::move(replacement);
    }
    MeshSubmissions.push_back(previous);MeshSubjects.push_back(VulkanFrameTrace::CurrentSubject());MeshTokens.push_back(previous->Generation);
}

void VulkanRenderManager3D::FlushMeshSubmissions() {
    static_assert(std::is_nothrow_move_constructible_v<MeshSubmission>,"Mesh submissions must transfer payload ownership without deep copies.");
    if(!Batcher) return;
    const bool traceFlush=std::getenv("HELENGINE_LINUX_FRAME_TIMINGS")!=nullptr;
    const auto cloneStart=traceFlush?std::chrono::steady_clock::now():std::chrono::steady_clock::time_point{};
    VulkanFrameTraceScope transferTrace("Batcher.Transfer");
    std::vector<const VulkanMeshBatcher::Input*> pointers; pointers.reserve(MeshSubmissions.size());
    // Transfer snapshot ownership to this pass; draw routing below only uses the remaining submission bindings and pipeline.
    std::unordered_set<const VulkanMeshMaterialSnapshot*> uniqueShared;size_t payloadBytes=0,materialCount=0;
    for(auto& submission:MeshSubmissions) {
        pointers.push_back(&submission->Input);
        if((traceFlush || VulkanFrameTrace::IsRecording()) && (submission->OwnedMaterial.has_value() || uniqueShared.insert(&submission->GetMaterial()).second)) { ++materialCount;payloadBytes+=submission->GetMaterial().GetPayloadBytes(); }
    }
    const VulkanMeshBatcher::InputView inputs(pointers,MeshSubjects);
    const auto planStart=traceFlush?std::chrono::steady_clock::now():std::chrono::steady_clock::time_point{};
    transferTrace.AddCounters(materialCount,payloadBytes);
    transferTrace.Finish();
    VulkanFrameTraceScope planTrace("Batcher.Plan");
    if(VulkanFrameTrace::IsRecording()) {
        std::vector<int> subjects; subjects.reserve(inputs.size());
        for(size_t index=0;index<inputs.size();++index) subjects.push_back(inputs.Subject(index));
        planTrace.SetContext(-1,VulkanFrameTrace::RegisterBatch(std::move(subjects)));
    }
    const bool retained=std::all_of(MeshTokens.begin(),MeshTokens.end(),[](uint64_t token) { return token!=0; });
    const std::vector<uint64_t> transientTokens;
    const auto& draws=Batcher->BuildRetained(inputs,retained?MeshTokens:transientTokens);
    planTrace.Finish();
    const auto queueStart=traceFlush?std::chrono::steady_clock::now():std::chrono::steady_clock::time_point{};
    VulkanFrameTraceScope queueDrawTrace("Batcher.QueueDraw");
    for(const auto& draw:draws) {
        VulkanFrameTraceScope DrawTrace("Batcher.QueueOneDraw");
        DrawTrace.AddCounters(1);
        if(VulkanFrameTrace::IsRecording()) {
            if(draw.InputCount==1) DrawTrace.SetContext(inputs.Subject(draw.InputIndex));
            else {
                std::vector<int> subjects; subjects.reserve(draw.InputCount);
                for(size_t index=draw.InputIndex;index<draw.InputIndex+draw.InputCount;++index) subjects.push_back(inputs.Subject(index));
                DrawTrace.SetContext(-1,VulkanFrameTrace::RegisterBatch(std::move(subjects)));
            }
        }
        const auto& material=MeshSubmissions.at(draw.InputIndex)->GetMaterial();
        if(MeshRenderer.IsIndirectPage(draw.Mesh)) {
            VulkanFrameTraceScope indirectTrace("Batcher.QueueIndirectPage");indirectTrace.AddCounters(draw.InputCount);
            std::vector<VulkanMeshRenderer::BufferBinding> baked;
            if(draw.Baked) baked=material.CreateBakedBindings();
            const auto& bindings=draw.Baked?baked:material.Buffers;
            const auto transform=std::find_if(bindings.begin(),bindings.end(),[](const auto& binding) { return binding.Slot==0 && binding.Bytes.size()==224; });
            if(transform==bindings.end()) throw std::logic_error("Supported indirect page requires the baked standard TransformBuffer.");
            std::array<float,16> worldViewProjection;std::memcpy(worldViewProjection.data(),transform->Bytes.data()+64,sizeof(worldViewProjection));
            const auto subjects=std::span<const int>(MeshSubjects).subspan(draw.InputIndex,draw.InputCount);
            MeshRenderer.QueueIndirectPage(draw.Mesh,material.Pipeline,bindings,material.Images,worldViewProjection,subjects);
        } else if(draw.Baked && material.SparseBaked) {
            const auto baked=material.CreateBakedBindings();
            MeshRenderer.QueueDraw(draw.Mesh,material.Pipeline,baked,material.Images,draw.First,draw.Count);
        } else {
            MeshRenderer.QueueDraw(draw.Mesh,material.Pipeline,draw.Baked?material.BakedBuffers:material.Buffers,material.Images,draw.First,draw.Count);
        }
        ++LastDrawCalls;
    }
    queueDrawTrace.Finish();
    VulkanFrameTraceScope cleanupTrace("Batcher.Cleanup");
    const auto cleanupStart=traceFlush?std::chrono::steady_clock::now():std::chrono::steady_clock::time_point{};
    const auto inputCount=inputs.size();
    VulkanFrameTraceScope SubmissionClearTrace("Batcher.ReleaseSubmissions");
    SubmissionClearTrace.AddCounters(MeshSubmissions.size());
    ClearMeshSubmissions();
    SubmissionClearTrace.Finish();
    VulkanFrameTraceScope InputClearTrace("Batcher.ReleaseInputs");
    InputClearTrace.AddCounters(inputs.size());
    pointers.clear();
    InputClearTrace.Finish();
    cleanupTrace.Finish();
    if(traceFlush) {
        const auto flushEnd=std::chrono::steady_clock::now();
        std::cout << "flush_inputs=" << inputCount << " flush_draws=" << draws.size()
            << " material_snapshots=" << materialCount << " material_snapshot_reuses=" << (inputCount-materialCount) << " material_snapshot_payload_bytes=" << payloadBytes
            << " flush_input_clone_ms=" << std::chrono::duration<double,std::milli>(planStart-cloneStart).count()
            << " flush_plan_ms=" << std::chrono::duration<double,std::milli>(queueStart-planStart).count()
            << " flush_queue_draw_ms=" << std::chrono::duration<double,std::milli>(cleanupStart-queueStart).count()
            << " flush_cleanup_ms=" << std::chrono::duration<double,std::milli>(flushEnd-cleanupStart).count() << std::endl;
    }
}
}
