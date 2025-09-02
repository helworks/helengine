
using helengine.sharpdx;

namespace helengine.editor.app {
    public partial class MainForm : Form {
        private Thread thread;
        private bool closed;

        public MainForm() {
            InitializeComponent();

            initialize();
        }

        private void makeStartScene() {
            Core core = Core.Instance;
            EditorEntity cube = new EditorEntity();
            cube.LayerMask = 0b0100000000000000;
            MeshComponent mesh = new MeshComponent();
            cube.AddComponent(mesh);
            ModelAsset modelData = ModelUtils.GenerateCubeMesh(float3.Zero, float3.One);
            RuntimeModel renderData = core.RenderManager.BuildModelFromRaw(modelData);
            mesh.Model = renderData;

            EditorEntity plane = new EditorEntity();
            plane.LayerMask = 0b0100000000000000;
            plane.Scale = new float3(10, 1, 10);
            MeshComponent planeMesh = new MeshComponent();
            plane.AddComponent(planeMesh);
            ModelAsset planeModelData = ModelUtils.GeneratePlaneMesh(float3.Zero, float3.One);
            RuntimeModel planeRenderData = core.RenderManager.BuildModelFromRaw(planeModelData);
            planeMesh.Model = planeRenderData;
        }

        private void initialize() {
            EditorCore core = new EditorCore(null);
            core.Initialize(new SharpDXRenderManager(), new InputManagerWindows(this.Handle));

            core.RenderManager.AddWindow(this.Handle, ClientSize.Width - 1, ClientSize.Height);

            Font font = new Font("Consolas", 16, FontStyle.Regular);
            FontAsset fontAsset = GDIFontProcessor.ImportFont(font);

            EditorEntity uiCam = new EditorEntity();
            uiCam.Position = new float3(0, 3, -8);
            CameraComponent compUiCamera = new CameraComponent();
            compUiCamera.LayerMask = 0b1000000000000000;
            compUiCamera.Viewport = new float4(0, 0, ClientSize.Width - 1, ClientSize.Height);
            uiCam.AddComponent(compUiCamera);

            EditorEntity sceneCam = new EditorEntity();
            sceneCam.Position = new float3(0, 3, -8);
            CameraComponent compCamera = new CameraComponent();
            compCamera.LayerMask = 0b0100000000000000;
            compCamera.Viewport = new float4(100, 100, 300, 300);
            sceneCam.AddComponent(compCamera);

            DockableViewport dockable = new DockableViewport(compCamera, fontAsset);
            dockable.Position = new float3(ClientSize.Width - 601, 0, 0);

            makeStartScene();

            thread = new Thread(threadUpdate);
            thread.Start();

            //EditorEntity fpsView = new EditorEntity();
            //fpsView.LayerMask = 0b00000010;
            //TextComponent fpsText = new TextComponent();
            //fpsText.Size = new int2(100, 100);
            //fpsText.Text = "FPS 200";
            //fpsView.Position = new float3(500, 100, 0);
            //fpsView.AddComponent(fpsText);
            //fpsText.Font = fontAsset;
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

            //Keyboard.SetActive(true);
        }

        protected override void OnDeactivate(EventArgs e) {
            base.OnDeactivate(e);

            //Keyboard.SetActive(false);
        }
    }
}
