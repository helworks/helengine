using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Input;
using System.Linq;
using System.Threading.Tasks;

namespace helengine.ui.Controls;

public class EditorPanel : UserControl {
    private bool _isDragging;
    private Point _initialPointerPosition;
    private double _initialLeft;
    private double _initialTop;
    private TranslateTransform _transform = new TranslateTransform();
    private Border _header;
    private TextBlock title;
    private Border content;

    private int borderSize = 4;
    private bool _isDocked = false;

    public string Title {
        get { return this.title.Text; }
        set { this.title.Text = value; }
    }

    public bool IsDocked {
        get { return _isDocked; }
        set {
            _isDocked = value;
            UpdateHeaderVisibility();
        }
    }

    public Control Child {
        get { return this.content.Child; }
        set {
            content.Child = value;
            value.Width = content.Width - borderSize;
            value.Height = content.Height - 2;
        }
    }

    public Size Size {
        get { return new Size(content.Width, content.Height); }
        set {
            content.Width = value.Width;
            content.Height = value.Height;
            if (Child != null) {
                Child.Width = value.Width;
                Child.Height = value.Height;
            }
        }
    }

    public EditorPanel() {
        BuildVisualTree();

        InitializeDragLogic();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e) {
        base.OnSizeChanged(e);

        UpdateSize(e.NewSize);
    }

    private void UpdateSize(Size size) {
        content.Width = size.Width;
        content.Height = size.Height;
        if (Child != null) {
            Child.Width = size.Width;
            Child.Height = size.Height;
        }
    }

    private void BuildVisualTree() {
        // Create window structure
        var mainBorder = new Border {
            Background = Brushes.Gray,
            CornerRadius = new CornerRadius(4)
        };

        var stackPanel = new StackPanel();

        // Header
        _header = new Border {
            Background = new SolidColorBrush(Color.Parse("#c231af")),
            Height = 20,
            CornerRadius = new CornerRadius(4, 4, 0, 0),
            BorderThickness = new Thickness(2, 2, 2, 0),
            BorderBrush = new SolidColorBrush(Color.Parse("#4431c2"))
        };

        title = new TextBlock {
            Text = "Window Title",
            Foreground = Brushes.White,
            Margin = new Thickness(10, 0)
        };

        _header.Child = title;

        // Content
        content = new Border {
            Background = Brushes.White,
            Height = 200,
            Width = 300,
            CornerRadius = new CornerRadius(0, 0, 4, 4),
            BorderThickness = new Thickness(2, 0, 2, 2),
            BorderBrush = new SolidColorBrush(Color.Parse("#4431c2"))
        };

        // Assemble hierarchy
        stackPanel.Children.Add(_header);
        stackPanel.Children.Add(content);
        mainBorder.Child = stackPanel;

        Content = mainBorder;
        RenderTransform = _transform;
    }

    private void UpdateHeaderVisibility() {
        if (_header != null) {
            _header.IsVisible = !_isDocked;

            // Update content corner radius based on docked state
            if (content != null) {
                if (_isDocked) {
                    // When docked, content should have no rounded corners (tabs will handle top styling)
                    content.CornerRadius = new CornerRadius(0);
                    content.BorderThickness = new Thickness(0);
                } else {
                    // When floating, restore original styling
                    content.CornerRadius = new CornerRadius(0, 0, 4, 4);
                    content.BorderThickness = new Thickness(2, 0, 2, 2);
                }
            }
        }
    }


    private void InitializeDragLogic() {
        _header.PointerPressed += (sender, e) => {
            if (!_isDragging) {
                _isDragging = true;

                _initialPointerPosition = e.GetPosition(Parent as Control);
                _initialLeft = Canvas.GetLeft(this);
                _initialTop = Canvas.GetTop(this);

                BringToFront();
                e.Pointer.Capture(_header);
            }
        };

        _header.PointerReleased += (sender, e) => {
            if (_isDragging) {
                _isDragging = false;
                e.Pointer.Capture(null);

                // Check if we should dock when releasing
                if (!_isDocked && Parent is PanelContainer panelContainer) {
                    var currentPosition = e.GetPosition(Parent as Control);
                    var targetArea = panelContainer.GetHeaderAtPosition(currentPosition);
                    if (targetArea != null) {
                        // Dock the panel
                        targetArea.AddPanel(this);
                        panelContainer.InvalidateArrange();
                    }
                    // Hide preview regardless
                    panelContainer.HideDockPreview();
                }
            }
        };

        _header.PointerMoved += (sender, e) => {
            if (_isDragging && e.Pointer.Captured == _header) {
                var currentPosition = e.GetPosition(Parent as Control);

                // Calculate delta from initial click
                var deltaX = currentPosition.X - _initialPointerPosition.X;
                var deltaY = currentPosition.Y - _initialPointerPosition.Y;

                // Update Canvas positions directly
                Canvas.SetLeft(this, _initialLeft + deltaX);
                Canvas.SetTop(this, _initialTop + deltaY);

                // Check for dock proximity if this is a floating panel (use header zone)
                if (!_isDocked && Parent is PanelContainer panelContainer) {
                    panelContainer.CheckDockProximity(this);
                }
            }
        };
    }

    private void BringToFront() {
        if (Parent is Canvas canvas) {
            var maxZ = canvas.Children.OfType<Control>().Max(c => c.ZIndex) + 1;
            this.ZIndex = maxZ;
        }
    }
}
