using System.Drawing;
using System.Windows.Forms;

namespace helengine.capture.windows.tests;
/// <summary>Draws deterministic synthetic pixels without activating or capturing another application.</summary>
sealed class SyntheticCaptureForm : Form {
    /// <summary>Changes a small marker so explicitly requested updates produce new source frames.</summary>
    bool Phase;
    /// <summary>Creates a plain test-owned window at a fixed size, without a taskbar entry.</summary>
    public SyntheticCaptureForm() {
        Text = "Helengine synthetic capture test"; ClientSize = new(320, 180);
        StartPosition = FormStartPosition.Manual; Location = new(100, 100); ShowInTaskbar = false;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }
    /// <summary>Showing the fixture does not steal keyboard focus from the user's application.</summary>
    protected override bool ShowWithoutActivation => true;
    /// <summary>Requests a real paint update while leaving the central color stripes unchanged.</summary>
    public void UpdateFrame() { Phase = !Phase; Invalidate(); Update(); }
    /// <summary>Paints BGRA-verifiable primary colors and a small changing marker.</summary>
    protected override void OnPaint(PaintEventArgs args) {
        int third = ClientSize.Width / 3;
        args.Graphics.FillRectangle(Brushes.Red, 0, 0, third, ClientSize.Height);
        args.Graphics.FillRectangle(Brushes.Lime, third, 0, third, ClientSize.Height);
        args.Graphics.FillRectangle(Brushes.Blue, third * 2, 0, ClientSize.Width - third * 2, ClientSize.Height);
        args.Graphics.FillRectangle(Phase ? Brushes.Black : Brushes.White, 0, 0, 4, 4);
    }
}
