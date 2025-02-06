using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using helengine.ui.Controls;
using System;
using System.Threading;

namespace helengine.ui.Views {
    public class MainWindow : Window {
        private EditorPanel panel;
        private EditorPanel sceneView;

        private D3D11Control control;
        private Thread thread;
        private bool closed;

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
            sceneView.Title = "scene";

            control = new D3D11Control();
            sceneView.Child = control;

            Canvas.SetLeft(panel, 50);
            Canvas.SetTop(panel, 50);
            Canvas.SetLeft(sceneView, 200);
            Canvas.SetTop(sceneView, 200);

            canvas.Children.Add(panel);
            canvas.Children.Add(sceneView);

            thread = new Thread(threadUpdate);
            thread.Start();
        }

        protected override void OnClosed(EventArgs e) {
            closed = true;
            base.OnClosed(e);
        }

        private void threadUpdate() {
            TimeSpan span = TimeSpan.FromMilliseconds(1000 / 60.0);
            for (; ; ) {
                if (closed) {
                    break;
                }

                Thread.Sleep(span);
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    control.QueueNextFrame();
                });
            }
        }
    }
}
