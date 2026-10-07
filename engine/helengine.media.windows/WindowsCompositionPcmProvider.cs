using NAudio.Wave;
namespace helengine.media.windows;
/// <summary>Pulls one bounded mixed stream into NAudio without maintaining per-track devices.</summary>
public sealed class WindowsCompositionPcmProvider : IWaveProvider {
    /// <summary>Current seek-generation reader.</summary>
    readonly Func<long,int,long,AudioBlock> Reader;
    /// <summary>Cancellation shared with the owning preview session.</summary>
    readonly CancellationToken Token;
    /// <summary>Generation associated with every read.</summary>
    readonly long Generation;
    /// <summary>Number of frames requested from this stream, separate from hardware consumption.</summary>
    long Position;
    /// <summary>Configures finite stereo float PCM for the output stream.</summary>
    public WindowsCompositionPcmProvider(AudioFormat format,long generation,Func<long,int,long,AudioBlock> reader,CancellationToken token) {WaveFormat=WaveFormat.CreateIeeeFloatWaveFormat(format.SampleRate,format.Channels);Reader=reader;Generation=generation;Token=token;}
    /// <summary>Float PCM format consumed by the single device.</summary>
    public WaveFormat WaveFormat {get;}
    /// <summary>Copies bounded blocks into the device buffer and returns zero at the timeline end.</summary>
    public int Read(byte[] buffer,int offset,int count) {
        if(Token.IsCancellationRequested) {return 0;}int alignment=WaveFormat.BlockAlign;if(count%alignment!=0) {throw new InvalidDataException("Device requested unaligned PCM.");}int written=0;
        while(written<count && !Token.IsCancellationRequested) {int frames=Math.Min(32768,(count-written)/alignment);var block=Reader(Position,frames,Generation);if(block.SampleCount==0) {break;}var target=MemoryMarshal.Cast<byte,float>(buffer.AsSpan(offset+written,block.SampleCount*alignment));block.Samples.Span.CopyTo(target);Position+=block.SampleCount;written+=block.SampleCount*alignment;if(block.SampleCount<frames) {break;}}
        return written;
    }
}
