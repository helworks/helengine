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

            // Group neighbors by expansion type for more intelligent distribution
            var columnNeighbors = neighbors.Where(n => CanExpandVertically(n, emptyArea)).ToList();
            var rowNeighbors = neighbors.Where(n => CanExpandHorizontally(n, emptyArea)).ToList();

            // Try to distribute space among same-column neighbors first (vertical expansion)
            if (columnNeighbors.Count > 0) {
                DistributeSpaceAmongNeighbors(columnNeighbors, emptyArea, true); // true = vertical
                return;
            }

            // Then try same-row neighbors (horizontal expansion)
            if (rowNeighbors.Count > 0) {
                DistributeSpaceAmongNeighbors(rowNeighbors, emptyArea, false); // false = horizontal
                return;
            }

            // Fallback: try individual expansion for other adjacent neighbors
            var sortedNeighbors = neighbors.OrderBy(n => GetExpansionPriority(n, emptyArea)).ToList();
            foreach (var neighbor in sortedNeighbors) {
                if (TryExpandNeighbor(neighbor, emptyArea)) {
                    break; // Successfully expanded one neighbor, we're done
                }
            }
        }

        private bool CanExpandVertically(PanelArea neighbor, PanelArea emptyArea) {
            var neighborBounds = neighbor.RelativeBounds;
            var emptyBounds = emptyArea.RelativeBounds;
            
            // Can expand vertically if they have overlapping X ranges and are above/below
            return (neighborBounds.X < emptyBounds.Right && neighborBounds.Right > emptyBounds.X) &&
                   (Math.Abs(neighborBounds.Bottom - emptyBounds.Top) < 0.001 || // Neighbor is above
                    Math.Abs(neighborBounds.Top - emptyBounds.Bottom) < 0.001);  // Neighbor is below
        }

        private bool CanExpandHorizontally(PanelArea neighbor, PanelArea emptyArea) {
            var neighborBounds = neighbor.RelativeBounds;
            var emptyBounds = emptyArea.RelativeBounds;
            
            // Can expand horizontally if they have overlapping Y ranges and are left/right
            return (neighborBounds.Y < emptyBounds.Bottom && neighborBounds.Bottom > emptyBounds.Y) &&
                   (Math.Abs(neighborBounds.Right - emptyBounds.Left) < 0.001 || // Neighbor is left
                    Math.Abs(neighborBounds.Left - emptyBounds.Right) < 0.001);  // Neighbor is right
        }

        private void DistributeSpaceAmongNeighbors(List<PanelArea> neighbors, PanelArea emptyArea, bool isVertical) {
            if (neighbors.Count == 0) return;

            var emptyBounds = emptyArea.RelativeBounds;

            if (isVertical) {
                // Group neighbors by direction (above vs below)
                var neighborsAbove = neighbors.Where(n => n.RelativeBounds.Bottom <= emptyBounds.Top + 0.001).ToList();
                var neighborsBelow = neighbors.Where(n => n.RelativeBounds.Top >= emptyBounds.Bottom - 0.001).ToList();
                
                // Each group of neighbors gets the full empty space
                if (neighborsAbove.Count > 0) {
                    double expansionAmount = emptyBounds.Height;
                    foreach (var neighbor in neighborsAbove) {
                        var nb = neighbor.RelativeBounds;
                        // Check if there's overlap (if there is, this neighbor should expand)
                        double overlapStart = Math.Max(nb.X, emptyBounds.X);
                        double overlapEnd = Math.Min(nb.Right, emptyBounds.Right);
                        double overlapWidth = Math.Max(0, overlapEnd - overlapStart);
                        
                        if (overlapWidth > 0) {
                            // Expand down by the full empty space height
                            neighbor.RelativeBounds = new Rect(nb.X, nb.Y, nb.Width, nb.Height + expansionAmount);
                        }
                    }
                }
                
                if (neighborsBelow.Count > 0) {
                    double expansionAmount = emptyBounds.Height;
                    foreach (var neighbor in neighborsBelow) {
                        var nb = neighbor.RelativeBounds;
                        // Check if there's overlap (if there is, this neighbor should expand)
                        double overlapStart = Math.Max(nb.X, emptyBounds.X);
                        double overlapEnd = Math.Min(nb.Right, emptyBounds.Right);
                        double overlapWidth = Math.Max(0, overlapEnd - overlapStart);
                        
                        if (overlapWidth > 0) {
                            // Expand up by the full empty space height
                            neighbor.RelativeBounds = new Rect(nb.X, nb.Y - expansionAmount, nb.Width, nb.Height + expansionAmount);
                        }
                    }
                }
            } else {
                // Group neighbors by direction (left vs right)
                var neighborsLeft = neighbors.Where(n => n.RelativeBounds.Right <= emptyBounds.Left + 0.001).ToList();
                var neighborsRight = neighbors.Where(n => n.RelativeBounds.Left >= emptyBounds.Right - 0.001).ToList();
                
                // Each group of neighbors gets the full empty space
                if (neighborsLeft.Count > 0) {
                    double expansionAmount = emptyBounds.Width;
                    foreach (var neighbor in neighborsLeft) {
                        var nb = neighbor.RelativeBounds;
                        // Check if there's overlap (if there is, this neighbor should expand)
                        double overlapStart = Math.Max(nb.Y, emptyBounds.Y);
                        double overlapEnd = Math.Min(nb.Bottom, emptyBounds.Bottom);
                        double overlapHeight = Math.Max(0, overlapEnd - overlapStart);
                        
                        if (overlapHeight > 0) {
                            // Expand right by the full empty space width
                            neighbor.RelativeBounds = new Rect(nb.X, nb.Y, nb.Width + expansionAmount, nb.Height);
                        }
                    }
                }
                
                if (neighborsRight.Count > 0) {
                    double expansionAmount = emptyBounds.Width;
                    foreach (var neighbor in neighborsRight) {
                        var nb = neighbor.RelativeBounds;
                        // Check if there's overlap (if there is, this neighbor should expand)
                        double overlapStart = Math.Max(nb.Y, emptyBounds.Y);
                        double overlapEnd = Math.Min(nb.Bottom, emptyBounds.Bottom);
                        double overlapHeight = Math.Max(0, overlapEnd - overlapStart);
                        
                        if (overlapHeight > 0) {
                            // Expand left by the full empty space width
                            neighbor.RelativeBounds = new Rect(nb.X - expansionAmount, nb.Y, nb.Width + expansionAmount, nb.Height);
                        }
                    }
                }
            }
        }

        private int GetExpansionPriority(PanelArea neighbor, PanelArea emptyArea) {
            var neighborBounds = neighbor.RelativeBounds;
            var emptyBounds = emptyArea.RelativeBounds;

            // Priority 1: Neighbors that share the same column (for vertical expansion)
            if (Math.Abs(neighborBounds.X - emptyBounds.X) < 0.001 && 
                Math.Abs(neighborBounds.Width - emptyBounds.Width) < 0.001) {
                return 1;
            }

            // Priority 2: Neighbors that share the same row (for horizontal expansion)  
            if (Math.Abs(neighborBounds.Y - emptyBounds.Y) < 0.001 && 
                Math.Abs(neighborBounds.Height - emptyBounds.Height) < 0.001) {
                return 2;
            }

            // Priority 3: Other adjacent neighbors
            return 3;
        }

        private bool TryExpandNeighbor(PanelArea neighbor, PanelArea emptyArea) {
            var neighborBounds = neighbor.RelativeBounds;
            var emptyBounds = emptyArea.RelativeBounds;

            // Check if we can expand vertically (same column)
            if (Math.Abs(neighborBounds.X - emptyBounds.X) < 0.001 && 
                Math.Abs(neighborBounds.Width - emptyBounds.Width) < 0.001) {
                
                if (AreVerticallyAdjacent(neighborBounds, emptyBounds)) {
                    if (neighborBounds.Bottom <= emptyBounds.Top) {
                        // Neighbor is above, expand down
                        neighbor.RelativeBounds = new Rect(neighborBounds.X, neighborBounds.Y, 
                            neighborBounds.Width, neighborBounds.Height + emptyBounds.Height);
                        return true;
                    } else if (neighborBounds.Top >= emptyBounds.Bottom) {
                        // Neighbor is below, expand up
                        neighbor.RelativeBounds = new Rect(neighborBounds.X, emptyBounds.Y, 
                            neighborBounds.Width, neighborBounds.Height + emptyBounds.Height);
                        return true;
                    }
                }
            }

            // Check if we can expand horizontally (same row)
            if (Math.Abs(neighborBounds.Y - emptyBounds.Y) < 0.001 && 
                Math.Abs(neighborBounds.Height - emptyBounds.Height) < 0.001) {
                
                if (AreHorizontallyAdjacent(neighborBounds, emptyBounds)) {
                    if (neighborBounds.Right <= emptyBounds.Left) {
                        // Neighbor is to the left, expand right
                        neighbor.RelativeBounds = new Rect(neighborBounds.X, neighborBounds.Y, 
                            neighborBounds.Width + emptyBounds.Width, neighborBounds.Height);
                        return true;
                    } else if (neighborBounds.Left >= emptyBounds.Right) {
                        // Neighbor is to the right, expand left
                        neighbor.RelativeBounds = new Rect(emptyBounds.X, neighborBounds.Y, 
                            neighborBounds.Width + emptyBounds.Width, neighborBounds.Height);
                        return true;
                    }
                }
            }

            return false; // Couldn't expand this neighbor
        }

        private List<PanelArea> FindNeighboringAreas(PanelArea targetArea) {
            var neighbors = new List<PanelArea>();
            var targetBounds = targetArea.RelativeBounds;

            foreach (var area in _areas) {
                if (area == targetArea) continue;

                var areaBounds = area.RelativeBounds;
                
                // Check if areas can expand to fill the target area's space
                if (CanExpandToFillSpace(areaBounds, targetBounds)) {
                    neighbors.Add(area);
                }
            }

            return neighbors;
        }

        private bool CanExpandToFillSpace(Rect areaBounds, Rect targetBounds) {
            // Check if area can expand vertically to fill target space
            bool canExpandVertically = 
                // Same or overlapping X range (column alignment)
                (areaBounds.X <= targetBounds.Right && areaBounds.Right >= targetBounds.X) &&
                // Area is above or below the target
                (Math.Abs(areaBounds.Bottom - targetBounds.Top) < 0.001 || // Area is above
                 Math.Abs(areaBounds.Top - targetBounds.Bottom) < 0.001);  // Area is below

            // Check if area can expand horizontally to fill target space  
            bool canExpandHorizontally = 
                // Same or overlapping Y range (row alignment)
                (areaBounds.Y <= targetBounds.Bottom && areaBounds.Bottom >= targetBounds.Y) &&
                // Area is left or right of the target
                (Math.Abs(areaBounds.Right - targetBounds.Left) < 0.001 || // Area is left
                 Math.Abs(areaBounds.Left - targetBounds.Right) < 0.001);  // Area is right

            return canExpandVertically || canExpandHorizontally;
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
            // Find which area contains this specific panel AND matches the tab header
            var area = _areas.FirstOrDefault(a => a.AssignedPanels.Contains(panel) && a.TabHeader == tabHeader);
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

        public void CheckDockProximity(EditorPanel panel, Point mousePosition) {
            if (panel.IsDocked) return; // Only check floating panels

            // Use the actual mouse position for accurate detection
            var testPoint = mousePosition;
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

                var px = testPoint.X;
                var py = testPoint.Y;

                // Only show split preview if mouse is actually inside the area bounds
                if (areaBounds.Contains(testPoint)) {
                    // Calculate distance to each edge
                    double dLeft = Math.Abs(px - areaBounds.X);
                    double dRight = Math.Abs(px - areaBounds.Right);
                    double dTop = Math.Abs(py - areaBounds.Y);
                    double dBottom = Math.Abs(py - areaBounds.Bottom);

                    // Find the closest edge
                    double minDistance = Math.Min(Math.Min(dLeft, dRight), Math.Min(dTop, dBottom));
                    
                    // Only show split preview if close enough to an edge
                    if (minDistance <= DOCK_PROXIMITY_THRESHOLD) {
                        DockDirection direction = DockDirection.None;
                        
                        if (minDistance == dLeft) {
                            direction = DockDirection.Left;
                        } else if (minDistance == dRight) {
                            direction = DockDirection.Right;
                        } else if (minDistance == dTop) {
                            direction = DockDirection.Top;
                        } else if (minDistance == dBottom) {
                            direction = DockDirection.Bottom;
                        }
                        
                        if (direction != DockDirection.None) {
                            _splitTargetArea = area;
                            _splitDirection = direction;
                            ShowSplitPreview(area, _splitDirection);
                            HideDockPreview();
                            return;
                        }
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
