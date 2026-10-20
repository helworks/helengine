namespace helengine.tools.windowrecorder;
/// <summary>Executable entry point forwarding Ctrl+C to the recorder's graceful completion path.</summary>
static class Program {
    /// <summary>Runs the CLI without terminating encoding when a stop request arrives.</summary>
    static async Task<int> Main(string[] args) {
        using var stop = new CancellationTokenSource(); Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; stop.Cancel(); };
        try { return await new RecorderApplication().RunAsync(args, Console.Out, Console.Error, stop.Token).ConfigureAwait(false); }
        catch (Exception error) { await RecorderApplication.WriteErrorAsync(Console.Out, Console.Error, error).ConfigureAwait(false); return 3; }
    }
}
