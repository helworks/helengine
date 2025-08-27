using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Collections.Generic;
using System.Linq;

namespace helengine.ui.Controls.Docking;

public enum DockPosition {
    Left,
    Right,
    Top,
    Bottom,
    Center
}

public class DockZone : UserControl {
    private Grid _mainGrid;
    private TabbedEditorPanel? _tabbedPanel;
    private DockZone? _leftChild;
    private DockZone? _rightChild;
    private DockZone? _topChild;
    private DockZone? _bottomChild;
    private bool _isLeaf = true;
    private GridSplitter? _splitter;

    public bool IsEmpty => _isLeaf && _tabbedPanel == null;
    public bool HasContent => _tabbedPanel != null && _tabbedPanel.GetTabCount() > 0;

    public DockZone() {
        BuildVisualTree();
    }

    private void BuildVisualTree() {
        _mainGrid = new Grid {
            Background = new SolidColorBrush(Color.Parse("#2d2d30")), // Dark background like Unity
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
        };

        // Ensure the zone stretches to fill available space
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;

        Content = _mainGrid;
    }

    public void SetContent(TabbedEditorPanel tabbedPanel) {
        if (!_isLeaf) {
            return; // Can't set content on split zones
        }

        _tabbedPanel = tabbedPanel;
        _mainGrid.Children.Clear();
        _mainGrid.Children.Add(tabbedPanel);
        
        // Ensure the tabbed panel fills the zone
        tabbedPanel.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        tabbedPanel.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
    }

    public TabbedEditorPanel? GetContent() {
        return _tabbedPanel;
    }

    public void RemoveContent() {
        if (_tabbedPanel != null) {
            _mainGrid.Children.Remove(_tabbedPanel);
            _tabbedPanel = null;
        }
    }

    public DockZone Split(DockPosition position, TabbedEditorPanel newPanel) {
        if (!_isLeaf) {
            return null; // Already split
        }

        _isLeaf = false;
        var currentContent = _tabbedPanel;
        RemoveContent();

        // Clear the main grid and set up for splitting
        _mainGrid.Children.Clear();
        _mainGrid.RowDefinitions.Clear();
        _mainGrid.ColumnDefinitions.Clear();

        var newZone = new DockZone();
        newZone.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        newZone.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
        
        var existingZone = new DockZone();
        existingZone.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        existingZone.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;

        // Move current content to existing zone
        if (currentContent != null) {
            existingZone.SetContent(currentContent);
        }

        // Set new content to new zone
        newZone.SetContent(newPanel);

        switch (position) {
            case DockPosition.Left:
                _mainGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _mainGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                _mainGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

                Grid.SetColumn(newZone, 0);
                Grid.SetColumn(existingZone, 2);

                _splitter = new GridSplitter {
                    Background = Brushes.Gray,
                    Width = 4,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
                };
                Grid.SetColumn(_splitter, 1);

                _leftChild = newZone;
                _rightChild = existingZone;
                break;

            case DockPosition.Right:
                _mainGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _mainGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                _mainGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

                Grid.SetColumn(existingZone, 0);
                Grid.SetColumn(newZone, 2);

                _splitter = new GridSplitter {
                    Background = Brushes.Gray,
                    Width = 4,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
                };
                Grid.SetColumn(_splitter, 1);

                _leftChild = existingZone;
                _rightChild = newZone;
                break;

            case DockPosition.Top:
                _mainGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
                _mainGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                _mainGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));

                Grid.SetRow(newZone, 0);
                Grid.SetRow(existingZone, 2);

                _splitter = new GridSplitter {
                    Background = Brushes.Gray,
                    Height = 4,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };
                Grid.SetRow(_splitter, 1);

                _topChild = newZone;
                _bottomChild = existingZone;
                break;

            case DockPosition.Bottom:
                _mainGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
                _mainGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                _mainGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));

                Grid.SetRow(existingZone, 0);
                Grid.SetRow(newZone, 2);

                _splitter = new GridSplitter {
                    Background = Brushes.Gray,
                    Height = 4,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };
                Grid.SetRow(_splitter, 1);

                _topChild = existingZone;
                _bottomChild = newZone;
                break;
        }

        _mainGrid.Children.Add(newZone);
        _mainGrid.Children.Add(existingZone);
        if (_splitter != null) {
            _mainGrid.Children.Add(_splitter);
        }

        return newZone;
    }

    public void Cleanup() {
        // Remove empty child zones and collapse the hierarchy
        if (_isLeaf) return;

        var children = new[] { _leftChild, _rightChild, _topChild, _bottomChild }
            .Where(c => c != null).ToList();

        var nonEmptyChildren = children.Where(c => c.HasContent).ToList();

        if (nonEmptyChildren.Count == 0) {
            // All children are empty, make this a leaf
            MakeLeaf();
        } else if (nonEmptyChildren.Count == 1) {
            // Only one child has content, collapse
            var contentChild = nonEmptyChildren.First();
            if (contentChild._isLeaf && contentChild._tabbedPanel != null) {
                // Move content up and make this a leaf
                var content = contentChild._tabbedPanel;
                contentChild.RemoveContent();
                MakeLeaf();
                SetContent(content);
            }
        }
    }

    private void MakeLeaf() {
        _isLeaf = true;
        _leftChild = null;
        _rightChild = null;
        _topChild = null;
        _bottomChild = null;
        _splitter = null;

        _mainGrid.Children.Clear();
        _mainGrid.RowDefinitions.Clear();
        _mainGrid.ColumnDefinitions.Clear();
    }

    public DockZone? FindEmptyZone() {
        if (_isLeaf) {
            return IsEmpty ? this : null;
        }

        var children = new[] { _leftChild, _rightChild, _topChild, _bottomChild }
            .Where(c => c != null);

        foreach (var child in children) {
            var emptyZone = child.FindEmptyZone();
            if (emptyZone != null) {
                return emptyZone;
            }
        }

        return null;
    }
}
