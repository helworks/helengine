using System.Runtime.InteropServices;

namespace helengine.editor.launcher {
    /// <summary>
    /// Memory-efficient borderless form with custom resizing support
    /// Based on BaseForm but optimized for minimal memory usage
    /// </summary>
    public class ResizableForm : Form {
        // Resizing constants
        const int BorderGripSize = 6;
        const int TopBorderGripSize = 3;
        const int BorderSpace = 20;

        // Resizing state
        ResizeDirection currentDirection = ResizeDirection.None;
        bool isResizing;

        // Native Windows API for better performance
        [DllImport("user32.dll")]
        static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        const int WM_NCLBUTTONDOWN = 0xA1;
        const int HTCAPTION = 2;

        enum ResizeDirection {
            None, Right, Left, Top, Bottom,
            TopRight, TopLeft, BottomRight, BottomLeft
        }

        public ResizableForm() {
            SetStyle(ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
        }

        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e);

            if (WindowState == FormWindowState.Maximized) return;

            if (e.Button == MouseButtons.Left) {
                UpdateResizeDirection(e.Location);
                if (currentDirection != ResizeDirection.None) {
                    isResizing = true;
                    Capture = true;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e);

            if (WindowState == FormWindowState.Maximized) return;

            if (isResizing) {
                PerformResize();
            } else {
                UpdateCursor(e.Location);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e) {
            base.OnMouseUp(e);
            
            if (isResizing) {
                isResizing = false;
                currentDirection = ResizeDirection.None;
                Capture = false;
                Cursor = Cursors.Default;
            }
        }

        void UpdateCursor(Point location) {
            currentDirection = GetResizeDirection(location);
            
            Cursor = currentDirection switch {
                ResizeDirection.Right or ResizeDirection.Left => Cursors.SizeWE,
                ResizeDirection.Top or ResizeDirection.Bottom => Cursors.SizeNS,
                ResizeDirection.TopLeft or ResizeDirection.BottomRight => Cursors.SizeNWSE,
                ResizeDirection.TopRight or ResizeDirection.BottomLeft => Cursors.SizeNESW,
                _ => Cursors.Default
            };
        }

        void UpdateResizeDirection(Point location) {
            currentDirection = GetResizeDirection(location);
        }

        ResizeDirection GetResizeDirection(Point location) {
            bool onLeft = location.X <= BorderGripSize;
            bool onRight = location.X >= Width - BorderGripSize;
            bool onTop = location.Y <= TopBorderGripSize;
            bool onBottom = location.Y >= Height - BorderGripSize;

            bool inHorizontalBorder = location.Y > BorderSpace && location.Y < Height - BorderSpace;
            bool inVerticalBorder = location.X > BorderSpace && location.X < Width - BorderSpace;

            if (onLeft && inHorizontalBorder) return ResizeDirection.Left;
            if (onRight && inHorizontalBorder) return ResizeDirection.Right;
            if (onTop && inVerticalBorder) return ResizeDirection.Top;
            if (onBottom && inVerticalBorder) return ResizeDirection.Bottom;
            if (onLeft && onTop) return ResizeDirection.TopLeft;
            if (onRight && onTop) return ResizeDirection.TopRight;
            if (onLeft && onBottom) return ResizeDirection.BottomLeft;
            if (onRight && onBottom) return ResizeDirection.BottomRight;

            return ResizeDirection.None;
        }

        void PerformResize() {
            Point cursor = Cursor.Position;
            Size minimum = MinimumSize;
            
            switch (currentDirection) {
                case ResizeDirection.Right:
                    Width = Math.Max(cursor.X - Location.X, minimum.Width);
                    break;
                    
                case ResizeDirection.Left:
                    int newWidth = Math.Max((Width + Location.X) - cursor.X, minimum.Width);
                    Location = new Point(cursor.X, Location.Y);
                    Width = newWidth;
                    break;
                    
                case ResizeDirection.Top:
                    int newHeight = Math.Max((Height + Location.Y) - cursor.Y, minimum.Height);
                    Location = new Point(Location.X, cursor.Y);
                    Height = newHeight;
                    break;
                    
                case ResizeDirection.Bottom:
                    Height = Math.Max(cursor.Y - Location.Y, minimum.Height);
                    break;
                    
                case ResizeDirection.TopLeft:
                    int newWidthTL = Math.Max((Width + Location.X) - cursor.X, minimum.Width);
                    int newHeightTL = Math.Max((Height + Location.Y) - cursor.Y, minimum.Height);
                    Location = new Point(cursor.X, cursor.Y);
                    Width = newWidthTL;
                    Height = newHeightTL;
                    break;
                    
                case ResizeDirection.TopRight:
                    Width = Math.Max(cursor.X - Location.X, minimum.Width);
                    int newHeightTR = Math.Max((Height + Location.Y) - cursor.Y, minimum.Height);
                    Location = new Point(Location.X, cursor.Y);
                    Height = newHeightTR;
                    break;
                    
                case ResizeDirection.BottomLeft:
                    int newWidthBL = Math.Max((Width + Location.X) - cursor.X, minimum.Width);
                    Location = new Point(cursor.X, Location.Y);
                    Width = newWidthBL;
                    Height = Math.Max(cursor.Y - Location.Y, minimum.Height);
                    break;
                    
                case ResizeDirection.BottomRight:
                    Width = Math.Max(cursor.X - Location.X, minimum.Width);
                    Height = Math.Max(cursor.Y - Location.Y, minimum.Height);
                    break;
            }
        }

        /// <summary>
        /// Enable dragging the form by its title bar area
        /// Call this from your title bar control's MouseDown event
        /// </summary>
        public void EnableTitleBarDrag() {
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
        }
    }
}
