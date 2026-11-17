#include "helpainting.h"
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <limits>
#include <initializer_list>
#include "PaintStrokeSampler.hpp"
#include "PaintBrushDefinition.hpp"
#include "PaintSample.hpp"
#include "PaintStamp.hpp"
#include <memory>
#include "PaintHistory.hpp"
#include "PaintStrokeRecord.hpp"
#include <atomic>
#include <new>
#include "PaintSession.hpp"
#include "PaintSnapshot.hpp"
#include "PaintCanvasDescriptor.hpp"
#include "IPaintSurfaceBackend.hpp"
#include "runtime/native_exceptions.hpp"

/** Records generated session output without substituting a second sampler implementation. */
class RecordingSurface final : public IPaintSurfaceBackend {
public:
    int Marks=0;
    PaintStamp Last{};
    bool Fail=false;
    void Reconfigure(PaintCanvasDescriptor*) override { Marks=0; }
    void Suspend() override { Marks=0; }
    void Reset(int64_t) override { Marks=0; }
    void Append(Array<PaintStamp>* values,int32_t count,PaintBlendMode,int64_t) override {
        if(Fail) throw new InvalidOperationException(HeCppString("Injected surface failure"));
        Marks+=count;
        if(count) Last=values->Data[count-1];
    }
    int64_t Submit() override { return 1; }
    PaintSubmissionStatus GetSubmissionStatus(int64_t) override { return PaintSubmissionStatus::Completed; }
    void ReleaseSubmission(int64_t) override {}
    int64_t AcquireSnapshot(int64_t,bool) override { return 1; }
    PaintSubmissionStatus GetSnapshotStatus(int64_t) override { return PaintSubmissionStatus::Completed; }
    void ReleaseSnapshot(int64_t) override {}
};

/** Counts real C++ allocation lifetimes in this test executable, including generated owned fields. */
static std::atomic<long> LiveAllocations{0};
void* operator new(std::size_t size) {
    void* value=std::malloc(size ? size : 1);
    if(!value) throw std::bad_alloc();
    ++LiveAllocations; return value;
}
void operator delete(void* value) noexcept {
    if(value) { --LiveAllocations; std::free(value); }
}
void operator delete(void* value,std::size_t) noexcept { ::operator delete(value); }

/** Fails this real generated-code test at the first violated public contract. */
static void require(bool condition,const char* label) {
    if (!condition) { std::fprintf(stderr,"FAIL: %s\n",label); std::exit(1); }
}

