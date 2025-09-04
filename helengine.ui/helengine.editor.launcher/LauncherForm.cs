using helengine;
using helengine.editor;
using helengine.sharpdx;
using Nucleus.Platform.Windows.Controls;

namespace helengine.editor.launcher {
    public partial class LauncherForm : Form {
        private TitleBarControl titleBarControl;
        private Thread thread;
        private bool closed;

        // UI entities using helengine.core components
        private Entity titleEntity;
        private ButtonUIElements createProjectButton;
        private ButtonUIElements browseProjectButton;
        private FontAsset uiFont;

        // Button constants
        private const int ButtonWidth = 300;
        private const int ButtonHeight = 60;
        private const int ButtonSpacing = 20;

        public LauncherForm() {
            InitializeComponent();
            initialize();
        }

        private void SetupTitleBar() {
            // Create and configure the title bar
            titleBarControl = new TitleBarControl();
            titleBarControl.Text = "helengine project manager";
            titleBarControl.Location = new Point(0, 0);
            titleBarControl.Width = this.Width;
            titleBarControl.TitleBarHeight = 24;

            this.Controls.Add(titleBarControl);
        }

        private void CreateLauncherUI() {
            // Create font asset for UI text
            Font font = new Font("Consolas", 16, FontStyle.Regular);
            uiFont = GDIFontProcessor.ImportFont(font);

            var colors = ThemeManager.Colors;
            
            // Calculate center positions
            int centerX = ClientSize.Width / 2;
            int centerY = ClientSize.Height / 2;

            // Create UI camera for rendering UI elements
            Entity sceneCam = new Entity();
            CameraComponent compCamera = new CameraComponent();
            compCamera.LayerMask = 0b1000000000000000; // UI layer
            compCamera.Viewport = new float4(0, 0, ClientSize.Width, ClientSize.Height);
            sceneCam.InitComponents();
            sceneCam.AddComponent(compCamera);

            // Title
            titleEntity = new Entity();
            titleEntity.LayerMask = 0b1000000000000000;
            titleEntity.Position = new float3(centerX - 100, centerY - 150, 0);
            titleEntity.Enabled = true;
            titleEntity.InitComponents();

            var titleText = new TextComponent();
            titleText.Text = "helengine";
            titleText.Color = new byte4(colors.TextPrimary.X, colors.TextPrimary.Y, colors.TextPrimary.Z, colors.TextPrimary.W);
            titleText.Size = new int2(200, 40);
            titleText.RenderOrder2D = 1;
            titleText.Font = uiFont;
            titleEntity.AddComponent(titleText);

            // Create Project Button using ButtonUtility
            float3 createButtonPos = new float3(centerX - ButtonWidth/2, centerY - ButtonHeight/2 - ButtonSpacing, 0);
            byte4 buttonBgColor = new byte4(colors.AccentSecondary.X, colors.AccentSecondary.Y, colors.AccentSecondary.Z, colors.AccentSecondary.W);
            byte4 textOnAccentColor = new byte4(colors.TextOnAccent.X, colors.TextOnAccent.Y, colors.TextOnAccent.Z, colors.TextOnAccent.W);
            
            createProjectButton = ButtonUtility.CreateButton(
                "create project",
                createButtonPos,
                new int2(ButtonWidth, ButtonHeight),
                uiFont,
                buttonBgColor,
                textOnAccentColor,
                OnCreateProjectClick
            );

            // Browse Project Button using ButtonUtility
            float3 browseButtonPos = new float3(centerX - ButtonWidth/2, centerY + ButtonHeight/2 + ButtonSpacing, 0);
            
            browseProjectButton = ButtonUtility.CreateButton(
                "browse project",
                browseButtonPos,
                new int2(ButtonWidth, ButtonHeight),
                uiFont,
                buttonBgColor,
                textOnAccentColor,
                OnBrowseProjectClick
            );
        }

