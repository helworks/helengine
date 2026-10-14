namespace helengine.media;
/// <summary>Mixes voice, clip audio, music and effects through one sample-accurate timeline.</summary>
public sealed class CompositionAudioMixer : IDisposable {
    /// <summary>Source factory borrowed from the render session.</summary>
    readonly IMediaSourceResolver Resolver;
    /// <summary>Independent audio source cursors keyed by pinned media metadata.</summary>
    readonly Dictionary<string,IMediaSource> Sources=new(StringComparer.Ordinal);
    /// <summary>Pitch-preserving stretches of whole clip source spans, keyed by source, span, rate and format, so every block of a rate-changed clip indexes the same deterministic result whatever the block size.</summary>
    readonly Dictionary<string,Stretched> StretchCache=new(StringComparer.Ordinal);
    /// <summary>Largest source read issued at once while filling a stretch.</summary>
    const int ReadChunk=32768;
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
                if(!double.IsFinite(clip.Gain) || clip.Gain<0 || clip.End<=clip.Start || clip.SourceIn<MediaTime.Zero || !MediaTime.IsValidRate(clip.Rate) || clip.SourceOut-clip.SourceIn<(clip.End-clip.Start).AtRate(clip.Rate) || clip.Envelopes==null || clip.Envelopes.Any(envelope=>envelope==null || !envelope.IsValid(clip.End-clip.Start))) {throw new InvalidDataException("Audio clip or envelope is unresolved or invalid.");}
                long start=Math.Max(firstSample,(clip.Start*new MediaTime(format.SampleRate,1)).Ceiling());long end=Math.Min(checked(firstSample+count),(clip.End*new MediaTime(format.SampleRate,1)).Ceiling());if(end<=start) {continue;}
                var reference=document.Media.Single(media=>media.Id==clip.MediaId);string key=JsonSerializer.Serialize(reference);
                if(!Sources.TryGetValue(key,out var source)) {source=Resolver.Open(reference);Sources.Add(key,source);}
                if(clip.Rate!=1) {MixStretched(clip,source,key,format,start,end,firstSample,accumulated);continue;}
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
    /// <summary>Adds a rate-changed clip: its whole source span is stretched once with the pitch preserved and cached, then indexed at the exact local time of every output sample.</summary>
    void MixStretched(AudioClip clip,IMediaSource source,string key,AudioFormat format,long start,long end,long firstSample,double[] accumulated) {
        var rate=new MediaTime(format.SampleRate,1);long sourceFirst=(clip.SourceIn*rate).Numerator/(clip.SourceIn*rate).Denominator;long sourceEnd=(clip.SourceOut*rate).Ceiling();
        string cacheKey=$"{key}|{sourceFirst}|{sourceEnd}|{clip.Rate.ToString("R",System.Globalization.CultureInfo.InvariantCulture)}|{format.SampleRate}|{format.Channels}";
        if(!StretchCache.TryGetValue(cacheKey,out var stretched)) {
            int frames=checked((int)(sourceEnd-sourceFirst));var pcm=new float[checked(frames*format.Channels)];
            for(int offset=0;offset<frames;offset+=ReadChunk) {int count=Math.Min(ReadChunk,frames-offset);var block=source.ReadAudio(sourceFirst+offset,count,format).Samples.Span;block[..Math.Min(block.Length,count*format.Channels)].CopyTo(pcm.AsSpan(offset*format.Channels));}
            foreach(float sample in pcm) {if(!float.IsFinite(sample)) {throw new InvalidDataException("Audio source contains non-finite PCM.");}}
            stretched=new(AudioTimeStretch.Stretch(pcm,format.Channels,format.SampleRate,clip.Rate),(clip.SourceIn*rate-new MediaTime(sourceFirst,1)).ToSeconds()/clip.Rate);StretchCache.Add(cacheKey,stretched);
        }
        int channels=format.Channels;int length=stretched.Samples.Length/channels;
        for(long position=start;position<end;position++) {
            var local=new MediaTime(position,format.SampleRate)-clip.Start;double gain=clip.Gain;foreach(var envelope in clip.Envelopes) {gain*=envelope.At(local);}
            double index=stretched.Lead+(local*rate).ToSeconds();long whole=(long)Math.Floor(index);double fraction=index-whole;
            for(int channel=0;channel<channels;channel++) {
                double first=whole>=0 && whole<length?stretched.Samples[whole*channels+channel]:0;double second=whole+1>=0 && whole+1<length?stretched.Samples[(whole+1)*channels+channel]:0;
                accumulated[checked((int)(position-firstSample))*channels+channel]+=(first+(second-first)*fraction)*gain;
            }
        }
    }
    /// <summary>A cached stretch: interleaved output PCM and the output-frame offset of the clip's exact source start inside it.</summary>
    sealed record Stretched(float[] Samples,double Lead);
    /// <summary>Releases all cursors while leaving the caller-owned resolver available.</summary>
    public void Dispose() {if(Disposed) {return;}Disposed=true;foreach(var source in Sources.Values) {source.Dispose();}Sources.Clear();StretchCache.Clear();}
}
