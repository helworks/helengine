namespace helengine.painting {
    /// <summary>One round mark in document units; the surface backend applies its explicit pixel scale.</summary>
    public struct PaintStamp {
        /// <summary>Applies uniform color across the surface clip instead of a round, pressure-scaled mark.</summary>
        public bool FillCanvas;
        /// <summary>Horizontal mark center in document units.</summary>
        public double X;
        /// <summary>Vertical mark center in document units.</summary>
        public double Y;
        /// <summary>Pressure-adjusted radius in document units.</summary>
        public double Radius;
        /// <summary>Straight red component, premultiplied only by the fragment shader.</summary>
        public double Red;
        /// <summary>Straight green component.</summary>
        public double Green;
        /// <summary>Straight blue component.</summary>
        public double Blue;
        /// <summary>Normalized inward edge softness; zero uses the original antialiased hard edge.</summary>
        public double Blur;
        /// <summary>Brush opacity before geometric coverage.</summary>
        public double Opacity;
    }
}
