using System.Security.Cryptography;
using System.Text;
namespace helengine.media;
/// <summary>Streams bounded composition frames and mixed PCM, then atomically publishes successful encoding.</summary>
public sealed class CompositionExporter {
    /// <summary>Caller-owned GPU compositor used for both preview and export.</summary>
    readonly IMediaCompositor Compositor;
    /// <summary>Caller-owned exact timeline mixer.</summary>
    readonly CompositionAudioMixer Mixer;
    /// <summary>Caller-owned final encoding boundary.</summary>
    readonly ICompositionEncoder Encoder;
    /// <summary>Pinned executable capabilities used before any encoding or GPU allocation.</summary>
    readonly MediaCapabilities Capabilities;
    /// <summary>Retains explicit dependencies without opening media or devices.</summary>
    public CompositionExporter(IMediaCompositor compositor,CompositionAudioMixer mixer,ICompositionEncoder encoder,MediaCapabilities capabilities) {Compositor=compositor;Mixer=mixer;Encoder=encoder;Capabilities=capabilities;}
    /// <summary>Exports one immutable snapshot with one video frame and one bounded PCM block in flight.</summary>
    public async Task<CompositionRenderReport> ExportAsync(CompositionDocument document,OutputProfile profile,string output,CancellationToken token) {
        token.ThrowIfCancellationRequested();var snapshot=CompositionJson.Parse(CompositionJson.Serialize(document));var errors=CompositionValidator.Validate(snapshot,Capabilities);if(errors.Count>0) {throw new InvalidDataException(string.Join("; ",errors.Select(error=>error.Code+": "+error.Message)));}
        string fingerprint=Fingerprint(snapshot,profile);if(!profile.PreserveAlpha) {snapshot.BackgroundColor=snapshot.BackgroundColor[..7]+"FF";}
        string target=Path.GetFullPath(output);Directory.CreateDirectory(Path.GetDirectoryName(target));string partial=target+"."+Guid.NewGuid().ToString("N")+".partial";string partialReport=partial+".report.json";
        var clock=new CompositionClock(snapshot.FrameRate,snapshot.Audio.SampleRate,snapshot.Duration);var report=new CompositionRenderReport{Fingerprint=fingerprint,CompositionId=snapshot.Id,Revision=snapshot.Revision,Profile=profile.Id,HasAudio=snapshot.Audio.Enabled,Width=profile.Width,Height=profile.Height};using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(token);
        try {
            await Encoder.BeginAsync(snapshot,profile,partial,cancellation.Token);
            Task video=Task.Run(async()=>{try {for(long index=0;index<clock.FrameCount;index++) {cancellation.Token.ThrowIfCancellationRequested();using var frame=Compositor.Render(snapshot,clock.FrameTime(index),new(profile.Width,profile.Height));await Encoder.WriteVideoFrameAsync(frame,index,cancellation.Token);report.FrameCount++;}await Encoder.FinishVideoAsync(cancellation.Token);}catch {cancellation.Cancel();throw;}},CancellationToken.None);
            Task audio=Task.Run(async()=>{try {if(snapshot.Audio.Enabled) {for(long first=0;first<clock.SampleCount;) {cancellation.Token.ThrowIfCancellationRequested();var mixed=Mixer.Render(snapshot,first,2048);await Encoder.WriteAudioBlockAsync(mixed.Block,cancellation.Token);report.SampleCount+=mixed.Block.SampleCount;report.AudioPeak=Math.Max(report.AudioPeak,mixed.Peaks.Maximum);first+=mixed.Block.SampleCount;}await Encoder.FinishAudioAsync(cancellation.Token);}}catch {cancellation.Cancel();throw;}},CancellationToken.None);
            await Task.WhenAll(video,audio);await Encoder.CompleteAsync(cancellation.Token);token.ThrowIfCancellationRequested();await File.WriteAllTextAsync(partialReport,JsonSerializer.Serialize(report,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.SnakeCaseLower,WriteIndented=true}),token);token.ThrowIfCancellationRequested();File.Move(partial,target,true);File.Move(partialReport,target+".report.json",true);return report;
        }catch {cancellation.Cancel();Encoder.Dispose();File.Delete(partial);File.Delete(partialReport);throw;}
    }
    /// <summary>Computes a stable pinned render identity used by the host's stale-job compare-and-swap.</summary>
    public static string Fingerprint(CompositionDocument document,OutputProfile profile)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CompositionJson.Serialize(document)+"\n"+profile.Id+"\n"+profile.Width+"x"+profile.Height))).ToLowerInvariant();
}
