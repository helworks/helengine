using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System.Collections.Generic;
using System.Linq;
using System;
using helengine.ui.Controls.Docking;

namespace helengine.ui.Controls;

public class TabUndockEventArgs : EventArgs {
    public EditorPanel Panel { get; }
    public Point Position { get; }

    public TabUndockEventArgs(EditorPanel panel, Point position) {
        Panel = panel;
        Position = position;
    }
}

public class TabbedEditorPanel : UserControl {
    private bool _isDragging;
    private Point _initialPointerPosition;
    private double _initialLeft;
    private double _initialTop;
    private TranslateTransform _transform = new TranslateTransform();
    
    private Border _mainBorder;
    private StackPanel _tabHeadersPanel;
    private Border _contentArea;
    private Grid _contentGrid;
    private List<EditorPanel> _panels = new List<EditorPanel>();
    private List<TabHeader> _tabHeaders = new List<TabHeader>();
    private int _selectedIndex = -1;
    
    private bool _isDockingEnabled = true;
    private TabHeader? _draggingTab;
    private Point _tabDragStart;

    private int borderSize = 4;

    public bool IsDockingEnabled {
        get { return _isDockingEnabled; }
        set { _isDockingEnabled = value; }
    }

    public event EventHandler<TabUndockEventArgs>? TabUndocked;

    public Size Size {
        get { 
            if (double.IsNaN(_mainBorder.Width) || double.IsNaN(_mainBorder.Height)) {
                return new Size(Bounds.Width, Bounds.Height);
            }
            return new Size(_mainBorder.Width, _mainBorder.Height); 
        }
        set {
            if (value.Width > 0 && value.Height > 0) {
                _mainBorder.Width = value.Width;
                _mainBorder.Height = value.Height;
                
                // Remove stretch alignment when setting explicit size
                _mainBorder.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
                _mainBorder.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
                
                UpdatePanelSizes();
            }
        }
    }

    private void UpdatePanelSizes() {
        var availableWidth = Math.Max(0, Bounds.Width - borderSize - 4);
        var availableHeight = Math.Max(0, Bounds.Height - 30 - 4); // Account for tab header
        
        foreach (var panel in _panels) {
            if (availableWidth > 0 && availableHeight > 0) {
                panel.Size = new Size(availableWidth, availableHeight);
            }
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e) {
        base.OnSizeChanged(e);
        
        // Update panel sizes when the container is resized
        if (e.NewSize.Width > 0 && e.NewSize.Height > 0) {
            UpdatePanelSizes();
        }
    }

    public TabbedEditorPanel() {
        // Set default size and alignment
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
        MinWidth = 200;
        MinHeight = 150;
        
        BuildVisualTree();
        InitializeDragLogic();
    }

    private void BuildVisualTree() {
        _mainBorder = new Border {
            Background = Brushes.Gray,
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
        };

        var mainStackPanel = new StackPanel();

        // Tab headers area
        _tabHeadersPanel = new StackPanel {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Background = new SolidColorBrush(Color.Parse("#8d31c2")),
            Height = 26
        };

        // Content area with Grid to hold all panels
        _contentArea = new Border {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(0, 0, 4, 4),
            BorderThickness = new Thickness(2, 0, 2, 2),
            BorderBrush = new SolidColorBrush(Color.Parse("#4431c2"))
        };

        // Grid to hold all panels simultaneously
        _contentGrid = new Grid();
        _contentArea.Child = _contentGrid;

        mainStackPanel.Children.Add(_tabHeadersPanel);
        mainStackPanel.Children.Add(_contentArea);
        _mainBorder.Child = mainStackPanel;

        Content = _mainBorder;
        RenderTransform = _transform;
    }

    private void InitializeDragLogic() {
        _tabHeadersPanel.PointerPressed += OnHeaderPointerPressed;
        _tabHeadersPanel.PointerReleased += OnHeaderPointerReleased;
        _tabHeadersPanel.PointerMoved += OnHeaderPointerMoved;
    }

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e) {
        if (_isDockingEnabled) {
            // When docked, only individual tabs can be dragged out
            var hitTab = GetTabHeaderAt(e.GetPosition(_tabHeadersPanel));
            if (hitTab != null) {
                _draggingTab = hitTab;
                _tabDragStart = e.GetPosition(this);
                e.Pointer.Capture(_tabHeadersPanel);
            }
        } else {
            // When floating, the whole panel can be dragged
            if (!_isDragging) {
                _isDragging = true;
                _initialPointerPosition = e.GetPosition(Parent as Control);
                _initialLeft = Canvas.GetLeft(this);
                _initialTop = Canvas.GetTop(this);
                BringToFront();
                e.Pointer.Capture(_tabHeadersPanel);
            }
        }
    }

    private void OnHeaderPointerReleased(object? sender, PointerReleasedEventArgs e) {
        if (_isDragging) {
            _isDragging = false;
            e.Pointer.Capture(null);
        }

        if (_draggingTab != null) {
            _draggingTab = null;
            e.Pointer.Capture(null);
        }
    }

