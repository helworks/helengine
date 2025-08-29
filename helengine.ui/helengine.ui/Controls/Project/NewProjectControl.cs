using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.IO;
using System.Linq;
using helengine.ui.Theming;
using helengine.ui.managers;

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

            // Create a responsive grid layout
            var mainGrid = new Grid {
                Margin = new Thickness(40),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 1200 // Maximum width for very large screens
            };

            // Define columns: 10% margin, 80% content, 10% margin (90% total content)
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star))); // Left margin
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(8, GridUnitType.Star))); // Content (80% of total)
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star))); // Right margin

            var panel = new StackPanel {
                Spacing = 20,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            Grid.SetColumn(panel, 1); // Place in content column

            // Back button
            var backButton = ThemedButton.Create(
                text: "← back",
                normalBg: ThemeManager.Colors.AccentSecondary,  // Inactive tab color
                normalBorder: ThemeManager.Colors.AccentTertiary, // Tab border color
                normalFore: ThemeManager.Colors.AccentQuaternary, // Inactive tab text
                hoverBg: ThemeManager.Colors.AccentPrimary, // Active tab color
                hoverBorder: ThemeManager.Colors.AccentPrimary, // Active tab color
                hoverFore: ThemeManager.Colors.TextOnAccent
            );
            backButton.HorizontalAlignment = HorizontalAlignment.Left;
            backButton.Click += (s, e) => BackRequested?.Invoke(this, EventArgs.Empty);
            panel.Children.Add(backButton);

            // Project name section
            var nameLabel = new TextBlock {
                Text = "name",
                FontSize = 14,
                Foreground = ThemeManager.Brushes.AccentSecondary,
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 10, 0, 5)
            };
            panel.Children.Add(nameLabel);

            _projectNameTextBox = new TextBox {
                Height = 50,
                HorizontalAlignment = HorizontalAlignment.Stretch, // Fill available width
                Background = ThemeManager.Brushes.SurfacePrimary, // Use theme surface
                Foreground = ThemeManager.Brushes.AccentSecondary, // Cyan text to match theme
                BorderBrush = ThemeManager.Brushes.AccentSecondary, // Cyan border
                BorderThickness = new Thickness(3),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                Padding = new Thickness(15, 5),
                CaretBrush = ThemeManager.Brushes.AccentSecondary, // Cyan caret
                SelectionBrush = ThemeManager.Brushes.AccentTertiary, // Purple selection
                MaxLength = 50,
                IsEnabled = true,
                Focusable = true,
                AcceptsReturn = false,
                AcceptsTab = false,
                TextWrapping = TextWrapping.NoWrap,
                VerticalContentAlignment = VerticalAlignment.Center,
                Watermark = "project name"
            };
            _projectNameTextBox.TextChanged += OnProjectNameChanged;
            panel.Children.Add(_projectNameTextBox);

            // Project path section
            var pathLabel = new TextBlock {
                Text = "folder",
                FontSize = 14,
                Foreground = ThemeManager.Brushes.AccentSecondary,
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 15, 0, 5)
            };
            panel.Children.Add(pathLabel);

            var pathPanel = new DockPanel {
                HorizontalAlignment = HorizontalAlignment.Stretch // Fill available width
            };
            
            var browseButton = ThemedButton.Create(
                text: "browse",
                normalBg: ThemeManager.Colors.AccentSecondary,  // Inactive tab color
                normalBorder: ThemeManager.Colors.AccentTertiary, // Tab border color
                normalFore: ThemeManager.Colors.AccentQuaternary, // Inactive tab text
                hoverBg: ThemeManager.Colors.AccentPrimary, // Active tab color
                hoverBorder: ThemeManager.Colors.AccentPrimary, // Active tab color
                hoverFore: ThemeManager.Colors.TextOnAccent
            );
            browseButton.Width = 100;
            browseButton.Height = 50;
            browseButton.Padding = new Thickness(0); // Remove default padding for perfect centering
            browseButton.HorizontalContentAlignment = HorizontalAlignment.Center;
            browseButton.VerticalContentAlignment = VerticalAlignment.Center;
            browseButton.Click += OnBrowseClick;
            DockPanel.SetDock(browseButton, Dock.Right);
            pathPanel.Children.Add(browseButton);

            _projectPathTextBox = new TextBox {
                Height = 50,
                Background = ThemeManager.Brushes.SurfacePrimary, // Use theme surface
                Foreground = ThemeManager.Brushes.AccentSecondary, // Cyan text to match theme
                BorderBrush = ThemeManager.Brushes.AccentSecondary, // Cyan border
                BorderThickness = new Thickness(3),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14 ,
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(0, 0, 10, 0),
                Padding = new Thickness(15, 5),
                CaretBrush = ThemeManager.Brushes.AccentSecondary, // Cyan caret
                SelectionBrush = ThemeManager.Brushes.AccentTertiary, // Purple selection
                HorizontalAlignment = HorizontalAlignment.Stretch, // Fill available space
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
                normalBg: ThemeManager.Colors.AccentSecondary,  // Inactive tab color
                normalBorder: ThemeManager.Colors.AccentTertiary, // Tab border color
                normalFore: ThemeManager.Colors.AccentQuaternary, // Inactive tab text
                hoverBg: ThemeManager.Colors.AccentPrimary, // Active tab color
                hoverBorder: ThemeManager.Colors.AccentPrimary, // Active tab color
                hoverFore: ThemeManager.Colors.TextOnAccent
            );
            createButton.Width = 200;
            createButton.Height = 45;
            createButton.FontSize = 14;
            createButton.HorizontalAlignment = HorizontalAlignment.Center;
            createButton.Margin = new Thickness(0, 30, 0, 20);
            createButton.Padding = new Thickness(0); // Remove default padding for perfect centering
            createButton.HorizontalContentAlignment = HorizontalAlignment.Center;
            createButton.VerticalContentAlignment = VerticalAlignment.Center;
            createButton.IsEnabled = false;
            createButton.Tag = "CreateButton";
            createButton.Click += OnCreateProject;
            panel.Children.Add(createButton);

            // Set default path
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var defaultProjectsPath = Path.Combine(documentsPath, "helengine projects");
            _projectPathTextBox.Text = defaultProjectsPath;

            mainGrid.Children.Add(panel);
            scrollViewer.Content = mainGrid;
            Content = scrollViewer;
        }

        public void FocusNameField() {
            _projectNameTextBox?.Focus();
            _projectNameTextBox?.SelectAll();
        }

        public void ClearFields() {
            if (_projectNameTextBox != null) _projectNameTextBox.Text = "";
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
                scrollViewer.Content is Grid mainGrid) {

                // Find the panel in the main grid
                var panel = mainGrid.Children.FirstOrDefault(c => c is StackPanel) as StackPanel;
                if (panel != null) {
                    var createButton = panel.Children
                        .OfType<Button>()
                        .FirstOrDefault(b => b.Tag?.ToString() == "CreateButton");

                    if (createButton != null) {
                        createButton.IsEnabled = enabled;
                    }
                }
            }
        }
    }
}
