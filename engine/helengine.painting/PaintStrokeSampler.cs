namespace helengine.painting {
    /// <summary>Replays the unchanged version-one/two authored spacing using its original binary64 arithmetic contract.</summary>
    public sealed class PaintStrokeSampler : IDisposable {
        /// <summary>Brush copied on construction so tools cannot alter an active stroke.</summary>
        [NativeOwnedMember] PaintBrushDefinition Brush;
        /// <summary>Whether an initial input point has been admitted.</summary>
        bool HasPrevious;
        /// <summary>Previous input point before the next interpolated segment.</summary>
        PaintSample Previous;
        /// <summary>Residual distance to the next stamp across input batches.</summary>
        double DistanceToNext;

        /// <summary>Starts one sampler with explicit supported brush settings.</summary>
        public PaintStrokeSampler([NativeNoEscape] PaintBrushDefinition brush) {
            if (PaintContract.ValidateBrush(brush)!=0) { throw new ArgumentException("Invalid or unsupported brush"); }
            Brush=brush.Copy();
        }

        /// <summary>Returns only the additional marks produced by this input point.</summary>
        public PaintStamp[] Append(PaintSample sample) {
            if (Brush==null) { throw new ObjectDisposedException("PaintStrokeSampler"); }
            if (PaintContract.ValidateSample(sample)!=0) { throw new ArgumentException("Invalid paint sample"); }
            if(Brush.IsSurfaceFill) {
                if(HasPrevious || sample.Index!=0 || sample.X!=0 || sample.Y!=0 || sample.Pressure!=1 || sample.Time!=0 || sample.Altitude!=0 || sample.Azimuth!=0)
                    throw new ArgumentException("A surface fill requires one canonical sample");
                HasPrevious=true;
                return new PaintStamp[] { new PaintStamp { FillCanvas=true,Radius=1,Red=Brush.Red,Green=Brush.Green,Blue=Brush.Blue,Opacity=Brush.Opacity } };
            }
            double spacing=Brush.Diameter*0.10;
            if (!HasPrevious) {
                Previous=sample; HasPrevious=true; DistanceToNext=spacing;
                return new PaintStamp[] { Stamp(sample.X,sample.Y,sample.Pressure) };
            }
            double dx=sample.X-Previous.X;
            double dy=sample.Y-Previous.Y;
            double length=Math.Sqrt(dx*dx+dy*dy);
            if (length/spacing+1>2000000) { throw new ArgumentException("Stroke exceeds stamp budget"); }
            List<PaintStamp> result=new List<PaintStamp>();
            if (length>0.000001) {
                double distance=DistanceToNext;
                while (distance<=length+0.000001) {
                    double t=Math.Min(1,distance/length);
                    result.Add(Stamp(Previous.X+dx*t,Previous.Y+dy*t,Previous.Pressure+(sample.Pressure-Previous.Pressure)*t));
                    distance+=spacing;
                }
                DistanceToNext=distance-length;
            }
            Previous=sample;
            return result.ToArray();
        }

        /// <summary>Creates the original v1 round mark without quantizing its authored geometry.</summary>
        PaintStamp Stamp(double x,double y,double pressure) {
            double response=Brush.Eraser ? 1 : 0.15+0.85*Math.Min(1,Math.Max(0,pressure));
            return new PaintStamp { X=x,Y=y,Radius=Brush.Diameter*response/2,
                Red=Brush.Red,Green=Brush.Green,Blue=Brush.Blue,Opacity=Brush.Opacity,Blur=Brush.Blur };
        }

        /// <summary>Clones interpolation state for validating a complete batch before accepting it.</summary>
        public PaintStrokeSampler Copy() {
            if (Brush==null) { throw new ObjectDisposedException("PaintStrokeSampler"); }
            PaintStrokeSampler result=new PaintStrokeSampler(Brush);
            result.HasPrevious=HasPrevious; result.Previous=Previous; result.DistanceToNext=DistanceToNext;
            return result;
        }

        /// <summary>Releases the native-owned captured brush once; managed callers use the same lifecycle.</summary>
        public void Dispose() { NativeOwnership.Release(ref Brush); }
    }
}
