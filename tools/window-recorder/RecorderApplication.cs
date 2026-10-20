using System.Text.Json;
using System.Text.Json.Serialization;
using helengine.recording.windows;
namespace helengine.tools.windowrecorder;
/// <summary>Provides one JSON result per command and keeps diagnostics off automation stdout.</summary>
sealed class RecorderApplication {
    /// <summary>Actual recording operation, with graceful-stop semantics.</summary>
    readonly Func<WindowRecordingOptions, CancellationToken, Task<WindowRecordingResult>> Record;
    /// <summary>Metadata-only window enumeration.</summary>
    readonly Func<IReadOnlyList<WindowCatalogEntry>> ListWindows;
    /// <summary>Stable automation serialization with readable string stop reasons.</summary>
    static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    /// <summary>Constructs the native CLI host.</summary>
    internal RecorderApplication() : this(new WindowRecorder().RecordAsync, WindowCatalog.List) { }
    /// <summary>Supplies external operations for stream/argument/lifecycle tests.</summary>
    internal RecorderApplication(Func<WindowRecordingOptions, CancellationToken, Task<WindowRecordingResult>> record, Func<IReadOnlyList<WindowCatalogEntry>> listWindows) { Record = record; ListWindows = listWindows; }
    /// <summary>Runs an entire command, awaiting graceful encoding completion before returning.</summary>
    internal async Task<int> RunAsync(string[] args, TextWriter stdout, TextWriter stderr, CancellationToken stopToken) {
        try {
            var command = RecorderCommandLine.Parse(args);
            if (command.IsWindowList) { await stdout.WriteLineAsync(JsonSerializer.Serialize(ListWindows(), JsonOptions)).ConfigureAwait(false); return 0; }
            var result = await Record(command.Options, stopToken).ConfigureAwait(false);
            await stdout.WriteLineAsync(JsonSerializer.Serialize(result, JsonOptions)).ConfigureAwait(false);
            if (!result.Succeeded) { await stderr.WriteLineAsync(result.ErrorMessage ?? result.StopReason.ToString()).ConfigureAwait(false); }
            return result.Succeeded ? 0 : 3;
        } catch (ArgumentException error) { await WriteErrorAsync(stdout, stderr, error).ConfigureAwait(false); return 2; }
        catch (Exception error) { await WriteErrorAsync(stdout, stderr, error).ConfigureAwait(false); return 3; }
    }
    /// <summary>Reports startup/runtime failures without producing an empty successful record.</summary>
    internal static async Task WriteErrorAsync(TextWriter stdout, TextWriter stderr, Exception error) {
        await stdout.WriteLineAsync(JsonSerializer.Serialize(new { Succeeded = false, HasOutput = false, ErrorCode = error.HResult, ErrorMessage = error.Message }, JsonOptions)).ConfigureAwait(false);
        await stderr.WriteLineAsync(error.Message).ConfigureAwait(false);
    }
    /// <summary>Creates invariant camelCase JSON with explicit enum names.</summary>
    static JsonSerializerOptions CreateJsonOptions() {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)); return options;
    }
}
