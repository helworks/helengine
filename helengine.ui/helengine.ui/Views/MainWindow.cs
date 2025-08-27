using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using helengine.ui.Controls;
using helengine.ui.Controls.Docking;
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

            Background = new SolidColorBrush(Color.Parse("#2d2d30")); // Unity-like dark theme

            // Create docking container as main content
            var dockingContainer = new DockingContainer();
            Content = dockingContainer;

            // Create individual panels
            panel = new EditorPanel();
            panel.Title = "Editor";
            panel.Child = new TextBlock { Text = "Editor Panel Content", Margin = new Thickness(10) };

            sceneView = new EditorPanel();
            sceneView.Title = "Scene";
            control = new D3D11Control();
            sceneView.Child = control;

            var propertiesPanel = new EditorPanel();
            propertiesPanel.Title = "Properties";
            propertiesPanel.Child = new TextBlock { Text = "Properties Panel Content", Margin = new Thickness(10) };

            var hierarchyPanel = new EditorPanel();
            hierarchyPanel.Title = "Hierarchy";
            hierarchyPanel.Child = new TextBlock { Text = "Hierarchy Panel Content", Margin = new Thickness(10) };

            // Create tabbed panels for docking
            var mainTabbedPanel = new TabbedEditorPanel();
            mainTabbedPanel.AddPanel(sceneView);
            mainTabbedPanel.AddPanel(panel);

            var sideTabbedPanel = new TabbedEditorPanel();
            sideTabbedPanel.AddPanel(propertiesPanel);
            sideTabbedPanel.AddPanel(hierarchyPanel);

            // Dock panels in different positions (Unity-style layout)
            dockingContainer.DockPanel(mainTabbedPanel, DockPosition.Center);
            dockingContainer.DockPanel(sideTabbedPanel, DockPosition.Right);

            // Handle tab undocking events to create floating panels
            mainTabbedPanel.TabUndocked += OnTabUndocked;
            sideTabbedPanel.TabUndocked += OnTabUndocked;

            thread = new Thread(threadUpdate);
            thread.Start();
        }

        private void OnTabUndocked(object? sender, TabUndockEventArgs e) {
            if (Content is DockingContainer dockingContainer) {
                // Force release capture on the source panel
                if (sender is TabbedEditorPanel sourcePanel) {
                    sourcePanel.ForceReleaseCapture();
                }
                
                // Create a new floating tabbed panel for the undocked tab
                var floatingPanel = new TabbedEditorPanel();
                floatingPanel.AddPanel(e.Panel);
                floatingPanel.TabUndocked += OnTabUndocked; // Handle further undocking

                // Undock it as a floating panel  
                dockingContainer.UndockPanel(floatingPanel, e.Position);
                
                // Ensure the floating panel gets focus for dragging
                Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                    floatingPanel.Focus();
                }, Avalonia.Threading.DispatcherPriority.Background);
            }
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
