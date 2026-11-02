#pragma once
#include "vulkan_inherited_material_cache.hpp"
#include "RenderManager3D.hpp"
#include "IRenderVisitor3D.hpp"
#include "IShaderRenderManager3D.hpp"
#include "vulkan_material_resource.hpp"
#include "vulkan_material_texture_cache.hpp"
#include "vulkan_mesh_batcher.hpp"
#include "vulkan_retained_submission_run.hpp"
#include "vulkan_mesh_visit_snapshots.hpp"
#include <array>
#include <optional>
#include "vulkan_mesh_batching_settings.hpp"
#include "float4x4.hpp"
#include "vulkan_preparation_generation.hpp"
#include "float3.hpp"
#include "ShaderStage.hpp"
#include <unordered_map>
#include <unordered_set>
#include <vector>

class ShaderProgramAsset;
class ShaderRuntimeMaterial;
class RuntimeTexture;
class LightComponent;
class Entity;

namespace helengine::vulkan {
class VulkanRenderer2D;
class VulkanRenderManager2D;
class VulkanRuntimeModel;
class VulkanMeshBatchGeometry;
/// <summary>Executes generated ordered camera, mesh/material, shadow and 2D command contracts on one native Vulkan device.</summary>
class VulkanRenderManager3D final : public ::RenderManager3D, public ::IRenderVisitor3D, public ::IShaderRenderManager3D {
public:
    /// <summary>Borrows the same-device renderer and generated 2D bridge used by each camera.</summary>
    VulkanRenderManager3D(VulkanRenderer2D& renderer,VulkanRenderManager2D& bridge);
    /// <summary>Releases still-owned native models and material wrappers before the borrowed graphics owner is destroyed.</summary>
    ~VulkanRenderManager3D() override;
    /// <summary>Configures validated static/dynamic limits before any core initialization uploads; changing live configuration is rejected.</summary>
    void ConfigureMeshBatching(const VulkanMeshBatchingSettings& settings);
    /// <summary>Converts configurable vertex values only at the fixed GPU32 boundary and preserves authored model identity, bounds and submesh ranges.</summary>
    ::RuntimeModel* BuildModelFromRaw(::ModelAsset* data) override;
    /// <summary>Loads through the generated public content contracts and shares only immutable imported material textures by source and cooked path.</summary>
    ::RuntimeMaterial* BuildMaterialFromRawAsset(::ContentManager* content,HeCppString path) override;
    /// <summary>Builds the generated material layout and immutable native pipeline from authored program binaries and reflected binding metadata.</summary>
    ::RuntimeMaterial* BuildMaterialFromRaw(::ShaderMaterialAsset* material,::ShaderAsset* shader) override;
    /// <summary>Selects the existing cooked Vulkan shader package without instantiating any managed GPU backend.</summary>
    ::ShaderCompileTarget get_ShaderCompileTarget() override;
    /// <summary>Rejects invalidation while a live native material still owns a pipeline from the shader.</summary>
    void InvalidateShaderResources(HeCppString shaderId,::ShaderAsset* shader) override;
    /// <summary>Releases native mesh memory and the generated model's copied submesh ownership.</summary>
    void ReleaseModel(::RuntimeModel* model) override;
    /// <summary>Releases the native root pipeline before disposing generated inherited material metadata.</summary>
    void ReleaseMaterial(::RuntimeMaterial* material) override;
    /// <summary>Releases all native resources after discarding unsent draw packets, with idempotent explicit shutdown.</summary>
    void Dispose() override;
    /// <summary>Queues the drawable's authored material/submesh pairs for the active generated camera.</summary>
    void Visit(::IDrawable3D* drawable) override;
    /// <summary>Visits active cameras in authored order, renders generated 2D commands and synchronizes one complete frame.</summary>
    void Draw() override;
    /// <summary>Registers one generated window and its same-device target before Core initialization.</summary>
    void AddWindow(intptr_t handle,int32_t width,int32_t height) override;
    /// <summary>Specifies whether a registered output preserves transparent clear alpha.</summary>
    void SetOutputTransparent(intptr_t handle,bool transparent);
    /// <summary>Revokes only the resized output readiness before updating its generated layout dimensions.</summary>
    void OnWindowResize(intptr_t handle,int32_t width,int32_t height) override;
    /// <summary>Reports complete scene-camera work for the selected output in the most recent draw.</summary>
    bool IsOutputReady(intptr_t handle) const;
    /// <summary>Borrows the completed GPU readback for a registered output, or an empty vector while capture is disabled.</summary>
    const std::vector<uint8_t>& GetOutputPixels(intptr_t handle) const;
    /// <summary>Controls CPU pixel capture, enabled by default; disabling immediately invalidates retained readbacks for all outputs.</summary>
    void SetReadbackEnabled(bool enabled);
    /// <summary>Reports real submitted draw calls for menu/core smoke tests and generated diagnostics.</summary>
    int32_t get_LastDrawCallCount() override { return LastDrawCalls; }
    /// <summary>Returns the latest actual GPU readback, or an empty vector while capture is disabled.</summary>
    const std::vector<uint8_t>& GetLastPixels() const { return LastPixels; }
    /// <summary>Reports actual Vulkan occlusion samples from generated mesh passes, excluding all UI draws.</summary>
    uint64_t GetLastVisibleMeshSamples() const { return LastVisibleMeshSamples; }
    /// <summary>Reports actual depth-tested caster samples from all shadow atlas tiles and cube faces in the latest frame.</summary>
    uint64_t GetLastVisibleShadowSamples() const { return LastVisibleShadowSamples; }
    /// <summary>Reports successful persistent root/inherited-property reuse in the latest complete Core draw.</summary>
    uint64_t GetRootMaterialCacheHits() const { return RootCacheHits; }
    /// <summary>Reports root/inherited-property rebuilds attempted in the latest complete Core draw.</summary>
    uint64_t GetRootMaterialCacheMisses() const { return RootCacheMisses; }
    /// <summary>Reports retained logical cache bytes, excluding allocator slack and unrelated snapshots/GPU storage.</summary>
    uint64_t GetRootMaterialCacheRetainedBytes() const { return RootCacheRetainedBytes; }
private:
    /// <summary>Owns an immutable material/resource snapshot for one already-visible color or shadow input.</summary>
    struct MeshSubmission {
        /// <summary>Retains the source, eligibility, actual compatibility and transform snapshot.</summary>
        VulkanMeshBatcher::Input Input;
        /// <summary>Identifies immutable retained input contents; zero denotes a transient submission.</summary>
        uint64_t Generation=0;
        /// <summary>Compares the current live material assignment without reading historical material pointers.</summary>
        const void* MaterialIdentity=nullptr;
        /// <summary>Retains a unique payload inline to avoid shared-control allocation when no material reference repeats.</summary>
        std::optional<VulkanMeshMaterialSnapshot> OwnedMaterial;
        /// <summary>Retains immutable shared bindings only when a visit actually repeats a material reference.</summary>
        std::shared_ptr<const VulkanMeshMaterialSnapshot> SharedMaterial;
        /// <summary>Publishes one unique input with moved compatibility keys and payload ownership.</summary>
        MeshSubmission(VulkanMeshBatcher::Input&& input,VulkanMeshMaterialSnapshot&& material):Input(std::move(input)),OwnedMaterial(std::move(material)) { }
        /// <summary>Publishes one input borrowing a const shared payload retained by other submeshes in this visit.</summary>
        MeshSubmission(VulkanMeshBatcher::Input&& input,const std::shared_ptr<const VulkanMeshMaterialSnapshot>& material):Input(std::move(input)),SharedMaterial(material) { }
        /// <summary>Returns the completed payload retained through either of the explicit ownership paths.</summary>
        const VulkanMeshMaterialSnapshot& GetMaterial() const { return SharedMaterial?*SharedMaterial:OwnedMaterial.value(); }
    };
    /// <summary>Queues an original draw directly when disabled or snapshots an ordered input for the active batcher.</summary>
    void QueueMeshSubmission(VulkanRuntimeModel* model,bool isStatic,const std::shared_ptr<const VulkanMeshBatchGeometry>& geometry,size_t pipeline,size_t pipelineKey,bool supported,uint32_t first,uint32_t count,std::vector<VulkanMeshRenderer::BufferBinding> buffers,std::vector<VulkanMeshRenderer::BufferBinding> bakedBuffers,std::vector<VulkanMeshRenderer::ImageBinding> images);
    /// <summary>Queues one original input while sharing its already-completed immutable material payload.</summary>
    void QueueMeshSubmission(VulkanRuntimeModel* model,bool isStatic,const std::shared_ptr<const VulkanMeshBatchGeometry>& geometry,const std::shared_ptr<const VulkanMeshMaterialSnapshot>& material,uint32_t first,uint32_t count);
    /// <summary>Moves unique material payloads and keys into one original input without shared-control or key-copy overhead.</summary>
    void QueueMeshSubmission(VulkanRuntimeModel* model,bool isStatic,const std::shared_ptr<const VulkanMeshBatchGeometry>& geometry,VulkanMeshMaterialSnapshot&& material,uint32_t first,uint32_t count);
    /// <summary>Captures original source ranges and transforms identically for unique and shared material payloads.</summary>
    static VulkanMeshBatcher::Input CreateMeshInput(VulkanRuntimeModel* model,bool isStatic,const std::shared_ptr<const VulkanMeshBatchGeometry>& geometry,bool supported,uint32_t first,uint32_t count);
    /// <summary>Reuses independently retained root and inherited preparation after validating the complete live ancestor chain.</summary>
    std::shared_ptr<const VulkanMeshMaterialSnapshot> BuildMaterialSnapshot(::ShaderRuntimeMaterial* material,::float4x4 world,const std::string& worldKey,std::array<std::vector<uint8_t>,4>& worldTransforms,std::array<std::vector<uint8_t>,4>& identityTransforms);
    /// <summary>Resolves current shadow and sampled descriptors, validating borrowed textures against the owning renderer on every visit.</summary>
    VkDescriptorImageInfo ResolveSnapshotImage(::ShaderRuntimeMaterial* material,const VulkanMaterialResource::Binding& binding,const VulkanRootMaterialCache::Payload* properties);
    /// <summary>Converts each ordered CPU output into exactly one validated Vulkan indexed draw, then counts those actual packets.</summary>
    void FlushMeshSubmissions();
    /// <summary>Appends exact binary fields to unambiguous material/pipeline cache identities.</summary>
    static void AppendBatchKey(std::string& key,const void* data,size_t size);
    /// <summary>Interns complete pipeline contracts using exact key equality, including reflection names.</summary>
    size_t InternBatchPipeline(const VulkanMaterialResource& resource,const std::vector<uint32_t>& vertex,const std::string& vertexEntry,const std::vector<uint32_t>& pixel,const std::string& pixelEntry,VulkanMeshRenderer::MaterialState state);
    /// <summary>Retains validated opt-in settings for upload-time source ownership.</summary>
    VulkanMeshBatchingSettings BatchingSettings;
    /// <summary>Retains only this pass's already-visible inputs, never a scene-wide visibility expansion.</summary>
    std::vector<std::shared_ptr<const MeshSubmission>> MeshSubmissions;
    /// <summary>Stores profiler subjects only for the current ordered pass, never in retained submissions.</summary>
    std::vector<int> MeshSubjects;
    /// <summary>Stores immutable submission generations in pass order; zero marks an ordinary transient input.</summary>
    std::vector<uint64_t> MeshTokens;
    /// <summary>Owns the latest fully validated static submission slots for one drawable identity.</summary>
    using RetainedRun=VulkanRetainedSubmissionRun<MeshSubmission>;
    /// <summary>Bounds retained CPU data to drawable runs visited in the current frame.</summary>
    std::unordered_map<const void*,RetainedRun> RetainedRuns;
    /// <summary>Provides exact never-recycled identity to each successfully constructed immutable submission.</summary>
    uint64_t NextSubmissionGeneration=0;
    /// <summary>Identifies the current frame across all camera passes.</summary>
    uint64_t SubmissionEpoch=0;
    /// <summary>Discards frame references and their current profiler/signature metadata together.</summary>
    void ClearMeshSubmissions();
    /// <summary>Reuses a validated immutable static slot or publishes its complete replacement before enqueuing it.</summary>
    void QueueRetainedMeshSubmission(RetainedRun& run,size_t index,VulkanRuntimeModel* model,::ShaderRuntimeMaterial* material,const std::shared_ptr<const VulkanMeshMaterialSnapshot>& snapshot,const std::shared_ptr<const VulkanMeshBatchGeometry>& geometry,uint32_t first,uint32_t count);
    /// <summary>Interns immutable exact shader/state contracts across repeated independent runtime roots.</summary>
    std::unordered_map<std::string,size_t> BatchPipelineKeys;
    /// <summary>Counts root/inherited cache hits in the latest complete draw across its cameras and outputs.</summary>
    uint64_t RootCacheHits=0;
    /// <summary>Counts attempted root/inherited-property rebuilds in the latest complete draw.</summary>
    uint64_t RootCacheMisses=0;
    /// <summary>Counts root and inherited materials currently owning persistent CPU property copies.</summary>
    uint64_t RootCacheEntries=0;
    /// <summary>Counts retained logical entry/signature/property bytes, excluding allocator slack/metadata.</summary>
    uint64_t RootCacheRetainedBytes=0;
    /// <summary>Counts only private constant bytes retained for root and inherited materials.</summary>
    uint64_t RootCachePayloadBytes=0;
    /// <summary>Tracks peak logical cache bytes including an old entry and its replacement during publication.</summary>
    uint64_t RootCachePeakBytes=0;
    /// <summary>Counts logical immutable source CPU bytes, separately from additional GPU geometry residency.</summary>
    uint64_t BatchSourceBytes=0;
    /// <summary>Requires a rendered camera to descend from an actual completed scene root rather than an untracked bootstrap/loading entity.</summary>
    static bool IsCompletedSceneCamera(::ICamera* camera,const std::unordered_set<::Entity*>& completedRoots);
    /// <summary>Stores output handles in stable registration and presentation order.</summary>
    std::vector<intptr_t> Outputs;
    /// <summary>Preserves the requested alpha policy separately from native window focus/input policy.</summary>
    std::unordered_map<intptr_t,bool> TransparentOutputs;
    /// <summary>Retains completed camera readiness independently for each output.</summary>
    std::unordered_map<intptr_t,bool> OutputReadiness;
    /// <summary>Retains actual synchronized GPU bytes for each completed output.</summary>
    std::unordered_map<intptr_t,std::vector<uint8_t>> OutputPixels;
    /// <summary>Finds the exact authored program and stage rather than substituting a default shader.</summary>
    static ::ShaderProgramAsset* FindProgram(::ShaderAsset* shader,HeCppString name,::ShaderStage stage);
    /// <summary>Copies the exact target/program/stage/variant binary into aligned SPIR-V words.</summary>
    static std::vector<uint32_t> ReadShader(::ShaderAsset* shader,HeCppString name,::ShaderStage stage,HeCppString variant);
    /// <summary>Combines matching per-stage bindings while preserving resource names, sets, unified slots and reflected byte sizes.</summary>
    static void AddBindings(VulkanMaterialResource& resource,::ShaderProgramAsset* program,VkShaderStageFlags stage);
    /// <summary>Acquires an authored imported texture from the material-only residency cache and retains its ownership on this independent native material.</summary>
    void ApplyImportedMaterialTexture(::ContentManager* content,HeCppString assetId,::ShaderRuntimeMaterial* material,HeCppString bindingName);
    /// <summary>Resolves a material texture through generated parent inheritance without inventing an image for absent required bindings.</summary>
    ::RuntimeTexture* ResolveMaterialTexture(::ShaderRuntimeMaterial* material,const std::string& name) const;
    /// <summary>Identifies authored property constants while leaving every camera/light/shadow/transform uniform live.</summary>
    static bool IsMaterialConstant(const VulkanMaterialResource::Binding& binding);
    /// <summary>Checks every ancestor identity/revision and exact live ordered binding configuration without resolving property values.</summary>
    const VulkanRootMaterialCache::Payload* FindRootMaterialProperties(::ShaderRuntimeMaterial* material,VulkanInheritedMaterialCache& cache);
    /// <summary>Publishes a complete resolved material ancestry and accounts for simultaneous old/new CPU property copies.</summary>
    void PublishRootMaterialProperties(::ShaderRuntimeMaterial* material,VulkanInheritedMaterialCache& cache,VulkanRootMaterialCache::Payload values);
    /// <summary>Checks root identity/revisions and live ordered binding configuration without resolving inherited properties.</summary>
    const VulkanRootMaterialCache::Payload* FindRootMaterialProperties(::ShaderRuntimeMaterial* material,VulkanMaterialResource& resource,VulkanRootMaterialCache::Identity& identity);
    /// <summary>Publishes only a completely resolved successful root visit and accounts for simultaneous old/new CPU copies.</summary>
    void PublishRootMaterialProperties(::ShaderRuntimeMaterial* material,VulkanMaterialResource& resource,const VulkanRootMaterialCache::Identity& identity,VulkanRootMaterialCache::Payload values);
    /// <summary>Copies a generated row-vector matrix into the column-major fixed GPU32 shader storage boundary.</summary>
    static void AppendShaderMatrix(std::vector<uint8_t>& output,::float4x4 matrix);
    /// <summary>Builds one immutable reflected uniform payload from generated transforms or authored material property bytes.</summary>
    std::vector<uint8_t> BuildUniform(::ShaderRuntimeMaterial* material,const VulkanMaterialResource::Binding& binding,::float4x4 world);
    /// <summary>Snapshots hierarchy-enabled authored lights visible to the active camera into the existing fixed 288-byte forward-light shader contract.</summary>
    void PrepareCameraLights(::ICamera* camera);
    /// <summary>Ports the existing backend directional/spot/point projection formulas using generated engine math and unchanged shader storage conventions.</summary>
    ::float4x4 BuildShadowViewProjection(::ICamera* camera,::LightComponent* light,int face) const;
    /// <summary>Queues actual hierarchy/layer-visible, material-authorized shadow casters for one light projection.</summary>
    void QueueShadowCasters(::LightComponent* light,::float4x4 viewProjection);
    /// <summary>Renders the selected lights' atlas tiles or six radial cube faces before publishing the existing 400-byte shadow shader metadata.</summary>
    void PrepareCameraShadows(::ICamera* camera);
    /// <summary>Loads aligned backend-internal SPIR-V from the explicit packaged shader directory.</summary>
    std::vector<uint32_t> LoadBackendShader(const char* name) const;
    /// <summary>Creates only the exact existing 80/144-byte builtin shadow pipelines on first actual shadow use.</summary>
    void EnsureShadowPipelines();
    /// <summary>Borrows the native Vulkan renderer.</summary>
    VulkanRenderer2D& Renderer;
    /// <summary>Borrows the generated 2D command bridge.</summary>
    VulkanRenderManager2D& Bridge;
    /// <summary>Owns indexed geometry and authored Vulkan pipelines on the same device as the UI target.</summary>
    VulkanMeshRenderer MeshRenderer;
    /// <summary>Owns bounded persistent and reusable transient batch handles; destroyed before the native mesh renderer.</summary>
    std::unique_ptr<VulkanMeshBatcher> Batcher;
    /// <summary>Owns a depth-one atlas descriptor for shader declarations when shadow metadata explicitly disables sampling.</summary>
    size_t NeutralShadowAtlas;
    /// <summary>Owns a genuine six-face radial-depth-one cube for disabled point-shadow declarations; active shadows require caster passes.</summary>
    size_t NeutralPointShadow;
    /// <summary>Owns the actual 2048-square directional/spot shadow atlas, allocated only when used.</summary>
    size_t ShadowAtlas=SIZE_MAX;
    /// <summary>Owns up to four actual 512-square six-face point-shadow resources.</summary>
    std::array<size_t,4> PointShadows {SIZE_MAX,SIZE_MAX,SIZE_MAX,SIZE_MAX};
    /// <summary>Selects the actual or explicitly disabled atlas descriptor for the active camera.</summary>
    size_t CurrentShadowAtlas=SIZE_MAX;
    /// <summary>Selects actual or explicitly disabled cube descriptors for the active camera.</summary>
    std::array<size_t,4> CurrentPointShadows {SIZE_MAX,SIZE_MAX,SIZE_MAX,SIZE_MAX};
    /// <summary>Owns the existing projected-depth builtin pipeline with its fixed 80-byte uniform contract.</summary>
    size_t DepthShadowPipeline=SIZE_MAX;
    /// <summary>Owns the existing radial-depth builtin pipeline with its fixed 144-byte uniform contract.</summary>
    size_t PointShadowPipeline=SIZE_MAX;
    /// <summary>Tracks generated model wrappers exclusively owned by this bridge.</summary>
    std::unordered_set<::RuntimeModel*> Models;
    /// <summary>Indexes immutable imported material textures weakly; public raw uploads and mutable atlases remain independently owned.</summary>
    VulkanMaterialTextureCache<::RuntimeTexture> MaterialTextures;
    /// <summary>Maps root material wrapper identity to its immutable native shader resource.</summary>
    std::unordered_map<::RuntimeMaterial*,VulkanMaterialResource> Materials;
    /// <summary>Retains inherited material preparation separately; roots keep their allocation-free inline resource cache.</summary>
    std::unordered_map<::RuntimeMaterial*,VulkanInheritedMaterialCache> MaterialPreparations;
    /// <summary>Borrows the camera only while synchronously visiting its ordered draw queue.</summary>
    ::ICamera* CurrentCamera=nullptr;
    /// <summary>Retains the generated view-projection matrix without changing the engine's selected arithmetic policy.</summary>
    ::float4x4 CurrentViewProjection;
    /// <summary>Retains the current authored camera position for the fixed GPU shader constants.</summary>
    ::float3 CurrentCameraPosition;
    /// <summary>Stores fixed GPU32 ambient and four forward light slots for the current camera only.</summary>
    std::vector<uint8_t> CurrentForwardLights;
    /// <summary>Borrows the selected four nonambient lights in the same order as the current forward shader slots.</summary>
    std::vector<::LightComponent*> CurrentLights;
    /// <summary>Owns the current camera's fixed GPU32 atlas/cube metadata and matrix bytes.</summary>
    std::vector<uint8_t> CurrentShadows;
    /// <summary>Publishes exact native camera values, uniform bytes and selected shadow identities after successful preparation.</summary>
    void PublishCameraPreparation();
    /// <summary>Versions the current successful camera/light/shadow dependency state once per camera visit.</summary>
    VulkanPreparationGeneration CameraPreparation;
    /// <summary>Prevents uploads after explicit renderer shutdown.</summary>
    bool Disposed=false;
    /// <summary>Stores the actual latest offscreen framebuffer bytes for isolated verification.</summary>
    std::vector<uint8_t> LastPixels;
    /// <summary>Keeps existing callers on readback unless the host explicitly requests GPU-only presentation.</summary>
    bool ReadbackEnabled=true;
    /// <summary>Stores the submitted count from the previous completed frame.</summary>
    int32_t LastDrawCalls = 0;
    /// <summary>Accumulates the hardware visibility results of all cameras' latest completed color mesh passes.</summary>
    uint64_t LastVisibleMeshSamples=0;
    /// <summary>Accumulates hardware visibility results from real shadow passes, excluding disabled neutral resources.</summary>
    uint64_t LastVisibleShadowSamples=0;
};
}
