using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering;
using helengine.ui.Controls;

namespace helengine.ui.Views {
    public class MainWindow : Window {
        private EditorPanel panel;
        private EditorPanel sceneView;

        private D3D11Control control;

        public MainWindow() {
            Title = "helengine v0";
            Width = 1280;
            Height = 720;

            Background = new SolidColorBrush(Color.Parse("#8d31c2"));

            var canvas = new Canvas();
            Content = canvas;

            panel = new EditorPanel();

            sceneView = new EditorPanel();
            sceneView.Size = new Size(640, 480);

            control = new D3D11Control();
            sceneView.Title = "scene";
            sceneView.Child = control;

            Canvas.SetLeft(panel, 50);
            Canvas.SetTop(panel, 50);
            Canvas.SetLeft(sceneView, 200);
            Canvas.SetTop(sceneView, 200);

            canvas.Children.Add(panel);
            canvas.Children.Add(sceneView);
        }
    }
}
