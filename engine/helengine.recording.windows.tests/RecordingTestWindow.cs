using System.Windows.Forms;
namespace helengine.recording.windows.tests;
/// <summary>Owns a dedicated STA, message loop and synthetic recording window.</summary>
sealed class RecordingTestWindow : IDisposable {
    /// <summary>Publishes only a visible, initialized test source.</summary>
    readonly TaskCompletionSource<RecordingTestForm> Ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>Lifetime of the fixture's message loop.</summary>
    readonly Thread Thread;
    /// <summary>Owned form which never represents a user application.</summary>
    readonly RecordingTestForm Form;
    /// <summary>Requested source width.</summary>
    readonly int Width;
    /// <summary>Requested source height.</summary>
    readonly int Height;
    /// <summary>Prevents repeated fixture shutdown.</summary>
    bool Disposed;
    /// <summary>Starts a nonactivating borderless source with a bounded startup wait.</summary>
    internal RecordingTestWindow(int width = 640, int height = 480) {
        Width = width; Height = height; Thread = new(Run) { IsBackground = true, Name = "Recording synthetic window" }; Thread.SetApartmentState(ApartmentState.STA); Thread.Start();
        Form = Ready.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult(); Handle = Form.Handle;
    }
    /// <summary>Explicit fixture-only HWND supplied to the recorder and CLI.</summary>
    internal IntPtr Handle { get; }
    /// <summary>Changes only the test source dimensions on its owner thread.</summary>
    internal void Resize(int width, int height) => Form.Invoke(() => { Form.ClientSize = new(width, height); Form.Refresh(); });
    /// <summary>Creates and tears down the synthetic form on the same STA.</summary>
    void Run() {
        try {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); using var form = new RecordingTestForm(Width, Height); form.Shown += (_, _) => Ready.TrySetResult(form); Application.Run(form);
        } catch (Exception error) { Ready.TrySetException(error); }
    }
    /// <summary>Closes only the fixture window and joins its thread without leaving a background form.</summary>
    public void Dispose() {
        if (Disposed) { return; } Disposed = true; Form.Invoke(Form.Close);
        if (!Thread.Join(TimeSpan.FromSeconds(10))) { throw new TimeoutException("Recording test window did not stop."); }
    }
}