    private void OnHeaderPointerMoved(object? sender, PointerEventArgs e) {
        if (_isDragging && e.Pointer.Captured == _tabHeadersPanel) {
            // Floating panel movement
            var currentPosition = e.GetPosition(Parent as Control);
            var deltaX = currentPosition.X - _initialPointerPosition.X;
            var deltaY = currentPosition.Y - _initialPointerPosition.Y;
            Canvas.SetLeft(this, _initialLeft + deltaX);
            Canvas.SetTop(this, _initialTop + deltaY);
        } else if (_draggingTab != null && e.Pointer.Captured == _tabHeadersPanel) {
            // Tab undocking logic
            var currentPosition = e.GetPosition(this);
            var distance = Math.Sqrt(
                Math.Pow(currentPosition.X - _tabDragStart.X, 2) + 
                Math.Pow(currentPosition.Y - _tabDragStart.Y, 2)
            );

            // If dragged far enough, undock the tab
            if (distance > 20) {
                UndockTab(_draggingTab, e.GetPosition(Parent as Control));
                _draggingTab = null;
                e.Pointer.Capture(null);
            }
        }
    }

    private TabHeader? GetTabHeaderAt(Point position) {
        foreach (var tab in _tabHeaders) {
            var tabBounds = tab.Bounds;
            if (tabBounds.Contains(position)) {
                return tab;
            }
        }
        return null;
    }

    private void UndockTab(TabHeader tabHeader, Point position) {
        var tabIndex = _tabHeaders.IndexOf(tabHeader);
        if (tabIndex >= 0 && tabIndex < _panels.Count) {
            var panel = _panels[tabIndex];
            
            // Remove the panel from this tabbed container
            RemovePanel(tabIndex);
            
            // Fire the undock event
            TabUndocked?.Invoke(this, new TabUndockEventArgs(panel, position));
        }
    }

    private void BringToFront() {
        if (Parent is Canvas canvas) {
            var maxZ = canvas.Children.OfType<Control>().Max(c => c.ZIndex) + 1;
            this.ZIndex = maxZ;
        }
    }

    public void AddPanel(EditorPanel panel) {
        if (panel == null || _panels.Contains(panel)) {
            return;
        }

        // Remove panel from its current parent
        if (panel.Parent is Canvas canvas) {
            canvas.Children.Remove(panel);
        } else if (panel.Parent is Panel panelParent) {
            panelParent.Children.Remove(panel);
        }

        // Create tab header
        var tabHeader = new TabHeader {
            Title = panel.Title
        };

        tabHeader.TabClicked += (s, e) => {
            var index = _tabHeaders.IndexOf(tabHeader);
            SelectTab(index);
        };

        tabHeader.CloseClicked += (s, e) => {
            var index = _tabHeaders.IndexOf(tabHeader);
            RemovePanel(index);
        };

        // Hide the panel's titlebar since we're using our custom tabs
        panel.ShowTitlebar = false;

        // Add panel to the grid (all panels will be children, but hidden by default)
        _contentGrid.Children.Add(panel);
        panel.IsVisible = false; // Hide by default

        // Adjust panel size to fill the grid
        panel.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        panel.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
        panel.Size = new Size(_contentArea.Width - 4, _contentArea.Height - 4);

        // Add to collections
        _panels.Add(panel);
        _tabHeaders.Add(tabHeader);
        _tabHeadersPanel.Children.Add(tabHeader);

        // Select this tab if it's the first one
        if (_panels.Count == 1) {
            SelectTab(0);
        }
    }

    public void RemovePanel(int index) {
        if (index < 0 || index >= _panels.Count) {
            return;
        }

        // Remove from collections
        var panel = _panels[index];
        var tabHeader = _tabHeaders[index];

        // Restore the panel's titlebar in case it's used elsewhere
        panel.ShowTitlebar = true;

        // Remove panel from the grid
        _contentGrid.Children.Remove(panel);

        _panels.RemoveAt(index);
        _tabHeaders.RemoveAt(index);
        _tabHeadersPanel.Children.RemoveAt(index);

        // Handle selection changes
        if (index == _selectedIndex) {
            _selectedIndex = -1;
            
            // Select another tab if available
            if (_panels.Count > 0) {
                var newIndex = index >= _panels.Count ? _panels.Count - 1 : index;
                SelectTab(newIndex);
            }
        } else if (index < _selectedIndex) {
            // Adjust selected index if we removed a tab before the selected one
            _selectedIndex--;
        }
    }

    public void SelectTab(int index) {
        if (index < 0 || index >= _panels.Count) {
            return;
        }

        // Update visual state of tab headers
        for (int i = 0; i < _tabHeaders.Count; i++) {
            _tabHeaders[i].IsSelected = (i == index);
        }

        // Hide all panels, then show the selected one
        for (int i = 0; i < _panels.Count; i++) {
            _panels[i].IsVisible = (i == index);
        }

        _selectedIndex = index;
    }

    public EditorPanel GetSelectedPanel() {
        if (_selectedIndex >= 0 && _selectedIndex < _panels.Count) {
            return _panels[_selectedIndex];
        }
        return null;
    }

    public int GetTabCount() {
        return _panels.Count;
    }

    public EditorPanel GetPanel(int index) {
        if (index >= 0 && index < _panels.Count) {
            return _panels[index];
        }
        return null;
    }
}
