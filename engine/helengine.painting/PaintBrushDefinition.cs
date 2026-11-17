namespace helengine.painting {
    /// <summary>Versioned portable brush settings, independent of input devices and file formats.</summary>
    public sealed class PaintBrushDefinition {
        /// <summary>Behavior version used when replaying authored strokes.</summary>
        public int Version;
        /// <summary>Portable brush identity; versions one and two support hard and soft round marks.</summary>
        public string Id;
        /// <summary>Maximum diameter in the consumer's document units.</summary>
        public double Diameter;
        /// <summary>Unpremultiplied red color channel.</summary>
        public double Red;
        /// <summary>Unpremultiplied green color channel.</summary>
        public double Green;
        /// <summary>Unpremultiplied blue color channel.</summary>
        public double Blue;
        /// <summary>Brush opacity before geometric coverage.</summary>
        public double Opacity;
        /// <summary>Normalized inward edge softness for version two; zero preserves the legacy hard edge.</summary>
        public double Blur;
        /// <summary>Whether coverage removes existing premultiplied color and alpha.</summary>
        public bool Eraser;

        /// <summary>Identifies a single uniform application over the surface's existing geometric clip.</summary>
        public bool IsSurfaceFill { get { return Id == "helengine.fill"; } }

        /// <summary>Captures a uniform fill independently of round-brush diameter, softness and pressure.</summary>
        [NativeOwnedReturn]
        public static PaintBrushDefinition CreateSurfaceFill(double red,double green,double blue,double opacity) {
            PaintBrushDefinition result=new PaintBrushDefinition { Version=1,Id="helengine.fill",Diameter=1,Red=red,Green=green,Blue=blue,Opacity=opacity };
            if(PaintContract.ValidateBrush(result)!=0) { NativeOwnership.Release(ref result); throw new ArgumentException("Invalid fill color"); }
            return result;
        }

        /// <summary>Copies authored settings so later tool changes cannot mutate a stored stroke.</summary>
        public PaintBrushDefinition Copy() {
            return new PaintBrushDefinition { Version=Version, Id=Id, Diameter=Diameter,
                Red=Red, Green=Green, Blue=Blue, Opacity=Opacity, Blur=Blur, Eraser=Eraser };
        }
    }
}
