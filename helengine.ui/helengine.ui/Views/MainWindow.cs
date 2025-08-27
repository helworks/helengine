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

            // Create individual panels
            panel = new EditorPanel();
            panel.Title = "Editor";
            panel.Child = new TextBlock { Text = "Editor Panel Content", Margin = new Thickness(10) };

            sceneView = new EditorPanel();
            sceneView.Size = new Size(640, 480);
            sceneView.Title = "Scene";

            control = new D3D11Control();
            sceneView.Child = control;

            // Create another panel for demonstration
            var propertiesPanel = new EditorPanel();
            propertiesPanel.Title = "Properties";
            propertiesPanel.Child = new TextBlock { Text = "Properties Panel Content", Margin = new Thickness(10) };

            // Create custom tabbed panel and add all panels to it
            var tabbedPanel = new TabbedEditorPanel();
            tabbedPanel.Size = new Size(700, 500);
            
            tabbedPanel.AddPanel(panel);
            tabbedPanel.AddPanel(sceneView);
            tabbedPanel.AddPanel(propertiesPanel);

            Canvas.SetLeft(tabbedPanel, 50);
            Canvas.SetTop(tabbedPanel, 50);

            // Create a standalone panel to show the difference (with titlebar)
            var standalonePanel = new EditorPanel();
            standalonePanel.Title = "Standalone";
            standalonePanel.Size = new Size(250, 150);
            standalonePanel.Child = new TextBlock { Text = "This panel has a titlebar", Margin = new Thickness(10) };

            Canvas.SetLeft(standalonePanel, 800);
            Canvas.SetTop(standalonePanel, 50);

            canvas.Children.Add(tabbedPanel);
            canvas.Children.Add(standalonePanel);

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
