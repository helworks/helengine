namespace helengine.editor.tests.managers.project;

/// <summary>Imports real fixture PNG pixels so distinct platform alternatives retain their identities during content deduplication and actual PS1 cooking.</summary>
internal sealed class SceneDependencyTextureImporter : ITextureImporter {
    /// <summary>Reads an actual PNG texel, matching the image bytes later consumed by the PS1 source decoder.</summary>
    public TextureAsset ImportTexture(Stream stream) {
        using System.Drawing.Bitmap bitmap = new(stream);
        System.Drawing.Color color = bitmap.GetPixel(0, 0);
        return new TextureAsset { Width = 1, Height = 1, Colors = [color.R, color.G, color.B, color.A] };
    }
}
