using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Collections.Generic;
using System.Linq;

namespace helengine.ui.Controls;

public class TabbedEditorPanel : UserControl {
    private bool _isDragging;
    private Point _initialPointerPosition;
    private double _initialLeft;
    private double _initialTop;
    private TranslateTransform _transform = new TranslateTransform();
    
    private Border _mainBorder;
    private StackPanel _tabHeadersPanel;
    private Border _contentArea;
    private List<EditorPanel> _panels = new List<EditorPanel>();
    private List<TabHeader> _tabHeaders = new List<TabHeader>();
    private int _selectedIndex = -1;

    private int borderSize = 4;

    public Size Size {
        get { return new Size(_mainBorder.Width, _mainBorder.Height); }
        set {
            _mainBorder.Width = value.Width;
            _mainBorder.Height = value.Height;
            _contentArea.Width = value.Width - borderSize;
            _contentArea.Height = value.Height - 30; // Account for tab header height
            
            // Update all panels to fit content area
            foreach (var panel in _panels) {
                panel.Size = new Size(value.Width - borderSize - 4, value.Height - 30 - 4);
            }
        }
    }

    public TabbedEditorPanel() {
        BuildVisualTree();
        InitializeDragLogic();
    }

    private void BuildVisualTree() {
        _mainBorder = new Border {
            Background = Brushes.Gray,
            CornerRadius = new CornerRadius(4),
            Width = 400,
            Height = 300
        };

        var mainStackPanel = new StackPanel();

        // Tab headers area
        _tabHeadersPanel = new StackPanel {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Background = new SolidColorBrush(Color.Parse("#8d31c2")),
            Height = 26
        };

        // Content area
        _contentArea = new Border {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(0, 0, 4, 4),
            BorderThickness = new Thickness(2, 0, 2, 2),
            BorderBrush = new SolidColorBrush(Color.Parse("#4431c2"))
        };

        mainStackPanel.Children.Add(_tabHeadersPanel);
        mainStackPanel.Children.Add(_contentArea);
        _mainBorder.Child = mainStackPanel;

        Content = _mainBorder;
        RenderTransform = _transform;
    }

    private void InitializeDragLogic() {
        _tabHeadersPanel.PointerPressed += (sender, e) => {
            if (!_isDragging) {
                _isDragging = true;

                _initialPointerPosition = e.GetPosition(Parent as Control);
                _initialLeft = Canvas.GetLeft(this);
                _initialTop = Canvas.GetTop(this);

                BringToFront();
                e.Pointer.Capture(_tabHeadersPanel);
            }
        };

        _tabHeadersPanel.PointerReleased += (sender, e) => {
            if (_isDragging) {
                _isDragging = false;
                e.Pointer.Capture(null);
            }
        };

        _tabHeadersPanel.PointerMoved += (sender, e) => {
            if (_isDragging && e.Pointer.Captured == _tabHeadersPanel) {
                var currentPosition = e.GetPosition(Parent as Control);

                var deltaX = currentPosition.X - _initialPointerPosition.X;
                var deltaY = currentPosition.Y - _initialPointerPosition.Y;

                Canvas.SetLeft(this, _initialLeft + deltaX);
                Canvas.SetTop(this, _initialTop + deltaY);
            }
        };
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

        // Add to collections
        _panels.Add(panel);
        _tabHeaders.Add(tabHeader);
        _tabHeadersPanel.Children.Add(tabHeader);

        // Adjust panel size
        panel.Size = new Size(_contentArea.Width - 4, _contentArea.Height - 4);

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

        _panels.RemoveAt(index);
        _tabHeaders.RemoveAt(index);
        _tabHeadersPanel.Children.RemoveAt(index);

        // Clear content if this was the selected tab
        if (index == _selectedIndex) {
            _contentArea.Child = null;
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

        // Show the selected panel
        _contentArea.Child = _panels[index];
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
