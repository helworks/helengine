using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace helengine.ui.Controls {
    public class PanelContainer : Canvas {
        private readonly List<PanelArea> _areas = new List<PanelArea>();
        private const double DOCK_PROXIMITY_THRESHOLD = 50; // pixels

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
            }

           



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
                RedockPanel(e.Panel, tabHeader);
            }
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

            // Position tab headers and panels in their assigned areas
            foreach (var area in _areas) {
                var areaLeft = area.RelativeBounds.X * finalSize.Width;
                var areaTop = area.RelativeBounds.Y * finalSize.Height;
                var areaWidth = area.RelativeBounds.Width * finalSize.Width;
                var areaHeight = area.RelativeBounds.Height * finalSize.Height;

                // Position tab header
                SetLeft(area.TabHeader, areaLeft);
                SetTop(area.TabHeader, areaTop);
                area.TabHeader.Width = areaWidth;
                area.TabHeader.Height = tabHeight;

                // Position panels in this area
                foreach (var panel in area.AssignedPanels) {
                    if (Children.Contains(panel)) {
                        // Set Canvas position properties (account for tab header height)
                        SetLeft(panel, areaLeft);
                        SetTop(panel, areaTop + tabHeight);

                        // Set size to fill the area minus tab header
                        panel.Width = areaWidth;
                        panel.Height = areaHeight - tabHeight;

                        // Only show the active panel
                        panel.IsVisible = (panel == area.ActivePanel);
                    }
                }
            }

            // Hide any panels not assigned to areas (but keep tab headers and floating panels visible)
            foreach (Control child in Children) {
                bool isAssigned = _areas.Any(area => area.AssignedPanels.Contains(child));
                bool isTabHeader = _areas.Any(area => area.TabHeader == child);
                bool isFloatingPanel = child is EditorPanel editorPanel && !editorPanel.IsDocked;

                if (!isAssigned && !isTabHeader && !isFloatingPanel) {
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
            } else {
                HideDockPreview();
            }
        }

        private void ShowDockPreview(PanelArea area) {
            area.TabHeader.ShowPreview();
        }

        public void HideDockPreview() {
            foreach (var area in _areas) {
                area.TabHeader.HidePreview();
            }
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
        public Rect RelativeBounds { get; }
        public List<EditorPanel> AssignedPanels { get; }
        public TabHeader TabHeader { get; }
        public Control? ActivePanel { get; set; }
        public event EventHandler? ActivePanelChanged;

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
        }
    }
}
