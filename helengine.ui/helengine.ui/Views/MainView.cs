using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using helengine.ui.Controls;

namespace helengine.ui.Views {
    public class MainView : UserControl {
        private EditorPanel panel;

        public MainView() {
            Width = 1280;
            Height = 720;

            Background = new SolidColorBrush(Color.Parse("#8d31c2"));

            var canvas = new Canvas {
                //Background = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE))
            };
            Content = canvas;

            panel = new EditorPanel();

            Canvas.SetLeft(panel, 50);
            Canvas.SetTop(panel, 50);

            canvas.Children.Add(panel);
        }
    }
}
