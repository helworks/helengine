namespace helengine.painting {
    /// <summary>Fixed binary64 authored input, retaining legacy stroke data without quantization.</summary>
    public struct PaintSample {
        /// <summary>Stable zero-based index used for delayed input corrections.</summary>
        public int Index;
        /// <summary>Horizontal document coordinate.</summary>
        public double X;
        /// <summary>Vertical document coordinate.</summary>
        public double Y;
        /// <summary>Normalized input pressure.</summary>
        public double Pressure;
        /// <summary>Monotonic host timestamp retained for future brush behavior.</summary>
        public double Time;
        /// <summary>Stylus altitude in radians.</summary>
        public double Altitude;
        /// <summary>Stylus azimuth in radians.</summary>
        public double Azimuth;
    }
}
