namespace helengine.media;
/// <summary>Mixes voice, clip audio, music and effects through one sample-accurate timeline.</summary>
public sealed class CompositionAudioMixer : IDisposable {
    /// <summary>Source factory borrowed from the render session.</summary>
    readonly IMediaSourceResolver Resolver;
    /// <summary>Independent audio source cursors keyed by pinned media metadata.</summary>
    readonly Dictionary<string,IMediaSource> Sources=new(StringComparer.Ordinal);
    /// <summary>Prevents reads after cursor disposal.</summary>
    bool Disposed;
    /// <summary>Retains an explicit verified source resolver.</summary>
    public CompositionAudioMixer(IMediaSourceResolver resolver) {Resolver=resolver ?? throw new ArgumentNullException(nameof(resolver));}
    /// <summary>Renders a bounded output interval independent of previous calls and query order.</summary>
    public AudioMixResult Render(CompositionDocument document,long firstSample,int sampleCount) {
        if(Disposed) {throw new ObjectDisposedException(nameof(CompositionAudioMixer));}
        ArgumentNullException.ThrowIfNull(document);
        if(firstSample<0 || sampleCount<0 || sampleCount>32768) {throw new ArgumentOutOfRangeException(nameof(sampleCount));}
        var format=new AudioFormat(document.Audio.SampleRate,document.Audio.Channels);var clock=new CompositionClock(document.FrameRate,format.SampleRate,document.Duration);
        int count=(int)Math.Min(sampleCount,Math.Max(0,clock.SampleCount-firstSample));var accumulated=new double[checked(count*format.Channels)];
        if(document.Audio.Enabled) {
            foreach(var clip in document.AudioClips) {
                if(clip.Muted || clip.Gain==0) {continue;}
                if(!double.IsFinite(clip.Gain) || clip.Gain<0 || clip.End<=clip.Start || clip.SourceIn<MediaTime.Zero || clip.SourceOut-clip.SourceIn<clip.End-clip.Start || clip.Envelopes==null || clip.Envelopes.Any(envelope=>envelope==null || !envelope.IsValid(clip.End-clip.Start))) {throw new InvalidDataException("Audio clip or envelope is unresolved or invalid.");}
                long start=Math.Max(firstSample,(clip.Start*new MediaTime(format.SampleRate,1)).Ceiling());long end=Math.Min(checked(firstSample+count),(clip.End*new MediaTime(format.SampleRate,1)).Ceiling());if(end<=start) {continue;}
                var reference=document.Media.Single(media=>media.Id==clip.MediaId);string key=JsonSerializer.Serialize(reference);
                if(!Sources.TryGetValue(key,out var source)) {source=Resolver.Open(reference);Sources.Add(key,source);}
                var exactSource=(new MediaTime(start,format.SampleRate)-clip.Start+clip.SourceIn)*new MediaTime(format.SampleRate,1);long sourceIndex=exactSource.Numerator/exactSource.Denominator;double fraction=(double)(exactSource.Numerator%exactSource.Denominator)/exactSource.Denominator;
                int frames=checked((int)(end-start));var block=source.ReadAudio(sourceIndex,frames+(fraction>0?1:0),format);var samples=block.Samples.Span;
                for(int index=0;index<frames;index++) {
                    long position=start+index;var local=new MediaTime(position,format.SampleRate)-clip.Start;double gain=clip.Gain;foreach(var envelope in clip.Envelopes) {gain*=envelope.At(local);}
                    for(int channel=0;channel<format.Channels;channel++) {
                        double sample=samples[index*format.Channels+channel];if(fraction>0) {sample=sample*(1-fraction)+samples[(index+1)*format.Channels+channel]*fraction;}
                        if(!double.IsFinite(sample)) {throw new InvalidDataException("Audio source contains non-finite PCM.");}
                        accumulated[checked((int)(position-firstSample))*format.Channels+channel]+=sample*gain;
                    }
                }
            }
        }
        var output=new float[accumulated.Length];double peak=0;for(int index=0;index<output.Length;index++) {output[index]=(float)accumulated[index];peak=Math.Max(peak,Math.Abs(accumulated[index]));}
        return new(new(firstSample,output,format,peak==0),new(peak));
    }
    /// <summary>Releases all cursors while leaving the caller-owned resolver available.</summary>
    public void Dispose() {if(Disposed) {return;}Disposed=true;foreach(var source in Sources.Values) {source.Dispose();}Sources.Clear();}
}