        private void OnCreateProjectClick(int2 relPos, int2 delta, PointerInteraction state) {
            var colors = ThemeManager.Colors;
            byte4 hoverColor = new byte4(colors.AccentPrimary.X, colors.AccentPrimary.Y, colors.AccentPrimary.Z, colors.AccentPrimary.W);
            byte4 normalColor = new byte4(colors.AccentSecondary.X, colors.AccentSecondary.Y, colors.AccentSecondary.Z, colors.AccentSecondary.W);

            if (state == PointerInteraction.Release) {
                // Handle create project action
                MessageBox.Show("Create Project clicked!", "helengine", MessageBoxButtons.OK, MessageBoxIcon.Information);
                // Reset to normal color after click
                ButtonUtility.SetButtonNormalState(createProjectButton, normalColor);
            } else if (state == PointerInteraction.Hover) {
                // Change button color on hover
                ButtonUtility.SetButtonHoverState(createProjectButton, hoverColor);
            } else if (state == PointerInteraction.None) {
                // Reset to normal color when not hovering
                ButtonUtility.SetButtonNormalState(createProjectButton, normalColor);
            }
        }

        private void OnBrowseProjectClick(int2 relPos, int2 delta, PointerInteraction state) {
            var colors = ThemeManager.Colors;
            byte4 hoverColor = new byte4(colors.AccentPrimary.X, colors.AccentPrimary.Y, colors.AccentPrimary.Z, colors.AccentPrimary.W);
            byte4 normalColor = new byte4(colors.AccentSecondary.X, colors.AccentSecondary.Y, colors.AccentSecondary.Z, colors.AccentSecondary.W);

            if (state == PointerInteraction.Release) {
                // Handle browse project action
                MessageBox.Show("Browse Project clicked!", "helengine", MessageBoxButtons.OK, MessageBoxIcon.Information);
                // Reset to normal color after click
                ButtonUtility.SetButtonNormalState(browseProjectButton, normalColor);
            } else if (state == PointerInteraction.Hover) {
                // Change button color on hover
                ButtonUtility.SetButtonHoverState(browseProjectButton, hoverColor);
            } else if (state == PointerInteraction.None) {
                // Reset to normal color when not hovering
                ButtonUtility.SetButtonNormalState(browseProjectButton, normalColor);
            }
        }

        private void initialize() {
            SetupTitleBar();

            Core core = new Core();
            core.Initialize(new SharpDXRenderManager(), new InputManagerWindows(this.Handle));

            core.RenderManager.AddWindow(this.Handle, ClientSize.Width - 1, ClientSize.Height);

            // Create the launcher UI using helengine.core components
            CreateLauncherUI();

            thread = new Thread(threadUpdate);
            thread.Start();
        }

        private void threadUpdate() {
            TimeSpan span = TimeSpan.FromMilliseconds(1000 / 120.0);
            for (; ; ) {
                Thread.Sleep(span);
                if (closed) {
                    break;
                }

                try {
                    Invoke(() => {
                        Core.Instance.Update();
                        Core.Instance.Draw();
                    });
                } catch { }
            }
        }

        protected override void OnClosed(EventArgs e) {
            base.OnClosed(e);

            closed = true;
            Core.Instance.Dispose();
        }

        protected override void OnActivated(EventArgs e) {
            base.OnActivated(e);
        }

        protected override void OnDeactivate(EventArgs e) {
            base.OnDeactivate(e);
        }

        protected override void OnResize(EventArgs e) {
            base.OnResize(e);
            
            // Update titlebar width
            if (titleBarControl != null) {
                titleBarControl.Width = this.Width;
            }

            // Recalculate UI positions on resize
            if (titleEntity != null) {
                int centerX = ClientSize.Width / 2;
                int centerY = ClientSize.Height / 2;
                
                titleEntity.Position = new float3(centerX - 100, centerY - 150, 0);
                
                // Update button positions using ButtonUtility
                float3 createButtonPos = new float3(centerX - ButtonWidth/2, centerY - ButtonHeight/2 - ButtonSpacing, 0);
                float3 browseButtonPos = new float3(centerX - ButtonWidth/2, centerY + ButtonHeight/2 + ButtonSpacing, 0);
                
                ButtonUtility.UpdateButtonPosition(createProjectButton, createButtonPos);
                ButtonUtility.UpdateButtonPosition(browseProjectButton, browseButtonPos);
            }
        }
    }
}
