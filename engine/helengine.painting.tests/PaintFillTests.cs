namespace helengine.painting.tests {
    /// <summary>Uniform clip fills remain one replayable operation with ordinary history and strict admission.</summary>
    public sealed class PaintFillTests {
        /// <summary>Filling, undoing and restoring a saved snapshot preserve color and one raster command.</summary>
        [Fact]
        public void FillSurvivesHistoryAndReload() {
            var backend=new RecordingPaintBackend();
            var canvas=new PaintCanvasDescriptor{Width=20,Height=30,PixelWidth=200,PixelHeight=300,MaxSamples=100,MaxStamps=1000,MaxStrokes=100};
            using var session=new PaintSession(backend,canvas,10);
            var fill=PaintBrushDefinition.CreateSurfaceFill(.2,.4,.6,.75);
            session.BeginStroke("fill",fill);session.AppendSamples(new[]{new PaintSample{Pressure=1}},1);long token=session.CommitStroke();
            Assert.True(Assert.Single(backend.Stamps).FillCanvas);
            using var snapshot=session.CaptureSnapshot();using var record=snapshot.GetStroke(0);
            Assert.Equal(1,record.SampleCount);Assert.Equal(1,record.StampBudget);Assert.True(record.GetBrush().IsSurfaceFill);
            session.Undo(token);Assert.Empty(backend.Stamps);session.Redo(token);Assert.True(Assert.Single(backend.Stamps).FillCanvas);
            var restoredBackend=new RecordingPaintBackend();using var restored=new PaintSession(restoredBackend,canvas,10);restored.Load(snapshot);
            var mark=Assert.Single(restoredBackend.Stamps);Assert.True(mark.FillCanvas);Assert.Equal(.75,mark.Opacity);Assert.Equal(.2,mark.Red);
        }
        /// <summary>A fill cannot accumulate pointer samples, softness or pressure corrections as if it were a round brush.</summary>
        [Fact]
        public void FillRejectsNoncanonicalRecordsAndExtraInput() {
            var fill=PaintBrushDefinition.CreateSurfaceFill(1,0,0,1);
            Assert.Throws<ArgumentException>(()=>new PaintStrokeRecord("bad",fill,new[]{new PaintSample{Pressure=.5}}));
            Assert.Throws<ArgumentException>(()=>new PaintStrokeRecord("bad",fill,new[]{new PaintSample{Pressure=1},new PaintSample{Index=1,Pressure=1}}));
            using var sampler=new PaintStrokeSampler(fill);Assert.True(Assert.Single(sampler.Append(new PaintSample{Pressure=1})).FillCanvas);
            Assert.Throws<ArgumentException>(()=>sampler.Append(new PaintSample{Index=1,Pressure=1}));
            fill.Blur=.5;Assert.NotEqual(0,PaintContract.ValidateBrush(fill));
        }
    }
}
