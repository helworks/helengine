using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System.Collections.Generic;
using System.Linq;
using helengine.ui.Controls.Docking;

namespace helengine.ui.Controls.Docking;

// Custom canvas that only allows hit testing on children, not empty background areas
public class FloatingCanvas : Canvas {
    public FloatingCanvas() {
        Background = null; // Null background allows clicks to pass through
        IsHitTestVisible = true;
    }
}

public class DockingContainer : UserControl {
    private FloatingCanvas _floatingCanvas;
    private DockZone _rootDockZone;
    private List<TabbedEditorPanel> _floatingPanels = new List<TabbedEditorPanel>();
    private DockDropIndicator? _dropIndicator;
    
    public DockingContainer() {
        BuildVisualTree();
    }

    private void BuildVisualTree() {
        var mainGrid = new Grid();
        
        // Root dock zone takes up the full area
        _rootDockZone = new DockZone();
        _rootDockZone.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        _rootDockZone.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
        mainGrid.Children.Add(_rootDockZone);
        
        // Custom canvas that only allows hit testing on children, not background
        _floatingCanvas = new FloatingCanvas();
        mainGrid.Children.Add(_floatingCanvas);

        Content = mainGrid;
    }

    public void DockPanel(TabbedEditorPanel panel, DockPosition position = DockPosition.Center) {
        // Remove from floating if it was floating
        if (_floatingPanels.Contains(panel)) {
            _floatingCanvas.Children.Remove(panel);
            _floatingPanels.Remove(panel);
        }

        // Find a suitable dock zone or create a split
        if (_rootDockZone.IsEmpty) {
            _rootDockZone.SetContent(panel);
        } else {
            // Find the best zone to split or add to
            var targetZone = FindBestDockZone(position);
            if (targetZone != null) {
                if (position == DockPosition.Center && targetZone.HasContent) {
                    // Add to existing tabbed panel
                    var existingTabbed = targetZone.GetContent();
                    if (existingTabbed != null && panel.GetTabCount() > 0) {
                        // Move all panels from the new one to the existing one
                        for (int i = panel.GetTabCount() - 1; i >= 0; i--) {
                            var editorPanel = panel.GetPanel(i);
                            if (editorPanel != null) {
                                panel.RemovePanel(i);
                                existingTabbed.AddPanel(editorPanel);
                            }
                        }
                    }
                } else {
                    // Split the zone
                    targetZone.Split(position, panel);
                }
            }
        }

        // Ensure panel is not draggable when docked
        panel.IsDockingEnabled = true;
    }

    public void UndockPanel(TabbedEditorPanel panel, Point position) {
        // Remove from dock zone
        RemoveFromDockZones(panel);
        
        // Add to floating canvas
        _floatingPanels.Add(panel);
        _floatingCanvas.Children.Add(panel);
        
        // Position the floating panel
        Canvas.SetLeft(panel, position.X);
        Canvas.SetTop(panel, position.Y);
        
        // Enable dragging for floating panel
        panel.IsDockingEnabled = false;
        
        // Floating panels handle their own hit testing - canvas stays non-hit-testable
    }

    private void RemoveFromDockZones(TabbedEditorPanel panel) {
        RemoveFromDockZone(_rootDockZone, panel);
        _rootDockZone.Cleanup();
    }

    private bool RemoveFromDockZone(DockZone zone, TabbedEditorPanel panel) {
        if (zone.GetContent() == panel) {
            zone.RemoveContent();
            return true;
        }

        // Recursively search child zones
        var children = new[] { 
            GetChildZone(zone, DockPosition.Left),
            GetChildZone(zone, DockPosition.Right),
            GetChildZone(zone, DockPosition.Top),
            GetChildZone(zone, DockPosition.Bottom)
        }.Where(c => c != null);

        foreach (var child in children) {
            if (RemoveFromDockZone(child, panel)) {
                return true;
            }
        }

        return false;
    }

    private DockZone? GetChildZone(DockZone zone, DockPosition position) {
        // This would need access to DockZone's private fields
        // For now, returning null - we'll need to add public accessors to DockZone
        return null;
    }

    private DockZone? FindBestDockZone(DockPosition position) {
        // For now, just return the root zone
        // In a more sophisticated implementation, this would find the best zone based on mouse position
        return _rootDockZone;
    }

    public void ShowDropIndicator(Point position, DockPosition dockPosition) {
        HideDropIndicator();
        
        _dropIndicator = new DockDropIndicator(dockPosition);
        _floatingCanvas.Children.Add(_dropIndicator);
        
        // Position the indicator based on dock position and mouse location
        Canvas.SetLeft(_dropIndicator, position.X - 25);
        Canvas.SetTop(_dropIndicator, position.Y - 25);
    }

    public void HideDropIndicator() {
        if (_dropIndicator != null) {
            _floatingCanvas.Children.Remove(_dropIndicator);
            _dropIndicator = null;
        }
    }

    public DockPosition? GetDropTarget(Point position) {
        // Determine what dock position would be used at this location
        var bounds = Bounds;
        var centerX = bounds.Width / 2;
        var centerY = bounds.Height / 2;
        var margin = 100; // Pixels from edge to trigger side docking

        if (position.X < margin) return DockPosition.Left;
        if (position.X > bounds.Width - margin) return DockPosition.Right;
        if (position.Y < margin) return DockPosition.Top;
        if (position.Y > bounds.Height - margin) return DockPosition.Bottom;
        
        return DockPosition.Center;
    }
}

// Simple drop indicator for visual feedback
public class DockDropIndicator : Border {
    public DockDropIndicator(DockPosition position) {
        Width = 50;
        Height = 50;
        Background = new SolidColorBrush(Color.FromArgb(128, 0, 120, 215)); // Semi-transparent blue
        BorderBrush = new SolidColorBrush(Color.FromArgb(255, 0, 120, 215));
        BorderThickness = new Thickness(2);
        CornerRadius = new CornerRadius(4);

        // Add an icon or text to indicate drop position
        Child = new TextBlock {
            Text = GetPositionIcon(position),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Foreground = Brushes.White,
            FontSize = 16
        };
    }

    private string GetPositionIcon(DockPosition position) {
        return position switch {
            DockPosition.Left => "◀",
            DockPosition.Right => "▶",
            DockPosition.Top => "▲",
            DockPosition.Bottom => "▼",
            DockPosition.Center => "⊞",
            _ => "?"
        };
    }
}