/** Executes the authored validation through the same C entry point used by native consumers. */
int main() {
    static_assert(sizeof(HPBrush)==56);
    static_assert(sizeof(HPSample)==56 && offsetof(HPSample,x)==8);
    static_assert(sizeof(HPCanvas)==40 && sizeof(HPStamp)==56);
    static_assert(offsetof(HPBrush,diameter)==16);
    static_assert(offsetof(HPBrush,opacity)==48);
    require(hp_abi_version()==1,"ABI version");
    HPBrush brush{1,0,"cozy.round",1.5,0.12,0.16,0.10,1};
    require(hp_validate_brush(&brush)==0,"valid generated brush");
    for (double value:{0.2,12.0}) {
        brush.diameter=value;
        require(hp_validate_brush(&brush)==0,"inclusive diameter boundary");
    }
    for (double value:{-1.0,0.0,12.1,std::numeric_limits<double>::quiet_NaN(),std::numeric_limits<double>::infinity()}) {
        brush.diameter=value;
        require(hp_validate_brush(&brush)==1,"invalid diameter rejected by generated code");
    }
    brush.diameter=1.5; brush.version=2;
    require(hp_validate_brush(&brush)==2,"new brush version");
    brush.version=1; brush.id="unknown";
    require(hp_validate_brush(&brush)==2,"unknown brush");
    brush.id="cozy.round"; brush.opacity=1.01;
    require(hp_validate_brush(&brush)==1,"invalid opacity");
    require(hp_validate_brush(nullptr)==1,"missing brush");
    char error[128]{};
    require(hp_last_error(error,sizeof(error))>1 && std::strlen(error)>0,"bounded error response");
    brush.opacity=1;
    require(hp_validate_brush(&brush)==0,"valid retry");
    hp_last_error(error,sizeof(error));
    require(error[0]==0,"success clears stale error");
    PaintBrushDefinition authored;
    authored.Version=1; authored.Id=HeCppString("cozy.round"); authored.Diameter=2;
    authored.Red=0.25; authored.Green=0.5; authored.Blue=0.75; authored.Opacity=1;
    PaintStrokeSampler sampler(&authored);
    PaintSample sample; sample.X=0; sample.Y=1; sample.Pressure=1;
    std::unique_ptr<Array<PaintStamp>> tap(sampler.Append(sample));
    require(tap->Length==1 && tap->Data[0].Radius==1,"native generated tap");
    sample.X=10;
    std::unique_ptr<Array<PaintStamp>> marks(sampler.Append(sample));
    require(marks->Length==50,"native generated spacing");
    for (int i=0;i<50;++i) require(std::abs(marks->Data[i].X-(i+1)*0.2)<1e-8,"native mark positions");
    sampler.Dispose();
    sampler.Dispose();
    const long before=LiveAllocations.load();
    for(int repeat=0;repeat<100;repeat++) {
        Array<PaintSample> inputs({sample});
        PaintStrokeRecord stroke(HeCppString("stroke-a"),&authored,&inputs);
        PaintHistory history(2);
        const auto a=history.Append(&stroke);
        const auto b=history.Append(&stroke);
        const auto c=history.Append(&stroke);
        history.ForgetTransition(a);
        history.Undo(c); history.Undo(b);
        require(history.get_StrokeCount()==1 && !history.get_CanUndo(),"native retained base");
        history.Redo(b);
        require(history.get_Revision()==b,"native restored revision");
        history.DiscardRedo();
        require(!history.get_CanRedo(),"native discarded branch");
        PaintStrokeRecord* copied=history.GetStroke(0);
        require(copied->get_Id()==HeCppString("stroke-a"),"native stable identity");
        copied->Dispose(); delete copied;
        history.Dispose(); stroke.Dispose();
    }
    if(LiveAllocations.load()!=before) std::fprintf(stderr,"Allocation delta: %ld\n",LiveAllocations.load()-before);
    require(LiveAllocations.load()==before,"generated history releases every owned C++ allocation");
    for(int repeat=0;repeat<100;repeat++) {
        RecordingSurface backend;
        PaintCanvasDescriptor canvas;
        canvas.Width=72; canvas.Height=210; canvas.PixelWidth=851; canvas.PixelHeight=2481;
        canvas.MaxSamples=100000; canvas.MaxStamps=2000000; canvas.MaxStrokes=10000;
        PaintSession session(&backend,&canvas,100);
        PaintSample first; first.Index=0; first.X=1; first.Y=2; first.Pressure=0;
        Array<PaintSample> input({first});
        session.BeginStroke(HeCppString("corrected"),&authored);
        session.AppendSamples(&input,1);
        input.Data[0].Pressure=1;
        session.CorrectSamples(&input,1);
        const auto token=session.CommitStroke();
        require(backend.Marks==1 && backend.Last.Radius==1 && backend.Last.Red==0.25,"native corrected replay and color");
        session.Undo(token); require(backend.Marks==0,"native undo clears pixels");
        session.Redo(token); require(backend.Marks==1,"native redo restores pixels");
        auto* snapshot=session.CaptureSnapshot();
        PaintSession reopened(&backend,&canvas,100); reopened.Load(snapshot);
        require(!reopened.get_CanUndo(),"native reopen has no history");
        snapshot->Dispose(); delete snapshot; reopened.Dispose();
        session.BeginStroke(HeCppString("abort"),&authored); session.AppendSamples(&input,1); session.CancelStroke();
        require(backend.Marks==1,"native cancel restores committed drawing");
        session.BeginStroke(HeCppString("failure"),&authored); backend.Fail=true;
        bool failed=false;
        try { session.AppendSamples(&input,1); }
        catch(InvalidOperationException* error) { failed=true; delete error; }
        require(failed && session.get_RenderFailed(),"native reports surface failure");
        session.CommitStroke();
        auto* recovered=session.CaptureSnapshot();
        require(recovered->get_StrokeCount()==2,"native failure preserves accepted input");
        recovered->Dispose(); delete recovered; session.Dispose(); session.Dispose();
    }
    if(LiveAllocations.load()!=before) std::fprintf(stderr,"Session allocation delta: %ld\n",LiveAllocations.load()-before);
    require(LiveAllocations.load()==before,"generated sessions release every owned C++ allocation");
    for(int repeat=0;repeat<100;repeat++) {
        RecordingSurface backend;
        HPCanvas canvas{72,210,851,2481,100000,2000000,10000,0};
        HPSession* session=nullptr;
        require(hp_session_create(&backend,&canvas,100,&session)==0 && session,"C ABI creates generated session");
        require(hp_session_begin(session,"stable-id",&brush)==0,"C ABI begins stroke");
        HPSample point{0,0,1,2,0.7,123.25,1.2,0.8};
        require(hp_session_append(session,&point,1)==0,"C ABI admits authored sample");
        int64_t token=0; require(hp_session_commit(session,&token)==0 && token>0,"C ABI commit token");
        HPSnapshot* snapshot=nullptr;
        require(hp_session_snapshot(session,&snapshot)==0,"C ABI captures snapshot");
        HPBrush output{}; int32_t count=0; char id[129]{}; size_t required=0;
        require(hp_snapshot_stroke(snapshot,0,&output,&count,id,sizeof(id),&required)==0 && count==1 && std::strcmp(id,"stable-id")==0,"C ABI snapshot identity");
        HPSample copied{}; require(hp_snapshot_samples(snapshot,0,&copied,1)==0 && copied.pressure==point.pressure && copied.time==point.time,"C ABI binary64 roundtrip");
        require(hp_session_undo(session,token)==0 && hp_session_redo(session,token)==0,"C ABI history");
        require(hp_session_reconfigure(session,&canvas,-20,-30)==0,"C ABI surface reconfiguration");
        require(backend.Last.X==point.x && backend.Last.Y==point.y,"reconfiguration preserves local stamp coordinates");
        require(hp_session_suspend(session)==0 && backend.Marks==0,"C ABI suspension releases queued marks");
        require(hp_session_undo(session,token)==0 && hp_session_redo(session,token)==0 && backend.Marks==0,"inactive history stays CPU only");
        require(hp_session_reconfigure(session,&canvas,-20,-30)==0 && backend.Marks==1,"activation replays CPU history");
        require(hp_session_begin(session,"stable-id",&brush)!=0,"C ABI catches generated errors");
        hp_snapshot_destroy(snapshot); hp_session_destroy(session);
        int64_t revision=0; uint32_t flags=0;
        require(hp_session_state(session,&revision,&flags)!=0,"released session is rejected");
        require(hp_snapshot_info(snapshot,&count,&revision)!=0,"released snapshot is rejected");
        hp_snapshot_destroy(snapshot); hp_session_destroy(session);
        HPSession* replacement=nullptr;
        require(hp_session_create(&backend,&canvas,100,&replacement)==0,"create after release");
        require(hp_session_state(session,&revision,&flags)!=0,"stale handle cannot alias new session");
        require(hp_snapshot_info(reinterpret_cast<HPSnapshot*>(replacement),&count,&revision)!=0,"cross-type handle rejected");
        HPSampler* cpu=nullptr;
        require(hp_sampler_create(&brush,&cpu)==0,"portable ABI sampler");
        const HPStamp* batch=nullptr; int32_t size=0;
        require(hp_sampler_append(cpu,&point,&batch,&size)==0 && size==1,"portable sampler tap");
        hp_sampler_destroy(cpu); hp_sampler_destroy(cpu);
        require(hp_sampler_append(cpu,&point,&batch,&size)!=0,"released sampler rejected");
        hp_session_destroy(replacement);
        HPSession* limitedSession=nullptr;
        require(hp_session_create(&backend,&canvas,100,&limitedSession)==0,"create redo admission session");
        require(hp_session_begin(limitedSession,"redo-admission",&brush)==0,"begin retained redo record");
        HPSample pair[]={point,point}; pair[1].index=1;
        require(hp_session_append(limitedSession,pair,2)==0 && hp_session_commit(limitedSession,&token)==0,"commit two-sample redo record");
        require(hp_session_undo(limitedSession,token)==0,"undo before reducing admission");
        canvas.max_samples=1;
        require(hp_session_reconfigure(limitedSession,&canvas,0,0)==0,"reduce admission without applied samples");
        require(hp_session_redo(limitedSession,token)!=0,"generated redo rejects reduced sample budget");
        require(hp_session_state(limitedSession,&revision,&flags)==0 && revision==0 && (flags&4) && !(flags&8),"rejected redo preserves revision and forward history without renderer fault");
        canvas.max_samples=100000;
        require(hp_session_reconfigure(limitedSession,&canvas,0,0)==0 && hp_session_redo(limitedSession,token)==0,"same redo token works after restoring admission");
        hp_session_destroy(limitedSession);
    }
    if(LiveAllocations.load()!=before) std::fprintf(stderr,"ABI allocation delta: %ld\n",LiveAllocations.load()-before);
    require(LiveAllocations.load()==before,"C ABI releases generated exceptions and owned data");
    std::puts("PASS: generated painting ABI, admission and numeric boundaries");
}
