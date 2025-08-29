using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace helengine.ui.Controls {
    public class ThemedButton : Button {
        public static readonly StyledProperty<IBrush?> NormalBackgroundProperty =
            AvaloniaProperty.Register<ThemedButton, IBrush?>(nameof(NormalBackground));

        public static readonly StyledProperty<IBrush?> HoverBackgroundProperty =
            AvaloniaProperty.Register<ThemedButton, IBrush?>(nameof(HoverBackground));

        public static readonly StyledProperty<IBrush?> NormalBorderBrushProperty =
            AvaloniaProperty.Register<ThemedButton, IBrush?>(nameof(NormalBorderBrush));

        public static readonly StyledProperty<IBrush?> HoverBorderBrushProperty =
            AvaloniaProperty.Register<ThemedButton, IBrush?>(nameof(HoverBorderBrush));

        public static readonly StyledProperty<IBrush?> NormalForegroundProperty =
            AvaloniaProperty.Register<ThemedButton, IBrush?>(nameof(NormalForeground));

        public static readonly StyledProperty<IBrush?> HoverForegroundProperty =
            AvaloniaProperty.Register<ThemedButton, IBrush?>(nameof(HoverForeground));

        public IBrush? NormalBackground { get => GetValue(NormalBackgroundProperty); set => SetValue(NormalBackgroundProperty, value); }
        public IBrush? HoverBackground { get => GetValue(HoverBackgroundProperty); set => SetValue(HoverBackgroundProperty, value); }
        public IBrush? NormalBorderBrush { get => GetValue(NormalBorderBrushProperty); set => SetValue(NormalBorderBrushProperty, value); }
        public IBrush? HoverBorderBrush { get => GetValue(HoverBorderBrushProperty); set => SetValue(HoverBorderBrushProperty, value); }
        public IBrush? NormalForeground { get => GetValue(NormalForegroundProperty); set => SetValue(NormalForegroundProperty, value); }
        public IBrush? HoverForeground { get => GetValue(HoverForegroundProperty); set => SetValue(HoverForegroundProperty, value); }

        public ThemedButton() {
            FontFamily = new FontFamily("Consolas");
            FontWeight = FontWeight.Bold;
            BorderThickness = new Thickness(2);
            Padding = new Thickness(15, 8);
            Cursor = new Cursor(StandardCursorType.Hand);

            PointerEntered += (_, __) => ApplyHover(true);
            PointerExited += (_, __) => ApplyHover(false);
            AttachedToVisualTree += (_, __) => ApplyHover(false);
        }

        private void ApplyHover(bool isHover) {
            if (isHover) {
                if (HoverBackground != null) Background = HoverBackground;
                if (HoverBorderBrush != null) BorderBrush = HoverBorderBrush;
                if (HoverForeground != null) Foreground = HoverForeground;
            } else {
                if (NormalBackground != null) Background = NormalBackground;
                if (NormalBorderBrush != null) BorderBrush = NormalBorderBrush;
                if (NormalForeground != null) Foreground = NormalForeground;
            }
        }

        public static ThemedButton Create(
            string text,
            Color normalBg,
            Color normalBorder,
            Color normalFore,
            Color hoverBg,
            Color hoverBorder,
            Color hoverFore
        ) {
            return new ThemedButton {
                Content = text,
                NormalBackground = new SolidColorBrush(normalBg),
                NormalBorderBrush = new SolidColorBrush(normalBorder),
                NormalForeground = new SolidColorBrush(normalFore),
                HoverBackground = new SolidColorBrush(hoverBg),
                HoverBorderBrush = new SolidColorBrush(hoverBorder),
                HoverForeground = new SolidColorBrush(hoverFore)
            };
        }
    }
}


