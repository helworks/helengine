using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
 

namespace helengine.ui.Controls {
    public class ResizeOverlay : Grid {
        private readonly int _rowSpan;
        private const int GripThickness = 12;

        public ResizeOverlay(int rowSpan) {
            _rowSpan = rowSpan;
            IsHitTestVisible = true;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;
            ZIndex = 9999;

            CreateEdge(WindowEdge.West, HorizontalAlignment.Left, VerticalAlignment.Stretch, StandardCursorType.SizeWestEast);
            CreateEdge(WindowEdge.East, HorizontalAlignment.Right, VerticalAlignment.Stretch, StandardCursorType.SizeWestEast);
            CreateEdge(WindowEdge.North, HorizontalAlignment.Stretch, VerticalAlignment.Top, StandardCursorType.SizeNorthSouth);
            CreateEdge(WindowEdge.South, HorizontalAlignment.Stretch, VerticalAlignment.Bottom, StandardCursorType.SizeNorthSouth);

            CreateCorner(WindowEdge.NorthWest, HorizontalAlignment.Left, VerticalAlignment.Top, StandardCursorType.TopLeftCorner);
            CreateCorner(WindowEdge.NorthEast, HorizontalAlignment.Right, VerticalAlignment.Top, StandardCursorType.TopRightCorner);
            CreateCorner(WindowEdge.SouthWest, HorizontalAlignment.Left, VerticalAlignment.Bottom, StandardCursorType.BottomLeftCorner);
            CreateCorner(WindowEdge.SouthEast, HorizontalAlignment.Right, VerticalAlignment.Bottom, StandardCursorType.BottomRightCorner);
        }

        private void CreateEdge(WindowEdge edge, HorizontalAlignment h, VerticalAlignment v, StandardCursorType cursorType) {
            var b = new Border {
                Background = Brushes.Transparent,
                Width = (h == HorizontalAlignment.Stretch) ? double.NaN : GripThickness,
                Height = (v == VerticalAlignment.Stretch) ? double.NaN : GripThickness,
                HorizontalAlignment = h,
                VerticalAlignment = v,
            };
            SetRowSpan(b, _rowSpan);
            b.PointerPressed += (s, e) => BeginResize(edge, e);
            b.PointerEntered += (s, e) => b.Cursor = new Cursor(cursorType);
            Children.Add(b);
        }

        private void CreateCorner(WindowEdge edge, HorizontalAlignment h, VerticalAlignment v, StandardCursorType cursorType) {
            var b = new Border {
                Background = Brushes.Transparent,
                Width = GripThickness,
                Height = GripThickness,
                HorizontalAlignment = h,
                VerticalAlignment = v,
            };
            SetRowSpan(b, _rowSpan);
            b.PointerPressed += (s, e) => BeginResize(edge, e);
            b.PointerEntered += (s, e) => b.Cursor = new Cursor(cursorType);
            Children.Add(b);
        }

        private void BeginResize(WindowEdge edge, PointerPressedEventArgs e) {
            if (TopLevel.GetTopLevel(this) is Window w) {
                w.BeginResizeDrag(edge, e);
            }
        }
    }
}


