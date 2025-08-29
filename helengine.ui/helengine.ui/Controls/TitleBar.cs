using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using helengine.ui.Theming;

namespace helengine.ui.Controls {
    public class TitleBar : Border {
        public static readonly StyledProperty<string?> TitleProperty =
            AvaloniaProperty.Register<TitleBar, string?>(nameof(Title));

        public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

        private readonly TextBlock _titleText;
        private readonly ThemedButton _minBtn;
        private readonly ThemedButton _maxBtn;
        private readonly ThemedButton _closeBtn;

        public bool EnableMaximize { get; set; } = true;

        public TitleBar() {
            Background = ThemeManager.Brushes.SurfacePrimary;
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

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            DockPanel.SetDock(btnPanel, Dock.Right);

            _minBtn = ThemedButton.Create("_",
                ThemeManager.Colors.SurfacePrimary, ThemeManager.Colors.AccentSecondary, ThemeManager.Colors.TextOnAccent,
                ThemeManager.Colors.AccentSecondary, ThemeManager.Colors.AccentQuaternary, ThemeManager.Colors.TextOnAccent);
            _minBtn.Width = 36; _minBtn.Height = 24; _minBtn.FontSize = 12;
            _minBtn.Click += (_, __) => {
                if (TopLevel.GetTopLevel(this) is Window w) w.WindowState = WindowState.Minimized;
            };

            _maxBtn = ThemedButton.Create("[]",
                ThemeManager.Colors.SurfacePrimary, ThemeManager.Colors.AccentSecondary, ThemeManager.Colors.TextOnAccent,
                ThemeManager.Colors.AccentSecondary, ThemeManager.Colors.AccentQuaternary, ThemeManager.Colors.TextOnAccent);
            _maxBtn.Width = 36; _maxBtn.Height = 24; _maxBtn.FontSize = 10;
            _maxBtn.Click += (_, __) => ToggleMaximize();

            _closeBtn = ThemedButton.Create("X",
                ThemeManager.Colors.AccentPrimary, ThemeManager.Colors.StateDanger, ThemeManager.Colors.TextOnAccent,
                ThemeManager.Colors.StateDanger, ThemeManager.Colors.AccentQuaternary, ThemeManager.Colors.TextOnAccent);
            _closeBtn.Width = 36; _closeBtn.Height = 24; _closeBtn.FontSize = 12;
            _closeBtn.Click += (_, __) => { if (TopLevel.GetTopLevel(this) is Window w) w.Close(); };

            btnPanel.Children.Add(_minBtn);
            btnPanel.Children.Add(_maxBtn);
            btnPanel.Children.Add(_closeBtn);

            root.Children.Add(btnPanel);

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


