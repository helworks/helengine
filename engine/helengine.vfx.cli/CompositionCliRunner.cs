using System.Drawing.Imaging;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using helengine.media;
using helengine.media.windows;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
namespace helengine.vfx.cli;
/// <summary>Runs the versioned composition protocol without exposing model-authored commands or shader code.</summary>
public static class CompositionCliRunner {
    /// <summary>Returns exact installed effects, curves and finite parameter ranges.</summary>
    public static string CapabilitiesJson()=>WindowsMediaCapabilities.Describe().Describe().GetRawText();
    /// <summary>Routes controlled commands and emits deterministic nonzero caller-facing failures.</summary>
    public static int Run(string[] args) {
        try {return RunCore(args);}catch(Exception error) when(error is InvalidDataException or IOException or JsonException or ArgumentException or InvalidOperationException or OverflowException or OperationCanceledException) {Console.Error.WriteLine(error.Message);return error is OperationCanceledException?130:1;}
    }
    /// <summary>Validates options and source ownership before constructing a GPU device.</summary>
    static int RunCore(string[] args) {
        if(args.Length==0) {throw new InvalidDataException("Expected composition capabilities, validate, render or frame.");}string command=args[0];if(command=="capabilities") {if(args.Length!=2 || args[1]!="--json") {throw new InvalidDataException("Use composition capabilities --json.");}Console.WriteLine(CapabilitiesJson());return 0;}
        if(command is not ("validate" or "render" or "frame")) {throw new InvalidDataException("Unknown composition command.");}var options=new Dictionary<string,string>(StringComparer.Ordinal);var allowed=command=="validate"?new[]{"--input","--assets-root"}:command=="frame"?new[]{"--input","--assets-root","--time","--out","--width","--height","--warp"}:new[]{"--input","--assets-root","--out","--profile","--ffmpeg","--width","--height","--warp"};for(int index=1;index<args.Length;index+=2) {if(index+1>=args.Length || !allowed.Contains(args[index]) || !options.TryAdd(args[index],args[index+1])) {throw new InvalidDataException("Invalid or repeated composition option.");}}
        string input=Required(options,"--input");string root=Path.GetFullPath(Required(options,"--assets-root"));var document=CompositionJson.Parse(File.ReadAllText(input));var diagnostics=CompositionValidator.Validate(document,WindowsMediaCapabilities.Describe());if(diagnostics.Count>0) {Console.WriteLine(JsonSerializer.Serialize(new{valid=false,diagnostics}));return 1;}ValidateAssets(document,root);if(command=="validate") {Console.WriteLine("{\"valid\":true,\"diagnostics\":[]}");return 0;}
        int width=options.TryGetValue("--width",out string widthText)?int.Parse(widthText,CultureInfo.InvariantCulture):document.Width;int height=options.TryGetValue("--height",out string heightText)?int.Parse(heightText,CultureInfo.InvariantCulture):document.Height;var profile=OutputProfile.Resolve(command=="render"?Required(options,"--profile"):"mov-prores4444-pcm.v1",width,height);string output=Path.GetFullPath(Required(options,"--out"));using var cancellation=new CancellationTokenSource();ConsoleCancelEventHandler cancel=(sender,eventArgs)=>{eventArgs.Cancel=true;cancellation.Cancel();};Console.CancelKeyPress+=cancel;
        try {using var device=new Device(options.TryGetValue("--warp",out string warp) && warp=="true"?DriverType.Warp:DriverType.Hardware,DeviceCreationFlags.BgraSupport);using var resolver=new WindowsMediaSourceResolver(root,device);using var compositor=new DirectX11MediaCompositor(device,resolver);
            if(command=="frame") {string[] values=Required(options,"--time").Split('/');if(values.Length!=2) {throw new InvalidDataException("Frame time must be numerator/denominator.");}var time=new MediaTime(long.Parse(values[0],CultureInfo.InvariantCulture),long.Parse(values[1],CultureInfo.InvariantCulture));if(time<MediaTime.Zero || time>=document.Duration) {throw new InvalidDataException("Frame time is outside the timeline.");}using var frame=compositor.Render(document,time,new(width,height));SavePng(frame,output);return 0;}
            string ffmpeg=options.TryGetValue("--ffmpeg",out string executable)?executable:"ffmpeg";using var encoder=new FfmpegCompositionEncoder(ffmpeg);using var mixer=new CompositionAudioMixer(resolver);var exporter=new CompositionExporter(compositor,mixer,encoder,WindowsMediaCapabilities.Describe());var report=exporter.ExportAsync(document,profile,output,cancellation.Token).GetAwaiter().GetResult();Console.WriteLine(JsonSerializer.Serialize(report,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.SnakeCaseLower}));return 0;
        }finally {Console.CancelKeyPress-=cancel;}
    }
    /// <summary>Rejects unresolved paths, reparse traversal and changed source hashes before decoder allocation.</summary>
    static void ValidateAssets(CompositionDocument document,string root) {
        if(!Directory.Exists(root)) {throw new DirectoryNotFoundException(root);}foreach(var reference in document.Media) {if(Path.IsPathRooted(reference.Path) || reference.Path.Contains(':')) {throw new InvalidDataException("Source path must be owned and relative.");}string path=Path.GetFullPath(Path.Combine(root,reference.Path));if(!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) {throw new InvalidDataException("Source escaped its assets root.");}string current=path;while(current!=null) {if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) {throw new InvalidDataException("Assets cannot traverse reparse points.");}if(string.Equals(current,root,StringComparison.OrdinalIgnoreCase)) {break;}current=Path.GetDirectoryName(current);}using var source=File.OpenRead(path);string hash=Convert.ToHexString(SHA256.HashData(source));if(!string.Equals(hash,reference.Sha256,StringComparison.OrdinalIgnoreCase)) {throw new InvalidDataException("Source hash no longer matches its pinned reference.");}}
    }
    /// <summary>Returns one required host option without silent defaults.</summary>
    static string Required(Dictionary<string,string> options,string key)=>options.TryGetValue(key,out string value) && !string.IsNullOrWhiteSpace(value)?value:throw new InvalidDataException("Missing "+key);
    /// <summary>Writes an owned straight-RGBA diagnostic frame as PNG through the explicit readback boundary.</summary>
    static void SavePng(MediaVideoFrame frame,string output) {
        Directory.CreateDirectory(Path.GetDirectoryName(output));byte[] rgba=frame.Surface.ReadRgba().ToArray();using var bitmap=new System.Drawing.Bitmap(frame.Surface.Width,frame.Surface.Height,PixelFormat.Format32bppArgb);var data=bitmap.LockBits(new(0,0,bitmap.Width,bitmap.Height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);try {byte[] row=new byte[bitmap.Width*4];for(int y=0;y<bitmap.Height;y++) {for(int x=0;x<bitmap.Width;x++) {int index=(y*bitmap.Width+x)*4;row[x*4]=rgba[index+2];row[x*4+1]=rgba[index+1];row[x*4+2]=rgba[index];row[x*4+3]=rgba[index+3];}System.Runtime.InteropServices.Marshal.Copy(row,0,IntPtr.Add(data.Scan0,y*data.Stride),row.Length);}}finally {bitmap.UnlockBits(data);}bitmap.Save(output,ImageFormat.Png);
    }
}
