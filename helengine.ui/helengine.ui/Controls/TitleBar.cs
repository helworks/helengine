using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using helengine.ui.Theming;

namespace helengine.ui.Controls {
    public class TitleBar : Border {
        private readonly TextBlock _titleText;

        public static readonly StyledProperty<string?> TitleProperty =
            AvaloniaProperty.Register<TitleBar, string?>(nameof(Title));

        public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

        public bool EnableMaximize { get; set; } = true;

        public TitleBar() {
            Background = ThemeManager.Brushes.BackgroundPrimary; // Match ProjectChooserWindow header
            Height = 36;
            Padding = new Thickness(8, 6);

            var root = new DockPanel { LastChildFill = false };

            _titleText = new TextBlock {
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14,
                FontWeight = FontWeight.SemiBold,
                Foreground = ThemeManager.Brushes.AccentPrimary,
                Text = Title ?? string.Empty
            };
            this.PropertyChanged += (s, e) => {
                if (e.Property == TitleProperty) {
                    _titleText.Text = Title ?? string.Empty;
                }
            };

            DockPanel.SetDock(_titleText, Dock.Left);
            root.Children.Add(_titleText);

            Child = root;

            PointerPressed += OnPointerPressed;
            DoubleTapped += (_, __) => ToggleMaximize();
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e) {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) {
                if (TopLevel.GetTopLevel(this) is Window w) {
                    w.BeginMoveDrag(e);
                }
            }
        }

        private void ToggleMaximize() {
            if (!EnableMaximize) return;
            if (TopLevel.GetTopLevel(this) is Window w) {
                w.WindowState = w.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
        }
    }
}
