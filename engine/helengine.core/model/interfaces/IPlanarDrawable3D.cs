namespace helengine {
    /// <summary>Identifies a flat drawable whose transparency depth is determined by its world-space plane.</summary>
    public interface IPlanarDrawable3D {
        /// <summary>Gets the world-space normal of the plane passing through the owning entity's position.</summary>
        float3 WorldPlaneNormal { get; }
    }
}
