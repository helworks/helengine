using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
namespace helengine.recording.windows.tests;
/// <summary>Paints fixed color bands and an animated marker on an exclusively test-owned borderless window.</summary>
sealed class RecordingTestForm : Form {
    /// <summary>Animates only fixture content without accessing other windows.</summary>
    readonly System.Windows.Forms.Timer Timer = new() { Interval = 16 };
    /// <summary>Marker time is independent from the recording output clock.</summary>
    readonly Stopwatch Clock = Stopwatch.StartNew();
    /// <summary>Creates a physical-pixel source whose dimensions match the encoded assertion.</summary>
    internal RecordingTestForm(int width, int height) {
        AutoScaleMode = AutoScaleMode.None; FormBorderStyle = FormBorderStyle.None; ClientSize = new(width, height); StartPosition = FormStartPosition.Manual; Location = new(80, 80);
        Text = "Helengine recording synthetic fixture"; ShowInTaskbar = false; DoubleBuffered = true;
        Timer.Tick += (_, _) => Invalidate(); Timer.Start();
    }
    /// <summary>Prevents the fixture from taking keyboard focus away from Helena.</summary>
    protected override bool ShowWithoutActivation => true;
    /// <summary>Produces known RGB colors and motion which stale-texture reuse would lose.</summary>
    protected override void OnPaint(PaintEventArgs args) {
        int width = ClientSize.Width; int height = ClientSize.Height;
        args.Graphics.FillRectangle(Brushes.Red, 0, 0, width / 3, height);
        args.Graphics.FillRectangle(Brushes.Lime, width / 3, 0, width / 3, height);
        args.Graphics.FillRectangle(Brushes.Blue, width * 2 / 3, 0, width - width * 2 / 3, height);
        int position = (int)(Clock.Elapsed.TotalSeconds * 90) % Math.Max(1, width - 16);
        args.Graphics.FillRectangle(Brushes.White, position, height / 2, 16, height / 2); base.OnPaint(args);
    }
    /// <summary>Stops message-loop-owned animation before releasing the form.</summary>
    protected override void Dispose(bool disposing) { if (disposing) { Timer.Dispose(); } base.Dispose(disposing); }
}
