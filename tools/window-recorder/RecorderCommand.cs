using helengine.recording.windows;
namespace helengine.tools.windowrecorder;
/// <summary>One validated metadata-list or explicit-window-record command.</summary>
sealed class RecorderCommand {
    /// <summary>Whether to enumerate metadata without starting capture.</summary>
    internal bool IsWindowList { get; init; }
    /// <summary>Validated recording arguments, absent for metadata listing.</summary>
    internal WindowRecordingOptions Options { get; init; }
}
