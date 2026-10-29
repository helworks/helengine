#include "vulkan_mesh_batcher.hpp"
#include "mesh_buffer_upload.hpp"
#include <cmath>
#include <chrono>
#include <iostream>
#include <map>
#include <cstdlib>
#include <new>
#include <atomic>
#include <stdexcept>
/// <summary>Counts ordinary allocations only inside the key-binding measurement.</summary>
static std::atomic<size_t> KeyAllocations{0};
/// <summary>Enables allocation measurement after snapshot and input capacity preparation.</summary>
static std::atomic<bool> CountKeyAllocations{false};
/// <summary>Measures the actual string/control-block allocation path in this CPU fixture.</summary>
void* operator new(std::size_t size) {
    if(CountKeyAllocations) ++KeyAllocations;
    if(void* memory=std::malloc(size?size:1)) return memory;
    throw std::bad_alloc();
}
/// <summary>Releases measured allocations through the matching allocator.</summary>
void operator delete(void* memory) noexcept { std::free(memory); }
/// <summary>Supports compiler-selected sized destruction through the same allocator.</summary>
void operator delete(void* memory,std::size_t) noexcept { std::free(memory); }
using namespace helengine::vulkan;
/// <summary>Rejects mismatches in actual production planner outputs.</summary>
static void Require(bool value,const char* message) { if(!value) throw std::runtime_error(message); }
/// <summary>Tracks uploaded real CPU geometry and enforces final-owner release, including upload failures.</summary>
class Device final {
public:
    /// <summary>Owns successful geometry uploads by stable unique handle.</summary>
    std::map<size_t,VulkanMeshBatcher::Geometry> Live;
    /// <summary>Allocates stable handles without reuse.</summary>
    size_t Next=100;
    /// <summary>Counts uploaded persistent payloads.</summary>
    size_t Uploads=0;
    /// <summary>Counts transient rewrites.</summary>
    size_t Updates=0;
    /// <summary>Requests one transactional upload failure.</summary>
    bool Fail=false;
    /// <summary>Injects failure when the index owner is requested after successful vertex ownership.</summary>
    bool FailIndex=false;
    /// <summary>Counts constructed resource owners independently from cache publication.</summary>
    int Created=0;
    /// <summary>Counts destroyed resource owners including failed transactions.</summary>
    int Destroyed=0;
    /// <summary>Tracks an allocated resource owner independently of the planner/cache lifecycle.</summary>
    class Buffer final {
    public:
        /// <summary>Records successful owner construction for deterministic partial-allocation testing.</summary>
        explicit Buffer(Device& device):Owner(device) { ++Owner.Created; }
        /// <summary>Records exactly one release when transactional or final ownership ends.</summary>
        ~Buffer() { ++Owner.Destroyed; }
    private:
        /// <summary>Borrows the fixture whose counters outlive every buffer.</summary>
        Device& Owner;
    };
    /// <summary>Owns only completed resource pairs by published GPU-like handle.</summary>
    std::map<size_t,std::array<std::unique_ptr<Buffer>,2>> Owners;
    /// <summary>Uses the exact production transaction boundary and fails index allocation after a real vertex owner exists.</summary>
    std::array<std::unique_ptr<Buffer>,2> Allocate() {
        std::array<std::unique_ptr<Buffer>,2> owners;
        MeshBufferUpload::Initialize(owners[0],owners[1],
            [this] { return std::make_unique<Buffer>(*this); },
            [this] { if(FailIndex) { Require(Created==Destroyed+1,"second factory did not observe the first live owner"); throw std::runtime_error("index failure"); } return std::make_unique<Buffer>(*this); });
        return owners;
    }
    /// <summary>Returns callbacks used by the production batch cache.</summary>
    VulkanMeshBatcher::Backend Backend() {
        return {[this](const auto& geometry) { ++Uploads; if(Fail) throw std::runtime_error("upload failure"); auto owners=Allocate(); auto handle=Next++; Live.emplace(handle,geometry); Owners.emplace(handle,std::move(owners)); return handle; },
            [this](size_t handle,const auto& geometry) { ++Updates; if(Fail) throw std::runtime_error("update failure"); if(handle==SIZE_MAX) { auto owners=Allocate(); handle=Next++; Owners.emplace(handle,std::move(owners)); } else Require(Live.contains(handle),"update absent mesh"); Live[handle]=geometry; return handle; },
            [this](size_t handle) { Require(Live.erase(handle)==1 && Owners.erase(handle)==1,"release absent or doubly owned mesh"); }};
    }
};
/// <summary>Builds triangles with one unused source vertex to exercise referenced-vertex limits.</summary>
static std::shared_ptr<const VulkanMeshBatcher::Geometry> Source() {
    auto source=std::make_shared<VulkanMeshBatcher::Geometry>();
    source->Vertices={{{0,0,0},{1,1,0},{0,0}},{{1,0,0},{1,1,0},{1,0}},{{0,1,0},{1,1,0},{0,1}},{{99,99,99},{0,0,1},{0,0}}};
    source->Indices={0,1,2,0,2,1}; return source;
}
/// <summary>Snapshots an affine CPU transform for planner tests without replacing the engine geometry adapter.</summary>
static VulkanMeshBatcher::Input Input(size_t mesh,float x=0,bool isStatic=true) {
    VulkanMeshBatcher::Input input; input.Mesh=mesh; input.Source=Source(); input.First=0; input.Count=3; input.Static=isStatic; input.Supported=true; input.State="material"; input.Transform=std::to_string(x);
    input.Bake=[x](const auto& source) { auto vertex=source; vertex.Position[0]+=x; return vertex; }; return input;
}
/// <summary>Checks ordering, exact state equality and zero-copy same-mesh range merging.</summary>
static void Ranges() {
    VulkanMeshBatcher batcher({true,1024,false,3,1024},{});
    auto first=Input(7); first.Source.reset(); auto second=first; second.First=3;
    const auto draws=batcher.Build({first,second});
    Require(draws.size()==1 && draws[0].Mesh==7 && draws[0].First==0 && draws[0].Count==6 && draws[0].InputCount==2 && !draws[0].Baked,"contiguous static ranges must coalesce without upload");
    second.State="other"; Require(batcher.Build({first,second}).size()==2,"incompatible material merged");
    second.State=first.State; second.Transform="moved"; Require(batcher.Build({first,second}).size()==2,"different worlds coalesced");
    second.Transform=first.Transform; second.First=6; Require(batcher.Build({first,second}).size()==2,"range gap lost");
    second.First=3; second.Supported=false; Require(batcher.Build({first,second}).size()==2,"unsupported shader merged");
    VulkanMeshBatcher disabled({},{}); Require(disabled.Build({first,first}).size()==2,"disabled feature changed draws");
}
/// <summary>Exercises actual cross-mesh baking, exact remapping, cache hits, state invalidation and deterministic retirement.</summary>
static void StaticCache() {
    Device device;
    {
        VulkanMeshBatcher batcher({true,216,false,3,0},device.Backend());
        auto a=Input(1),b=Input(2,3); batcher.BeginFrame(); auto draws=batcher.Build({a,b});
        Require(draws.size()==1 && draws[0].Baked && draws[0].Count==6,"different meshes failed to batch at exact byte budget");
        const auto handle=draws[0].Mesh; const auto& geometry=device.Live.at(handle);
        Require(geometry.Vertices.size()==6 && geometry.Indices==std::vector<uint32_t>({0,1,2,3,4,5}),"referenced vertex remap changed triangle order");
        Require(geometry.Vertices[3].Position[0]==3 && geometry.Vertices[4].Position[0]==4,"cross-mesh transform was not baked");
        Require(batcher.GetStaticBytes()==216,"static budget accounting wrong"); batcher.EndFrame();
        batcher.BeginFrame(); draws=batcher.Build({a,b}); batcher.EndFrame(); Require(draws[0].Mesh==handle && device.Uploads==1 && batcher.GetCounters().CacheHits==1,"unchanged static cache reuploaded");
        b=Input(2,5); batcher.BeginFrame(); draws=batcher.Build({a,b}); batcher.EndFrame(); Require(draws.size()==2 && device.Live.empty(),"changed transform reused stale cache or exceeded resident budget");
        batcher.BeginFrame(); draws=batcher.Build({a,b}); batcher.EndFrame(); Require(draws.size()==1 && device.Live.at(draws[0].Mesh).Vertices[3].Position[0]==5,"changed static transform failed to rebuild");
        a.State=b.State="changed-constants-and-textures"; batcher.BeginFrame(); draws=batcher.Build({a,b}); batcher.EndFrame(); Require(draws.size()==2 && device.Live.empty(),"changed material reused stale cache");
        batcher.BeginFrame(); draws=batcher.Build({a,b}); batcher.EndFrame(); Require(draws.size()==1,"changed material did not rebuild");
        batcher.BeginFrame(); batcher.EndFrame(); Require(device.Live.empty() && batcher.GetStaticBytes()==0,"scene unload retained batches");
        device.Fail=true; bool failed=false; try { batcher.Build({a,b}); } catch(const std::runtime_error&) { failed=true; }
        Require(failed && device.Live.empty() && batcher.GetStaticBytes()==0,"upload failure swallowed or published partial cache");
        device.Fail=false; Require(batcher.Build({a,b}).size()==1,"cache failed after upload failure");
        batcher.Clear(); Require(device.Live.empty(),"explicit source release did not invalidate all cache owners"); batcher.Clear();
        batcher.Build({a,b});
    }
    Require(device.Live.empty(),"final batch owner leaked resources");
}
/// <summary>Requires warm static hits to avoid source-index planning and invalidated immutable snapshots to revalidate before rebuilding.</summary>
static void WarmCacheIndexVisits() {
    Device device; VulkanMeshBatcher batcher({true,216,false,3,0},device.Backend());
    auto a=Input(1),b=Input(2,3);
    batcher.BeginFrame(); auto draws=batcher.Build({a,b}); batcher.EndFrame();
    Require(draws.size()==1 && batcher.GetCounters().ReferencedIndexVisits==6,"cold static build failed to count actual referenced-index visits");
    const auto mesh=draws[0].Mesh;
    batcher.BeginFrame(); draws=batcher.Build({a,b}); batcher.EndFrame();
    Require(draws.size()==1 && draws[0].Mesh==mesh && batcher.GetCounters().CacheHits==1 && batcher.GetCounters().ReferencedIndexVisits==0,"warm static cache hit rescanned immutable source indices");
    for(int change=0;change<3;++change) {
        if(change==0) b=Input(2,5);
        else if(change==1) a.State=b.State="changed-effective-material";
        else { a.Mesh=3; a.Source=Source(); }
        const auto uploads=device.Uploads;
        batcher.BeginFrame(); draws=batcher.Build({a,b}); batcher.EndFrame();
        Require(draws.size()==2 && !draws[0].Baked && !draws[1].Baked && draws[0].Mesh==a.Mesh && draws[1].Mesh==b.Mesh && batcher.GetCounters().CacheHits==0 && batcher.GetCounters().ReferencedIndexVisits==6 && device.Uploads==uploads && device.Live.empty(),"invalidated source/world/material snapshot bypassed bounded fallback or reused stale geometry");
        batcher.BeginFrame(); draws=batcher.Build({a,b}); batcher.EndFrame();
        Require(draws.size()==1 && draws[0].Baked && batcher.GetCounters().ReferencedIndexVisits==6 && device.Uploads==uploads+1,"invalidated static snapshot did not revalidate and rebuild after old residency retired");
        const auto rebuilt=draws[0].Mesh;
        batcher.BeginFrame(); draws=batcher.Build({a,b}); batcher.EndFrame();
        Require(draws.size()==1 && draws[0].Mesh==rebuilt && draws[0].Count==6 && batcher.GetCounters().CacheHits==1 && batcher.GetCounters().ReferencedIndexVisits==0,"rebuilt static cache did not restore index-scan-free reuse");
    }
}
/// <summary>Checks moving dynamic inputs, independent modes, transient capacity and per-original-input referenced limits.</summary>
static void Dynamic() {
    Device device;
    {
        VulkanMeshBatcher batcher({false,0,true,3,216},device.Backend()); auto a=Input(1,0,false),b=Input(2,3,false);
        batcher.BeginFrame(); auto draws=batcher.Build({a,b}); Require(draws.size()==1 && batcher.GetDynamicBytes()==216,"dynamic only mode failed for three referenced vertices in four-vertex mesh");
        const auto handle=draws[0].Mesh; batcher.EndFrame(); b=Input(2,8,false); batcher.BeginFrame(); draws=batcher.Build({a,b}); batcher.EndFrame();
        Require(draws[0].Mesh==handle && device.Updates==2 && device.Live.at(handle).Vertices[3].Position[0]==8,"dynamic geometry did not refresh moving transform in reused allocation");
        auto incompatible=b; incompatible.State="different-texture"; Require(batcher.Build({a,incompatible}).size()==2,"different sampled resources merged");
        auto large=std::make_shared<VulkanMeshBatcher::Geometry>(*a.Source); large->Indices={0,1,2,0,2,3}; a.Source=large; a.Count=6;
        Require(batcher.Build({a,b}).size()==2,"oversized dynamic referenced input merged");
        device.Fail=true; bool failed=false; try { batcher.Build({b,b}); } catch(const std::runtime_error&) { failed=true; } Require(failed,"dynamic update failure swallowed");
    }
    Require(device.Live.empty(),"transient owner leaked"); device.Fail=false;
    VulkanMeshBatcher tooSmall({false,0,true,3,215},device.Backend()); Require(tooSmall.Build({Input(1),Input(2)}).size()==2 && device.Live.empty(),"dynamic allocation exceeded budget");
    VulkanMeshBatcher both({true,216,true,3,216},device.Backend());
    auto moving=Input(3,0,false),moving2=Input(4,4,false); both.BeginFrame(); const auto draws=both.Build({Input(1),Input(2),moving,moving2}); both.EndFrame();
    Require(draws.size()==2 && both.GetCounters().StaticBatches==1 && both.GetCounters().DynamicBatches==1,"combined modes duplicated or omitted inputs");
}
/// <summary>Requires failure after vertex ownership to clean up before either static cache or transient-slot publication.</summary>
static void PartialUploadFailure() {
    for(bool dynamic:{false,true}) {
        Device device; device.FailIndex=true;
        VulkanMeshBatcher batcher({!dynamic,216,dynamic,3,216},device.Backend()); batcher.BeginFrame();
        bool failed=false; try { batcher.Build({Input(1),Input(2)}); } catch(const std::runtime_error&) { failed=true; }
        Require(failed && device.Created==1 && device.Destroyed==1,"index failure did not release the successfully allocated vertex owner");
        Require(device.Live.empty() && device.Owners.empty() && batcher.GetStaticBytes()==0 && batcher.GetDynamicBytes()==0 && batcher.GetCounters().CacheMisses==0,"partial upload published cache/slot or resident accounting");
        device.FailIndex=false; const auto draws=batcher.Build({Input(1),Input(2)});
        Require(draws.size()==1 && device.Created==3 && device.Destroyed==1,"partial failure prevented a clean complete retry");
        batcher.Clear(); Require(device.Live.empty() && device.Created==device.Destroyed,"successful retry owners were not released exactly once");
    }
}
/// <summary>Exercises a large compatible cache hit and full-budget transform invalidation; a loose guard catches repeated quadratic/cubic suffix planning.</summary>
static void LargeOrderedRun() {
    constexpr size_t count=10000;
    Device device; VulkanMeshBatcher batcher({true,count*108,false,3,0},device.Backend());
    const auto source=Source(); size_t bakedVertices=0;
    std::vector<VulkanMeshBatcher::Input> inputs; inputs.reserve(count);
    for(size_t i=0;i<count;++i) { auto input=Input(i+1); input.Source=source; input.Bake=[&bakedVertices](const auto& vertex) { ++bakedVertices; return vertex; }; inputs.push_back(std::move(input)); }
    const auto start=std::chrono::steady_clock::now();
    batcher.BeginFrame(); auto draws=batcher.Build(inputs); batcher.EndFrame();
    Require(draws.size()==1 && draws[0].Count==count*3 && device.Uploads==1 && bakedVertices==count*3,"large compatible run failed to upload exactly once");
    const auto mesh=draws[0].Mesh;
    batcher.BeginFrame(); draws=batcher.Build(inputs); batcher.EndFrame();
    Require(draws.size()==1 && draws[0].Mesh==mesh && batcher.GetCounters().CacheHits==1 && device.Uploads==1 && bakedVertices==count*3,"large cache hit rebaked or reuploaded geometry");
    for(auto& input:inputs) { input.Transform="changed-world"; input.Bake=[&bakedVertices](const auto& sourceVertex) { ++bakedVertices; auto vertex=sourceVertex; vertex.Position[0]+=1; return vertex; }; }
    batcher.BeginFrame(); draws=batcher.Build(inputs);
    Require(draws.size()==count && device.Uploads==1 && bakedVertices==count*3,"full-budget changed transforms failed to use ordinary draws without rebaking");
    for(size_t i=0;i<count;++i) Require(draws[i].Mesh==inputs[i].Mesh && draws[i].InputIndex==i && draws[i].First==0 && draws[i].Count==3 && !draws[i].Baked,"budget fallback lost source order or duplicated an input");
    batcher.EndFrame(); Require(device.Live.empty() && batcher.GetStaticBytes()==0,"invalidated large cache retained obsolete residency");
    batcher.BeginFrame(); draws=batcher.Build(inputs); batcher.EndFrame();
    Require(draws.size()==1 && device.Uploads==2 && bakedVertices==count*6 && device.Live.at(draws[0].Mesh).Vertices[0].Position[0]==1,"retired large cache failed to rebuild current transforms");
    Require(std::chrono::steady_clock::now()-start<std::chrono::seconds(5),"large batching cache hit/fallback exceeded loose planning complexity guard");
}
/// <summary>Checks corruption is a visible error rather than a budget-driven fallback.</summary>
static void Corruption() {
    Device device; VulkanMeshBatcher batcher({false,0,true,3,10000},device.Backend()); auto a=Input(1); auto bad=std::make_shared<VulkanMeshBatcher::Geometry>(*a.Source); bad->Indices[1]=999; a.Source=bad;
    bool rejected=false; try { batcher.Build({a,Input(2)}); } catch(const std::invalid_argument&) { rejected=true; } Require(rejected,"corrupt index silently fell back");
    a=Input(1); a.First=3; a.Count=6; rejected=false; try { batcher.Build({a}); } catch(const std::invalid_argument&) { rejected=true; } Require(rejected,"corrupt range silently fell back");
}
/// <summary>Checks aliases survive owner release, input relocation, exact binary comparisons and cache serialization.</summary>
static void SharedKeys() {
    const std::string state("material\0tail",13), transform("world\0tail",10);
    auto owner=std::make_shared<const std::array<std::string,3>>(std::array<std::string,3>{state,"",transform});
    std::weak_ptr<const std::array<std::string,3>> lifetime=owner;
    auto shared=Input(7); shared.State="ignored"; shared.Transform="ignored";
    shared.SharedState={owner,&(*owner)[0]}; shared.SharedCacheState={owner,&(*owner)[1]}; shared.SharedTransform={owner,&(*owner)[2]};
    owner.reset();
    auto copied=shared; std::vector<VulkanMeshBatcher::Input> relocated; relocated.push_back(std::move(copied));
    shared={}; relocated.reserve(32);
    Require(!lifetime.expired() && relocated[0].GetState()==state && relocated[0].GetCacheState().empty() && relocated[0].GetTransform()==transform,"key aliases did not retain immutable storage through input copies and moves");
    auto owned=Input(7); owned.State=state; owned.Transform=transform; owned.First=3;
    VulkanMeshBatcher ranges({true,1024,false,3,0},{});
    auto rangeInput=relocated[0]; rangeInput.Source.reset();
    Require(ranges.Build({rangeInput,owned}).size()==1,"owned/shared equal binary keys did not coalesce");
    owned.State.back()='X'; Require(ranges.Build({rangeInput,owned}).size()==2,"state bytes after NUL were ignored");
    owned.State=state; owned.Transform.back()='X'; Require(ranges.Build({rangeInput,owned}).size()==2,"transform bytes after NUL were ignored");
    Device device; VulkanMeshBatcher cache({true,4096,false,3,0},device.Backend());
    owned.Mesh=8; owned.First=0; owned.Transform=transform;
    cache.BeginFrame(); auto draws=cache.Build({relocated[0],owned}); cache.EndFrame();
    Require(draws.size()==1 && draws[0].Baked,"shared/owned material failed cross-mesh planning");
    const auto handle=draws[0].Mesh;
    auto equivalent=relocated[0]; equivalent.SharedState.reset(); equivalent.SharedCacheState.reset(); equivalent.SharedTransform.reset(); equivalent.State=state; equivalent.CacheState=state; equivalent.Transform=transform;
    cache.BeginFrame(); draws=cache.Build({equivalent,owned}); cache.EndFrame();
    Require(draws.size()==1 && draws[0].Mesh==handle && device.Uploads==1,"empty shared cache state failed owned state fallback identity");
    equivalent.SharedCacheState=std::make_shared<const std::string>(std::string("cache\0A",7));
    cache.BeginFrame(); draws=cache.Build({equivalent,owned}); cache.EndFrame(); const auto changed=draws[0].Mesh;
    Require(changed!=handle && device.Uploads==2,"shared cache state was omitted from serialization");
    equivalent.SharedCacheState=std::make_shared<const std::string>(std::string("cache\0B",7));
    cache.BeginFrame(); draws=cache.Build({equivalent,owned}); cache.EndFrame();
    Require(draws[0].Mesh!=changed && device.Uploads==3,"cache state bytes after NUL were ignored");
    equivalent.SharedTransform=std::make_shared<const std::string>(std::string("world\0TAIL",10));
    cache.BeginFrame(); draws=cache.Build({equivalent,owned}); cache.EndFrame();
    Require(device.Uploads==4,"shared transform changes were omitted from serialization");
    rangeInput={}; relocated.clear(); Require(lifetime.expired(),"input keys leaked their immutable owner");
}
/// <summary>Measures key preparation and release with long payloads, excluding vector and snapshot creation.</summary>
static void SharedKeyAllocations() {
    constexpr size_t count=21940;
    auto owner=std::make_shared<const std::array<std::string,3>>(std::array<std::string,3>{std::string(2048,'s'),std::string(2048,'c'),std::string(128,'t')});
    std::vector<VulkanMeshBatcher::Input> inputs(count);
    KeyAllocations=0; CountKeyAllocations=true;
    for(auto& input:inputs) { input.State=(*owner)[0]; input.CacheState=(*owner)[1]; input.Transform=(*owner)[2]; }
    CountKeyAllocations=false; const auto ownedAllocations=KeyAllocations.load();
    inputs.clear(); inputs.resize(count);
    KeyAllocations=0; CountKeyAllocations=true;
    for(auto& input:inputs) { input.SharedState={owner,&(*owner)[0]}; input.SharedCacheState={owner,&(*owner)[1]}; input.SharedTransform={owner,&(*owner)[2]}; }
    CountKeyAllocations=false; const auto sharedAllocations=KeyAllocations.load();
    Require(ownedAllocations==count*3 && sharedAllocations==0,"long immutable key binding allocated payload/control-block storage");
    std::cout << "key_inputs=" << count << " owned_key_allocations=" << ownedAllocations << " shared_key_allocations=" << sharedAllocations << '\n';
}
/// <summary>Replays immutable ordered inputs while refreshing live cache leases and frame-local subjects.</summary>
static void RetainedPlans() {
    Device device; VulkanMeshBatcher batcher({true,4096,false,3,1024},device.Backend());
    auto a=Input(7),b=Input(8); std::vector<const VulkanMeshBatcher::Input*> pointers{&a,&b}; std::vector<int> subjects{11,12};
    const VulkanMeshBatcher::InputView view(pointers,subjects); const std::vector<uint64_t> tokens{1,2};
    batcher.BeginFrame(); auto mesh=batcher.BuildRetained(view,tokens).at(0).Mesh; batcher.EndFrame();
    subjects={21,22}; Require(view.Subject(0)==21,"retained trace subject was captured from old frame");
    for(int frame=0;frame<3;++frame) {
        batcher.BeginFrame(); KeyAllocations=0; CountKeyAllocations=true; const auto& output=batcher.BuildRetained(view,tokens); CountKeyAllocations=false; batcher.EndFrame();
        Require(KeyAllocations==0,"warm retained plan allocated");
        Require(output.size()==1 && output[0].Mesh==mesh && device.Live.contains(mesh),"replay lost resident batch lease");
        Require(batcher.GetCounters().PlanReplays==1 && batcher.GetCounters().ReferencedIndexVisits==0 && batcher.GetCounters().CacheHits==1,"warm replay ran planner or lost counters");
    }
    batcher.Clear(); batcher.BeginFrame(); mesh=batcher.BuildRetained(view,tokens).at(0).Mesh; batcher.EndFrame();
    Require(device.Uploads==2 && batcher.GetCounters().PlanReplays==0,"clear replayed released geometry");
    batcher.BeginFrame(); batcher.EndFrame(); Require(!device.Live.contains(mesh),"invisible batch failed retirement");
    batcher.BeginFrame(); batcher.BuildRetained(view,tokens); batcher.EndFrame();
    Require(device.Uploads==3 && batcher.GetCounters().PlanReplays==0,"evicted plan replayed stale mesh");
    batcher.BeginFrame(); batcher.BuildRetained(view,{2,1}); batcher.EndFrame();
    Require(batcher.GetCounters().PlanReplays==0,"changed ordered tokens replayed");
    VulkanMeshBatcher dynamic({false,0,true,3,4096},device.Backend()); a.Static=b.Static=false;
    dynamic.BeginFrame(); dynamic.BuildRetained(view,tokens); dynamic.EndFrame(); dynamic.BeginFrame(); dynamic.BuildRetained(view,tokens); dynamic.EndFrame();
    Require(dynamic.GetCounters().PlanReplays==0 && device.Updates==2,"dynamic work was retained");
}
/// <summary>Retries a capacity-limited cold plan after unvisited geometry retires, instead of freezing fallback forever.</summary>
static void RetainedBudgetRecovery() {
    Device device; VulkanMeshBatcher batcher({true,256,false,3,1024},device.Backend());
    auto a=Input(7),b=Input(8),c=Input(9),d=Input(10);
    std::vector<VulkanMeshBatcher::Input> old{a,b};
    batcher.BeginFrame(); batcher.Build(old); batcher.EndFrame();
    std::vector<const VulkanMeshBatcher::Input*> pointers{&c,&d}; std::vector<int> subjects{-1,-1};
    const VulkanMeshBatcher::InputView view(pointers,subjects);
    batcher.BeginFrame(); const auto& blocked=batcher.BuildRetained(view,{5,6});
    Require(blocked.size()==2 && !blocked[0].Baked,"capacity test failed to exhaust static budget"); batcher.EndFrame();
    batcher.BeginFrame(); const auto& recovered=batcher.BuildRetained(view,{5,6}); batcher.EndFrame();
    Require(recovered.size()==1 && recovered[0].Baked && batcher.GetCounters().PlanReplays==0,"retained capacity fallback failed to recover after eviction");
}
/// <summary>Preserves original submesh slots through same-source coalescing and retains geometry across camera state changes.</summary>
static void IndirectStaticRanges() {
    Device device;auto backend=device.Backend();backend.StaticIndirectPages=true;
    VulkanMeshBatcher batcher({true,104,false,3,0},std::move(backend));
    auto a=Input(7),b=a;b.First=3;
    batcher.BeginFrame();auto draws=batcher.Build({a,b});batcher.EndFrame();
    Require(draws.size()==1 && !draws[0].Baked && device.Live.contains(draws[0].Mesh),"single coalesced unit did not create a local-space indirect static page");
    const auto handle=draws[0].Mesh;const auto& page=device.Live.at(handle);
    Require(page.Ranges.size()==2 && page.Ranges[0].FirstIndex==0 && page.Ranges[0].IndexCount==3 && page.Ranges[1].FirstIndex==3 && page.Ranges[1].IndexCount==3,"coalescing lost authored indirect ranges");
    Require(page.BorrowedMesh==a.Mesh && page.BorrowedSource==a.Source && page.Vertices.empty() && page.Indices.empty(),"single-source page duplicated or baked original geometry");
    Require(batcher.GetStaticBytes()==104 && batcher.GetCounters().UploadedBytes==104,"borrowed page must account metadata only");
    auto changedA=a,changedB=b;changedA.State=changedB.State="new-material";
    Device spareDevice;auto spareBackend=spareDevice.Backend();spareBackend.StaticIndirectPages=true;
    VulkanMeshBatcher spare({true,4096,false,3,0},std::move(spareBackend));
    std::vector<VulkanMeshBatcher::Input> stable{a,b};spare.BeginFrame();spare.BuildRetained(VulkanMeshBatcher::InputView(stable),{1,2});spare.EndFrame();
    stable={changedA,changedB};spare.BeginFrame();spare.BuildRetained(VulkanMeshBatcher::InputView(stable),{3,4});spare.EndFrame();
    spare.BeginFrame();spare.BuildRetained(VulkanMeshBatcher::InputView(stable),{3,4});spare.EndFrame();
    Require(spare.GetCounters().PlanReplays==1,"unrelated retirement invalidated a fully resident successful plan");
    a.CacheState=b.CacheState="geometry";a.State=b.State="cameraA";
    batcher.Clear();batcher.BeginFrame();draws=batcher.Build({a,b});batcher.EndFrame();const auto cameraHandle=draws[0].Mesh;
    a.State=b.State="cameraB";batcher.BeginFrame();draws=batcher.Build({a,b});batcher.EndFrame();
    Require(draws[0].Mesh==cameraHandle && !batcher.GetCounters().UploadedBytes,"camera change rebuilt persistent geometry");
    a.Count=6;batcher.BeginFrame();draws=batcher.Build({a});batcher.EndFrame();
    Require(draws.size()==1 && draws[0].Mesh==a.Mesh,"changed segmentation reused stale page slots");
    batcher.BeginFrame();draws=batcher.Build({a});batcher.EndFrame();
    Require(!draws[0].Baked && device.Live.at(draws[0].Mesh).Ranges.size()==1,"changed segmentation failed page rebuild after retirement");
    const auto localHandle=draws[0].Mesh;a.Transform="moved-exact-native-world";
    batcher.BeginFrame();draws=batcher.Build({a});batcher.EndFrame();
    Require(draws[0].Mesh==localHandle && !batcher.GetCounters().UploadedBytes,"local-space page rebuilt unchanged source geometry after world change");
    batcher.Clear();auto unsupported=Input(12);unsupported.Supported=false;
    batcher.BeginFrame();draws=batcher.Build({unsupported});batcher.EndFrame();Require(!draws[0].Baked && device.Live.empty(),"unsupported shader entered GPU page path");
}
/// <summary>Retains stable capacity fallback while invalidating it whenever residency changes enough to permit recovery.</summary>
static void RetainedSaturatedBudget() {
    Device device;VulkanMeshBatcher batcher({true,216,false,3,0},device.Backend());
    auto a=Input(1),b=Input(2),c=Input(3),d=Input(4);c.State=d.State="other";
    std::vector<VulkanMeshBatcher::Input> inputs{a,b,c,d};const std::vector<uint64_t> tokens{1,2,3,4};
    batcher.BeginFrame();batcher.BuildRetained(VulkanMeshBatcher::InputView(inputs),tokens);batcher.EndFrame();
    batcher.BeginFrame();const auto& warm=batcher.BuildRetained(VulkanMeshBatcher::InputView(inputs),tokens);batcher.EndFrame();
    Require(warm.size()==3 && batcher.GetCounters().PlanReplays==1,"steady static budget saturation forced full planning forever");
    const auto oldHandle=warm[0].Mesh;batcher.Clear();batcher.BeginFrame();batcher.Build(inputs);batcher.EndFrame();
    Require(batcher.GetStaticBytes()==216 && !device.Live.contains(oldHandle),"equal-byte replacement test failed to change lease");
    batcher.BeginFrame();batcher.BuildRetained(VulkanMeshBatcher::InputView(inputs),tokens);batcher.EndFrame();
    Require(batcher.GetCounters().PlanReplays==0,"equal-byte replacement reused an invalid old lease");
}
/// <summary>Respects device allocation capacity without creating temporary pages and recovers after old pages retire.</summary>
static void IndirectPageCapacity() {
    Device device;auto backend=device.Backend();backend.StaticIndirectPages=true;backend.StaticPageLimit=1;
    VulkanMeshBatcher batcher({true,4096,false,3,0},std::move(backend));auto a=Input(1),b=Input(2);b.State="other";
    std::vector<VulkanMeshBatcher::Input> inputs{a,b};
    batcher.BeginFrame();const auto cold=batcher.BuildRetained(VulkanMeshBatcher::InputView(inputs),{1,2});batcher.EndFrame();
    Require(cold.size()==2 && device.Live.contains(cold[0].Mesh) && cold[1].Mesh==b.Mesh && device.Uploads==1,"page capacity did not preserve original mesh fallback");
    batcher.BeginFrame();batcher.BuildRetained(VulkanMeshBatcher::InputView(inputs),{1,2});batcher.EndFrame();
    Require(batcher.GetCounters().PlanReplays==1 && device.Uploads==1,"page allocation cap prevented stable replay");
    inputs={b};batcher.BeginFrame();batcher.BuildRetained(VulkanMeshBatcher::InputView(inputs),{2});batcher.EndFrame();
    batcher.BeginFrame();const auto recovered=batcher.BuildRetained(VulkanMeshBatcher::InputView(inputs),{2});batcher.EndFrame();
    Require(device.Live.contains(recovered[0].Mesh) && device.Uploads==2 && batcher.GetCounters().PlanReplays==0,"page capacity did not recover after retirement");
}
/// <summary>Runs CPU batch and lifetime checks without Vulkan device creation.</summary>
int main() {
    try { IndirectPageCapacity(); IndirectStaticRanges(); RetainedSaturatedBudget(); RetainedBudgetRecovery(); RetainedPlans(); SharedKeys(); SharedKeyAllocations(); Ranges(); StaticCache(); WarmCacheIndexVisits(); Dynamic(); PartialUploadFailure(); LargeOrderedRun(); Corruption(); std::cout << "PASS mesh-batching ranges cross-mesh remap static-cache moving-dynamic budgets modes failures partial-upload large-run lifetime\n"; return 0; }
    catch(const std::exception& error) { std::cerr << "FAIL " << error.what() << '\n'; return 1; }
}
