namespace helengine.painting {
    /// <summary>Explicit surface geometry and consumer admission budgets, independent of file format.</summary>
    public sealed class PaintCanvasDescriptor {
        /// <summary>Document width before conversion to pixels.</summary>
        public double Width;
        /// <summary>Document height before conversion to pixels.</summary>
        public double Height;
        /// <summary>Local document coordinate mapped to the target's left edge; samples remain unchanged.</summary>
        public double OriginX;
        /// <summary>Local document coordinate mapped to the target's top edge; samples remain unchanged.</summary>
        public double OriginY;
        /// <summary>Allocated horizontal pixel extent.</summary>
        public int PixelWidth;
        /// <summary>Allocated vertical pixel extent.</summary>
        public int PixelHeight;
        /// <summary>Total admitted input sample budget.</summary>
        public int MaxSamples;
        /// <summary>Conservative total raster mark budget.</summary>
        public int MaxStamps;
        /// <summary>Total persisted stroke budget.</summary>
        public int MaxStrokes;

        /// <summary>Returns whether the explicit dimensions and budgets can be admitted safely.</summary>
        public bool IsValid() {
            return double.IsFinite(Width) && double.IsFinite(Height) && Width>=1 && Height>=1 && Width<=32768 && Height<=32768
                && double.IsFinite(OriginX) && double.IsFinite(OriginY) && Math.Abs(OriginX)<=32768 && Math.Abs(OriginY)<=32768
                && PixelWidth>0 && PixelHeight>0 && PixelWidth<=8192 && PixelHeight<=8192
                && (long)PixelWidth*PixelHeight<=24000000
                && MaxSamples>0 && MaxSamples<=100000 && MaxStamps>0 && MaxStamps<=2000000
                && MaxStrokes>0 && MaxStrokes<=10000;
        }

        /// <summary>Copies the admission policy to isolate a live surface from caller mutation.</summary>
        public PaintCanvasDescriptor Copy() {
            return new PaintCanvasDescriptor { Width=Width,Height=Height,OriginX=OriginX,OriginY=OriginY,PixelWidth=PixelWidth,PixelHeight=PixelHeight,
                MaxSamples=MaxSamples,MaxStamps=MaxStamps,MaxStrokes=MaxStrokes };
        }
    }
}
