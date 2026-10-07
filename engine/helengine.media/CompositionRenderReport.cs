namespace helengine.media;
/// <summary>Receipt for one immutable render snapshot, emitted only after successful encoding.</summary>
public sealed class CompositionRenderReport {
    /// <summary>Receipt schema version.</summary>
    public string Schema {get;set;}="helengine.media.render-report.v1";
    /// <summary>SHA256 of composition JSON, profile and output dimensions.</summary>
    public string Fingerprint {get;set;}="";
    /// <summary>Exact composition identity.</summary>
    public string CompositionId {get;set;}="";
    /// <summary>Composition snapshot revision.</summary>
    public int Revision {get;set;}
    /// <summary>Exact output profile version.</summary>
    public string Profile {get;set;}="";
    /// <summary>Number of composed frames supplied before encoding.</summary>
    public long FrameCount {get;set;}
    /// <summary>Number of mixed sample frames supplied before codec padding.</summary>
    public long SampleCount {get;set;}
    /// <summary>Whether a PCM stream was encoded.</summary>
    public bool HasAudio {get;set;}
    /// <summary>Peak mixed PCM magnitude before codec conversion, without normalization.</summary>
    public double AudioPeak {get;set;}
    /// <summary>Output width.</summary>
    public int Width {get;set;}
    /// <summary>Output height.</summary>
    public int Height {get;set;}
}
