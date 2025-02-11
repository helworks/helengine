namespace helengine {
    public interface IDrawable2D {
        Entity Parent { get; }

        byte RenderOrder2D { get; set; }

        float Rotation { get; set; }

        byte4 Color { get; set; }

        float4 SourceRect { get; set; }
        int2 Size { get; set; }

        void Draw();
    }
}
