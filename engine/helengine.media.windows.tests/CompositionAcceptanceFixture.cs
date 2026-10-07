using System.Security.Cryptography;
using System.Text.Json;
using helengine.media;
namespace helengine.media.windows.tests;
/// <summary>Own six-second spoken/impulse source, post, mask, captions and explicit visual/audio overlaps.</summary>
public static class CompositionAcceptanceFixture {
    /// <summary>Owned synthetic media rather than user uploads.</summary>
    public const string Root="C:/dev/helworks/builds/helengine/media-composition/fixtures/acceptance-assets";
    /// <summary>Builds one exact timeline with format-independent normalized placement.</summary>
    public static CompositionDocument Create(int width,int height) {
        var source=Reference("source.mov","video",64,64);var post=Reference("post.png","image",256,256);var mask=Reference("mask.png","image",256,256);var music=Reference("music.wav","audio",0,0);
        var document=new CompositionDocument{Id="acceptance",Revision=1,Width=width,Height=height,FrameRate=new(24,1),Duration=new(6,1),BackgroundColor="#00000000",Media=[source,post,mask,music],AudioClips=[new(){Id="voice",MediaId=source.Id,Start=MediaTime.Zero,End=new(6,1),SourceOut=new(6,1)},new(){Id="music",MediaId=music.Id,Start=MediaTime.Zero,End=new(6,1),SourceOut=new(6,1),Gain=.2,Envelopes=[new(){Type="linear",Start=MediaTime.Zero,Duration=new(1,2),From=0,To=1},new(){Type="linear",Start=new(5,1),Duration=new(1,1),From=1,To=0}]}],Layers=[new(){Id="speaker",MediaId=source.Id,Fit="cover",Start=MediaTime.Zero,End=new(7,2),SourceOut=new(7,2)},new(){Id="post-a",MediaId=post.Id,Order=1,Start=new(3,2),End=new(7,2),Viewport=new(){X=.14,Y=.14,Width=.72,Height=.55},Mask=new(){MediaId=mask.Id},Animations=[Track("opacity",0,0,.25,1,"linear.v1"),Track("zoom",.5,1,1.5,1.2,"smoothstep.v1")]},Caption("caption-a","UMA FRASE EXIGE CONTEXTO",new(1,5),new(7,2),2,Math.Min(width,height)),new(){Id="scene-a",Kind="group",Start=MediaTime.Zero,End=new(7,2),Members=["speaker","post-a","caption-a"]},new(){Id="post-b",MediaId=post.Id,Start=new(3,1),End=new(6,1),Viewport=new(){X=.14,Y=.14,Width=.72,Height=.55},Mask=new(){MediaId=mask.Id},Animations=[Track("zoom",0,1,2,1.2,"smoothstep.v1")]},Caption("caption-b","IMAGEM E FALA NO MESMO TEMPO",new(3,1),new(6,1),2,Math.Min(width,height)),new(){Id="scene-b",Kind="group",Order=10,Start=new(3,1),End=new(6,1),Members=["post-b","caption-b"]}],Transitions=[new(){Id="crossfade",FromLayer="scene-a",ToLayer="scene-b",Start=new(3,1),Duration=new(1,2)}]};return document;
    }
    /// <summary>Pins measured source bytes and dimensions.</summary>
    static MediaReference Reference(string name,string kind,int width,int height)=>new(){Id=name,Kind=kind,Path=name,Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Root,name)))).ToLowerInvariant(),Width=width,Height=height,Duration=kind=="image"?MediaTime.Zero:new(6,1)};
    /// <summary>Builds a bounded scene-local animation preserving its named curve.</summary>
    static PropertyAnimation Track(string property,double start,double from,double end,double to,string curve)=>new(){Property=property,Keyframes=[new(){Time=MediaTime.FromSeconds(start),Value=from,Curve=curve},new(){Time=MediaTime.FromSeconds(end),Value=to,Curve=curve}]};
    /// <summary>Creates independent authored text without invented word timestamps.</summary>
    static VisualLayer Caption(string id,string text,MediaTime start,MediaTime end,int order,int canvasSize)=>new(){Id=id,Kind="text",Order=order,Start=start,End=end,Text=new(){Cues=[new(){Text=text,Start=start,End=end}],Style=JsonSerializer.SerializeToElement(new{FontFamily="Segoe UI",FontSize=32.0*canvasSize/540,Bold=true,Uppercase=false,TextColor="#FFFFFF",OutlineColor="#000000",OutlineWidth=2.0*canvasSize/540,ShadowOffset=0,CenterY=.83,Animation="None"})}};
}
