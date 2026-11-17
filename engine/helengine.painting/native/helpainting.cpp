#include "helpainting.h"
#include "PaintBrushDefinition.hpp"
#include "PaintContract.hpp"
#include "PaintSession.hpp"
#include "PaintSnapshot.hpp"
#include "PaintStrokeRecord.hpp"
#include "PaintHistory.hpp"
#include "PaintCanvasDescriptor.hpp"
#include "PaintStrokeSampler.hpp"
#include "PaintSample.hpp"
#include "PaintStamp.hpp"
#include "IPaintSurfaceBackend.hpp"
#include "runtime/native_exceptions.hpp"
#include <algorithm>
#include <atomic>
#include <limits>
#include <map>
#include <mutex>
#include <cstring>
#include <exception>
#include <memory>
#include <stdexcept>
#include <vector>

/** Error storage never retains caller input or allocates while reporting an allocation failure. */
static thread_local char LastError[512]{};
/** Releases generated IDisposable payloads before deleting their native owning wrapper. */
template<class T> struct DisposeDelete {
    void operator()(T* value) const noexcept { if(value) { value->Dispose(); delete value; } }
};
template<class T> using Owned=std::unique_ptr<T,DisposeDelete<T>>;
/** A sampler owns its generated implementation and the borrowed ABI batch it most recently returned. */
struct HPSampler {
    Owned<PaintStrokeSampler> Value;
    std::vector<HPStamp> Marks;
};
/** Copies bounded diagnostics, keeping all C entry points exception-safe. */
static void error(const char* message) noexcept {
    const auto count=std::min(std::strlen(message),sizeof(LastError)-1);
    std::memcpy(LastError,message,count); LastError[count]=0;
}
/** Catches and deletes generated pointer exceptions as well as ordinary native value exceptions. */
template<class F> static int32_t guarded(F&& action) noexcept {
    try { action(); error(""); return 0; }
    catch(Exception* failure) { error(failure ? failure->what() : "Generated painting failure"); delete failure; return 1; }
    catch(const std::invalid_argument& failure) { error(failure.what()); return 1; }
    catch(const std::exception& failure) { error(failure.what()); return 3; }
    catch(...) { error("Unknown native painting failure"); return 3; }
}
/** Requires explicit live caller ownership rather than manufacturing default data. */
static void require(bool valid,const char* message) { if(!valid) throw std::invalid_argument(message); }
/** Copies the bounded UTF-8 identity before any generated owner can retain it. */
static HeCppString identity(const char* id) {
    require(id && strnlen(id,513)<=512,"Invalid bounded painting identity");
    return HeCppString(id);
}
/** Translates fixed ABI fields without duplicating the generated validation rules. */
static PaintBrushDefinition brushValue(const HPBrush* input) {
    require(input && input->eraser<=1,"Invalid brush ABI");
    require(input->version==1,"Legacy C brush ABI supports version one only; use the C# painting library for soft brushes");
    PaintBrushDefinition result;
    result.Version=static_cast<int32_t>(input->version); result.Id=identity(input->id);
    result.Diameter=input->diameter; result.Red=input->red; result.Green=input->green;
    result.Blue=input->blue; result.Opacity=input->opacity; result.Eraser=input->eraser!=0;
    return result;
}
/** Translates original binary64 authored values without a float round trip. */
static PaintSample sampleValue(const HPSample& input) {
    require(input.reserved==0,"Nonzero sample ABI reserved field");
    PaintSample result; result.Index=input.index; result.X=input.x; result.Y=input.y;
    result.Pressure=input.pressure; result.Time=input.time; result.Altitude=input.altitude; result.Azimuth=input.azimuth;
    return result;
}
/** Copies a bounded sample array before generated code admits or stores it. */
static std::unique_ptr<Array<PaintSample>> samplesValue(const HPSample* input,int32_t count) {
    require(input && count>0 && count<=100000,"Invalid sample array ABI");
    auto result=std::make_unique<Array<PaintSample>>(count);
    for(int i=0;i<count;i++) result->Data[i]=sampleValue(input[i]);
    return result;
}
/** Tokens are process-unique identities, never addresses of released generated objects. */
static std::atomic<std::uintptr_t> NextHandle{1};
/** Allocates a token without wrapping into a previously issued identity. */
static std::uintptr_t nextHandle() {
    auto value=NextHandle.load();
    do { require(value<std::numeric_limits<std::uintptr_t>::max(),"Painting handle space exhausted"); }
    while(!NextHandle.compare_exchange_weak(value,value+1));
    return value;
}
/** Validates opaque handles and keeps an in-progress call alive across an early release.
 * Callers still serialize mutations to each individual session/sampler, as documented by the ABI.
 * Tree nodes are removed on release; no tombstone or per-handle build-up remains.
 */
