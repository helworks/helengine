using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.IO;
using System.Linq;
using helengine.ui.Models;
using helengine.ui.Services;
using helengine.ui.Theming;

namespace helengine.ui.Controls {
    public class NewProjectControl : UserControl {
        private readonly ProjectManager _projectManager;
        private TextBox? _projectNameTextBox;
        private TextBox? _projectPathTextBox;
        private TextBlock? _errorText;

        public event EventHandler<Project>? ProjectCreated;
        public event EventHandler? BackRequested;

        public NewProjectControl() {
            _projectManager = new ProjectManager();
            InitializeComponent();
        }

        private void InitializeComponent() {
            var scrollViewer = new ScrollViewer {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(20)
            };

            var panel = new StackPanel {
                Margin = new Thickness(20),
                Spacing = 20,
                MaxWidth = 600,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Back button
            var backButton = ThemedButton.Create(
                text: "← back",
                normalBg: ThemeManager.Colors.AccentTertiary,
                normalBorder: ThemeManager.Colors.AccentQuaternary,
                normalFore: ThemeManager.Colors.TextOnAccent,
                hoverBg: ThemeManager.Colors.AccentSecondary,
                hoverBorder: ThemeManager.Colors.AccentPrimary,
                hoverFore: ThemeManager.Colors.TextOnAccent
            );
            backButton.HorizontalAlignment = HorizontalAlignment.Left;
            backButton.Click += (s, e) => BackRequested?.Invoke(this, EventArgs.Empty);
            panel.Children.Add(backButton);

            // Project name section
            var nameLabel = new TextBlock {
                Text = "name:",
                FontSize = 14,
                Foreground = ThemeManager.Brushes.AccentSecondary,
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 10, 0, 5)
            };
            panel.Children.Add(nameLabel);

            _projectNameTextBox = new TextBox {
                Height = 50,
                MinWidth = 300,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = new SolidColorBrush(Color.FromRgb(15, 15, 15)), // Dark background
                Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0)), // Bright green text
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 255, 0)), // Green border
                BorderThickness = new Thickness(3),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                Padding = new Thickness(15),
                CaretBrush = new SolidColorBrush(Color.FromRgb(0, 255, 0)), // Green caret
                SelectionBrush = new SolidColorBrush(Color.FromRgb(0, 100, 0)), // Dark green selection
                MaxLength = 50,
                IsEnabled = true,
                Focusable = true,
                AcceptsReturn = false,
                AcceptsTab = false,
                TextWrapping = TextWrapping.NoWrap,
                VerticalContentAlignment = VerticalAlignment.Center,
                Text = "MyAwesomeProject"
            };
            _projectNameTextBox.TextChanged += OnProjectNameChanged;
            _projectNameTextBox.GotFocus += (s, e) => {
                if (_projectNameTextBox.Text == "MyAwesomeProject") {
                    _projectNameTextBox.SelectAll();
                }
            };
            _projectNameTextBox.LostFocus += (s, e) => {
                if (string.IsNullOrWhiteSpace(_projectNameTextBox.Text)) {
                    _projectNameTextBox.Text = "MyAwesomeProject";
                }
            };
            panel.Children.Add(_projectNameTextBox);

            // Project path section
            var pathLabel = new TextBlock {
                Text = "folder:",
                FontSize = 14,
                Foreground = ThemeManager.Brushes.AccentSecondary,
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 15, 0, 5)
            };
            panel.Children.Add(pathLabel);

            var pathPanel = new DockPanel();
            
            var browseButton = ThemedButton.Create(
                text: "browse",
                normalBg: Color.FromRgb(102, 255, 153),
                normalBorder: Color.FromRgb(255, 255, 102),
                normalFore: Color.FromRgb(25, 15, 35),
                hoverBg: Color.FromRgb(255, 102, 204),
                hoverBorder: Color.FromRgb(102, 255, 255),
                hoverFore: Color.FromRgb(25, 15, 35)
            );
            browseButton.Width = 100;
            browseButton.Height = 50;
            browseButton.Click += OnBrowseClick;
            DockPanel.SetDock(browseButton, Dock.Right);
            pathPanel.Children.Add(browseButton);

            _projectPathTextBox = new TextBox {
                Height = 50,
                Background = new SolidColorBrush(Color.FromRgb(15, 15, 15)), // Dark background
                Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 0)), // Bright yellow text
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 255, 0)), // Yellow border
                BorderThickness = new Thickness(3),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 16,
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(0, 0, 10, 0),
                Padding = new Thickness(15),
                CaretBrush = new SolidColorBrush(Color.FromRgb(255, 255, 0)), // Yellow caret
                SelectionBrush = new SolidColorBrush(Color.FromRgb(100, 100, 0)), // Dark yellow selection
                IsEnabled = true,
                Focusable = true,
                IsReadOnly = false,
                AcceptsReturn = false,
                AcceptsTab = false,
                TextWrapping = TextWrapping.NoWrap,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _projectPathTextBox.TextChanged += OnProjectPathChanged;
            pathPanel.Children.Add(_projectPathTextBox);
            panel.Children.Add(pathPanel);

            // Error text
            _errorText = new TextBlock {
                Foreground = new SolidColorBrush(Color.FromRgb(255, 80, 80)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Margin = new Thickness(0, 10, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                IsVisible = false
            };
            panel.Children.Add(_errorText);

            // Create button
            var createButton = ThemedButton.Create(
                text: "create",
                normalBg: Color.FromRgb(255, 102, 204),
                normalBorder: Color.FromRgb(102, 255, 255),
                normalFore: Color.FromRgb(25, 15, 35),
                hoverBg: Color.FromRgb(102, 255, 255),
                hoverBorder: Color.FromRgb(255, 255, 102),
                hoverFore: Color.FromRgb(25, 15, 35)
            );
            createButton.Width = 200;
            createButton.Height = 45;
            createButton.FontSize = 14;
            createButton.HorizontalAlignment = HorizontalAlignment.Center;
            createButton.Margin = new Thickness(0, 20);
            createButton.IsEnabled = false;
            createButton.Tag = "CreateButton";
            createButton.Click += OnCreateProject;
            panel.Children.Add(createButton);

            // Set default path
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var defaultProjectsPath = Path.Combine(documentsPath, "helengine projects");
            _projectPathTextBox.Text = defaultProjectsPath;

            scrollViewer.Content = panel;
            Content = scrollViewer;
        }

        public void FocusNameField() {
            _projectNameTextBox?.Focus();
            _projectNameTextBox?.SelectAll();
        }

        public void ClearFields() {
            if (_projectNameTextBox != null) _projectNameTextBox.Text = "MyAwesomeProject";
            if (_errorText != null) _errorText.IsVisible = false;
        }

        private void OnProjectNameChanged(object? sender, TextChangedEventArgs e) {
            ValidateInput();
        }

        private void OnProjectPathChanged(object? sender, TextChangedEventArgs e) {
            ValidateInput();
        }

        private async void OnBrowseClick(object? sender, EventArgs e) {
            try {
                var dialog = new OpenFolderDialog {
                    Title = "select project folder"
                };

                var window = TopLevel.GetTopLevel(this) as Window;
                var result = await dialog.ShowAsync(window);
                if (!string.IsNullOrEmpty(result) && _projectPathTextBox != null) {
                    _projectPathTextBox.Text = result;
                }
            } catch (Exception ex) {
                ShowError($"error selecting folder: {ex.Message}");
            }
        }

        private async void OnCreateProject(object? sender, EventArgs e) {
            try {
                if (_projectNameTextBox == null || _projectPathTextBox == null) return;

                var projectName = _projectNameTextBox.Text?.Trim() ?? string.Empty;
                var projectPath = _projectPathTextBox.Text?.Trim() ?? string.Empty;
                var fullProjectPath = Path.Combine(projectPath, projectName);

                Directory.CreateDirectory(fullProjectPath);

                var project = await _projectManager.CreateProjectAsync(projectName, fullProjectPath);
                ProjectCreated?.Invoke(this, project);

            } catch (Exception ex) {
                ShowError($"error creating project: {ex.Message}");
            }
        }

        private bool ValidateInput() {
            if (_projectNameTextBox == null || _projectPathTextBox == null) return false;

            HideError();

            var projectName = _projectNameTextBox.Text?.Trim();
            var projectPath = _projectPathTextBox.Text?.Trim();

            if (string.IsNullOrEmpty(projectName) || projectName == "MyAwesomeProject") {
                ShowError("project name is required");
                EnableCreateButton(false);
                return false;
            }

            if (string.IsNullOrEmpty(projectPath)) {
                ShowError("project folder is required");
                EnableCreateButton(false);
                return false;
            }

            if (projectName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) {
                ShowError("project name contains invalid characters");
                EnableCreateButton(false);
                return false;
            }

            var fullProjectPath = Path.Combine(projectPath, projectName);

            if (Directory.Exists(fullProjectPath)) {
                ShowError("a folder with this name already exists");
                EnableCreateButton(false);
                return false;
            }

            if (!Directory.Exists(projectPath)) {
                try {
                    Directory.CreateDirectory(projectPath);
                } catch {
                    ShowError("cannot create project folder - check permissions");
                    EnableCreateButton(false);
                    return false;
                }
            }

            EnableCreateButton(true);
            return true;
        }

        private void ShowError(string message) {
            if (_errorText != null) {
                _errorText.Text = message;
                _errorText.IsVisible = true;
            }
        }

        private void HideError() {
            if (_errorText != null) {
                _errorText.IsVisible = false;
            }
        }

        private void EnableCreateButton(bool enabled) {
            if (Content is ScrollViewer scrollViewer && 
                scrollViewer.Content is StackPanel stackPanel) {
                
                var createButton = stackPanel.Children
                    .OfType<Button>()
                    .FirstOrDefault(b => b.Tag?.ToString() == "CreateButton");
                if (createButton != null) {
                    createButton.IsEnabled = enabled;
                }
            }
        }
    }
}
