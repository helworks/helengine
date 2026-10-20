using System.Diagnostics;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;

namespace helengine.capture.windows.tests;

/// <summary>Captures exclusively windows owned by these tests, never user application windows or the desktop.</summary>
[Collection("Window capture")]
[Trait("Category", "Integration")]
public sealed class WindowCaptureIntegrationTests {
    /// <summary>Verifies BGRA channel order and a native-width painted stripe without a scaling step.</summary>
    [Fact]
    public void CapturesKnownBgraPatternWithoutScaling() {
        using var window = new SyntheticCaptureWindow(); using var session = WindowCaptureSession.Open(window.Handle, new());
        using var frame = Acquire(session, window); var bytes = frame.CopyPixels();
        Assert.Equal(WindowCapturePixelFormat.Bgra8, frame.Format); Assert.Equal(frame.Width * frame.Height * 4, bytes.Length);
        AssertPixel(bytes, frame.Width, frame.Width / 6, frame.Height / 2, 0, 0, 255);
        AssertPixel(bytes, frame.Width, frame.Width / 2, frame.Height / 2, 0, 255, 0);
        AssertPixel(bytes, frame.Width, frame.Width * 5 / 6, frame.Height / 2, 255, 0, 0);
        int redPixels = 0;
        for (int x = 0; x < frame.Width; x++) {
            int offset = (frame.Height / 2 * frame.Width + x) * 4;
            if (bytes[offset] == 0 && bytes[offset + 1] == 0 && bytes[offset + 2] == 255) { redPixels++; }
        }
        Assert.Equal(window.ClientWidth / 3, redPixels);
    }
    /// <summary>Captured timing comes from the system QPC clock and sequence numbers cannot repeat.</summary>
    [Fact]
    public void DeliversNewSequencesAndSourceClock() {
        using var window = new SyntheticCaptureWindow(); using var session = WindowCaptureSession.Open(window.Handle, new());
        long before = Stopwatch.GetTimestamp(); using var first = Acquire(session, window); window.Update(); using var second = Acquire(session, window);
        Assert.True(second.SequenceNumber > first.SequenceNumber); Assert.True(second.Timestamp >= first.Timestamp);
        double nowSeconds = (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
        Assert.InRange(first.Timestamp.TotalSeconds, (double)before / Stopwatch.Frequency - 1, nowSeconds + 1);
    }
    /// <summary>Repeated size changes do not mutate a consumer-held texture or allocate beyond its capacity ledger.</summary>
    [Fact]
    public void ResizeKeepsHeldFramePixelsStable() {
        using var window = new SyntheticCaptureWindow(); using var session = WindowCaptureSession.Open(window.Handle, new());
        using var held = Acquire(session, window); byte[] original = held.CopyPixels(); int originalWidth = held.Width;
        foreach (int width in new[] { 420, 260, 500 }) {
            window.Resize(width, 220);
            using var resized = Acquire(session, window, frame => frame.Width != originalWidth);
            Assert.Equal(original, held.CopyPixels());
        }
    }
    /// <summary>A held frame occupies capacity; discarded incoming frames are counted and release permits recovery.</summary>
    [Fact]
    public void BudgetOverflowIsReportedAndRecovers() {
        using var window = new SyntheticCaptureWindow(); using var session = WindowCaptureSession.Open(window.Handle, new() { BufferCapacity = 1 });
        var held = Acquire(session, window); ulong sequence = held.SequenceNumber;
        try {
            var deadline = Stopwatch.StartNew();
            while (session.Statistics.OverflowDiscardedFrames == 0 && deadline.Elapsed < TimeSpan.FromSeconds(10)) { window.Update(); Thread.Sleep(20); }
            Assert.True(session.Statistics.OverflowDiscardedFrames > 0); Assert.False(session.TryAcquireFrame(out var absent)); Assert.Null(absent);
        } finally { held.Dispose(); }
        using var next = Acquire(session, window); Assert.True(next.SequenceNumber > sequence);
    }
    /// <summary>Both close orders preserve the delivered frame and make subsequent acquisition terminate explicitly.</summary>
    [Theory][InlineData(true)][InlineData(false)]
    public void WindowCloseAndSessionDisposePreserveHeldFrame(bool closeWindowFirst) {
        using var window = new SyntheticCaptureWindow(); var session = WindowCaptureSession.Open(window.Handle, new());
        using var held = Acquire(session, window); var original = held.CopyPixels();
        try {
            if (closeWindowFirst) {
                window.Dispose(); Assert.False(SyntheticCaptureWindow.IsWindowAlive(window.Handle)); var deadline = Stopwatch.StartNew();
                while (session.State == WindowCaptureState.Running && deadline.Elapsed < TimeSpan.FromSeconds(10)) { Thread.Sleep(10); }
                Assert.Equal(WindowCaptureState.WindowClosed, session.State);
                Assert.Throws<WindowCaptureClosedException>(() => session.TryAcquireFrame(out _));
            }
            session.Dispose(); session.Dispose(); Assert.Equal(WindowCaptureState.Disposed, session.State);
            Assert.Equal(original, held.CopyPixels()); Assert.Throws<ObjectDisposedException>(() => session.TryAcquireFrame(out _));
            held.Dispose(); held.Dispose(); Assert.Throws<ObjectDisposedException>(() => held.CopyPixels());
            Assert.Throws<ObjectDisposedException>(() => held.TextureHandle);
        } finally { session.Dispose(); }
    }
    /// <summary>The imported device remains owned and usable by its original caller after capture disposal.</summary>
    [Fact]
    public void ExternalDeviceReferenceRemainsOwnedByCaller() {
        using var window = new SyntheticCaptureWindow(); using var device = new Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport);
        var session = WindowCaptureSession.Open(window.Handle, device.NativePointer, new());
        using var held = Acquire(session, window); session.Dispose(); Assert.NotEmpty(held.CopyPixels());
        device.ImmediateContext.Flush(); Assert.Equal(device.NativePointer, held.DeviceHandle);
    }
    /// <summary>Repeated polling never returns an already transferred sequence as a newly captured frame.</summary>
    [Fact]
    public void NoUpdatesNeverReturnPreviousLeaseAgain() {
        using var window = new SyntheticCaptureWindow(); using var session = WindowCaptureSession.Open(window.Handle, new());
        using var first = Acquire(session, window); var sequences = new HashSet<ulong> { first.SequenceNumber };
        for (int index = 0; index < 20; index++) {
            if (session.TryAcquireFrame(out var next)) { using (next) { Assert.True(sequences.Add(next.SequenceNumber)); } }
            else { Assert.Null(next); }
            Thread.Sleep(10);
        }
    }
    /// <summary>Waits a bounded time while explicitly updating only the test-owned source window.</summary>
    static CapturedWindowFrame Acquire(WindowCaptureSession session, SyntheticCaptureWindow window, Func<CapturedWindowFrame, bool> predicate = null) {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < TimeSpan.FromSeconds(10)) {
            window.Update();
            if (session.TryAcquireFrame(out var frame)) {
                if (predicate == null || predicate(frame)) { return frame; }
                frame.Dispose();
            }
            Thread.Sleep(20);
        }
        throw new TimeoutException($"Synthetic window did not produce a matching captured frame; state={session.State}.");
    }
    /// <summary>Checks an interior pixel using explicit BGRA component expectations.</summary>
    static void AssertPixel(byte[] bytes, int width, int x, int y, byte blue, byte green, byte red) {
        int offset = (y * width + x) * 4;
        Assert.Equal(blue, bytes[offset]); Assert.Equal(green, bytes[offset + 1]); Assert.Equal(red, bytes[offset + 2]);
    }
}
