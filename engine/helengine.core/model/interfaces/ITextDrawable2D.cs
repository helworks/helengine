namespace helengine {
    public interface ITextDrawable2D: IDrawable2D {
        string Text { get; set; }

        FontAsset Font { get; set; }
    }
}
