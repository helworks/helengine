namespace helengine.vfx.captions;

/// <summary>An ordered transcription usable by previews and sequence exports.</summary>
public sealed class CaptionDocument {
    /// <summary>Cumulative latest end times let silence lookup stop before unrelated old cues.</summary>
    readonly double[] LatestEnds;

    /// <summary>Sorts nonempty cues by start time while retaining overlapping captions.</summary>
    public CaptionDocument(IEnumerable<CaptionCue> cues) {
        ArgumentNullException.ThrowIfNull(cues);
        CaptionCue[] ordered = cues.ToArray();
        if (ordered.Length == 0 || ordered.Any(cue => cue == null)) {
            throw new FormatException("The transcription must contain at least one non-null caption.");
        }
        Cues = Array.AsReadOnly(ordered.OrderBy(cue => cue.Start).ToArray());
        Duration = ordered.Max(cue => cue.End);
        LatestEnds = new double[Cues.Count];
        for (int index = 0; index < Cues.Count; index++) {
            LatestEnds[index] = Math.Max(Cues[index].End, index == 0 ? 0 : LatestEnds[index - 1]);
        }
    }

    /// <summary>Gets captions in stable start-time order.</summary>
    public IReadOnlyList<CaptionCue> Cues { get; }
    /// <summary>Gets the end of the last caption, including leading silence.</summary>
    public double Duration { get; }

    /// <summary>Returns the latest-starting active cue, or null during silence.</summary>
    public CaptionCue FindCue(double time) {
        if (!double.IsFinite(time) || time < 0) {
            throw new ArgumentOutOfRangeException(nameof(time));
        }
        int low = 0;
        int high = Cues.Count - 1;
        while (low <= high) {
            int middle = low + (high - low) / 2;
            if (Cues[middle].Start <= time) {
                low = middle + 1;
            } else {
                high = middle - 1;
            }
        }
        for (int index = high; index >= 0; index--) {
            if (LatestEnds[index] <= time) {
                break;
            }
            if (time < Cues[index].End) {
                return Cues[index];
            }
        }
        return null;
    }
}
