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

            // Create custom panel container with left and right areas
            var panelContainer = new PanelContainer();
            Content = panelContainer;

            // Left panels (multiple for testing tabs)
            panel = new EditorPanel();
            panel.Title = "assets";
            var assetManager = new AssetManagerControl();
            panel.Child = assetManager;
            panelContainer.AssignPanelToArea(panel, "Left");
            
            // Right panels with scene view and additional panels
            sceneView = new EditorPanel();
            sceneView.Title = "scene";
            control = new D3D11Control();
            sceneView.Child = control;
            panelContainer.AssignPanelToArea(sceneView, "Right");
            
            var gamePanel = new EditorPanel();
            gamePanel.Title = "game";
            panelContainer.AssignPanelToArea(gamePanel, "Right");

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
