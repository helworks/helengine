namespace helengine {
    public interface IDrawable3D {
        byte RenderOrder3D { get; set; }

        RenderModelData? RenderData { get; set; }
    }
}
