using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace helengine.ui.Controls {
    enum DockDirection { None, Left, Right, Top, Bottom }
    
    public class PanelContainer : Canvas {
        private readonly List<PanelArea> _areas = new List<PanelArea>();
        private const double DOCK_PROXIMITY_THRESHOLD = 50; // pixels
        private readonly Rectangle _splitPreview = new Rectangle();
        private PanelArea? _splitTargetArea;
        private DockDirection _splitDirection = DockDirection.None;

        public PanelContainer() {
            // Initialize with left and right areas
            _areas.Add(new PanelArea("Left", new Rect(0, 0, 0.5, 1.0)));
            _areas.Add(new PanelArea("Right", new Rect(0.5, 0, 0.5, 1.0)));

            // Add tab headers to canvas and wire up events
            foreach (var area in _areas) {
                Children.Add(area.TabHeader);
                area.ActivePanelChanged += OnAreaActivePanelChanged;
                area.TabHeader.TabDragStarted += OnTabDragStarted;
                area.TabHeader.PanelDropped += OnPanelDropped;
                area.Emptied += OnAreaEmptied;
            }

            // Setup split preview rectangle (hidden by default)
            _splitPreview.IsHitTestVisible = false;
            _splitPreview.IsVisible = false;
            _splitPreview.ZIndex = 1000;
            _splitPreview.Stroke = new SolidColorBrush(Color.FromRgb(255, 200, 0));
            _splitPreview.StrokeThickness = 2;
            _splitPreview.Fill = new SolidColorBrush(Color.FromArgb(64, 255, 200, 0));
            Children.Add(_splitPreview);
        }

        private void OnAreaActivePanelChanged(object? sender, EventArgs e) {
            // Force a layout update when active panel changes
            InvalidateArrange();
        }

        private void OnTabDragStarted(object? sender, TabDragEventArgs e) {
            if (e.Tag is EditorPanel panel && sender is TabHeader tabHeader) {
                UndockPanel(panel, tabHeader);
            }
        }

        private void OnPanelDropped(object? sender, PanelDropEventArgs e) {
            if (sender is TabHeader tabHeader) {
                // Only allow docking into visible header areas (prevents recreating hidden/zero-width side)
                if (tabHeader.IsVisible && tabHeader.Width > 0) {
                    RedockPanel(e.Panel, tabHeader);
                }
            }
        }

        private void OnAreaEmptied(object? sender, EventArgs e) {
            // Remove the empty area and redistribute its space to neighboring areas
            if (sender is PanelArea emptyArea) {
                // Ensure it's actually empty
                if (emptyArea.AssignedPanels.Count == 0) {
                    // Remove header from children
                    if (Children.Contains(emptyArea.TabHeader)) {
                        Children.Remove(emptyArea.TabHeader);
                    }

                    // Redistribute the empty area's space to neighboring areas
                    RedistributeEmptyAreaSpace(emptyArea);
                    
                    _areas.Remove(emptyArea);
                    InvalidateArrange();
                }
            }
        }

        private void RedistributeEmptyAreaSpace(PanelArea emptyArea) {
            var emptyBounds = emptyArea.RelativeBounds;
            
            // Find neighboring areas that can expand into the empty space
            var neighbors = FindNeighboringAreas(emptyArea);
            
            if (neighbors.Count == 0) return;

            // Distribute the empty area's space proportionally among neighbors
            foreach (var neighbor in neighbors) {
                var neighborBounds = neighbor.RelativeBounds;
                var newBounds = neighborBounds;

                // Determine how to expand based on spatial relationship
                if (AreHorizontallyAdjacent(neighborBounds, emptyBounds)) {
                    // Expand horizontally
                    if (neighborBounds.Right <= emptyBounds.Left) {
                        // Neighbor is to the left, expand right
                        newBounds = new Rect(neighborBounds.X, neighborBounds.Y, 
                            neighborBounds.Width + emptyBounds.Width, neighborBounds.Height);
                    } else if (neighborBounds.Left >= emptyBounds.Right) {
                        // Neighbor is to the right, expand left
                        newBounds = new Rect(emptyBounds.X, neighborBounds.Y, 
                            neighborBounds.Width + emptyBounds.Width, neighborBounds.Height);
                    }
                } else if (AreVerticallyAdjacent(neighborBounds, emptyBounds)) {
                    // Expand vertically
                    if (neighborBounds.Bottom <= emptyBounds.Top) {
                        // Neighbor is above, expand down
                        newBounds = new Rect(neighborBounds.X, neighborBounds.Y, 
                            neighborBounds.Width, neighborBounds.Height + emptyBounds.Height);
                    } else if (neighborBounds.Top >= emptyBounds.Bottom) {
                        // Neighbor is below, expand up
                        newBounds = new Rect(neighborBounds.X, emptyBounds.Y, 
                            neighborBounds.Width, neighborBounds.Height + emptyBounds.Height);
                    }
                }

                neighbor.RelativeBounds = newBounds;
                
                // For simplicity, expand the first valid neighbor and break
                // In a more complex system, you might distribute among multiple neighbors
                break;
            }
        }

        private List<PanelArea> FindNeighboringAreas(PanelArea targetArea) {
            var neighbors = new List<PanelArea>();
            var targetBounds = targetArea.RelativeBounds;

            foreach (var area in _areas) {
                if (area == targetArea) continue;

                var areaBounds = area.RelativeBounds;
                
                // Check if areas are adjacent (sharing an edge)
                if (AreHorizontallyAdjacent(areaBounds, targetBounds) || 
                    AreVerticallyAdjacent(areaBounds, targetBounds)) {
                    neighbors.Add(area);
                }
            }

            return neighbors;
        }

        private bool AreHorizontallyAdjacent(Rect rect1, Rect rect2) {
            // Check if they share a vertical edge and have overlapping Y ranges
            bool shareVerticalEdge = Math.Abs(rect1.Right - rect2.Left) < 0.001 || 
                                   Math.Abs(rect2.Right - rect1.Left) < 0.001;
            bool overlapVertically = !(rect1.Bottom <= rect2.Top || rect2.Bottom <= rect1.Top);
            
            return shareVerticalEdge && overlapVertically;
        }

        private bool AreVerticallyAdjacent(Rect rect1, Rect rect2) {
            // Check if they share a horizontal edge and have overlapping X ranges
            bool shareHorizontalEdge = Math.Abs(rect1.Bottom - rect2.Top) < 0.001 || 
                                     Math.Abs(rect2.Bottom - rect1.Top) < 0.001;
            bool overlapHorizontally = !(rect1.Right <= rect2.Left || rect2.Right <= rect1.Left);
            
            return shareHorizontalEdge && overlapHorizontally;
        }

        private void RedockPanel(EditorPanel panel, TabHeader targetTabHeader) {
            // Find which area this tab header belongs to
            var targetArea = _areas.FirstOrDefault(a => a.TabHeader == targetTabHeader);
            if (targetArea != null) {
                // If panel is already in an area, remove it first
                var currentArea = _areas.FirstOrDefault(a => a.AssignedPanels.Contains(panel));
                if (currentArea != null) {
                    currentArea.RemovePanel(panel);
                }

                // Dock the panel into the target area
                targetArea.AddPanel(panel);

                // Reset Z-index for docked panel (will be properly set in ArrangeOverride)
                panel.ZIndex = 0;

                // Force layout update
                InvalidateArrange();
            }
        }

        private void UndockPanel(EditorPanel panel, TabHeader tabHeader) {
            // Find which area contains this panel
            var area = _areas.FirstOrDefault(a => a.AssignedPanels.Contains(panel));
            if (area != null) {
                // Get the tab header's position to place the panel near it
                var tabHeaderLeft = Canvas.GetLeft(tabHeader);
                var tabHeaderTop = Canvas.GetTop(tabHeader);

                // Remove panel from the area (this will set IsDocked = false)
                area.RemovePanel(panel);

                // Position the panel near where the tab was, with some default size
                Canvas.SetLeft(panel, tabHeaderLeft + 20);
                Canvas.SetTop(panel, tabHeaderTop + 30);
                panel.Width = 300;
                panel.Height = 220;

                // Make sure it's visible and on top
                panel.IsVisible = true;
                panel.ZIndex = 1000;

                // Add it back as a floating panel
                if (!Children.Contains(panel)) {
                    Children.Add(panel);
                }

                // Force layout update
                InvalidateArrange();
            }
        }

        public void AssignPanelToArea(EditorPanel panel, string areaName) {
            var area = _areas.FirstOrDefault(a => a.Name == areaName);
            if (area != null) {
                area.AddPanel(panel);

                // Ensure the panel is in our Children collection
                if (!Children.Contains(panel)) {
                    Children.Add(panel);
                }

                UpdateLayout();
            }
        }

        protected override Size ArrangeOverride(Size finalSize) {
            // Let Canvas do its normal arrangement first
            var result = base.ArrangeOverride(finalSize);

            const double tabHeight = 25;
            const int dockedPanelZIndex = 0;
            const int tabHeaderZIndex = 10;
            const int floatingPanelZIndex = 1000;
            const int splitPreviewZIndex = 2000;

            // Arrange all areas based on their relative bounds
            foreach (var area in _areas) {
                bool hasContent = area.AssignedPanels.Count > 0;
                
                if (hasContent) {
                    // Calculate absolute bounds from relative bounds
                    double areaLeft = area.RelativeBounds.X * finalSize.Width;
                    double areaTop = area.RelativeBounds.Y * finalSize.Height;
                    double areaWidth = area.RelativeBounds.Width * finalSize.Width;
                    double areaHeight = area.RelativeBounds.Height * finalSize.Height;

                    // Position tab header with appropriate Z-index
                    area.TabHeader.IsVisible = true;
                    area.TabHeader.ZIndex = tabHeaderZIndex;
                    SetLeft(area.TabHeader, areaLeft);
                    SetTop(area.TabHeader, areaTop);
                    area.TabHeader.Width = areaWidth;
                    area.TabHeader.Height = tabHeight;

                    // Position panels in this area
                    foreach (var panel in area.AssignedPanels) {
                        if (Children.Contains(panel)) {
                            SetLeft(panel, areaLeft);
                            SetTop(panel, areaTop + tabHeight);
                            panel.Width = areaWidth;
                            panel.Height = areaHeight - tabHeight;
                            panel.IsVisible = (panel == area.ActivePanel);
                            // Ensure docked panels have low Z-index
                            panel.ZIndex = dockedPanelZIndex;
                        }
                    }
                } else {
                    // Hide empty areas
                    area.TabHeader.IsVisible = false;
                }
            }

            // Manage Z-index for all children and handle visibility
            foreach (Control child in Children) {
                bool isAssigned = _areas.Any(area => area.AssignedPanels.Contains(child));
                bool isTabHeader = _areas.Any(area => area.TabHeader == child);
                bool isFloatingPanel = child is EditorPanel editorPanel && !editorPanel.IsDocked;
                bool isSplitPreview = child == _splitPreview;

                if (isFloatingPanel) {
                    // Ensure floating panels are always on top
                    child.ZIndex = floatingPanelZIndex;
                } else if (isSplitPreview) {
                    // Split preview should be above everything
                    child.ZIndex = splitPreviewZIndex;
                } else if (!isAssigned && !isTabHeader && !isFloatingPanel && !isSplitPreview) {
                    child.IsVisible = false;
                }
            }

            return result;
        }

        public void CheckDockProximity(EditorPanel panel) {
            if (panel.IsDocked) return; // Only check floating panels

            var panelLeft = Canvas.GetLeft(panel);
            var panelTop = Canvas.GetTop(panel);

            // Handle NaN values from Canvas positioning
            if (double.IsNaN(panelLeft)) panelLeft = 0;
            if (double.IsNaN(panelTop)) panelTop = 0;

            // Use current pointer position to test header hit, fallback to panel top-left
            var testPoint = new Point(panelLeft, panelTop);
            var headerArea = GetHeaderAtPosition(testPoint);
            if (headerArea != null) {
                ShowDockPreview(headerArea);
                HideSplitPreview();
                return;
            }

            // Otherwise, check for split preview against area bodies
            var containerSize = Bounds.Size;
            _splitTargetArea = null;
            _splitDirection = DockDirection.None;

            foreach (var area in _areas) {
                var areaBounds = new Rect(
                    area.RelativeBounds.X * containerSize.Width,
                    area.RelativeBounds.Y * containerSize.Height,
                    area.RelativeBounds.Width * containerSize.Width,
                    area.RelativeBounds.Height * containerSize.Height
                );

                // Consider proximity to edges
                var px = testPoint.X;
                var py = testPoint.Y;
                if (areaBounds.Contains(testPoint) ||
                    Math.Abs(px - areaBounds.X) < DOCK_PROXIMITY_THRESHOLD ||
                    Math.Abs(px - (areaBounds.X + areaBounds.Width)) < DOCK_PROXIMITY_THRESHOLD ||
                    Math.Abs(py - areaBounds.Y) < DOCK_PROXIMITY_THRESHOLD ||
                    Math.Abs(py - (areaBounds.Y + areaBounds.Height)) < DOCK_PROXIMITY_THRESHOLD) {

                    // Choose closest edge
                    double dLeft = Math.Abs(px - areaBounds.X);
                    double dRight = Math.Abs(px - (areaBounds.X + areaBounds.Width));
                    double dTop = Math.Abs(py - areaBounds.Y);
                    double dBottom = Math.Abs(py - (areaBounds.Y + areaBounds.Height));

                    double min = Math.Min(Math.Min(dLeft, dRight), Math.Min(dTop, dBottom));
                    if (min <= DOCK_PROXIMITY_THRESHOLD) {
                        _splitTargetArea = area;
                        if (min == dLeft) _splitDirection = DockDirection.Left;
                        else if (min == dRight) _splitDirection = DockDirection.Right;
                        else if (min == dTop) _splitDirection = DockDirection.Top;
                        else _splitDirection = DockDirection.Bottom;
                        ShowSplitPreview(area, _splitDirection);
                        HideDockPreview();
                        return;
                    }
                }
            }

            // Nothing nearby
            HideDockPreview();
            HideSplitPreview();
        }

        private void ShowDockPreview(PanelArea area) {
            area.TabHeader.ShowPreview();
        }

        public void HideDockPreview() {
            foreach (var area in _areas) {
                area.TabHeader.HidePreview();
            }
        }

        private void ShowSplitPreview(PanelArea area, DockDirection dir) {
            var sz = Bounds.Size;
            var b = new Rect(
                area.RelativeBounds.X * sz.Width,
                area.RelativeBounds.Y * sz.Height,
                area.RelativeBounds.Width * sz.Width,
                area.RelativeBounds.Height * sz.Height
            );
            Rect r = b;
            switch (dir) {
                case DockDirection.Left:
                    r = new Rect(b.X, b.Y, b.Width * 0.5, b.Height);
                    break;
                case DockDirection.Right:
                    r = new Rect(b.X + b.Width * 0.5, b.Y, b.Width * 0.5, b.Height);
                    break;
                case DockDirection.Top:
                    r = new Rect(b.X, b.Y, b.Width, b.Height * 0.5);
                    break;
                case DockDirection.Bottom:
                    r = new Rect(b.X, b.Y + b.Height * 0.5, b.Width, b.Height * 0.5);
                    break;
            }
            _splitPreview.Width = r.Width;
            _splitPreview.Height = r.Height;
            Canvas.SetLeft(_splitPreview, r.X);
            Canvas.SetTop(_splitPreview, r.Y);
            _splitPreview.IsVisible = true;
        }

        private void HideSplitPreview() {
            _splitPreview.IsVisible = false;
            _splitTargetArea = null;
            _splitDirection = DockDirection.None;
        }

        public void CommitSplitIfAny(EditorPanel panel) {
            if (_splitTargetArea == null || _splitDirection == DockDirection.None) return;

            var target = _splitTargetArea;
            var original = target.RelativeBounds;
            PanelArea newArea;

            switch (_splitDirection) {
                case DockDirection.Left:
                    // New left half
                    newArea = CreateArea(new Rect(original.X, original.Y, original.Width * 0.5, original.Height));
                    target.RelativeBounds = new Rect(original.X + original.Width * 0.5, original.Y, original.Width * 0.5, original.Height);
                    break;
                case DockDirection.Right:
                    // New right half
                    newArea = CreateArea(new Rect(original.X + original.Width * 0.5, original.Y, original.Width * 0.5, original.Height));
                    target.RelativeBounds = new Rect(original.X, original.Y, original.Width * 0.5, original.Height);
                    break;
                case DockDirection.Top:
                    // New top half
                    newArea = CreateArea(new Rect(original.X, original.Y, original.Width, original.Height * 0.5));
                    target.RelativeBounds = new Rect(original.X, original.Y + original.Height * 0.5, original.Width, original.Height * 0.5);
                    break;
                default:
                    // Bottom
                    newArea = CreateArea(new Rect(original.X, original.Y + original.Height * 0.5, original.Width, original.Height * 0.5));
                    target.RelativeBounds = new Rect(original.X, original.Y, original.Width, original.Height * 0.5);
                    break;
            }

            // Dock panel into the new area
            newArea.AddPanel(panel);
            if (!Children.Contains(panel)) Children.Add(panel);

            // Reset Z-index for docked panel (will be properly set in ArrangeOverride)
            panel.ZIndex = 0;

            // Refresh
            HideSplitPreview();
            InvalidateArrange();
        }

        private PanelArea CreateArea(Rect relativeBounds) {
            var name = $"Area{_areas.Count + 1}";
            var area = new PanelArea(name, relativeBounds);
            Children.Add(area.TabHeader);
            area.ActivePanelChanged += OnAreaActivePanelChanged;
            area.TabHeader.TabDragStarted += OnTabDragStarted;
            area.TabHeader.PanelDropped += OnPanelDropped;
            area.Emptied += OnAreaEmptied;
            _areas.Add(area);
            return area;
        }

        public PanelArea? GetDockAreaAtPosition(Point position) {
            var containerSize = Bounds.Size;
            if (containerSize.Width <= 0 || containerSize.Height <= 0) return null;

            foreach (var area in _areas) {
                var areaBounds = new Rect(
                    area.RelativeBounds.X * containerSize.Width,
                    area.RelativeBounds.Y * containerSize.Height,
                    area.RelativeBounds.Width * containerSize.Width,
                    area.RelativeBounds.Height * containerSize.Height
                );

                if (areaBounds.Contains(position)) {
                    return area;
                }
            }

            return null;
        }

        public PanelArea? GetHeaderAtPosition(Point position) {
            foreach (var area in _areas) {
                var left = Canvas.GetLeft(area.TabHeader);
                var top = Canvas.GetTop(area.TabHeader);
                if (double.IsNaN(left)) left = 0;
                if (double.IsNaN(top)) top = 0;

                var width = area.TabHeader.Bounds.Width > 0 ? area.TabHeader.Bounds.Width : area.TabHeader.Width;
                var height = area.TabHeader.Bounds.Height > 0 ? area.TabHeader.Bounds.Height : area.TabHeader.Height;
                var headerRect = new Rect(left, top, width, height);

                if (headerRect.Contains(position)) {
                    return area;
                }
            }
            return null;
        }
    }

    public class PanelArea {
        public string Name { get; }
        public Rect RelativeBounds { get; set; }
        public List<EditorPanel> AssignedPanels { get; }
        public TabHeader TabHeader { get; }
        public Control? ActivePanel { get; set; }
        public event EventHandler? ActivePanelChanged;
        public event EventHandler? Emptied;

        public PanelArea(string name, Rect relativeBounds) {
            Name = name;
            RelativeBounds = relativeBounds;
            AssignedPanels = new List<EditorPanel>();
            TabHeader = new TabHeader();
            TabHeader.TabSelected += OnTabSelected;
        }

        private void OnTabSelected(object? sender, TabSelectedEventArgs e) {
            if (e.Tag is Control panel && AssignedPanels.Contains(panel)) {
                ActivePanel = panel;
                // Notify the container that the active panel changed
                ActivePanelChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void AddPanel(EditorPanel panel) {
            AssignedPanels.Add(panel);

            // Get panel title (try EditorPanel first, fallback to type name)
            string title = panel.Title ?? "Panel";
            // Set the panel as docked to hide its individual title bar
            panel.IsDocked = true;

            TabHeader.AddTab(title, panel);

            // Set as active if it's the first panel
            if (AssignedPanels.Count == 1) {
                ActivePanel = panel;
            }
        }

        public void RemovePanel(EditorPanel panel) {
            AssignedPanels.Remove(panel);
            TabHeader.RemoveTab(panel);

            // Restore individual title bar when undocked
            panel.IsDocked = false;

            // Update active panel if needed
            if (ActivePanel == panel) {
                ActivePanel = AssignedPanels.FirstOrDefault();
            }

            if (AssignedPanels.Count == 0) {
                Emptied?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
