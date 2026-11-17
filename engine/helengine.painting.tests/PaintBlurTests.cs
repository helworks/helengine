namespace helengine.painting.tests {
    /// <summary>Protects soft-round admission, immutable history and the portable reference edge curve.</summary>
    public sealed class PaintBlurTests {
        /// <summary>Version two admits the menu's full size range while legacy behavior stays bounded and unblurred.</summary>
        [Fact]
        public void VersionedBlurIsValidated() {
            var brush=new PaintBrushDefinition {Version=2,Id="cozy.round",Diameter=20,Opacity=1,Blur=1};
            Assert.Equal(0,PaintContract.ValidateBrush(brush));
            brush.Diameter=20.1;Assert.Equal(1,PaintContract.ValidateBrush(brush));brush.Diameter=2;
            foreach(double value in new[]{-.01,1.01,double.NaN,double.PositiveInfinity}) {brush.Blur=value;Assert.Equal(1,PaintContract.ValidateBrush(brush));}
            brush.Blur=.5;brush.Version=1;Assert.Equal(1,PaintContract.ValidateBrush(brush));
            brush.Blur=0;Assert.Equal(0,PaintContract.ValidateBrush(brush));
        }
        /// <summary>Known radius samples fix the soft edge independently of sampling, opacity and backend derivatives.</summary>
        [Fact]
        public void SoftnessPreservesTheNominalDiameter() {
            Assert.Equal(1,PaintBrushCoverage.Evaluate(.99,0));
            Assert.Equal(1,PaintBrushCoverage.Evaluate(.5,.5));
            Assert.Equal(.5,PaintBrushCoverage.Evaluate(.75,.5),12);
            Assert.Equal(.5,PaintBrushCoverage.Evaluate(.5,1),12);
            Assert.Equal(0,PaintBrushCoverage.Evaluate(1,1));
            Assert.Equal(0,PaintBrushCoverage.Evaluate(1.2,.5));
            Assert.Throws<ArgumentOutOfRangeException>(()=>PaintBrushCoverage.Evaluate(0,double.NaN));
        }
        /// <summary>Mutating the tool cannot change captured marks; undo/redo retain blur for paint and erase.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void BlurSurvivesCaptureAndHistory(bool erase) {
            var brush=new PaintBrushDefinition {Version=2,Id="cozy.round",Diameter=2,Opacity=.7,Blur=.65,Eraser=erase};
            using var sampler=new PaintStrokeSampler(brush);
            using var record=new PaintStrokeRecord("soft",brush,new[]{new PaintSample {X=1,Y=2,Pressure=1}});
            brush.Blur=0;
            Assert.Equal(.65,sampler.Append(new PaintSample {Pressure=1})[0].Blur);
            using var history=new PaintHistory(100);long token=history.Append(record);
            history.Undo(token);history.Redo(token);
            using var restored=history.GetStroke(0);var captured=restored.GetBrush();
            Assert.Equal(.65,captured.Blur);Assert.Equal(erase,captured.Eraser);
            using var replay=new PaintStrokeSampler(captured);
            Assert.Equal(.65,replay.Append(restored.GetSample(0))[0].Blur);
        }
    }
}
