namespace helengine.painting {
    /// <summary>Owns immutable authored data for one committed stroke, independent of GPU resources.</summary>
    public sealed class PaintStrokeRecord : IDisposable {
        /// <summary>Captured brush settings owned by this record.</summary>
        [NativeOwnedMember] PaintBrushDefinition Brush;
        /// <summary>Captured value samples owned by this record.</summary>
        [NativeOwnedMember] PaintSample[] Samples;
        /// <summary>Consumer-supplied stable identity preserved by replay and history.</summary>
        public string Id { get; }
        /// <summary>Number of authored samples, without exposing a mutable array.</summary>
        public int SampleCount { get { return Samples.Length; } }
        /// <summary>Conservative v1 replay cost including segment rounding slack.</summary>
        public double StampBudget { get; }

        /// <summary>Copies validated input so later caller edits do not change committed data.</summary>
        public PaintStrokeRecord(string id,[NativeNoEscape] PaintBrushDefinition brush,[NativeNoEscape] PaintSample[] samples) {
            if (string.IsNullOrEmpty(id) || id.Length>128 || PaintContract.ValidateBrush(brush)!=0 || samples==null || samples.Length==0 || samples.Length>100000) {
                throw new ArgumentException("Invalid stroke record");
            }
            if(brush.IsSurfaceFill && (samples.Length!=1 || samples[0].Index!=0 || samples[0].X!=0 || samples[0].Y!=0 || samples[0].Pressure!=1 || samples[0].Time!=0 || samples[0].Altitude!=0 || samples[0].Azimuth!=0)) {
                throw new ArgumentException("Invalid surface fill record");
            }
            double cost=1;
            for(int i=0;i<samples.Length;i++) {
                if(PaintContract.ValidateSample(samples[i])!=0 || samples[i].Index!=i) throw new ArgumentException("Invalid stroke sample");
                if(i>0) {
                    double dx=samples[i].X-samples[i-1].X;
                    double dy=samples[i].Y-samples[i-1].Y;
                    cost+=Math.Sqrt(dx*dx+dy*dy)/(brush.Diameter*0.10)+1;
                }
                if(cost>2000000) throw new ArgumentException("Stroke exceeds stamp budget");
            }
            StampBudget=cost;
            Id=id; Brush=brush.Copy(); Samples=new PaintSample[samples.Length];
            Array.Copy(samples,Samples,samples.Length);
        }

        /// <summary>Returns one value sample without granting mutation of stored history.</summary>
        public PaintSample GetSample(int index) { return Samples[index]; }

        /// <summary>Returns an independently owned copy of the recorded brush.</summary>
        [NativeOwnedReturn]
        public PaintBrushDefinition GetBrush() { return Brush.Copy(); }

        /// <summary>Copies this immutable record for a different explicit owner.</summary>
        [NativeOwnedReturn]
        public PaintStrokeRecord Copy() { return new PaintStrokeRecord(Id,Brush,Samples); }

        /// <summary>Releases native allocations once after every borrower has finished.</summary>
        public void Dispose() { NativeOwnership.Release(ref Samples); NativeOwnership.Release(ref Brush); }
    }
}
