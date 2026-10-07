using System.Drawing.Imaging;
using System.Text.Json;
using System.Text.Json.Serialization;
using helengine.vfx.captions;
namespace helengine.media.windows;
/// <summary>Renders the existing caption engine at absolute composition time as an independent layer.</summary>
public sealed class CaptionLayerSource : IDisposable {
    /// <summary>Portable immutable text snapshot for this layer.</summary>
    readonly CompositionText Text;
    /// <summary>Graphics device borrowed from the render session.</summary>
    readonly Device Device;
    /// <summary>Authored canvas width used to scale pixel-sized type for previews.</summary>
    readonly int CanvasWidth;
    /// <summary>Caption rasterizer for the currently requested output resolution.</summary>
    CaptionRenderer Renderer;
    /// <summary>Output width used by the current rasterizer.</summary>
    int RendererWidth;
    /// <summary>Snapshot of the original portable style.</summary>
    readonly CaptionStyle Style;
    /// <summary>Read lease pins staged font bytes for the lifetime of the caption rasterizer.</summary>
    readonly FileStream FontLease;
    /// <summary>Existing caption document preserving any actual word alignment.</summary>
    readonly CaptionDocument Document;
    /// <summary>Validates and prepares text without allocating GPU surfaces.</summary>
    public CaptionLayerSource(CompositionText text,int canvasWidth,Device device,string assetsRoot=null) {
        Text=text;CanvasWidth=canvasWidth;Device=device;
        var options=new JsonSerializerOptions {PropertyNameCaseInsensitive=true};options.Converters.Add(new JsonStringEnumConverter());
        Style=JsonSerializer.Deserialize<CaptionStyle>(text.Style.GetRawText(),options) ?? throw new InvalidDataException("Caption style is missing.");if(!string.IsNullOrWhiteSpace(Style.FontFile)) {
            if(assetsRoot==null || Path.IsPathRooted(Style.FontFile) || Style.FontFile.Contains(':')) {throw new InvalidDataException("Font requires an owned relative path.");}string root=Path.GetFullPath(assetsRoot);string path=Path.GetFullPath(Path.Combine(root,Style.FontFile));if(!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) {throw new InvalidDataException("Font escaped its assets root.");}
            for(string current=path;current!=null;current=Path.GetDirectoryName(current)) {if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) {throw new InvalidDataException("Font cannot traverse reparse points.");}if(string.Equals(current,root,StringComparison.OrdinalIgnoreCase)) {break;}}FontLease=new(path,FileMode.Open,FileAccess.Read,FileShare.Read);if(text.Style.TryGetProperty("FontSha256",out var expectedFontHash)) {string actual=Convert.ToHexString(SHA256.HashData(FontLease));FontLease.Position=0;if(expectedFontHash.ValueKind!=JsonValueKind.String || !string.Equals(actual,expectedFontHash.GetString(),StringComparison.OrdinalIgnoreCase)) {FontLease.Dispose();throw new InvalidDataException("Caption font changed after its snapshot was pinned.");}}Style.FontFile=path;
        }
        try {Style.Validate();}catch {FontLease?.Dispose();throw;}
        Document=new(text.Cues.Select(cue=>new CaptionCue(cue.Text,cue.Start.ToSeconds(),cue.End.ToSeconds(),cue.Words.Select(word=>new CaptionWord(word.Text,word.Start.ToSeconds(),word.End.ToSeconds())))));
    }
    /// <summary>Evaluates absolute cue time and uploads the transparent raster without image-layer transforms.</summary>
    public MediaVideoFrame Render(MediaTime time,RenderSize size) {
        if(Renderer==null || RendererWidth!=size.Width) {Renderer?.Dispose();var style=Style.Copy();double scale=(double)size.Width/CanvasWidth;style.FontSize*=scale;style.OutlineWidth*=scale;style.ShadowOffset*=scale;Renderer=new(Document,style);RendererWidth=size.Width;}
        using var bitmap=Renderer.Render(time.ToSeconds(),size.Width,size.Height);var data=bitmap.LockBits(new System.Drawing.Rectangle(0,0,size.Width,size.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);var bytes=new byte[checked(size.Width*size.Height*4)];
        try {for(int row=0;row<size.Height;row++) {Marshal.Copy(IntPtr.Add(data.Scan0,row*data.Stride),bytes,row*size.Width*4,size.Width*4);}}
        finally {bitmap.UnlockBits(data);}
        for(int index=0;index<bytes.Length;index+=4) {byte blue=bytes[index];bytes[index]=bytes[index+2];bytes[index+2]=blue;}
        var pinned=GCHandle.Alloc(bytes,GCHandleType.Pinned);
        try {var description=new Texture2DDescription {Width=size.Width,Height=size.Height,MipLevels=1,ArraySize=1,Format=Format.R8G8B8A8_UNorm,SampleDescription=new(1,0),Usage=ResourceUsage.Immutable,BindFlags=BindFlags.ShaderResource};var texture=new Texture2D(Device,description,[new SharpDX.DataBox(pinned.AddrOfPinnedObject(),size.Width*4,0)]);return new(time,MediaTime.Zero,new DirectX11VideoSurface(Device,texture));}
        finally {pinned.Free();}
    }
    /// <summary>Releases the font/rasterizer resources retained by this layer.</summary>
    public void Dispose() {Renderer?.Dispose();Renderer=null;FontLease?.Dispose();}
}
