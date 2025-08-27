using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;

namespace helengine.ui.Controls;

public class TabHeader : UserControl {
    private Border _border;
    private TextBlock _titleText;
    private bool _isSelected;
    private bool _isHovered;

    public event EventHandler<EventArgs> TabClicked;
    public event EventHandler<EventArgs> CloseClicked;

    public string Title {
        get { return _titleText.Text; }
        set { _titleText.Text = value; }
    }

    public bool IsSelected {
        get { return _isSelected; }
        set {
            _isSelected = value;
            UpdateVisualState();
        }
    }

    public TabHeader() {
        BuildVisualTree();
        InitializeInteraction();
    }

    private void BuildVisualTree() {
        _border = new Border {
            Background = new SolidColorBrush(Color.Parse("#c231af")),
            BorderThickness = new Thickness(1, 1, 1, 0),
            BorderBrush = new SolidColorBrush(Color.Parse("#4431c2")),
            CornerRadius = new CornerRadius(4, 4, 0, 0),
            Padding = new Thickness(8, 4),
            MinWidth = 80,
            Height = 24
        };

        var stackPanel = new StackPanel {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 4
        };

        _titleText = new TextBlock {
            Text = "Tab",
            Foreground = Brushes.White,
            FontSize = 11,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        stackPanel.Children.Add(_titleText);
        _border.Child = stackPanel;

        Content = _border;
    }

    private void InitializeInteraction() {
        _border.PointerEntered += (s, e) => {
            _isHovered = true;
            UpdateVisualState();
        };

        _border.PointerExited += (s, e) => {
            _isHovered = false;
            UpdateVisualState();
        };

        _border.PointerPressed += (s, e) => {
            TabClicked?.Invoke(this, EventArgs.Empty);
        };
    }

    private void UpdateVisualState() {
        if (_isSelected) {
            _border.Background = new SolidColorBrush(Color.Parse("#d142bf")); // Lighter when selected
            _border.BorderThickness = new Thickness(2, 2, 2, 0);
        } else if (_isHovered) {
            _border.Background = new SolidColorBrush(Color.Parse("#b02a9f")); // Darker when hovered
            _border.BorderThickness = new Thickness(1, 1, 1, 0);
        } else {
            _border.Background = new SolidColorBrush(Color.Parse("#c231af")); // Default
            _border.BorderThickness = new Thickness(1, 1, 1, 0);
        }
    }
}
