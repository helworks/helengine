namespace helengine.painting.tests {
    /// <summary>Tests authoritative drawing state independently of UIKit, file storage and GPU implementation.</summary>
    public sealed class PaintSessionTests {
        /// <summary>Constructs a bounded consumer canvas with known document-to-pixel mapping.</summary>
        static PaintCanvasDescriptor Canvas(int maxSamples=100000) {
            return new PaintCanvasDescriptor { Width=72,Height=210,PixelWidth=851,PixelHeight=2481,
                MaxSamples=maxSamples,MaxStamps=2000000,MaxStrokes=10000 };
        }
        /// <summary>Constructs the explicit original round brush.</summary>
        static PaintBrushDefinition Brush() { return new PaintBrushDefinition { Version=1,Id="cozy.round",Diameter=2,Opacity=1 }; }
        /// <summary>Constructs a single stationary sample with stable index zero.</summary>
        static PaintSample[] Tap(double pressure=1) { return new[] { new PaintSample { Index=0,X=1,Y=2,Pressure=pressure } }; }

        /// <summary>Committing and navigating history preserves the authored ID and pressure.</summary>
        [Fact]
        public void CommitUndoRedoPreserveAuthoredData() {
            using PaintSession session=new(new RecordingPaintBackend(),Canvas(),100);
            session.BeginStroke("stroke-a",Brush()); session.AppendSamples(Tap(0.7),1);
            long token=session.CommitStroke();
            using PaintSnapshot saved=session.CaptureSnapshot();
            Assert.Equal("stroke-a",saved.GetStroke(0).Id); Assert.Equal(0.7,saved.GetStroke(0).GetSample(0).Pressure);
            session.Undo(token); Assert.Equal(0,session.CaptureSnapshot().StrokeCount);
            session.Redo(token); Assert.Equal(saved.Revision,session.Revision);
        }
        /// <summary>Host timeline eviction may arrive while the next Pencil gesture is already queued.</summary>
        [Fact]
        public void ForgetDuringActiveKeepsInput() {
            using PaintSession session=new(new RecordingPaintBackend(),Canvas(),100);
            session.BeginStroke("old",Brush()); session.AppendSamples(Tap(),1);
            long old=session.CommitStroke();
            session.BeginStroke("active",Brush()); session.AppendSamples(Tap(0.4),1);
            session.ForgetTransition(old);
            Assert.True(session.HasActiveStroke);
            long current=session.CommitStroke();
            using PaintSnapshot result=session.CaptureSnapshot();
            Assert.Equal(2,result.StrokeCount);
            Assert.Equal(0.4,result.GetStroke(1).GetSample(0).Pressure);
            session.Undo(current); Assert.False(session.CanUndo);
        }
        /// <summary>Abort does not turn an active gesture into an undoable document edit.</summary>
        [Fact]
        public void CancelDoesNotCreateHistory() {
            using PaintSession session=new(new RecordingPaintBackend(),Canvas(),100);
            session.BeginStroke("cancelled",Brush()); session.AppendSamples(Tap(),1); session.CancelStroke();
            Assert.False(session.HasActiveStroke); Assert.False(session.CanUndo); Assert.Equal(0,session.CaptureSnapshot().StrokeCount);
        }
        /// <summary>Delayed pressure is stored authoritatively once and replayed with the corrected brush size.</summary>
        [Fact]
        public void CorrectionAppliedOnceAtCommit() {
            RecordingPaintBackend backend=new(); using PaintSession session=new(backend,Canvas(),100);
            session.BeginStroke("corrected",Brush()); session.AppendSamples(Tap(0),1);
            session.CorrectSamples(Tap(1),1); session.CommitStroke();
            using PaintSnapshot result=session.CaptureSnapshot();
            Assert.Equal(1,result.GetStroke(0).GetSample(0).Pressure);
            Assert.Single(backend.Stamps); Assert.Equal(1,backend.Stamps[0].Radius);
        }
        /// <summary>A later invalid sample rejects the whole batch rather than retaining its valid prefix.</summary>
        [Fact]
        public void InvalidBatchDoesNotPartiallyAppend() {
            using PaintSession session=new(new RecordingPaintBackend(),Canvas(),100);
            session.BeginStroke("bounded",Brush());
            PaintSample[] points={new PaintSample { Index=0,Pressure=1 },new PaintSample { Index=1,Pressure=double.NaN }};
            Assert.Throws<ArgumentException>(()=>session.AppendSamples(points,2));
            session.AppendSamples(Tap(),1); session.CommitStroke();
            Assert.Equal(1,session.CaptureSnapshot().GetStroke(0).SampleCount);
        }
        /// <summary>GPU admission failure cannot destroy the input already accepted as document data.</summary>
        [Fact]
        public void SurfaceFailureKeepsAcceptedInputRecoverable() {
            RecordingPaintBackend backend=new() { FailAppend=true }; using PaintSession session=new(backend,Canvas(),100);
            session.BeginStroke("recoverable",Brush());
            Assert.Throws<InvalidOperationException>(()=>session.AppendSamples(Tap(),1));
            Assert.True(session.RenderFailed);
            session.CommitStroke(); Assert.Equal("recoverable",session.CaptureSnapshot().GetStroke(0).Id);
        }
        /// <summary>Opening a saved snapshot establishes content without reintroducing session undo entries.</summary>
        [Fact]
        public void LoadedSnapshotStartsWithEmptyHistory() {
            using PaintSession source=new(new RecordingPaintBackend(),Canvas(),100);
            source.BeginStroke("saved",Brush()); source.AppendSamples(Tap(),1); source.CommitStroke();
            using PaintSnapshot data=source.CaptureSnapshot();
            using PaintSession reopened=new(new RecordingPaintBackend(),Canvas(),100); reopened.Load(data);
            Assert.False(reopened.CanUndo); Assert.False(reopened.CanRedo); Assert.Equal("saved",reopened.CaptureSnapshot().GetStroke(0).Id);
        }
        /// <summary>Admission counts the whole drawing, and rejected input leaves the active gesture usable.</summary>
        [Fact]
        public void SampleBudgetIsDocumentWideAndTransactional() {
            using PaintSession session=new(new RecordingPaintBackend(),Canvas(2),100);
            session.BeginStroke("one",Brush()); session.AppendSamples(Tap(),1); session.CommitStroke();
            session.BeginStroke("two",Brush());
            PaintSample[] batch={new PaintSample { Index=0,Pressure=1 },new PaintSample { Index=1,Pressure=1 }};
            Assert.Throws<ArgumentException>(()=>session.AppendSamples(batch,2));
            session.AppendSamples(Tap(),1); session.CommitStroke();
            Assert.Throws<InvalidOperationException>(()=>session.BeginStroke("three",Brush()));
            Assert.Equal(2,session.CaptureSnapshot().StrokeCount);
        }
        /// <summary>Duplicate IDs and illegal corrections cannot alter authored state.</summary>
        [Fact]
        public void IdentityAndCorrectionErrorsAreAtomic() {
            using PaintSession session=new(new RecordingPaintBackend(),Canvas(),100);
            session.BeginStroke("one",Brush()); session.AppendSamples(Tap(0.3),1);
            PaintSample[] wrong=Tap(1); wrong[0].X=100;
            Assert.Throws<ArgumentException>(()=>session.CorrectSamples(wrong,1));
            Assert.Throws<InvalidOperationException>(()=>session.Undo(1));
            session.CommitStroke();
            Assert.Throws<ArgumentException>(()=>session.BeginStroke("one",Brush()));
            Assert.False(session.HasActiveStroke);
            Assert.Equal(0.3,session.CaptureSnapshot().GetStroke(0).GetSample(0).Pressure);
        }
        /// <summary>A rejected loaded drawing leaves the existing content and undo timeline intact.</summary>
        [Fact]
        public void InvalidLoadPreservesExistingHistory() {
            using PaintSession source=new(new RecordingPaintBackend(),Canvas(),100);
            for(int i=0;i<2;i++) { source.BeginStroke("source"+i,Brush()); source.AppendSamples(Tap(),1); source.CommitStroke(); }
            using PaintSession target=new(new RecordingPaintBackend(),Canvas(1),100);
            target.BeginStroke("retained",Brush()); target.AppendSamples(Tap(),1); target.CommitStroke();
            long revision=target.Revision;
            using PaintSnapshot data=source.CaptureSnapshot();
            Assert.Throws<ArgumentException>(()=>target.Load(data));
            Assert.Equal(revision,target.Revision); Assert.True(target.CanUndo);
            Assert.Equal("retained",target.CaptureSnapshot().GetStroke(0).Id);
        }
    }
}
