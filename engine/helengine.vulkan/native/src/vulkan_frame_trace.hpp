#pragma once
#include <array>
#include <source_location>
#include "vulkan_frame_trace_source_key.hpp"
#include "vulkan_frame_trace_subject_key.hpp"
#include <algorithm>
#include "vulkan_frame_trace_subject.hpp"
#include <memory>
#include <map>
#include "vulkan_frame_cpu_sample.hpp"
#include <chrono>
#include <cstdint>
#include <cstdlib>
#include <ostream>
#include <sstream>
#include <stdexcept>
#include <string_view>
#include <string>
#include <vector>
#include <iomanip>

namespace helengine::vulkan {
/// <summary>Buffers opt-in player-thread CPU wall-time spans; submission scopes include driver/GPU waits and are never GPU timestamps.</summary>
class VulkanFrameTrace final {
    /// <summary>Bounds diagnostic storage and rejects an over-budget frame instead of exporting a partial timeline.</summary>
    static constexpr int Capacity=262144;
    /// <summary>Limits every exported integer to the exact JavaScript integer range.</summary>
    static constexpr uint64_t MaximumSafeInteger=9007199254740991ULL;
    /// <summary>Records the caller-supplied frame counter without storing user or scene metadata.</summary>
    uint64_t Frame=0;
    /// <summary>Marks whether the player thread currently owns a valid measured root.</summary>
    bool Active=false;
    /// <summary>Tracks the open parent and enforces strict stack nesting.</summary>
    int Top=-1;
    /// <summary>Counts buffered events including the root, whose identifier is always zero.</summary>
    int Count=0;
    /// <summary>Stores the monotonic origin used solely for relative integer nanoseconds.</summary>
    std::chrono::steady_clock::time_point Origin;
    /// <summary>Rejects a clock that moves backwards within a frame.</summary>
    uint64_t Last=0;
    /// <summary>Borrows static software scope names until post-measurement serialization.</summary>
    std::array<const char*,Capacity> Names;
    /// <summary>Stores parent identifiers, with minus one reserved for the root.</summary>
    std::array<int,Capacity> Parents;
    /// <summary>Stores frame-relative monotonic event starts.</summary>
    std::array<uint64_t,Capacity> Starts;
    /// <summary>Stores frame-relative monotonic event ends after each scope finishes.</summary>
    std::array<uint64_t,Capacity> Ends;

