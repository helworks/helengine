using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace helengine.ui.Controls {
    public class AssetManagerControl : UserControl {
        private Popup? _customPopup;

        public AssetManagerControl() {
            InitializeComponent();
            SetupCustomPopup();
            SetupEventHandlers();
        }

        private void InitializeComponent() {
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));

            // Ensure the control can receive input
            Focusable = true;
            IsHitTestVisible = true;

            // Create a simple placeholder content
            var textBlock = new TextBlock {
                Text = "Asset Manager\n\nRight-click to open context menu",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                FontSize = 14
            };

            Content = textBlock;
        }

        private void SetupCustomPopup() {
            // Create a custom popup
            _customPopup = new Popup();

            // Create a simple stack panel with menu items
            var stackPanel = new StackPanel {
                Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                MinWidth = 150
            };

            // Create buttons that look like menu items
            var newFolderBtn = CreateMenuButton("new folder", () => OnNewFolder());
            var importAssetBtn = CreateMenuButton("import asset", () => OnImportAsset());
            var refreshBtn = CreateMenuButton("refresh", () => OnRefresh());
            var deleteBtn = CreateMenuButton("delete", () => OnDelete());

            stackPanel.Children.Add(newFolderBtn);
            stackPanel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(200, 200, 200)) }); // separator
            stackPanel.Children.Add(importAssetBtn);
            stackPanel.Children.Add(refreshBtn);
            stackPanel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(200, 200, 200)) }); // separator
            stackPanel.Children.Add(deleteBtn);

            // Wrap in a border for better visibility
            var border = new Border {
                Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                BorderThickness = new Thickness(1),
                Child = stackPanel
            };

            _customPopup.Child = border;
            _customPopup.Placement = PlacementMode.Pointer;

            // Handle clicking outside to close the popup
            _customPopup.LostFocus += (sender, e) => {
                _customPopup.IsOpen = false;
            };

            // Add global pointer handler to close popup when clicking outside
            _customPopup.Opened += (sender, e) => {
                if (TopLevel.GetTopLevel(this) is TopLevel topLevel) {
                    topLevel.PointerPressed += OnGlobalPointerPressed;
                }
            };

            _customPopup.Closed += (sender, e) => {
                if (TopLevel.GetTopLevel(this) is TopLevel topLevel) {
                    topLevel.PointerPressed -= OnGlobalPointerPressed;
                }
            };

            System.Diagnostics.Debug.WriteLine("Custom popup setup complete");
        }

        private Button CreateMenuButton(string text, System.Action onClick) {
            var button = new Button {
                Content = text,
                Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 0)),
                BorderThickness = new Thickness(0),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                Padding = new Thickness(8, 4),
                FontSize = 12
            };

            button.Click += (sender, e) => {
                _customPopup!.IsOpen = false;
                onClick();
            };

            // Add hover effect
            button.PointerEntered += (sender, e) => {
                button.Background = new SolidColorBrush(Color.FromRgb(220, 220, 220));
            };
            button.PointerExited += (sender, e) => {
                button.Background = new SolidColorBrush(Color.FromRgb(240, 240, 240));
            };

            return button;
        }

        private void SetupEventHandlers() {
            // Handle right-click for context menu
            PointerPressed += OnPointerPressed;
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e) {
            var point = e.GetCurrentPoint(this);

            if (point.Properties.IsRightButtonPressed) {
                var position = point.Position;

                // Open the custom popup
                if (_customPopup != null) {
                    _customPopup.PlacementTarget = this;
                    _customPopup.IsOpen = true;
                }

                e.Handled = true;
            } else if (point.Properties.IsLeftButtonPressed) {
                // Close popup if it's open and we left-click anywhere in the asset browser
                if (_customPopup != null && _customPopup.IsOpen) {
                    _customPopup.IsOpen = false;
                }
            }
        }

        private void OnGlobalPointerPressed(object? sender, PointerPressedEventArgs e) {
            // Close popup if clicking outside of it
            if (_customPopup != null && _customPopup.IsOpen) {
                // Check if the click is on the popup itself
                var isClickOnPopup = false;
                if (_customPopup.Child is Control popupChild) {
                    var clickPosition = e.GetPosition(popupChild);
                    isClickOnPopup = popupChild.Bounds.Contains(clickPosition);
                }

                // Also check if click is on this control (to allow right-clicking again)
                var clickOnThisControl = false;
                try {
                    var clickPosition = e.GetPosition(this);
                    clickOnThisControl = this.Bounds.Contains(clickPosition);
                } catch {
                    // Ignore position errors
                }

                // Close popup if click is outside both the popup and this control
                if (!isClickOnPopup && !clickOnThisControl) {
                    _customPopup.IsOpen = false;
                }
            }
        }

        private void OnNewFolder() {
            // TODO: Implement new folder creation
        }

        private void OnImportAsset() {
            // TODO: Implement asset import
        }

        private void OnRefresh() {
            // TODO: Implement refresh functionality
        }

        private void OnDelete() {
            // TODO: Implement delete functionality
        }
    }
}
