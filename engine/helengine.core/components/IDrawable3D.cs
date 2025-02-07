namespace helengine {
    public interface IDrawable3D {
        Entity Parent { get; }

        byte RenderOrder3D { get; set; }

        RenderModelData? RenderData { get; set; }
    }
}