    /// <summary>Stores actual item counts attached to each measured operation.</summary>
    std::array<uint64_t,Capacity> Items;
    /// <summary>Stores actual payload bytes attached to each measured operation, excluding allocator overhead.</summary>
    std::array<uint64_t,Capacity> Bytes;
    /// <summary>Distinguishes observed zero quantities from scopes whose item or byte count was never measured.</summary>
    std::array<uint8_t,Capacity> CounterPresence;
    /// <summary>Retains bounded completed CPU samples with copied software names until post-frame serialization.</summary>
    std::vector<VulkanFrameCpuSample> CpuSamples;
    /// <summary>Deduplicates compiler-provided containing-function locations for this frame.</summary>
    std::map<VulkanFrameTraceSourceKey,int> SourceLookup;
    /// <summary>Borrows compiler-owned static source strings until serialization.</summary>
    std::vector<std::source_location> Sources;
    /// <summary>Retains copied resource identities without borrowing scene objects.</summary>
    std::vector<VulkanFrameTraceSubject> Subjects;
    /// <summary>Deduplicates mesh occurrences by object, material, native geometry and submesh.</summary>
    std::map<VulkanFrameTraceSubjectKey,int> SubjectLookup;
    /// <summary>Assigns frame-local object identities without exposing addresses.</summary>
    std::map<const void*,int> Objects;
    /// <summary>Records explicit shared membership; shared timings are never divided among participants.</summary>
    std::vector<std::vector<int>> Batches;
    /// <summary>Reuses identical shared membership across preparation and command emission.</summary>
    std::map<std::vector<int>,int> BatchLookup;
    /// <summary>References source locations, or minus one for the synthetic frame root.</summary>
    std::array<int,Capacity> SourceIds;
    /// <summary>References inherited mesh identity, excluding shared batch work.</summary>
    std::array<int,Capacity> SubjectIds;
    /// <summary>References shared batch membership independently of mesh identity.</summary>
    std::array<int,Capacity> BatchIds;
    /// <summary>Labels measured CPU preparation or actual Vulkan command emission.</summary>
    std::array<const char*,Capacity> Phases;
    /// <summary>Shares the steady-clock microsecond epoch with the generated CPU profile provider.</summary>
    uint64_t OriginEpochUs=0;
    /// <summary>Escapes arbitrary software type names without admitting invalid JSON strings.</summary>
    static void WriteString(std::ostream& output,const char* value) {
        output << '"';
        for(const unsigned char* cursor=reinterpret_cast<const unsigned char*>(value);*cursor;++cursor) {
            if(*cursor=='"' || *cursor=='\\') output << '\\' << *cursor;
            else if(*cursor<32) output << "\\u" << std::hex << std::setw(4) << std::setfill('0') << unsigned(*cursor) << std::dec;
            else output << *cursor;
        }
        output << '"';
    }
    /// <summary>Lazily owns diagnostic storage only on recording threads; disabled threads retain no large TLS arrays, while inline callers share one owner under the C++ ODR.</summary>
    static VulkanFrameTrace& Current() {
        static thread_local std::unique_ptr<VulkanFrameTrace> recorder=std::make_unique<VulkanFrameTrace>();
        return *recorder;
    }
    /// <summary>Reads exact opt-in once; disabled scopes never touch the recorder or clock.</summary>
    static bool Enabled() {
        static const char* value=std::getenv("HELENGINE_FRAME_TRACE");
        static const bool enabled=value && std::string_view(value)=="1";
        return enabled;
    }
    /// <summary>Invalidates the frame before reporting a broken nesting contract, preventing accidental later export.</summary>
    void Invalid(const char* message) {
        Active=false;
        throw std::logic_error(message);
    }
    /// <summary>Samples elapsed wall time and checks monotonicity and exact serialization range.</summary>
    uint64_t Now() {
        const auto elapsed=std::chrono::duration_cast<std::chrono::nanoseconds>(std::chrono::steady_clock::now()-Origin).count();
        if(elapsed<0 || static_cast<uint64_t>(elapsed)<Last || static_cast<uint64_t>(elapsed)>MaximumSafeInteger) Invalid("Frame trace clock is nonmonotonic or outside the exact integer range.");
        Last=static_cast<uint64_t>(elapsed);
        return Last;
    }
public:
    /// <summary>Starts a completed-player-frame candidate; all prior invalid storage is reset without exporting it.</summary>
    static void BeginFrame(uint64_t frame) {
        if(!Enabled()) return;
        auto& state=Current();
        if(state.Active) state.Invalid("Frame trace BeginFrame requires the previous frame to be closed.");
        if(frame>MaximumSafeInteger) throw std::overflow_error("Frame trace counter exceeds the exact integer range.");
        state.SourceLookup.clear(); state.Sources.clear(); state.Subjects.clear(); state.SubjectLookup.clear(); state.Objects.clear(); state.Batches.clear(); state.BatchLookup.clear();
        state.SourceIds[0]=-1; state.SubjectIds[0]=-1; state.BatchIds[0]=-1; state.Phases[0]=nullptr;
        state.Frame=frame; state.Count=1; state.Top=0; state.Last=0;
        state.Names[0]="Frame"; state.Parents[0]=-1; state.Starts[0]=0;
        state.Origin=std::chrono::steady_clock::now();
        state.OriginEpochUs=std::chrono::duration_cast<std::chrono::microseconds>(state.Origin.time_since_epoch()).count();
        state.CpuSamples.clear(); state.Items[0]=0; state.Bytes[0]=0; state.CounterPresence[0]=0; state.Active=true;
    }
    /// <summary>Discards a non-rendering minimized-loop iteration without exporting a partial root or retaining open parents.</summary>
    static void DiscardFrame() {
        if(!Enabled()) return;
        auto& state=Current(); state.Active=false; state.Top=-1; state.Count=0;
    }
    /// <summary>Begins a named static software scope and returns its stack identity, or minus one when tracing is inactive.</summary>
    static int BeginScope(const char* name,std::source_location location=std::source_location::current()) {
        if(!Enabled()) return -1;
        auto& state=Current();
        if(!state.Active) return -1;
        if(!name || !*name) { state.Active=false; throw std::invalid_argument("Frame trace scope requires a static software name."); }
        for(const char* cursor=name;*cursor;++cursor) {
            if(!((*cursor>='A' && *cursor<='Z') || (*cursor>='a' && *cursor<='z') || (*cursor>='0' && *cursor<='9') || *cursor=='.' || *cursor=='_')) {
                state.Active=false; throw std::invalid_argument("Frame trace scope name must contain only software identifiers.");
            }
        }
        if(state.Count==Capacity) {
            state.Active=false;
            std::map<std::string_view,uint64_t> counts;
            for(int index=0;index<state.Count;++index) ++counts[state.Names[index]];
            std::ostringstream message;
            message << "Frame trace span capacity " << Capacity << " exceeded opening " << name << "; frame discarded. Buffered scope counts:";
            for(const auto& entry:counts) message << ' ' << entry.first << '=' << entry.second;
            throw std::overflow_error(message.str());
        }
        const int id=state.Count++;
        const VulkanFrameTraceSourceKey key{location.file_name(),location.function_name(),location.line()};
        auto source=state.SourceLookup.find(key);
        if(source==state.SourceLookup.end()) {
            source=state.SourceLookup.emplace(key,static_cast<int>(state.Sources.size())).first;
            state.Sources.push_back(location);
        }
        state.SourceIds[id]=source->second; state.SubjectIds[id]=state.SubjectIds[state.Top]; state.BatchIds[id]=state.BatchIds[state.Top]; state.Phases[id]=state.Phases[state.Top];
        state.Items[id]=0; state.Bytes[id]=0; state.CounterPresence[id]=0;
        state.Names[id]=name; state.Parents[id]=state.Top; state.Starts[id]=state.Now(); state.Top=id;
        return id;
    }
    /// <summary>Reports whether a frame is active without reading a clock or allocating storage.</summary>
    static bool IsEnabled() { return Enabled(); }
    /// <summary>Checks frame ownership before collecting optional quantities.</summary>
    static bool IsRecording() { return Enabled() && Current().Active; }
    /// <summary>Returns the current mesh reference for queued work, without touching disabled storage.</summary>
    static int CurrentSubject() { return IsRecording()?Current().SubjectIds[Current().Top]:-1; }
    /// <summary>Returns shared membership for deferred command emission.</summary>
    static int CurrentBatch() { return IsRecording()?Current().BatchIds[Current().Top]:-1; }
    /// <summary>Copies and interns actual engine identities only during enabled capture.</summary>
    static int RegisterSubject(const void* object,uint32_t entityId,const std::string& modelId,const std::string& materialId,uint64_t meshId,int submeshIndex,uint32_t indexCount) {
        if(!IsRecording()) return -1;
        auto& state=Current();
        if(!object || meshId>MaximumSafeInteger || submeshIndex<0) state.Invalid("Invalid frame trace subject identity.");
        const VulkanFrameTraceSubjectKey key{object,materialId,meshId,submeshIndex};
        const auto found=state.SubjectLookup.find(key); if(found!=state.SubjectLookup.end()) return found->second;
        const auto [entry,inserted]=state.Objects.emplace(object,static_cast<int>(state.Objects.size()));
        const int id=static_cast<int>(state.Subjects.size());
        state.Subjects.push_back({entry->second,entityId,modelId,materialId,meshId,submeshIndex,indexCount}); state.SubjectLookup.emplace(key,id); return id;
    }
    /// <summary>Captures unique real participants in a shared operation; callers supply a complete contiguous input range.</summary>
    static int RegisterBatch(std::vector<int> subjects) {
        if(!IsRecording()) return -1;
        auto& state=Current();
        subjects.erase(std::remove(subjects.begin(),subjects.end(),-1),subjects.end());
        std::sort(subjects.begin(),subjects.end()); subjects.erase(std::unique(subjects.begin(),subjects.end()),subjects.end());
        if(subjects.empty()) return -1;
        for(int subject:subjects) if(subject<0 || static_cast<size_t>(subject)>=state.Subjects.size()) state.Invalid("Unknown frame trace batch participant.");
        const auto found=state.BatchLookup.find(subjects); if(found!=state.BatchLookup.end()) return found->second;
        const int id=static_cast<int>(state.Batches.size()); state.BatchLookup.emplace(subjects,id); state.Batches.push_back(std::move(subjects)); return id;
    }
    /// <summary>Attaches actual work ownership to an open scope, clearing direct identity for shared work.</summary>
    static void SetContext(int id,int subject,int batch,const char* phase) {
        if(id<0 || !IsRecording()) return;
        auto& state=Current();
        if(id!=state.Top || subject< -1 || batch< -1 || (subject>=0 && static_cast<size_t>(subject)>=state.Subjects.size()) || (batch>=0 && static_cast<size_t>(batch)>=state.Batches.size())) state.Invalid("Invalid frame trace scope context.");
        state.SubjectIds[id]=batch>=0?-1:subject; state.BatchIds[id]=batch; state.Phases[id]=phase;
    }
    /// <summary>Accumulates directly measured quantities for one open scope with checked exact JSON integers.</summary>
    static void AddCounters(int id,uint64_t items,uint64_t bytes,uint8_t presence=3) {
        if(id<0 || !Enabled()) return;
        auto& state=Current(); if(!state.Active) return;
        if(id>=state.Count || items>MaximumSafeInteger-state.Items[id] || bytes>MaximumSafeInteger-state.Bytes[id]) state.Invalid("Frame trace counter outside exact integer range.");
        state.Items[id]+=items; state.Bytes[id]+=bytes; state.CounterPresence[id]|=presence;
    }
    /// <summary>Preserves actual provider intervals independently of render stack order, with explicit component identity and microsecond resolution.</summary>
    static void RecordCompletedSample(const char* name,uint64_t startEpochUs,uint64_t endEpochUs,uint32_t typeHash=0,uint64_t registrationId=0,uint32_t entityId=0,int32_t updateIndex=-1) {
        if(!IsRecording()) return;
        auto& state=Current();
        if(!name || !*name || endEpochUs<startEpochUs || startEpochUs<state.OriginEpochUs || (endEpochUs-state.OriginEpochUs)>MaximumSafeInteger/1000 || registrationId>MaximumSafeInteger) state.Invalid("Frame trace CPU sample has invalid measured boundaries or identity.");
        if(state.CpuSamples.size()>=Capacity) state.Invalid("Frame trace CPU sample capacity exceeded; frame discarded.");
        // Subtract the original nanosecond origin, retaining sub-microsecond quantization explicitly.
        const auto originNs=std::chrono::duration_cast<std::chrono::nanoseconds>(state.Origin.time_since_epoch()).count();
        const auto start=static_cast<int64_t>(startEpochUs*1000)-originNs;
        const auto end=static_cast<int64_t>(endEpochUs*1000)-originNs;
        state.CpuSamples.push_back({name,start,end,typeHash,registrationId,entityId,updateIndex});
    }
    /// <summary>Closes the current scope only; a mismatched parent invalidates the entire diagnostic frame.</summary>
    static void EndScope(int id) {
        if(id<0 || !Enabled()) return;
        auto& state=Current();
        if(!state.Active) return;
        if(id==0 || id!=state.Top) state.Invalid("Frame trace scopes must finish in reverse nesting order.");
        state.Ends[id]=state.Now(); state.Top=state.Parents[id];
    }
    /// <summary>Ends the measured root before serializing one exact JSON line with real renderer metrics; no export cost belongs to the root.</summary>
    static void EndFrame(std::ostream& output,uint64_t drawCalls,uint64_t meshSamples,uint64_t rootHits,uint64_t rootMisses,uint32_t width,uint32_t height,bool ready) {
        if(!Enabled()) return;
        auto& state=Current();
        if(!state.Active) throw std::logic_error("Cannot export an invalid or absent frame trace.");
        if(state.Top!=0) state.Invalid("Cannot export a frame trace with unfinished scopes.");
        state.Ends[0]=state.Now(); state.Active=false; state.Top=-1;
        if(drawCalls>MaximumSafeInteger || meshSamples>MaximumSafeInteger || rootHits>MaximumSafeInteger || rootMisses>MaximumSafeInteger) throw std::overflow_error("Frame trace metric exceeds the exact integer range.");
        std::ostringstream json;
        json << "HELENGINE_FRAME_TRACE {\"frame\":" << state.Frame << ",\"unit\":\"ns\",\"thread\":\"player\",\"spans\":[";
        for(int id=0;id<state.Count;++id) {
            if(id) json << ',';
            json << "{\"id\":" << id << ",\"parent\":";
            if(state.Parents[id]<0) json << "null"; else json << state.Parents[id];
            json << ",\"name\":\"" << state.Names[id] << "\",\"start\":" << state.Starts[id] << ",\"end\":" << state.Ends[id];
            if(state.SourceIds[id]>=0) json << ",\"sourceId\":" << state.SourceIds[id];
            if(state.SubjectIds[id]>=0) json << ",\"subjectId\":" << state.SubjectIds[id];
            if(state.BatchIds[id]>=0) json << ",\"batchId\":" << state.BatchIds[id];
            if(state.Phases[id]) { json << ",\"phase\":"; WriteString(json,state.Phases[id]); }
            if(state.CounterPresence[id]) {
                json << ",\"counters\":{";
                if(state.CounterPresence[id]&1) json << "\"items\":" << state.Items[id];
                if(state.CounterPresence[id]&2) { if(state.CounterPresence[id]&1) json << ','; json << "\"bytes\":" << state.Bytes[id]; }
                json << '}';
            }
            json << '}';
        }
        json << "],\"sources\":[";
        for(size_t i=0;i<state.Sources.size();++i) {
            if(i) json << ',';
            const auto& source=state.Sources[i];
            json << "{\"id\":" << i << ",\"function\":"; WriteString(json,source.function_name());
            json << ",\"file\":"; WriteString(json,source.file_name()); json << ",\"line\":" << source.line() << '}';
        }
        json << "],\"subjects\":[";
        for(size_t i=0;i<state.Subjects.size();++i) {
            if(i) json << ',';
            const auto& subject=state.Subjects[i];
            json << "{\"id\":" << i << ",\"objectId\":" << subject.ObjectId;
            if(subject.EntityId) json << ",\"entityId\":" << subject.EntityId;
            json << ",\"modelId\":"; WriteString(json,subject.ModelId.c_str()); json << ",\"materialId\":"; WriteString(json,subject.MaterialId.c_str());
            json << ",\"meshId\":" << subject.MeshId << ",\"submeshIndex\":" << subject.SubmeshIndex << ",\"indexCount\":" << subject.IndexCount << '}';
        }
        json << "],\"batches\":[";
        for(size_t i=0;i<state.Batches.size();++i) {
            if(i) json << ',';
            json << "{\"id\":" << i << ",\"subjectIds\":[";
            for(size_t j=0;j<state.Batches[i].size();++j) { if(j) json << ','; json << state.Batches[i][j]; } json << "]}";
        }
        json << "],\"cpuSamples\":[";
        for(size_t index=0;index<state.CpuSamples.size();++index) {
            if(index) json << ',';
            const auto& sample=state.CpuSamples[index];
            json << "{\"name\":"; WriteString(json,sample.Name.c_str());
            json << ",\"start\":" << sample.Start << ",\"end\":" << sample.End << ",\"typeHash\":" << sample.TypeHash << ",\"registrationId\":" << sample.RegistrationId << ",\"entityId\":" << sample.EntityId << ",\"updateIndex\":" << sample.UpdateIndex << '}';
        }
        json << "],\"originEpochUs\":" << state.OriginEpochUs << ",\"cpuSampleResolutionNs\":1000,\"spanCapacity\":" << Capacity << ",\"complete\":true,\"metrics\":{\"drawCalls\":" << drawCalls << ",\"meshSamples\":" << meshSamples << ",\"rootHits\":" << rootHits << ",\"rootMisses\":" << rootMisses << ",\"width\":" << width << ",\"height\":" << height << ",\"ready\":" << (ready?"true":"false") << "}}\n";
        output << json.str() << std::flush;
    }
};
}