template<class T,class H,class D=DisposeDelete<T>> class HandleRegistry {
    std::mutex Mutex;
    std::map<std::uintptr_t,std::shared_ptr<T>> Items;
public:
    /** Takes ownership before allocating registry storage so an allocation failure cannot leak payload. */
    H* Add(T* raw) {
        std::shared_ptr<T> value(raw,D{});
        const auto key=nextHandle();
        std::lock_guard lock(Mutex); Items.emplace(key,std::move(value));
        return reinterpret_cast<H*>(key);
    }
    /** Returns a retained live payload without dereferencing caller-provided addresses. */
    std::shared_ptr<T> Find(const H* handle) {
        std::lock_guard lock(Mutex);
        const auto found=Items.find(reinterpret_cast<std::uintptr_t>(handle));
        require(found!=Items.end(),"Invalid or released painting handle");
        return found->second;
    }
    /** Releasing an absent handle is idempotent and never releases another handle type. */
    void Remove(const H* handle) {
        std::shared_ptr<T> removed;
        {
            std::lock_guard lock(Mutex);
            const auto found=Items.find(reinterpret_cast<std::uintptr_t>(handle));
            if(found==Items.end()) return;
            removed=std::move(found->second); Items.erase(found);
        }
    }
};
static HandleRegistry<PaintSession,HPSession> Sessions;
static HandleRegistry<PaintSnapshot,HPSnapshot> Snapshots;
static HandleRegistry<HPSampler,HPSampler,std::default_delete<HPSampler>> Samplers;
/** Acquires a live generated session; pointer-shaped ABI values remain opaque to every consumer. */
static std::shared_ptr<PaintSession> sessionValue(HPSession* value) { return Sessions.Find(value); }
/** Acquires an immutable CPU snapshot for the duration of the current call. */
static std::shared_ptr<PaintSnapshot> snapshotValue(const HPSnapshot* value) { return Snapshots.Find(value); }
/** Copies one immutable record after validating an index rather than relying on native array assertions. */
static Owned<PaintStrokeRecord> recordValue(const HPSnapshot* value,int32_t index) {
    auto snapshot=snapshotValue(value);
    require(index>=0 && index<snapshot->get_StrokeCount(),"Snapshot record index out of range");
    return Owned<PaintStrokeRecord>(snapshot->GetStroke(index));
}

