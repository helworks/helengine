namespace helengine.painting {
    /// <summary>Owns portable drawing state and history while borrowing an explicit raster provider.</summary>
    public sealed class PaintSession : IDisposable {
        /// <summary>Provider whose host lifetime encloses this session.</summary>
        IPaintSurfaceBackend Backend;
        /// <summary>Captured immutable admission policy.</summary>
        [NativeOwnedMember] PaintCanvasDescriptor Canvas;
        /// <summary>Committed content and reversible transitions.</summary>
        [NativeOwnedMember] PaintHistory History;
        /// <summary>Captured active tool.</summary>
        [NativeOwnedMember] PaintBrushDefinition ActiveBrush;
        /// <summary>Incremental interpolation state.</summary>
        [NativeOwnedMember] PaintStrokeSampler ActiveSampler;
        /// <summary>Authoritative accepted samples.</summary>
        [NativeOwnedMember] List<PaintSample> ActiveSamples;
        /// <summary>Host supplied active identity.</summary>
        string ActiveId;
        /// <summary>Undo depth used for new loaded bases.</summary>
        readonly int HistoryLimit;
        /// <summary>Cached committed sample count, refreshed outside the input path.</summary>
        int CommittedSamples;
        /// <summary>Cached committed conservative mark cost.</summary>
        double CommittedCost;
        /// <summary>Active conservative mark cost.</summary>
        double ActiveCost;
        /// <summary>Estimated input requires one corrected replay at commit.</summary>
        bool Corrected;
        /// <summary>Surface failure preserves CPU content for recovery.</summary>
        bool Faulted;
        /// <summary>Inactive sessions retain CPU history without enqueuing raster work.</summary>
        bool Suspended;

        /// <summary>Creates a session with a borrowed provider and explicit admission policy.</summary>
        public PaintSession([NativeRetainsBorrow] IPaintSurfaceBackend backend,[NativeNoEscape] PaintCanvasDescriptor canvas,int historyLimit) {
            if(backend==null || canvas==null || !canvas.IsValid()) throw new ArgumentException("Invalid session configuration");
            if(historyLimit<1 || historyLimit>10000) throw new ArgumentOutOfRangeException("historyLimit");
            Backend=backend; HistoryLimit=historyLimit;
            Canvas=canvas.Copy(); History=new PaintHistory(historyLimit);
        }
        /// <summary>Whether a gesture is accepting input.</summary>
        public bool HasActiveStroke { get { return ActiveSamples!=null; } }
        /// <summary>Current committed content identity.</summary>
        public long Revision { get { CheckAlive(); return History.Revision; } }
        /// <summary>Whether the latest committed transition can be undone.</summary>
        public bool CanUndo { get { return History!=null && !HasActiveStroke && History.CanUndo; } }
        /// <summary>Whether a retained transition can be reapplied.</summary>
        public bool CanRedo { get { return History!=null && !HasActiveStroke && History.CanRedo; } }
        /// <summary>Whether the provider must be recovered before accepting more input.</summary>
        public bool RenderFailed { get { return Faulted; } }

        /// <summary>Reallocates an idle surface and replays existing records without replacing history or authored coordinates.</summary>
        public void Reconfigure([NativeNoEscape] PaintCanvasDescriptor canvas) {
            CheckIdle();
            if(canvas==null || !canvas.IsValid() || CommittedSamples>canvas.MaxSamples || CommittedCost>canvas.MaxStamps || History.StrokeCount>canvas.MaxStrokes)
                throw new ArgumentException("Invalid surface configuration");
            PaintCanvasDescriptor candidate=canvas.Copy();
            try {
                Backend.Reconfigure(candidate);
                NativeOwnership.Release(ref Canvas); Canvas=candidate; candidate=null;
                Suspended=false; Faulted=false; Replay();
            } catch { Faulted=true; throw; }
            finally { NativeOwnership.Release(ref candidate); }
        }
        /// <summary>Releases inactive raster resources while keeping the same CPU session and all history tokens.</summary>
        public void Suspend() {
            CheckIdle();
            Backend.Suspend(); Suspended=true;
        }

        /// <summary>Validates a whole snapshot before replacing content and establishing an irreversible base.</summary>
        public void Load([NativeNoEscape] PaintSnapshot snapshot) {
            CheckIdle();
            if(snapshot==null || snapshot.StrokeCount>Canvas.MaxStrokes) throw new ArgumentException("Invalid snapshot");
            PaintHistory candidate=new PaintHistory(HistoryLimit);
            try {
                int samples=0; double cost=0;
                for(int i=0;i<snapshot.StrokeCount;i++) {
                    PaintStrokeRecord stroke=snapshot.GetStroke(i);
                    try {
                        samples+=stroke.SampleCount; cost+=stroke.StampBudget;
                        if(candidate.ContainsId(stroke.Id) || samples>Canvas.MaxSamples || cost>Canvas.MaxStamps) throw new ArgumentException("Snapshot exceeds admission policy");
                        candidate.Append(stroke);
                    } finally { NativeOwnership.DisposeAndRelease(ref stroke); }
                }
                candidate.SealBase();
                NativeOwnership.DisposeAndRelease(ref History); History=candidate; candidate=null;
                Recount(); Faulted=false; Replay();
            } finally { NativeOwnership.DisposeAndRelease(ref candidate); }
        }
        /// <summary>Captures brush settings for one uniquely identified gesture.</summary>
        public void BeginStroke(string id,[NativeNoEscape] PaintBrushDefinition brush) {
            CheckIdle();
            if(Faulted || Suspended) throw new InvalidOperationException("Painting surface requires activation or recovery");
            if(string.IsNullOrEmpty(id) || id.Length>128 || History.ContainsId(id) || PaintContract.ValidateBrush(brush)!=0) throw new ArgumentException("Invalid or duplicate stroke");
            if(History.StrokeCount>=Canvas.MaxStrokes || CommittedSamples>=Canvas.MaxSamples || CommittedCost+1>Canvas.MaxStamps) throw new InvalidOperationException("Drawing complexity limit reached");
            PaintBrushDefinition captured=brush.Copy();
            PaintStrokeSampler sampler=null; List<PaintSample> samples=null;
            try {
                sampler=new PaintStrokeSampler(captured); samples=new List<PaintSample>();
                NativeOwnership.Release(ref ActiveBrush);
                NativeOwnership.DisposeAndRelease(ref ActiveSampler);
                NativeOwnership.Release(ref ActiveSamples);
                ActiveBrush=captured; captured=null; ActiveSampler=sampler; sampler=null;
                ActiveSamples=samples; samples=null; ActiveId=id; ActiveCost=0; Corrected=false;
            } finally {
                NativeOwnership.Release(ref captured); NativeOwnership.DisposeAndRelease(ref sampler); NativeOwnership.Release(ref samples);
            }
        }
        /// <summary>Atomically admits a valid batch before forwarding marks; GPU errors do not discard accepted input.</summary>
        public void AppendSamples([NativeNoEscape] PaintSample[] samples,int count) {
            CheckActive();
            if(Faulted) throw new InvalidOperationException("Painting surface requires recovery");
            CheckBatch(samples,count);
            if(ActiveBrush.IsSurfaceFill && (ActiveSamples.Count!=0 || count!=1 || samples[0].Index!=0 || samples[0].X!=0 || samples[0].Y!=0 || samples[0].Pressure!=1 || samples[0].Time!=0 || samples[0].Altitude!=0 || samples[0].Azimuth!=0))
                throw new ArgumentException("A surface fill requires one canonical sample");
            if(count>Canvas.MaxSamples-CommittedSamples-ActiveSamples.Count) throw new ArgumentException("Sample budget exceeded");
            double cost=ActiveCost;
            PaintSample previous=ActiveSamples.Count>0 ? ActiveSamples[ActiveSamples.Count-1] : new PaintSample();
            for(int i=0;i<count;i++) {
                PaintSample sample=samples[i];
                if(sample.Index!=ActiveSamples.Count+i) throw new ArgumentException("Nonconsecutive sample index");
                if(sample.Index==0) cost=1;
                else {
                    double dx=sample.X-previous.X; double dy=sample.Y-previous.Y;
                    cost+=Math.Sqrt(dx*dx+dy*dy)/(ActiveBrush.Diameter*0.10)+1;
                }
                if(CommittedCost+cost>Canvas.MaxStamps) throw new ArgumentException("Stamp budget exceeded");
                previous=sample;
            }
            PaintStrokeSampler candidate=ActiveSampler.Copy();
            List<PaintStamp> marks=new List<PaintStamp>(); PaintStamp[] output=null;
            try {
                for(int i=0;i<count;i++) {
                    PaintStamp[] additional=candidate.Append(samples[i]);
                    try { for(int j=0;j<additional.Length;j++) marks.Add(additional[j]); }
                    finally { NativeOwnership.Release(ref additional); }
                }
                output=marks.ToArray();
                ActiveSamples.Capacity=Math.Max(ActiveSamples.Capacity,Math.Min(Canvas.MaxSamples,Math.Max(ActiveSamples.Count+count,Math.Max(16,ActiveSamples.Capacity*2))));
                for(int i=0;i<count;i++) ActiveSamples.Add(samples[i]);
                NativeOwnership.DisposeAndRelease(ref ActiveSampler); ActiveSampler=candidate; candidate=null;
                ActiveCost=cost;
                try { Backend.Append(output,output.Length,ActiveBrush.Eraser ? PaintBlendMode.Erase : PaintBlendMode.Paint,Revision); }
                catch { Faulted=true; throw; }
            } finally {
                NativeOwnership.DisposeAndRelease(ref candidate); NativeOwnership.Release(ref marks); NativeOwnership.Release(ref output);
            }
        }
        /// <summary>Corrects pressure and orientation by index without changing admitted geometry.</summary>
        public void CorrectSamples([NativeNoEscape] PaintSample[] samples,int count) {
            CheckActive(); CheckBatch(samples,count);
            if(ActiveBrush.IsSurfaceFill) throw new InvalidOperationException("Surface fills have no pressure or orientation corrections");
            for(int i=0;i<count;i++) {
                int index=samples[i].Index;
                if(index>=ActiveSamples.Count || samples[i].X!=ActiveSamples[index].X || samples[i].Y!=ActiveSamples[index].Y) throw new ArgumentException("Correction must refer to existing geometry");
            }
            for(int i=0;i<count;i++) ActiveSamples[samples[i].Index]=samples[i];
            Corrected=true;
        }
        /// <summary>Commits one nonempty stroke, including accepted data recoverable after a surface failure.</summary>
        public long CommitStroke() {
            CheckActive();
            if(ActiveSamples.Count==0) throw new InvalidOperationException("Cannot commit empty stroke");
            PaintSample[] samples=ActiveSamples.ToArray(); PaintStrokeRecord stroke=null;
            long token;
            try { stroke=new PaintStrokeRecord(ActiveId,ActiveBrush,samples); token=History.Append(stroke); }
            finally { NativeOwnership.DisposeAndRelease(ref stroke); NativeOwnership.Release(ref samples); }
            bool replay=Corrected; Recount(); ClearActive();
            if(!Faulted) {
                if(replay) Replay();
                else PublishRevision();
            }
            return token;
        }
        /// <summary>Discards only active input and restores committed pixels.</summary>
        public void CancelStroke() { CheckActive(); ClearActive(); if(!Faulted) Replay(); }
        /// <summary>Restores the preceding committed content.</summary>
        public void Undo(long transition) { CheckIdle(); History.Undo(transition); Recount(); if(!Faulted) Replay(); }
        /// <summary>Reapplies the expected forward transition.</summary>
        public void Redo(long transition) {
            CheckIdle();
            if(History.CanRedo && (CommittedSamples>Canvas.MaxSamples-History.RedoSampleCount
                || CommittedCost+History.RedoStampBudget>Canvas.MaxStamps || History.StrokeCount>=Canvas.MaxStrokes))
                throw new InvalidOperationException("Painting redo exceeds surface admission policy");
            History.Redo(transition); Recount(); if(!Faulted) Replay();
        }
        /// <summary>Invalidates redo when the host creates a new metadata branch.</summary>
        public void DiscardRedo() { CheckIdle(); History.DiscardRedo(); }
        /// <summary>Forgets undo eligibility while retaining applied artwork.</summary>
        public void ForgetTransition(long transition) { CheckAlive(); History.ForgetTransition(transition); }
        /// <summary>Copies committed portable data, excluding the active gesture.</summary>
        [NativeOwnedReturn]
        public PaintSnapshot CaptureSnapshot() { CheckAlive(); return new PaintSnapshot(History); }
        /// <summary>Releases CPU ownership without disposing the borrowed provider.</summary>
        public void Dispose() { ClearActive(); NativeOwnership.DisposeAndRelease(ref History); NativeOwnership.Release(ref Canvas); Backend=null; }

        /// <summary>Rebuilds committed pixels through nonblocking provider commands.</summary>
        void Replay() {
            if(Suspended) return;
            try {
                Backend.Reset(Revision);
                for(int i=0;i<History.StrokeCount;i++) {
                    PaintStrokeRecord stroke=History.GetStroke(i);
                    PaintBrushDefinition brush=null; PaintStrokeSampler sampler=null;
                    try {
                        brush=stroke.GetBrush(); sampler=new PaintStrokeSampler(brush);
                        for(int j=0;j<stroke.SampleCount;j++) {
                            PaintStamp[] marks=sampler.Append(stroke.GetSample(j));
                            try { Backend.Append(marks,marks.Length,brush.Eraser ? PaintBlendMode.Erase : PaintBlendMode.Paint,Revision); }
                            finally { NativeOwnership.Release(ref marks); }
                        }
                    } finally {
                        NativeOwnership.DisposeAndRelease(ref sampler); NativeOwnership.Release(ref brush); NativeOwnership.DisposeAndRelease(ref stroke);
                    }
                }
            } catch { Faulted=true; throw; }
        }
        /// <summary>Refreshes admission totals only after committed content changes.</summary>
        void Recount() { CommittedSamples=History.SampleCount; CommittedCost=History.StampBudget; }
        /// <summary>Publishes committed identity without repainting unchanged pixels; CPU commit survives a provider error.</summary>
        void PublishRevision() {
            PaintStamp[] empty=new PaintStamp[0];
            try { Backend.Append(empty,0,PaintBlendMode.Paint,Revision); }
            catch { Faulted=true; throw; }
            finally { NativeOwnership.Release(ref empty); }
        }
        /// <summary>Releases transient input without touching history.</summary>
        void ClearActive() {
            NativeOwnership.Release(ref ActiveSamples); NativeOwnership.DisposeAndRelease(ref ActiveSampler);
            NativeOwnership.Release(ref ActiveBrush); ActiveId=null; ActiveCost=0; Corrected=false;
        }
        /// <summary>Validates a complete nonempty batch before mutation.</summary>
        void CheckBatch([NativeNoEscape] PaintSample[] samples,int count) {
            if(samples==null || count<1 || count>samples.Length || count>Canvas.MaxSamples) throw new ArgumentException("Invalid sample batch");
            for(int i=0;i<count;i++) if(PaintContract.ValidateSample(samples[i])!=0) throw new ArgumentException("Invalid sample");
        }
        /// <summary>Rejects access to disposed storage.</summary>
        void CheckAlive() { if(History==null) throw new ObjectDisposedException("PaintSession"); }
        /// <summary>Rejects operations requiring a completed input transaction.</summary>
        void CheckIdle() { CheckAlive(); if(HasActiveStroke) throw new InvalidOperationException("Active stroke must finish first"); }
        /// <summary>Rejects input outside an active gesture.</summary>
        void CheckActive() { CheckAlive(); if(!HasActiveStroke) throw new InvalidOperationException("No active stroke"); }
    }
}
