namespace helengine.vfx.captions.tests;

/// <summary>Requests cancellation immediately after the first exported frame is reported.</summary>
sealed class CaptionCancelProgress : IProgress<int> {
    /// <summary>Cancellation source owned by the test.</summary>
    readonly CancellationTokenSource Cancellation;

    /// <summary>Retains the cancellation source without owning its lifetime.</summary>
    public CaptionCancelProgress(CancellationTokenSource cancellation) => Cancellation = cancellation;

    /// <summary>Cancels synchronously during a frame completion callback.</summary>
    public void Report(int value) => Cancellation.Cancel();
}
