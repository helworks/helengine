namespace helengine.painting.tests {
    /// <summary>Protects allocation bounds and untrusted input before any GPU work is accepted.</summary>
    public sealed class PaintCanvasTests {
        /// <summary>Constructs the existing packaging canvas at 300 ppi with its consumer budgets.</summary>
        static PaintCanvasDescriptor Canvas() {
            return new PaintCanvasDescriptor { Width=72,Height=210,PixelWidth=851,PixelHeight=2481,MaxSamples=100000,MaxStamps=2000000,MaxStrokes=10000 };
        }
        /// <summary>Normal packaging data fits the first-version surface budget.</summary>
        [Fact]
        public void AdmitsCurrentPackagingCanvas() { Assert.True(Canvas().IsValid()); }
        /// <summary>Pixel multiplication cannot overflow or exceed the consumer's allocation bounds.</summary>
        [Fact]
        public void RejectsOversizedAndNonfiniteGeometry() {
            PaintCanvasDescriptor canvas=Canvas(); canvas.PixelWidth=int.MaxValue;
            Assert.False(canvas.IsValid());
            canvas.PixelWidth=8192; canvas.PixelHeight=8192; Assert.False(canvas.IsValid());
            canvas.PixelWidth=851; canvas.PixelHeight=2481; canvas.Width=double.NaN; Assert.False(canvas.IsValid());
        }
        /// <summary>Portable input rejects nonfinite pressure and geometry before producing marks.</summary>
        [Fact]
        public void RejectsInvalidInputSample() {
            Assert.Equal(0,PaintContract.ValidateSample(new PaintSample { X=32768,Y=-32768,Pressure=1 }));
            Assert.Equal(1,PaintContract.ValidateSample(new PaintSample { X=32769 }));
            Assert.Equal(1,PaintContract.ValidateSample(new PaintSample { Pressure=double.NaN }));
            Assert.Equal(1,PaintContract.ValidateSample(new PaintSample { Pressure=1.01 }));
            Assert.Equal(1,PaintContract.ValidateSample(new PaintSample { Time=double.PositiveInfinity }));
        }
    }
}
