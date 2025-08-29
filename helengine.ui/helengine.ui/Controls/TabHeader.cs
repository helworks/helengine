using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls.Shapes;
using helengine.ui.Theming;

namespace helengine.ui.Controls {
    public class TabHeader : Canvas {
        private readonly List<TabButton> _tabs = new List<TabButton>();
        private TabButton? _activeTab;
        private readonly Rectangle _dockPreview = new Rectangle();

        public event EventHandler<TabSelectedEventArgs>? TabSelected;
        public event EventHandler<TabDragEventArgs>? TabDragStarted;
        public event EventHandler<PanelDropEventArgs>? PanelDropped;
        
        public TabHeader() {
            _dockPreview.IsHitTestVisible = false;
            _dockPreview.IsVisible = false;
            _dockPreview.ZIndex = 1000;
            _dockPreview.Stroke = new SolidColorBrush(Color.FromRgb(64, 128, 255));
            _dockPreview.StrokeThickness = 2;
            _dockPreview.Fill = new SolidColorBrush(Color.FromArgb(64, 64, 128, 255));
            _dockPreview.Height = 25;
            Children.Add(_dockPreview);
        }

        protected override void OnSizeChanged(SizeChangedEventArgs e) {
            base.OnSizeChanged(e);

            _dockPreview.Width = e.NewSize.Width;
        }

        public void ShowPreview() {
            _dockPreview.Width = Bounds.Width > 0 ? Bounds.Width : Width;
            _dockPreview.Height = 25;
            Canvas.SetLeft(_dockPreview, 0);
            Canvas.SetTop(_dockPreview, 0);
            _dockPreview.IsVisible = true;
        }

        public void HidePreview() {
            _dockPreview.IsVisible = false;
        }

        public void AddTab(string title, object tag) {
            var tabButton = new TabButton(title, tag);
            tabButton.Clicked += OnTabClicked;
            tabButton.TabDragStarted += OnTabDragStarted;
            
            _tabs.Add(tabButton);
            Children.Add(tabButton);
            
            // Set as active if it's the first tab
            if (_tabs.Count == 1) {
                SetActiveTab(tabButton);
            }
            
            UpdateTabLayout();
        }
        
        public void RemoveTab(object tag) {
            var tab = _tabs.FirstOrDefault(t => t.Tag == tag);
            if (tab != null) {
                _tabs.Remove(tab);
                Children.Remove(tab);
                
                // If removing active tab, activate another one
                if (tab == _activeTab && _tabs.Count > 0) {
                    SetActiveTab(_tabs[0]);
                }
                
                UpdateTabLayout();
            }
        }
        
        private void OnTabClicked(object? sender, EventArgs e) {
            if (sender is TabButton tabButton) {
                SetActiveTab(tabButton);
            }
        }
        
        private void OnTabDragStarted(object? sender, TabDragEventArgs e) {
            TabDragStarted?.Invoke(this, e);
        }
        
        private void SetActiveTab(TabButton tab) {
            if (_activeTab != null) {
                _activeTab.IsActive = false;
            }
            
            _activeTab = tab;
            tab.IsActive = true;
            
            TabSelected?.Invoke(this, new TabSelectedEventArgs(tab.Tag));
        }
        
        private void UpdateTabLayout() {
            const double tabWidth = 120;
            double currentX = 0;
            
            foreach (var tab in _tabs) {
                SetLeft(tab, currentX);
                SetTop(tab, 0);
                tab.Width = tabWidth;
                tab.Height = 25;
                currentX += tabWidth;
            }
        }
    }
    
    public class TabButton : Border {
        private readonly TextBlock _textBlock;
        private bool _isActive;
        private bool _isDragging;
        private Point _initialPointerPosition;
        
        public string Title { get; }
        public new object Tag { get; }
        
        public event EventHandler? Clicked;
        public event EventHandler<TabDragEventArgs>? TabDragStarted;
        
        public bool IsActive {
            get => _isActive;
            set {
                _isActive = value;
                UpdateAppearance();
            }
        }
        
        public TabButton(string title, object tag) {
            Title = title;
            Tag = tag;
            
            _textBlock = new TextBlock {
                Text = title,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Foreground = Brushes.White,
                FontSize = 11
            };
            
            Child = _textBlock;
            CornerRadius = new CornerRadius(4, 4, 0, 0);
            BorderThickness = new Thickness(1, 1, 1, 0);
            BorderBrush = ThemeManager.Brushes.AccentTertiary;
            
            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
            UpdateAppearance();
        }
        
        private void OnPointerPressed(object? sender, PointerPressedEventArgs e) {
            _initialPointerPosition = e.GetPosition(this);
            _isDragging = false;
            e.Pointer.Capture(this);
        }
        
        private void OnPointerMoved(object? sender, PointerEventArgs e) {
            if (e.Pointer.Captured == this && !_isDragging) {
                var currentPosition = e.GetPosition(this);
                var deltaX = Math.Abs(currentPosition.X - _initialPointerPosition.X);
                var deltaY = Math.Abs(currentPosition.Y - _initialPointerPosition.Y);
                
                // Start dragging if moved more than threshold (like Unity)
                if (deltaX > 5 || deltaY > 5) {
                    _isDragging = true;
                    TabDragStarted?.Invoke(this, new TabDragEventArgs(Tag));
                }
            }
        }
        
        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e) {
            if (e.Pointer.Captured == this) {
                e.Pointer.Capture(null);
                
                // Only fire click if we weren't dragging
                if (!_isDragging) {
                    Clicked?.Invoke(this, EventArgs.Empty);
                }
                
                _isDragging = false;
            }
        }
        
        private void UpdateAppearance() {
            if (_isActive) {
                Background = ThemeManager.Brushes.AccentPrimary;
                _textBlock.Foreground = Brushes.White;
            } else {
                Background = ThemeManager.Brushes.AccentSecondary;
                _textBlock.Foreground = ThemeManager.Brushes.AccentQuaternary;
            }
        }
    }
    
    public class TabSelectedEventArgs : EventArgs {
        public object Tag { get; }
        
        public TabSelectedEventArgs(object tag) {
            Tag = tag;
        }
    }
    
    public class TabDragEventArgs : EventArgs {
        public object Tag { get; }
        
        public TabDragEventArgs(object tag) {
            Tag = tag;
        }
    }
    
    public class PanelDropEventArgs : EventArgs {
        public EditorPanel Panel { get; }
        
        public PanelDropEventArgs(EditorPanel panel) {
            Panel = panel;
        }
    }
}
