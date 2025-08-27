using Avalonia;
using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace helengine.ui.Controls {
    public class PanelContainer : Canvas {
        private readonly List<PanelArea> _areas = new List<PanelArea>();
        
        public PanelContainer() {
            // Initialize with left and right areas
            _areas.Add(new PanelArea("Left", new Rect(0, 0, 0.5, 1.0)));
            _areas.Add(new PanelArea("Right", new Rect(0.5, 0, 0.5, 1.0)));
            
            // Add tab headers to canvas and wire up events
            foreach (var area in _areas) {
                Children.Add(area.TabHeader);
                area.ActivePanelChanged += OnAreaActivePanelChanged;
            }
        }
        
        private void OnAreaActivePanelChanged(object? sender, EventArgs e) {
            // Force a layout update when active panel changes
            InvalidateArrange();
        }
        
        public void AssignPanelToArea(Control panel, string areaName) {
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
            
            // Hide any panels not assigned to areas (but keep tab headers visible)
            foreach (Control child in Children) {
                bool isAssigned = _areas.Any(area => area.AssignedPanels.Contains(child));
                bool isTabHeader = _areas.Any(area => area.TabHeader == child);
                
                if (!isAssigned && !isTabHeader) {
                    child.IsVisible = false;
                }
            }
            
            return result;
        }
    }
    
    internal class PanelArea {
        public string Name { get; }
        public Rect RelativeBounds { get; }
        public List<Control> AssignedPanels { get; }
        public TabHeader TabHeader { get; }
        public Control? ActivePanel { get; set; }
        public event EventHandler? ActivePanelChanged;
        
        public PanelArea(string name, Rect relativeBounds) {
            Name = name;
            RelativeBounds = relativeBounds;
            AssignedPanels = new List<Control>();
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
        
        public void AddPanel(Control panel) {
            AssignedPanels.Add(panel);
            
            // Get panel title (try EditorPanel first, fallback to type name)
            string title = "Panel";
            if (panel is EditorPanel editorPanel) {
                title = editorPanel.Title ?? "Panel";
                // Set the panel as docked to hide its individual title bar
                editorPanel.IsDocked = true;
            } else {
                title = panel.GetType().Name;
            }
            
            TabHeader.AddTab(title, panel);
            
            // Set as active if it's the first panel
            if (AssignedPanels.Count == 1) {
                ActivePanel = panel;
            }
        }
        
        public void RemovePanel(Control panel) {
            AssignedPanels.Remove(panel);
            TabHeader.RemoveTab(panel);
            
            // Restore individual title bar when undocked
            if (panel is EditorPanel editorPanel) {
                editorPanel.IsDocked = false;
            }
            
            // Update active panel if needed
            if (ActivePanel == panel) {
                ActivePanel = AssignedPanels.FirstOrDefault();
            }
        }
    }
}
