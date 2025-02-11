namespace helengine {
    public interface IDrawable3D {
        Entity Parent { get; }

        byte RenderOrder3D { get; set; }

        RuntimeModel? Model { get; set; }

        byte Variant { get; set; }
    }
}
