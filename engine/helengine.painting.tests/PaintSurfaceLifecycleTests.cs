namespace helengine.painting.tests {
    /// <summary>Exercises generic surface changes without replacing portable history or authored coordinates.</summary>
    public sealed class PaintSurfaceLifecycleTests {
        /// <summary>Constructs a small explicit surface that can move over negative local coordinates.</summary>
        static PaintCanvasDescriptor Canvas() {
            return new PaintCanvasDescriptor { Width=20,Height=30,PixelWidth=200,PixelHeight=300,
                MaxSamples=100000,MaxStamps=2000000,MaxStrokes=10000 };
        }
        /// <summary>Commits one negatively positioned original sample and returns its history identity.</summary>
        static long Paint(PaintSession session) {
            session.BeginStroke("local",new PaintBrushDefinition { Version=1,Id="cozy.round",Diameter=2,Opacity=1 });
            session.AppendSamples(new[] { new PaintSample { X=-4.25,Y=-2.5,Pressure=0.7,Time=123.25 } },1);
            return session.CommitStroke();
        }
        /// <summary>Resizing changes allocation policy only, retaining revision, input values and undo/redo tokens.</summary>
        [Fact]
        public void ReconfigurePreservesLocalSamplesAndHistory() {
            RecordingPaintBackend backend=new(); using PaintSession session=new(backend,Canvas(),100);
            long token=Paint(session); PaintCanvasDescriptor canvas=Canvas();
            canvas.Width=40; canvas.OriginX=-10; canvas.OriginY=-20;
            session.Reconfigure(canvas);
            Assert.Equal(token,session.Revision); Assert.Equal(-4.25,backend.Stamps[0].X);
            using PaintSnapshot snapshot=session.CaptureSnapshot();
            using PaintStrokeRecord stroke=snapshot.GetStroke(0);
            Assert.Equal("local",stroke.Id); Assert.Equal(-4.25,stroke.GetSample(0).X);
            Assert.Equal(123.25,stroke.GetSample(0).Time);
            session.Undo(token); Assert.Empty(backend.Stamps);
            session.Redo(token); Assert.Single(backend.Stamps);
        }
        /// <summary>Inactive history changes cannot enqueue GPU packets; activation explicitly replays the current CPU state.</summary>
        [Fact]
        public void SuspendedUndoRedoRemainCpuOnlyUntilReconfigure() {
            RecordingPaintBackend backend=new(); using PaintSession session=new(backend,Canvas(),100);
            long token=Paint(session); session.Suspend();
            Assert.Empty(backend.Stamps);
            session.Undo(token); session.Redo(token); Assert.Empty(backend.Stamps);
            Assert.Throws<InvalidOperationException>(()=>session.BeginStroke("inactive",new PaintBrushDefinition()));
            session.Reconfigure(Canvas()); Assert.Single(backend.Stamps); Assert.Equal(token,session.Revision);
        }
        /// <summary>Rejected geometry and active-input transitions leave the accepted stroke usable.</summary>
        [Fact]
        public void InvalidOrActiveChangesAreAtomic() {
            RecordingPaintBackend backend=new(); using PaintSession session=new(backend,Canvas(),100);
            PaintCanvasDescriptor invalid=Canvas(); invalid.Width=double.NaN;
            Assert.Throws<ArgumentException>(()=>session.Reconfigure(invalid));
            invalid=Canvas(); invalid.OriginX=double.PositiveInfinity;
            Assert.Throws<ArgumentException>(()=>session.Reconfigure(invalid));
            session.BeginStroke("accepted",new PaintBrushDefinition { Version=1,Id="cozy.round",Diameter=2,Opacity=1 });
            Assert.Throws<InvalidOperationException>(()=>session.Reconfigure(Canvas()));
            Assert.Throws<InvalidOperationException>(()=>session.Suspend());
            session.AppendSamples(new[] { new PaintSample { Pressure=1 } },1);
            session.CommitStroke(); Assert.Single(backend.Stamps);
        }
        /// <summary>A failed replay preserves CPU content and is recoverable through explicit reconfiguration.</summary>
        [Fact]
        public void ReplayFailurePreservesCommittedContentForRecovery() {
            RecordingPaintBackend backend=new(); using PaintSession session=new(backend,Canvas(),100);
            long token=Paint(session); backend.FailAppend=true;
            Assert.Throws<InvalidOperationException>(()=>session.Reconfigure(Canvas()));
            Assert.True(session.RenderFailed); Assert.Equal(token,session.Revision);
            backend.FailAppend=false; session.Reconfigure(Canvas());
            Assert.False(session.RenderFailed); Assert.Single(backend.Stamps);
        }
        /// <summary>The host's remaining document-wide allowance governs this face without losing existing CPU records.</summary>
        [Fact]
        public void ReconfigureAppliesRemainingDocumentBudgets() {
            RecordingPaintBackend backend=new(); using PaintSession session=new(backend,Canvas(),100);
            long token=Paint(session); PaintCanvasDescriptor limited=Canvas(); limited.MaxSamples=1; limited.MaxStamps=1;
            session.Reconfigure(limited);
            Assert.Throws<InvalidOperationException>(()=>session.BeginStroke("beyond-global-limit",
                new PaintBrushDefinition { Version=1,Id="cozy.round",Diameter=2,Opacity=1 }));
            session.Undo(token); session.Redo(token); Assert.Equal(token,session.Revision);
            session.Reconfigure(Canvas());
            session.BeginStroke("available-again",new PaintBrushDefinition { Version=1,Id="cozy.round",Diameter=2,Opacity=1 });
            session.AppendSamples(new[] { new PaintSample { Pressure=1 } },1); session.CommitStroke();
            using PaintSnapshot saved=session.CaptureSnapshot(); Assert.Equal(2,saved.StrokeCount);
        }
        /// <summary>A smaller active allowance rejects redo before moving history, and enlarging it restores the same retained token.</summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void RedoPreflightsReconfiguredSampleStampAndStrokeBudgets(int budget) {
            RecordingPaintBackend backend=new(); using PaintSession session=new(backend,Canvas(),100);
            if(budget==2) Paint(session);
            session.BeginStroke("retained-redo",new PaintBrushDefinition { Version=1,Id="cozy.round",Diameter=2,Opacity=1 });
            session.AppendSamples(new[] { new PaintSample { Index=0,Pressure=1 },new PaintSample { Index=1,Pressure=1 } },2);
            long token=session.CommitStroke(); session.Undo(token);
            long before=session.Revision;
            PaintCanvasDescriptor limited=Canvas();
            if(budget==0) limited.MaxSamples=1;
            else if(budget==1) limited.MaxStamps=1;
            else limited.MaxStrokes=1;
            session.Reconfigure(limited);
            int marks=backend.Stamps.Count;
            Assert.Throws<InvalidOperationException>(()=>session.Redo(token));
            Assert.Equal(before,session.Revision); Assert.True(session.CanRedo); Assert.False(session.RenderFailed);
            Assert.Equal(marks,backend.Stamps.Count);
            using PaintSnapshot rejected=session.CaptureSnapshot(); Assert.Equal(budget==2 ? 1 : 0,rejected.StrokeCount);
            session.Reconfigure(Canvas()); session.Redo(token);
            Assert.Equal(token,session.Revision); Assert.False(session.CanRedo);
            using PaintSnapshot restored=session.CaptureSnapshot();
            using PaintStrokeRecord stroke=restored.GetStroke(restored.StrokeCount-1);
            Assert.Equal("retained-redo",stroke.Id); Assert.Equal(2,stroke.SampleCount);
        }
    }
}
