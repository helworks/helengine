using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace helengine.capture.windows.tests;
/// <summary>Owns a dedicated message-loop thread and a synthetic source window used only by integration tests.</summary>
sealed class SyntheticCaptureWindow : IDisposable {
    /// <summary>Publishes only after the fixture has a visible window and initialized message pump.</summary>
    readonly TaskCompletionSource<SyntheticCaptureForm> Ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>Owns the message loop until this fixture is disposed.</summary>
    readonly Thread Thread;
    /// <summary>Test-owned source, created and destroyed on its message thread.</summary>
    readonly SyntheticCaptureForm Form;
    /// <summary>Makes repeated fixture disposal safe.</summary>
    bool Disposed;
    /// <summary>Starts a nonactivating synthetic window and bounds startup to ten seconds.</summary>
    public SyntheticCaptureWindow() {
        Thread = new(Run) { IsBackground = true, Name = "Synthetic capture window" };
        Thread.SetApartmentState(ApartmentState.STA); Thread.Start();
        Form = Ready.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult(); Handle = Form.Handle;
    }
    /// <summary>Native identity belongs exclusively to this fixture.</summary>
    public IntPtr Handle { get; }
    /// <summary>Confirms that a fixture really destroyed its HWND before testing capture closure.</summary>
    [DllImport("user32.dll", EntryPoint = "IsWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowAlive(IntPtr window);
    /// <summary>Actual physical client width used to check that capture did not scale its painted stripes.</summary>
    public int ClientWidth => (int)Form.Invoke(() => Form.ClientSize.Width);
    /// <summary>Repaints only this fixture's marker on its message thread.</summary>
    public void Update() { ObjectDisposedException.ThrowIf(Disposed, this); Form.Invoke(Form.UpdateFrame); }
    /// <summary>Changes the synthetic client area and submits a synchronous paint.</summary>
    public void Resize(int width, int height) {
        ObjectDisposedException.ThrowIf(Disposed, this);
        Form.Invoke(() => { Form.ClientSize = new(width, height); Form.UpdateFrame(); });
    }
    /// <summary>Closes the source before joining its message loop, with a bounded teardown wait.</summary>
    public void Dispose() {
        if (Disposed) { return; }
        Disposed = true; Form.Invoke(Form.Close);
        if (!Thread.Join(TimeSpan.FromSeconds(10))) { throw new TimeoutException("Synthetic window message thread did not stop."); }
    }
    /// <summary>Creates and releases the fixture on one STA, preserving startup exceptions for the test.</summary>
    void Run() {
        try {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            using var form = new SyntheticCaptureForm(); form.Shown += (_, _) => Ready.TrySetResult(form);
            Application.Run(form);
        } catch (Exception error) { Ready.TrySetException(error); }
    }
}