uint32_t hp_abi_version(void) { return 1; }
int32_t hp_validate_brush(const HPBrush* input) {
    int result=1;
    const int failure=guarded([&] {
        if(!input) { result=PaintContract::ValidateBrush(nullptr); return; }
        if(input->version!=1) { result=2; return; }
        auto brush=brushValue(input); result=PaintContract::ValidateBrush(&brush);
    });
    if(failure) return failure;
    if(result) error(result==2 ? "Unsupported brush identity or version" : "Invalid painting data");
    return result;
}
size_t hp_last_error(char* destination,size_t capacity) {
    const size_t length=std::strlen(LastError);
    if(destination && capacity) {
        const size_t count=std::min(length,capacity-1); std::memcpy(destination,LastError,count); destination[count]=0;
    }
    return length+1;
}
int32_t hp_session_create(void* backend,const HPCanvas* input,int32_t limit,HPSession** result) {
    if(result) *result=nullptr;
    return guarded([&] {
        require(result && input && input->reserved==0 && backend,"Invalid session creation ABI");
        PaintCanvasDescriptor canvas;
        canvas.Width=input->width; canvas.Height=input->height;
        canvas.PixelWidth=input->pixel_width; canvas.PixelHeight=input->pixel_height;
        canvas.MaxSamples=input->max_samples; canvas.MaxStamps=input->max_stamps; canvas.MaxStrokes=input->max_strokes;
        *result=Sessions.Add(new PaintSession(static_cast<IPaintSurfaceBackend*>(backend),&canvas,limit));
    });
}
void hp_session_destroy(HPSession* session) { Sessions.Remove(session); }
int32_t hp_session_reconfigure(HPSession* session,const HPCanvas* input,double originX,double originY) {
    return guarded([&] {
        require(input && input->reserved==0,"Invalid surface configuration ABI");
        PaintCanvasDescriptor canvas;
        canvas.Width=input->width; canvas.Height=input->height; canvas.OriginX=originX; canvas.OriginY=originY;
        canvas.PixelWidth=input->pixel_width; canvas.PixelHeight=input->pixel_height;
        canvas.MaxSamples=input->max_samples; canvas.MaxStamps=input->max_stamps; canvas.MaxStrokes=input->max_strokes;
        sessionValue(session)->Reconfigure(&canvas);
    });
}
int32_t hp_session_suspend(HPSession* session) { return guarded([&] { sessionValue(session)->Suspend(); }); }
int32_t hp_session_begin(HPSession* session,const char* id,const HPBrush* input) {
    return guarded([&] { auto brush=brushValue(input); sessionValue(session)->BeginStroke(identity(id),&brush); });
}
int32_t hp_session_append(HPSession* session,const HPSample* input,int32_t count) {
    return guarded([&] { auto values=samplesValue(input,count); sessionValue(session)->AppendSamples(values.get(),count); });
}
int32_t hp_session_correct(HPSession* session,const HPSample* input,int32_t count) {
    return guarded([&] { auto values=samplesValue(input,count); sessionValue(session)->CorrectSamples(values.get(),count); });
}
int32_t hp_session_commit(HPSession* session,int64_t* token) {
    if(token) *token=0;
    std::shared_ptr<PaintSession> value; int64_t before=0;
    int result=guarded([&] {
        require(token,"Missing transition output"); value=sessionValue(session); before=value->get_Revision();
        *token=value->CommitStroke();
    });
    if(result && value && value->get_Revision()!=before) { *token=value->get_Revision(); return 4; }
    return result;
}
int32_t hp_session_cancel(HPSession* session) { return guarded([&] { sessionValue(session)->CancelStroke(); }); }
/** Distinguishes successful CPU navigation from a later raster replay failure for the host timeline. */
static int32_t navigate(HPSession* session,int64_t token,bool redo) {
    std::shared_ptr<PaintSession> value; int64_t before=0;
    int result=guarded([&] { value=sessionValue(session); before=value->get_Revision(); if(redo) value->Redo(token); else value->Undo(token); });
    if(result && value && value->get_Revision()!=before) return 4;
    return result;
}
int32_t hp_session_undo(HPSession* session,int64_t token) { return navigate(session,token,false); }
int32_t hp_session_redo(HPSession* session,int64_t token) { return navigate(session,token,true); }
int32_t hp_session_discard_redo(HPSession* session) { return guarded([&] { sessionValue(session)->DiscardRedo(); }); }
int32_t hp_session_forget(HPSession* session,int64_t token) { return guarded([&] { sessionValue(session)->ForgetTransition(token); }); }
int32_t hp_session_state(HPSession* session,int64_t* revision,uint32_t* flags) {
    return guarded([&] {
        require(revision && flags,"Missing state outputs"); auto value=sessionValue(session);
        *revision=value->get_Revision();
        *flags=(value->get_HasActiveStroke()?1u:0u)|(value->get_CanUndo()?2u:0u)|(value->get_CanRedo()?4u:0u)|(value->get_RenderFailed()?8u:0u);
    });
}
int32_t hp_session_snapshot(HPSession* session,HPSnapshot** result) {
    if(result) *result=nullptr;
    return guarded([&] { require(result,"Missing snapshot output"); *result=Snapshots.Add(sessionValue(session)->CaptureSnapshot()); });
}
int32_t hp_session_load(HPSession* session,const HPSnapshot* snapshot) {
    return guarded([&] { sessionValue(session)->Load(snapshotValue(snapshot).get()); });
}
int32_t hp_snapshot_create(const HPStrokeInput* inputs,int32_t count,HPSnapshot** result) {
    if(result) *result=nullptr;
    return guarded([&] {
        require(result && count>=0 && count<=10000 && (!count || inputs),"Invalid snapshot ABI");
        Owned<PaintHistory> history(new PaintHistory(100));
        int64_t samples=0; double cost=0;
        for(int i=0;i<count;i++) {
            require(inputs[i].reserved==0,"Nonzero stroke ABI reserved field");
            samples+=inputs[i].count; require(samples<=100000,"Snapshot sample budget exceeded");
            auto brush=brushValue(&inputs[i].brush); auto points=samplesValue(inputs[i].samples,inputs[i].count);
            Owned<PaintStrokeRecord> stroke(new PaintStrokeRecord(identity(inputs[i].id),&brush,points.get()));
            cost+=stroke->get_StampBudget();
            require(!history->ContainsId(stroke->get_Id()) && cost<=2000000,"Duplicate identity or excessive snapshot marks");
            history->Append(stroke.get());
        }
        history->SealBase();
        *result=Snapshots.Add(new PaintSnapshot(history.get()));
    });
}
void hp_snapshot_destroy(HPSnapshot* snapshot) { Snapshots.Remove(snapshot); }
int32_t hp_snapshot_info(const HPSnapshot* snapshot,int32_t* count,int64_t* revision) {
    return guarded([&] { require(count && revision,"Missing snapshot outputs"); auto value=snapshotValue(snapshot); *count=value->get_StrokeCount(); *revision=value->get_Revision(); });
}
int32_t hp_snapshot_stroke(const HPSnapshot* snapshot,int32_t index,HPBrush* brush,int32_t* count,char* id,size_t capacity,size_t* required) {
    return guarded([&] {
        require(brush && count && required,"Missing stroke outputs");
        auto record=recordValue(snapshot,index); std::unique_ptr<PaintBrushDefinition> tool(record->GetBrush());
        const auto identity=record->get_Id(); *required=identity.size()+1; *count=record->get_SampleCount();
        *brush=HPBrush{static_cast<uint32_t>(tool->Version),tool->Eraser?1u:0u,"cozy.round",tool->Diameter,tool->Red,tool->Green,tool->Blue,tool->Opacity};
        require(id && capacity>=*required,"Stroke identity output buffer too small");
        std::memcpy(id,identity.data(),identity.size()); id[identity.size()]=0;
    });
}
int32_t hp_snapshot_samples(const HPSnapshot* snapshot,int32_t index,HPSample* samples,int32_t capacity) {
    return guarded([&] {
        auto record=recordValue(snapshot,index); require(samples && capacity>=record->get_SampleCount(),"Sample output buffer too small");
        for(int i=0;i<record->get_SampleCount();i++) {
            auto value=record->GetSample(i);
            samples[i]=HPSample{value.Index,0,value.X,value.Y,value.Pressure,value.Time,value.Altitude,value.Azimuth};
        }
    });
}
int32_t hp_sampler_create(const HPBrush* input,HPSampler** result) {
    if(result) *result=nullptr;
    return guarded([&] {
        require(result,"Missing sampler output"); auto brush=brushValue(input);
        auto value=std::make_unique<HPSampler>(); value->Value.reset(new PaintStrokeSampler(&brush)); *result=Samplers.Add(value.release());
    });
}
void hp_sampler_destroy(HPSampler* sampler) { Samplers.Remove(sampler); }
int32_t hp_sampler_append(HPSampler* sampler,const HPSample* input,const HPStamp** marks,int32_t* count) {
    if(marks) *marks=nullptr; if(count) *count=0;
    return guarded([&] {
        require(sampler && input && marks && count,"Invalid sampler input/output");
        auto value=Samplers.Find(sampler);
        auto point=sampleValue(*input); std::unique_ptr<Array<PaintStamp>> values(value->Value->Append(point));
        value->Marks.clear(); value->Marks.reserve(values->Length);
        for(int i=0;i<values->Length;i++) {
            const auto& mark=values->Data[i];
            value->Marks.push_back({mark.X,mark.Y,mark.Radius,mark.Red,mark.Green,mark.Blue,mark.Opacity});
        }
        *marks=value->Marks.data(); *count=static_cast<int32_t>(value->Marks.size());
    });
}
