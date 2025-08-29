using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using helengine.ui.Controls;
using Avalonia.Platform;
using System;
using System.Threading;

namespace helengine.ui.Views {
    public class MainWindow : Window {
        private EditorPanel panel;
        private EditorPanel sceneView;

        private D3D11Control control;
        private Thread? thread;
        private bool closed;

        public MainWindow() {
            Title = "helengine - main editor";
            Width = 1280;
            Height = 720;

            // 90s deep purple background
            Background = new SolidColorBrush(Color.FromRgb(25, 15, 35));

            // Hide system titlebar and extend client area
            CanResize = true;
            SystemDecorations = SystemDecorations.BorderOnly;
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.PreferSystemChrome;
            ExtendClientAreaTitleBarHeightHint = 36;

            // Root layout with TitleBar
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            root.RowDefinitions.Add(new RowDefinition(GridLength.Star));

            var titleBar = new TitleBar { Title = "helengine" };
            Grid.SetRow(titleBar, 0);
            root.Children.Add(titleBar);

            // Create custom panel container with left and right areas
            var panelContainer = new PanelContainer();
            Grid.SetRow(panelContainer, 1);
            root.Children.Add(panelContainer);
            Content = root;

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

            // Start the update thread only after the window is properly opened
            Opened += OnWindowOpened;
        }

        private void OnWindowOpened(object? sender, EventArgs e) {
            // Start the update thread now that the window is fully initialized
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
                    // Check if control is still valid and not disposed
                    if (control != null && !closed) {
                        try {
                            control.QueueNextFrame();
                        } catch {
                            // Ignore any exceptions during frame updates
                        }
                    }
                });
            }
        }
    }
}
