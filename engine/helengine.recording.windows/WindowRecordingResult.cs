using helengine.capture.windows;
namespace helengine.recording.windows;
/// <summary>Reports actual recording behavior, including failures which produced a valid shortened clip.</summary>
public sealed class WindowRecordingResult {
    /// <summary>Whether this session completed and owns the reported destination.</summary>
    public bool HasOutput { get; internal set; }
    /// <summary>Completed destination, null when no valid output was published.</summary>
    public string OutputPath { get; internal set; }
    /// <summary>Normal stop or precise failure category.</summary>
    public WindowRecordingStopReason StopReason { get; internal set; } = WindowRecordingStopReason.NoFrames;
    /// <summary>True only for normal termination with a completed output file.</summary>
    public bool Succeeded => HasOutput && StopReason is WindowRecordingStopReason.DurationReached or WindowRecordingStopReason.StopRequested or WindowRecordingStopReason.WindowClosed;
    /// <summary>Original HRESULT/libav code where available, otherwise the managed operation code.</summary>
    public int ErrorCode { get; internal set; }
    /// <summary>Primary failure, never replaced by a teardown error.</summary>
    public string ErrorMessage { get; internal set; }
    /// <summary>Secondary teardown errors, bounded by the number of owned components.</summary>
    public List<string> CleanupErrors { get; } = new();
    /// <summary>Duration requested by the caller, excluding preparation.</summary>
    public TimeSpan? RequestedDuration { get; internal set; }
    /// <summary>Measured time between recording origin and stop, excluding drain/publication.</summary>
    public TimeSpan RecordingDuration { get; internal set; }
    /// <summary>Exact encoded slot count represented to 100 ns precision.</summary>
    public TimeSpan EncodedDuration => TimeSpan.FromTicks(checked((long)((Int128)EncodedFrames * TimeSpan.TicksPerSecond / 60)));
    /// <summary>Original content width.</summary>
    public int SourceWidth { get; internal set; }
    /// <summary>Original content height.</summary>
    public int SourceHeight { get; internal set; }
    /// <summary>Even encoded width including black padding.</summary>
    public int OutputWidth { get; internal set; }
    /// <summary>Even encoded height including black padding.</summary>
    public int OutputHeight { get; internal set; }
    /// <summary>Encoded video codec.</summary>
    public string Codec => "hevc";
    /// <summary>Required hardware implementation.</summary>
    public string Encoder => "hevc_nvenc";
    /// <summary>Actual GPU adapter name.</summary>
    public string AdapterName { get; internal set; }
    /// <summary>Actual adapter LUID.</summary>
    public long AdapterLuid { get; internal set; }
    /// <summary>Constant output frame-rate numerator.</summary>
    public int FpsNumerator => 60;
    /// <summary>Constant output frame-rate denominator.</summary>
    public int FpsDenominator => 1;
    /// <summary>Configured target bit rate.</summary>
    public long Bitrate { get; internal set; }
    /// <summary>Configured maximum bit rate.</summary>
    public long MaxBitrate { get; internal set; }
    /// <summary>Output images accepted by the encoder.</summary>
    public long EncodedFrames { get; internal set; }
    /// <summary>Output slots reusing a previous source observation.</summary>
    public long RepeatedFrames { get; internal set; }
    /// <summary>Intermediate source observations superseded by temporal selection.</summary>
    public long ResampledSourceDiscards { get; internal set; }
    /// <summary>Older observations discarded while refreshing content after codec setup.</summary>
    public long PreparationDiscardedFrames { get; internal set; }
    /// <summary>Known capture-side counters; cannot describe observations Windows never delivered.</summary>
    public WindowCaptureStatistics CaptureStatistics { get; internal set; }
    /// <summary>Maximum measured output lateness, distinct from repeated static content.</summary>
    public TimeSpan MaxLag { get; internal set; }
}
