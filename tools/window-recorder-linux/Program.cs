using System.Text.Json;

namespace helengine.tools.windowrecorderlinux;

/// <summary>Runs an explicitly scoped Linux window recording and cancels only resources owned by this invocation.</summary>
static class Program {
    /// <summary>Returns success only after the encoder has independently decoded and published the complete MP4.</summary>
    static async Task<int> Main(string[] args) {
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
        try {
            return await new RecorderApplication().RunAsync(args, Console.Out, Console.Error, cancellation.Token);
        } catch (OperationCanceledException) {
            await Console.Error.WriteLineAsync(JsonSerializer.Serialize(new { status = "cancelled", message = "Recording cancelled; no unverified output is published." }));
            return 130;
        } catch (Exception error) {
            await Console.Error.WriteLineAsync(JsonSerializer.Serialize(new { status = "failed", message = error.Message }));
            return 1;
        }
    }
}
