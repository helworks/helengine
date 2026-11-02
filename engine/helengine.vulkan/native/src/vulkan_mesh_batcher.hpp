#pragma once
#include "vulkan_mesh_renderer.hpp"
#include "vulkan_batch_input_view.hpp"
#include "vulkan_frame_trace_scope.hpp"
#include <algorithm>
#include <functional>
#include <memory>
#include <stdexcept>
#include <string>
#include <unordered_map>
#include <unordered_set>
#include <vector>

namespace helengine::vulkan {
/// <summary>Plans strictly ordered batches and owns bounded static and reusable dynamic GPU geometry through an explicit backend.</summary>
class VulkanMeshBatcher final {
public:
    /// <summary>Uses the renderer's fixed float32 vertex boundary.</summary>
    using Vertex=VulkanMeshRenderer::Vertex;
    /// <summary>Retains immutable source or temporary remapped triangle geometry.</summary>
    struct Geometry {
        /// <summary>Stores position, normal and UV attributes at the established GPU boundary.</summary>
        std::vector<Vertex> Vertices;
        /// <summary>Preserves authored triangle and vertex order.</summary>
        std::vector<uint32_t> Indices;
        /// <summary>Preserves original ordered submesh slots for persistent GPU visibility; empty for ordinary geometry.</summary>
        std::vector<VulkanIndirectRange> Ranges;
        /// <summary>Borrows an immutable source mesh for metadata-only local-space pages; SIZE_MAX selects owned geometry.</summary>
        size_t BorrowedMesh=SIZE_MAX;
        /// <summary>Keeps matching original CPU spans alive only for the synchronous bounds-upload callback.</summary>
        std::shared_ptr<const Geometry> BorrowedSource;
    };
    /// <summary>Bounds extra logical GPU geometry bytes independently for both feature modes.</summary>
    struct Settings {
        /// <summary>Enables persistent batching only for authored static inputs.</summary>
        bool StaticEnabled=false;
        /// <summary>Bounds resident persistent vertex and index payloads.</summary>
        uint64_t StaticBudgetBytes=0;
        /// <summary>Enables rebuilding compatible small inputs from their current transforms.</summary>
        bool DynamicEnabled=false;
        /// <summary>Limits unique referenced vertices of each original input before range merging.</summary>
        uint32_t DynamicMaxVertices=1024;
        /// <summary>Bounds retained reusable transient vertex and index capacity.</summary>
        uint64_t DynamicBudgetBytes=0;
    };
    /// <summary>Snapshots one already-visible, ordered draw without borrowing mutable scene state.</summary>
    struct Input {
        /// <summary>References frame-local profiler ownership without retaining the scene object.</summary>
        int TraceSubject=-1;
        /// <summary>Identifies a live immutable source mesh; handles must never be recycled.</summary>
        size_t Mesh=0;
        /// <summary>Retains source geometry only when geometry baking is enabled.</summary>
        std::shared_ptr<const Geometry> Source;
        /// <summary>Selects the original index range.</summary>
        uint32_t First=0;
        /// <summary>Counts original triangle indices.</summary>
        uint32_t Count=0;
        /// <summary>Records the existing entity Static flag.</summary>
        bool Static=false;
        /// <summary>Restricts merging to the explicitly supported opaque unskinned shader contract.</summary>
        bool Supported=false;
        /// <summary>Compares actual shader, state, uniform and sampled resource identities; never material pointers.</summary>
        std::string State;
        /// <summary>Omits camera-only uniform bytes from persistent geometry identity while preserving effective material state.</summary>
        std::string CacheState;
        /// <summary>Compares all engine transform storage values without rounding to GPU32.</summary>
        std::string Transform;
        /// <summary>Aliases immutable material storage and independently retains its owner; takes precedence over State when present.</summary>
        std::shared_ptr<const std::string> SharedState;
        /// <summary>Aliases immutable geometry-compatible material storage, including an intentionally empty fallback key.</summary>
        std::shared_ptr<const std::string> SharedCacheState;
        /// <summary>Aliases the immutable transform snapshot and retains it independently of draw submissions.</summary>
        std::shared_ptr<const std::string> SharedTransform;
        /// <summary>Bakes one vertex using engine arithmetic with a captured immutable matrix snapshot.</summary>
        std::function<Vertex(const Vertex&)> Bake;
        /// <summary>Borrows exact effective state bytes from retained immutable storage or the compatible owned-string path.</summary>
        const std::string& GetState() const { return SharedState?*SharedState:State; }
        /// <summary>Borrows the exact persistent state key; an empty result still falls back to effective state during serialization.</summary>
        const std::string& GetCacheState() const { return SharedCacheState?*SharedCacheState:CacheState; }
        /// <summary>Borrows exact native scalar transform bytes without a per-input payload copy.</summary>
        const std::string& GetTransform() const { return SharedTransform?*SharedTransform:Transform; }

    };
    /// <summary>Borrows ordered immutable inputs with separate current-pass profiler subjects.</summary>
    using InputView=VulkanBatchInputView<Input>;
    /// <summary>Selects either an unchanged source range or an identity-world uploaded batch.</summary>
    struct Output {
        /// <summary>Identifies the live source or batch mesh handle.</summary>
        size_t Mesh;
        /// <summary>Selects the first index in that handle.</summary>
        uint32_t First;
        /// <summary>Counts submitted indices without duplicate submission.</summary>
        uint32_t Count;
        /// <summary>Uses the first participant's compatible immutable material snapshots.</summary>
        size_t InputIndex;
        /// <summary>Selects identity-world uniform bytes for baked geometry.</summary>
        bool Baked;
        /// <summary>Counts contiguous original inputs represented by this output, including coalesced ranges.</summary>
        size_t InputCount=1;
    };
    /// <summary>Separates CPU batching tests from graphics device ownership while exercising the real cache and planner.</summary>
    struct Backend {
        /// <summary>Uploads a fresh persistent batch; failures propagate before cache publication.</summary>
        std::function<size_t(const Geometry&)> Upload;
        /// <summary>Updates an idle transient handle or allocates one when given SIZE_MAX.</summary>
        std::function<size_t(size_t,const Geometry&)> Update;
        /// <summary>Releases an owned idle batch exactly once.</summary>
        std::function<void(size_t)> Release;
        /// <summary>Requests persistent original-range pages, including eligible single-unit runs.</summary>
        bool StaticIndirectPages=false;
        /// <summary>Reserves device allocation capacity by limiting simultaneously resident indirect page owners.</summary>
        size_t StaticPageLimit=SIZE_MAX;
    };
    /// <summary>Reports numeric work and logical resident capacity for opt-in diagnostics.</summary>
    struct Counters {
        /// <summary>Counts original inputs in the current frame.</summary>
        uint64_t Inputs=0;
        /// <summary>Counts complete plans reused after exact immutable token and resident lease validation.</summary>
        uint64_t PlanReplays=0;
        /// <summary>Counts actual output draw packets.</summary>
        uint64_t Draws=0;
        /// <summary>Counts additional ranges eliminated without geometry copying.</summary>
        uint64_t Coalesced=0;
        /// <summary>Counts baked persistent batches selected.</summary>
        uint64_t StaticBatches=0;
        /// <summary>Counts successful persistent cache reuse.</summary>
        uint64_t CacheHits=0;
        /// <summary>Counts newly uploaded persistent batches.</summary>
        uint64_t CacheMisses=0;
        /// <summary>Counts rebuilt transient batches selected.</summary>
        uint64_t DynamicBatches=0;
        /// <summary>Counts bytes actually uploaded or rewritten this frame.</summary>
        uint64_t UploadedBytes=0;
        /// <summary>Counts actual source-index visits made by referenced-vertex budget planning in this frame.</summary>
        uint64_t ReferencedIndexVisits=0;
    };
    /// <summary>Captures validated settings and a backend whose device outlives this owner.</summary>
    VulkanMeshBatcher(Settings settings,Backend backend):Config(settings),Device(std::move(backend)) { }
    /// <summary>Releases all remaining handles after synchronous device submissions have completed.</summary>
    ~VulkanMeshBatcher() { Clear(); }
    /// <summary>Prevents duplicate geometry ownership.</summary>
    VulkanMeshBatcher(const VulkanMeshBatcher&)=delete;
    /// <summary>Prevents duplicate geometry ownership.</summary>
    VulkanMeshBatcher& operator=(const VulkanMeshBatcher&)=delete;
    /// <summary>Starts one frame without invalidating static data still visible in another camera or shadow pass.</summary>
    void BeginFrame() { ++Epoch; Stats={}; }
    /// <summary>Retires geometry unseen in every pass of this frame, including unloaded scenes and changed transforms/materials.</summary>
    void EndFrame() {
        VulkanFrameTraceScope RetireTrace("Batcher.RetireCache");
        for(auto it=Cache.begin();it!=Cache.end();) {
            if(it->second.Epoch!=Epoch) { Device.Release(it->second.Mesh); StaticBytes-=it->second.Bytes; it=Cache.erase(it); }
            else { ++it; }
        }
    }
    /// <summary>Releases persistent and transient ownership deterministically after model/material release or disposal.</summary>
    void Clear() {
        for(const auto& item:Cache) Device.Release(item.second.Mesh);
        for(const auto& item:Transient) Device.Release(item.Mesh);
        Cache.clear(); Transient.clear(); StaticBytes=DynamicBytes=0;
        ReplayTokens.clear(); ReplayLeases.clear(); ReplayOutputs.clear();
    }
    /// <summary>Returns logical persistent vertex/index residency.</summary>
    uint64_t GetStaticBytes() const { return StaticBytes; }
    /// <summary>Counts currently resident GPU page owners, excluding ordinary static geometry.</summary>
    size_t GetStaticPageCount() const { return Device.StaticIndirectPages?Cache.size():0; }
    /// <summary>Returns retained transient vertex/index capacity.</summary>
    uint64_t GetDynamicBytes() const { return DynamicBytes; }
    /// <summary>Borrows this frame's numeric counters.</summary>
    const Counters& GetCounters() const { return Stats; }
    /// <summary>Builds a complete pass after prior GPU use has completed; never reorders across an incompatible or oversized input.</summary>
    std::vector<Output> Build(const std::vector<Input>& inputs) { return Build(InputView(inputs)); }
    /// <summary>Plans borrowed inputs without copying their captured geometry or material keys.</summary>
    std::vector<Output> Build(InputView inputs) { return BuildPlan(inputs,false); }
private:
    /// <summary>Builds one ordered pass, capturing resident keys while already located only when retained replay requests them.</summary>
    std::vector<Output> BuildPlan(InputView inputs,bool captureLeases) {
        LastBuildCapacityFallback=false;
        VulkanFrameTraceScope CoalesceTrace("Batcher.CoalesceInputs");
        CoalesceTrace.AddCounters(inputs.size());
        Stats.Inputs+=inputs.size();
        std::vector<Unit> units;
        for(size_t i=0;i<inputs.size();++i) {
            const auto& input=inputs[i];
            if(!input.Count || input.Count%3 || input.First%3) throw std::invalid_argument("Batch input requires a nonempty triangle range.");
            if(input.Source) ValidateRange(input);
            const int mode=GetMode(inputs,i);
            if(!units.empty() && mode && units.back().Mode==mode) {
                auto& previous=units.back(); const auto& first=inputs[previous.InputIndex];
                if(input.Mesh==first.Mesh && input.GetState()==first.GetState() && input.GetTransform()==first.GetTransform() && uint64_t(previous.First)+previous.Count==input.First && uint64_t(previous.Count)+input.Count<=UINT32_MAX) {
                    previous.Count+=input.Count; ++previous.Inputs; ++Stats.Coalesced; continue;
                }
            }
            units.push_back({i,1,input.First,input.Count,mode});
        }
        CoalesceTrace.Finish();
        VulkanFrameTraceScope PlanTrace("Batcher.PlanCompatibleRuns");
        std::vector<Output> result; size_t transientSlot=0,compatibleEnd=0;
        for(size_t i=0;i<units.size();) {
            if(i>=compatibleEnd) {
                const auto& first=inputs[units[i].InputIndex]; compatibleEnd=i+1;
                if(units[i].Mode && first.Source && first.Bake) {
                    while(compatibleEnd<units.size() && units[compatibleEnd].Mode==units[i].Mode && inputs[units[compatibleEnd].InputIndex].GetState()==first.GetState() && inputs[units[compatibleEnd].InputIndex].Source && inputs[units[compatibleEnd].InputIndex].Bake) ++compatibleEnd;
                }
                // Probe a full static run once before inspecting immutable indices. Split cold runs retain the bounded prefix planner below.
                if(units[i].Mode==1 && (compatibleEnd-i>1 || (Device.StaticIndirectPages && first.Source && first.Bake))) {
                    const auto found=Cache.find(BuildKey(inputs,units,i,compatibleEnd));
                    if(found!=Cache.end()) {
                        const auto count=TotalIndices(units,i,compatibleEnd);
                        found->second.Epoch=Epoch; ++Stats.CacheHits; ++Stats.StaticBatches;
                        if(captureLeases) ReplayLeases.emplace_back(found->first,found->second.Mesh);
                        result.push_back({found->second.Mesh,0,count,units[i].InputIndex,!(Device.StaticIndirectPages && compatibleEnd-i==1),units[compatibleEnd-1].InputIndex+units[compatibleEnd-1].Inputs-units[i].InputIndex});
                        i=compatibleEnd; continue;
                    }
                }
            }
            const size_t end=compatibleEnd;
            bool batched=false;
            // Greedily fill a bounded prefix. A budget limit leaves every remainder on its valid original path.
            if(end-i>1 || (Device.StaticIndirectPages && units[i].Mode==1 && inputs[units[i].InputIndex].Source && inputs[units[i].InputIndex].Bake)) {
                uint64_t estimate=0,indexCount=0; size_t bounded=i;
                const auto budget=units[i].Mode==1?Config.StaticBudgetBytes:Config.DynamicBudgetBytes;
                while(bounded<end) {
                    const auto& unit=units[bounded]; const auto& input=inputs[unit.InputIndex];
                    const bool borrowedPage=Device.StaticIndirectPages && units[i].Mode==1 && end-i==1;
                    const uint64_t geometryBytes=borrowedPage?0:uint64_t(CountReferenced(input,unit.First,unit.Count,&inputs,unit.InputIndex,unit.Inputs))*sizeof(Vertex)+uint64_t(unit.Count)*sizeof(uint32_t);
                    const uint64_t bytes=geometryBytes+(Device.StaticIndirectPages && units[i].Mode==1?uint64_t(unit.Inputs)*IndirectRangeBytes:0);
                    if(bytes>budget-estimate || indexCount+unit.Count>UINT32_MAX) break;
                    estimate+=bytes; indexCount+=unit.Count; ++bounded;
                }
                if(bounded-i>1 || (bounded>i && Device.StaticIndirectPages && units[i].Mode==1)) {
                    VulkanFrameTraceScope BuildTrace("Batcher.BuildBatch");
                    TraceRange(BuildTrace,inputs,units[i].InputIndex,units[bounded-1].InputIndex+units[bounded-1].Inputs);
                    const auto key=BuildKey(inputs,units,i,bounded);
                    size_t mesh=SIZE_MAX;
                    if(units[i].Mode==1) {
                        const auto found=Cache.find(key);
                        if(found!=Cache.end()) { found->second.Epoch=Epoch; mesh=found->second.Mesh; ++Stats.CacheHits; }
                        else if(estimate<=Config.StaticBudgetBytes-StaticBytes && (!Device.StaticIndirectPages || Cache.size()<Device.StaticPageLimit)) {
                            const auto geometry=BakeGeometry(inputs,units,i,bounded,Device.StaticIndirectPages,Device.StaticIndirectPages && bounded-i==1); const auto bytes=GeometryBytes(geometry);
                            mesh=Device.Upload(geometry);
                            try { Cache.emplace(key,Entry{mesh,bytes,Epoch}); } catch(...) { Device.Release(mesh); throw; }
                            StaticBytes+=bytes; ++Stats.CacheMisses; Stats.UploadedBytes+=bytes;
                        }
                        if(mesh!=SIZE_MAX) {
                            ++Stats.StaticBatches;
                            if(captureLeases) ReplayLeases.emplace_back(key,mesh);
                        }
                    } else {
                        const auto geometry=BakeGeometry(inputs,units,i,bounded);
                        mesh=UpdateTransient(transientSlot,geometry);
                        if(mesh!=SIZE_MAX) { ++transientSlot; ++Stats.DynamicBatches; Stats.UploadedBytes+=GeometryBytes(geometry); }
                    }
                    if(mesh!=SIZE_MAX) { result.push_back({mesh,0,static_cast<uint32_t>(indexCount),units[i].InputIndex,!(Device.StaticIndirectPages && units[i].Mode==1 && bounded-i==1),units[bounded-1].InputIndex+units[bounded-1].Inputs-units[i].InputIndex}); }
                    else {
                        if(units[i].Mode==1) LastBuildCapacityFallback=true;
                        // Allocation unavailability consumes this entire planned prefix once; retrying each suffix would repeatedly rescan unchanged work.
                        for(size_t fallback=i;fallback<bounded;++fallback) { const auto& unit=units[fallback]; result.push_back({inputs[unit.InputIndex].Mesh,unit.First,unit.Count,unit.InputIndex,false,unit.Inputs}); }
                    }
                    i=bounded; batched=true;
                }
            }
            if(!batched) { const auto& unit=units[i++]; result.push_back({inputs[unit.InputIndex].Mesh,unit.First,unit.Count,unit.InputIndex,false,unit.Inputs}); }
        }
        Stats.Draws+=result.size(); return result;
    }
public:
    /// <summary>Reuses a completed static pass only when the caller supplies identical immutable-run tokens; borrowed inputs must remain immutable for a token's lifetime.</summary>
    const std::vector<Output>& BuildRetained(InputView inputs,const std::vector<uint64_t>& tokens) {
        bool eligible=!tokens.empty();
        for(size_t i=0;eligible && i<inputs.size();++i) {
            const auto& input=inputs[i];
            if(Config.DynamicEnabled && input.Supported && !(Config.StaticEnabled && input.Static)) eligible=false;
        }
        bool resident=eligible && tokens==ReplayTokens && inputs.size()==ReplayInputCount && (!ReplayCapacitySensitive || (StaticBytes==ReplayStaticBytes && Cache.size()==ReplayStaticEntries));
        if(resident) {
            for(const auto& lease:ReplayLeases) {
                const auto found=Cache.find(lease.first);
                if(found==Cache.end() || found->second.Mesh!=lease.second) { resident=false; break; }
            }
        }
        if(resident) {
            VulkanFrameTraceScope ReplayTrace("Batcher.ReplayPlan");
            TraceRange(ReplayTrace,inputs,0,inputs.size());
            for(const auto& lease:ReplayLeases) Cache.at(lease.first).Epoch=Epoch;
            Stats.Inputs+=inputs.size(); Stats.Draws+=ReplayOutputs.size(); Stats.Coalesced+=ReplayCoalesced;
            Stats.StaticBatches+=ReplayStaticBatches; Stats.CacheHits+=ReplayStaticBatches; ++Stats.PlanReplays;
            return ReplayOutputs;
        }
        ReplayTokens.clear(); ReplayLeases.clear(); ReplayOutputs.clear();
        const auto before=Stats;
        ReplayOutputs=BuildPlan(inputs,eligible);
        if(eligible) {
            // BuildPlan captures leases before publishing tokens: an exception leaves replay unavailable.
            ReplayCapacitySensitive=LastBuildCapacityFallback;ReplayStaticEntries=Cache.size();ReplayStaticBytes=StaticBytes;ReplayInputCount=inputs.size(); ReplayCoalesced=Stats.Coalesced-before.Coalesced; ReplayStaticBatches=Stats.StaticBatches-before.StaticBatches;
            ReplayTokens=tokens;
        }
        return ReplayOutputs;
    }
private:
    /// <summary>Accounts the persistent two-vec4 bounds plus five uint32 indirect command fields at their fixed GPU layout.</summary>
    static constexpr uint64_t IndirectRangeBytes=8*sizeof(float)+5*sizeof(uint32_t);
    /// <summary>Represents contiguous ranges sharing one source, transform, state and eligibility mode.</summary>
    struct Unit {
        /// <summary>Identifies the first original input.</summary>
        size_t InputIndex;
        /// <summary>Counts original inputs coalesced into this range.</summary>
        size_t Inputs;
        /// <summary>Preserves original index offset.</summary>
        uint32_t First;
        /// <summary>Counts contiguous indices.</summary>
        uint32_t Count;
        /// <summary>Uses zero for fallback, one for static and two for dynamic.</summary>
        int Mode;
    };
    /// <summary>Owns one published persistent cache handle without retaining source objects or scene pointers.</summary>
    struct Entry {
        /// <summary>Identifies the uploaded geometry.</summary>
        size_t Mesh;
        /// <summary>Accounts resident logical bytes.</summary>
        uint64_t Bytes;
        /// <summary>Records last frame use across every camera/pass.</summary>
        uint64_t Epoch;
    };
    /// <summary>Owns one reusable transient allocation with independent vertex/index capacities.</summary>
    struct DynamicEntry {
        /// <summary>Identifies the idle reusable geometry.</summary>
        size_t Mesh;
        /// <summary>Bounds vertex writes.</summary>
        size_t Vertices;
        /// <summary>Bounds index writes.</summary>
        size_t Indices;
    };
    /// <summary>Associates a measured range with all original participants, including merged submesh ranges.</summary>
    static void TraceRange(VulkanFrameTraceScope& scope,InputView inputs,size_t first,size_t end) {
        if(!VulkanFrameTrace::IsRecording()) return;
        if(end-first==1) { scope.SetContext(inputs.Subject(first)); return; }
        std::vector<int> subjects; subjects.reserve(end-first);
        for(size_t i=first;i<end;++i) subjects.push_back(inputs.Subject(i));
        scope.SetContext(-1,VulkanFrameTrace::RegisterBatch(std::move(subjects)));
    }
    /// <summary>Checks corrupt source ranges even when eligibility or budgets require ordinary fallback.</summary>
    static void ValidateRange(const Input& input) {
        if(input.First>input.Source->Indices.size() || input.Count>input.Source->Indices.size()-input.First) throw std::invalid_argument("Batch input exceeds its source index range.");
    }
    /// <summary>Counts unique referenced vertices, rejecting corruption before either budget fallback or upload.</summary>
    size_t CountReferenced(const Input& input,uint32_t first,uint32_t count,const InputView* originalInputs=nullptr,size_t originalFirst=0,size_t originalCount=1) {
        VulkanFrameTraceScope ReferencedTrace("Batcher.CountReferencedVertices");
        ReferencedTrace.AddCounters(count);
        if(originalInputs) TraceRange(ReferencedTrace,*originalInputs,originalFirst,originalFirst+originalCount);
        else ReferencedTrace.SetContext(input.TraceSubject);
        if(!input.Source) return SIZE_MAX;
        std::unordered_set<uint32_t> referenced;
        for(uint32_t i=0;i<count;++i) { ++Stats.ReferencedIndexVisits; const auto index=input.Source->Indices.at(size_t(first)+i); if(index>=input.Source->Vertices.size()) throw std::invalid_argument("Batch source contains a corrupt vertex index."); referenced.insert(index); }
        return referenced.size();
    }
    /// <summary>Chooses one feature per input, with static preferred when both settings are enabled.</summary>
    int GetMode(InputView inputs,size_t index) {
        const auto& input=inputs[index];
        if(!input.Supported) return 0;
        if(Config.StaticEnabled && input.Static) return 1;
        if(Config.DynamicEnabled && input.Source && CountReferenced(input,input.First,input.Count,&inputs,index)<=Config.DynamicMaxVertices) return 2;
        return 0;
    }
    /// <summary>Counts indices with a wider accumulator before the validated uint32 GPU boundary.</summary>
    static uint32_t TotalIndices(const std::vector<Unit>& units,size_t first,size_t end) {
        uint64_t count=0; for(size_t i=first;i<end;++i) count+=units[i].Count;
        if(count>UINT32_MAX) throw std::length_error("Batch index count exceeds uint32.");
        return static_cast<uint32_t>(count);
    }
    /// <summary>Appends a length-prefixed binary field, avoiding delimiter collisions in exact cache identities.</summary>
    static void Append(std::string& key,const void* bytes,size_t count) { const uint64_t size=count; key.append(reinterpret_cast<const char*>(&size),sizeof(size)); key.append(static_cast<const char*>(bytes),count); }
    /// <summary>Compares complete source range, engine transform and effective state snapshots without hash-only equality.</summary>
    std::string BuildKey(InputView inputs,const std::vector<Unit>& units,size_t first,size_t end) {
        VulkanFrameTraceScope KeyTrace("Batcher.BuildGeometryKey");
        TraceRange(KeyTrace,inputs,units[first].InputIndex,units[end-1].InputIndex+units[end-1].Inputs);
        std::string key; const auto& inputState=inputs[units[first].InputIndex]; const auto& state=inputState.GetCacheState().empty()?inputState.GetState():inputState.GetCacheState(); Append(key,state.data(),state.size());
        for(size_t i=first;i<end;++i) { const auto& unit=units[i]; const auto& input=inputs[unit.InputIndex]; Append(key,&input.Mesh,sizeof(input.Mesh)); Append(key,&unit.First,sizeof(unit.First)); Append(key,&unit.Count,sizeof(unit.Count));
            if(!(Device.StaticIndirectPages && unit.Mode==1 && end-first==1)) Append(key,input.GetTransform().data(),input.GetTransform().size());
            if(Device.StaticIndirectPages && unit.Mode==1) {
                Append(key,&unit.Inputs,sizeof(unit.Inputs));
                for(size_t original=unit.InputIndex;original<unit.InputIndex+unit.Inputs;++original) Append(key,&inputs[original].Count,sizeof(inputs[original].Count));
            }
        }
        KeyTrace.AddCounters(end-first,key.size());
        return key;
    }
    /// <summary>Bakes only referenced vertices and remaps indices while preserving every original triangle's order.</summary>
    static Geometry BakeGeometry(InputView inputs,const std::vector<Unit>& units,size_t first,size_t end,bool retainRanges=false,bool borrowGeometry=false) {
        VulkanFrameTraceScope BakeTrace("Batcher.BakeGeometry");
        TraceRange(BakeTrace,inputs,units[first].InputIndex,units[end-1].InputIndex+units[end-1].Inputs);
        Geometry geometry;
        if(borrowGeometry) {
            if(end-first!=1 || !retainRanges) throw std::logic_error("Borrowed static page requires exactly one retained source unit.");
            const auto& unit=units[first];const auto& input=inputs[unit.InputIndex];
            geometry.BorrowedMesh=input.Mesh;geometry.BorrowedSource=input.Source;
            for(size_t original=unit.InputIndex;original<unit.InputIndex+unit.Inputs;++original) geometry.Ranges.push_back({inputs[original].First,inputs[original].Count});
            BakeTrace.AddCounters(unit.Count,GeometryBytes(geometry));return geometry;
        }
        geometry.Indices.reserve(TotalIndices(units,first,end));
        for(size_t i=first;i<end;++i) {
            const auto& unit=units[i]; const auto& input=inputs[unit.InputIndex]; std::unordered_map<uint32_t,uint32_t> remap;
            if(retainRanges) {
                uint32_t firstIndex=static_cast<uint32_t>(geometry.Indices.size());
                for(size_t original=unit.InputIndex;original<unit.InputIndex+unit.Inputs;++original) {
                    const auto count=inputs[original].Count;geometry.Ranges.push_back({firstIndex,count});firstIndex+=count;
                }
            }
            for(uint32_t j=0;j<unit.Count;++j) {
                const auto index=input.Source->Indices.at(size_t(unit.First)+j); if(index>=input.Source->Vertices.size()) throw std::invalid_argument("Batch source contains a corrupt vertex index.");
                auto found=remap.find(index);
                if(found==remap.end()) { if(geometry.Vertices.size()>=UINT32_MAX) throw std::length_error("Batch vertices exceed uint32."); const uint32_t mapped=static_cast<uint32_t>(geometry.Vertices.size()); geometry.Vertices.push_back(input.Bake(input.Source->Vertices[index])); found=remap.emplace(index,mapped).first; }
                geometry.Indices.push_back(found->second);
            }
        }
        BakeTrace.AddCounters(geometry.Indices.size(),GeometryBytes(geometry));
        return geometry;
    }
    /// <summary>Accounts fixed-layout vertices and uint32 indices without implicit numeric conversions.</summary>
    static uint64_t GeometryBytes(const Geometry& geometry) { return uint64_t(geometry.Vertices.size())*sizeof(Vertex)+uint64_t(geometry.Indices.size())*sizeof(uint32_t)+uint64_t(geometry.Ranges.size())*IndirectRangeBytes; }
    /// <summary>Reuses only a pass-idle slot, keeping total retained allocation capacity within its configured budget.</summary>
    size_t UpdateTransient(size_t slot,const Geometry& geometry) {
        VulkanFrameTraceScope TransientTrace("Batcher.UpdateTransient");
        TransientTrace.AddCounters(1,GeometryBytes(geometry));
        const auto bytes=GeometryBytes(geometry);
        if(slot<Transient.size()) {
            auto& entry=Transient[slot];
            if(entry.Vertices>=geometry.Vertices.size() && entry.Indices>=geometry.Indices.size()) { const size_t updated=Device.Update(entry.Mesh,geometry); if(updated!=entry.Mesh) throw std::logic_error("Transient update replaced a retained handle."); return updated; }
            const uint64_t oldBytes=uint64_t(entry.Vertices)*sizeof(Vertex)+uint64_t(entry.Indices)*sizeof(uint32_t);
            if(bytes>Config.DynamicBudgetBytes-(DynamicBytes-oldBytes)) return SIZE_MAX;
            Device.Release(entry.Mesh); DynamicBytes-=oldBytes; Transient.erase(Transient.begin()+slot);
        }
        if(bytes>Config.DynamicBudgetBytes-DynamicBytes) return SIZE_MAX;
        const size_t mesh=Device.Update(SIZE_MAX,geometry);
        try { Transient.insert(Transient.begin()+slot,{mesh,geometry.Vertices.size(),geometry.Indices.size()}); } catch(...) { Device.Release(mesh); throw; }
        DynamicBytes+=bytes; return mesh;
    }
    /// <summary>Retains validated per-mode limits.</summary>
    Settings Config;
    /// <summary>Owns callbacks borrowing the live graphics device.</summary>
    Backend Device;
    /// <summary>Owns exact-key persistent geometry.</summary>
    std::unordered_map<std::string,Entry> Cache;
    /// <summary>Owns reusable transient slots, idle between synchronous passes.</summary>
    std::vector<DynamicEntry> Transient;
    /// <summary>Counts resident persistent geometry bytes.</summary>
    uint64_t StaticBytes=0;
    /// <summary>Counts retained transient geometry bytes.</summary>
    uint64_t DynamicBytes=0;
    /// <summary>Identifies a complete frame across all pass builds.</summary>
    uint64_t Epoch=0;
    /// <summary>Collects current-frame numeric diagnostics.</summary>
    Counters Stats;
    /// <summary>Rejects capacity-limited replay when retirement or another pass changes available residency.</summary>
    uint64_t ReplayStaticBytes=0;
    /// <summary>Tracks allocation-slot availability independently of total resident bytes.</summary>
    size_t ReplayStaticEntries=0;
    /// <summary>Marks a plan whose capacity fallback can improve after unrelated cache entries retire.</summary>
    bool ReplayCapacitySensitive=false;
    /// <summary>Records whether the latest full plan fell back because resident capacity was unavailable.</summary>
    bool LastBuildCapacityFallback=false;
    /// <summary>Records exact ordered immutable run generations and lengths supplied by the renderer.</summary>
    std::vector<uint64_t> ReplayTokens;
    /// <summary>Retains the current output plan without retaining scene pointers.</summary>
    std::vector<Output> ReplayOutputs;
    /// <summary>Associates exact persistent cache keys with expected non-recycled mesh handles.</summary>
    std::vector<std::pair<std::string,size_t>> ReplayLeases;
    /// <summary>Rejects malformed reuse with a different number of original inputs.</summary>
    size_t ReplayInputCount=0;
    /// <summary>Preserves per-frame range coalescing accounting on replay.</summary>
    uint64_t ReplayCoalesced=0;
    /// <summary>Counts persistent outputs whose leases are refreshed on replay.</summary>
    uint64_t ReplayStaticBatches=0;
};
}
