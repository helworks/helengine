namespace helengine.painting.tests {
    /// <summary>Literal brush fixtures protect spacing, pressure and legacy replay behavior.</summary>
    public sealed class PaintSamplerTests {
        /// <summary>Creates a diameter-two authored brush with explicit full opacity.</summary>
        static PaintBrushDefinition Brush(bool eraser=false) {
            return new PaintBrushDefinition { Version=1, Id="cozy.round", Diameter=2, Red=0.25, Green=0.5, Blue=0.75, Opacity=1, Eraser=eraser };
        }

        /// <summary>A ten-unit line at 0.2-unit spacing contains 51 marks regardless of point batching.</summary>
        [Fact]
        public void SampleBatchBoundariesDoNotChangeStamps() {
            PaintStrokeSampler a=new(Brush()); PaintStrokeSampler b=new(Brush());
            List<PaintStamp> first=new(a.Append(new PaintSample { X=0,Y=1,Pressure=1 }));
            first.AddRange(a.Append(new PaintSample { X=10,Y=1,Pressure=1 }));
            List<PaintStamp> second=new();
            for(int i=0;i<=10;i++) second.AddRange(b.Append(new PaintSample { X=i,Y=1,Pressure=1 }));
            Assert.Equal(51,first.Count); Assert.Equal(51,second.Count);
            for(int i=0;i<51;i++) {
                Assert.Equal(i*0.2,first[i].X,8); Assert.Equal(first[i].X,second[i].X,8);
                Assert.Equal(1,first[i].Radius,8); Assert.Equal(1,second[i].Radius,8);
            }
        }

        /// <summary>Zero pressure retains the original minimum radius; the eraser ignores pressure.</summary>
        [Fact]
        public void LegacyPressureAndTapMatch() {
            PaintStrokeSampler paint=new(Brush());
            PaintStamp[] tap=paint.Append(new PaintSample { X=4,Y=5,Pressure=0 });
            Assert.Single(tap); Assert.Equal(0.15,tap[0].Radius,12);
            Assert.Empty(paint.Append(new PaintSample { X=4,Y=5,Pressure=1 }));
            PaintStamp[] erase=new PaintStrokeSampler(Brush(true)).Append(new PaintSample { Pressure=0 });
            Assert.Equal(1,erase[0].Radius,12);
        }

        /// <summary>Brush mutations by a caller do not change the already-started stroke.</summary>
        [Fact]
        public void BrushDefinitionIsCapturedAtStrokeStart() {
            PaintBrushDefinition brush=Brush(); PaintStrokeSampler sampler=new(brush);
            brush.Diameter=8;
            Assert.Equal(1,sampler.Append(new PaintSample { Pressure=1 })[0].Radius,12);
        }

        /// <summary>Explicit native teardown makes later input fail without touching released memory.</summary>
        [Fact]
        public void DisposedSamplerRejectsInput() {
            PaintStrokeSampler sampler=new(Brush());
            sampler.Dispose(); sampler.Dispose();
            Assert.Throws<ObjectDisposedException>(()=>sampler.Append(new PaintSample()));
        }
    }
}
