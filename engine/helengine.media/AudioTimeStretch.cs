namespace helengine.media;
/// <summary>Changes the duration of PCM without changing its pitch by waveform-similarity overlap-add (WSOLA): Hann-windowed frames of about 30 ms are laid down every half frame, each read near its nominal stretched input position at the offset (within about 10 ms) that best continues the previous frame's waveform. Pure and deterministic, so the same input always produces the same output.</summary>
public static class AudioTimeStretch {
    /// <summary>Frame length in seconds.</summary>
    const double FrameSeconds=0.030;
    /// <summary>Largest search offset around the nominal input position, in seconds.</summary>
    const double ToleranceSeconds=0.010;
    /// <summary>Decimation factor of the coarse similarity search.</summary>
    const int Coarse=4;
    /// <summary>Stretches interleaved PCM so it plays <paramref name="rate"/> times faster with the same pitch.</summary>
    /// <param name="input">Interleaved PCM frames.</param>
    /// <param name="channels">Channels per frame.</param>
    /// <param name="sampleRate">Frames per second, used to size the analysis frames.</param>
    /// <param name="rate">Source seconds consumed per output second; above 1 shortens the audio.</param>
    /// <returns>Interleaved PCM of <c>round(frames / rate)</c> frames.</returns>
    public static float[] Stretch(ReadOnlySpan<float> input,int channels,int sampleRate,double rate) {
        if(channels<1 || input.Length%channels!=0) {throw new ArgumentOutOfRangeException(nameof(channels));}
        if(sampleRate<1000) {throw new ArgumentOutOfRangeException(nameof(sampleRate));}
        if(!MediaTime.IsValidRate(rate)) {throw new ArgumentOutOfRangeException(nameof(rate));}
        int frames=input.Length/channels;int outFrames=(int)Math.Round(frames/rate);var output=new float[checked(outFrames*channels)];
        if(frames==0 || outFrames==0) {return output;}
        int hop=Math.Max(Coarse,(int)Math.Round(FrameSeconds*sampleRate/2/Coarse)*Coarse);int length=hop*2;int tolerance=Math.Max(Coarse,(int)Math.Round(ToleranceSeconds*sampleRate/Coarse)*Coarse);
        // Periodic Hann: two windows half a frame apart sum to exactly one, so steady overlap-add needs no normalization.
        var window=new double[length];for(int index=0;index<length;index++) {window[index]=0.5-0.5*Math.Cos(2*Math.PI*index/length);}
        int pad=length+tolerance*2+Coarse*2;var mono=new double[frames+pad*2];
        for(int frame=0;frame<frames;frame++) {double sum=0;for(int channel=0;channel<channels;channel++) {sum+=input[frame*channels+channel];}mono[pad+frame]=sum/channels;}
        var coarse=new double[mono.Length/Coarse];for(int index=0;index<coarse.Length;index++) {double sum=0;for(int offset=0;offset<Coarse;offset++) {sum+=mono[index*Coarse+offset];}coarse[index]=sum/Coarse;}
        var accumulated=new double[(outFrames+length)*channels];
        // Frame 0 starts half a frame before the output so its falling half and frame 1's rising half rebuild the first samples exactly.
        long previous=-hop;
        for(long frame=0;;frame++) {
            long outStart=(frame-1)*hop;if(outStart>=outFrames) {break;}
            long position=frame==0?-hop:Best(mono,coarse,pad,Math.Max(0,(long)Math.Round(outStart*rate)),previous+hop,length,tolerance,frames);
            for(int index=0;index<length;index++) {
                long target=outStart+index;long source=position+index;if(target<0 || target>=outFrames || source<0 || source>=frames) {continue;}
                for(int channel=0;channel<channels;channel++) {accumulated[target*channels+channel]+=input[(int)source*channels+channel]*window[index];}
            }
            previous=position;
        }
        for(int index=0;index<output.Length;index++) {output[index]=(float)accumulated[index];}
        return output;
    }
    /// <summary>Finds the input position near <paramref name="nominal"/> whose frame best matches the natural continuation of the previous frame: a coarse normalized cross-correlation on a decimated signal, refined at full resolution.</summary>
    static long Best(double[] mono,double[] coarse,int pad,long nominal,long natural,int length,int tolerance,int frames) {
        long low=Math.Max(0,nominal-tolerance);long high=Math.Min(Math.Max(low,frames-1),nominal+tolerance);if(natural>=frames) {return nominal;}
        long best=nominal;double score=double.NegativeInfinity;
        for(long candidate=low/Coarse*Coarse;candidate<=high;candidate+=Coarse) {double value=Similarity(coarse,(pad+candidate)/Coarse,(pad+natural)/Coarse,length/Coarse);if(value>score) {score=value;best=candidate;}}
        long center=best;score=double.NegativeInfinity;
        for(long candidate=Math.Max(low,center-Coarse+1);candidate<=Math.Min(high,center+Coarse-1);candidate++) {double value=Similarity(mono,pad+candidate,pad+natural,length);if(value>score) {score=value;best=candidate;}}
        return best;
    }
    /// <summary>Correlation of a candidate frame with the reference frame, normalized by the candidate energy so loud passages are not favored.</summary>
    static double Similarity(double[] signal,long candidate,long reference,int length) {
        double dot=0,energy=1e-12;int count=(int)Math.Min(length,Math.Min(signal.Length-candidate,signal.Length-reference));
        for(int index=0;index<count;index++) {double value=signal[candidate+index];dot+=value*signal[reference+index];energy+=value*value;}
        return dot/Math.Sqrt(energy);
    }
}
