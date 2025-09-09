namespace helengine {
    public interface IRoundedRectDrawable2D : IDrawable2D {
        float Radius { get; set; }
        float BorderThickness { get; set; }
        byte4 FillColor { get; set; }
        byte4 BorderColor { get; set; }
    }
}
