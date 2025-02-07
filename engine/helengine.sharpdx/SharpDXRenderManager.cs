using helengine;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Runtime.InteropServices;
using Buffer = SharpDX.Direct3D11.Buffer;
using D3DDevice = SharpDX.Direct3D11.Device;
using DxgiFactory1 = SharpDX.DXGI.Factory1;

[StructLayout(LayoutKind.Sequential)]
struct Vertex {
    public float3 Position;
    public float3 Normal;
    public float2 TexCoord;

    public Vertex(float3 pos, float3 normal, float2 tc) {
        Position = pos; Normal = normal; TexCoord = tc;
    }
}

namespace helengine.sharpdx {
    public class SharpDXRenderManager : RenderManager {
        List<SharpDXWindow> windows;
        Dictionary<IntPtr, SharpDXWindow> windowsDict;

        public D3DDevice Device { get; private set; }
        public Adapter1 Adapter { get; private set; }

        public SharpDXRenderManager() {
            windows = new List<SharpDXWindow>();
            windowsDict = new Dictionary<nint, SharpDXWindow>();

            var factory = new DxgiFactory1();

            Adapter = factory.GetAdapter1(0);

            Device = new D3DDevice(Adapter, DeviceCreationFlags.None, new[]
            {
                FeatureLevel.Level_12_1,
                FeatureLevel.Level_12_0,
                FeatureLevel.Level_11_1,
                FeatureLevel.Level_11_0,
                FeatureLevel.Level_10_0,
                FeatureLevel.Level_9_3,
                FeatureLevel.Level_9_2,
                FeatureLevel.Level_9_1,
            });
        }

        public override void Dispose() {
            base.Dispose();

            if (windows != null) {
                lock (windows) {
                    for (int i = 0; i < windows.Count; i++) {
                        windows[i].Dispose();
                    }
                }
                windows = null;
            }
        }

        public override void AddWindow(IntPtr handle, int width, int height) {
            using (var factory = Adapter.GetParent<Factory>()) {
                SharpDXWindow window = new SharpDXWindow();
                windows.Add(window);
                windowsDict.Add(handle, window);

                var desc = new SwapChainDescription() {
                    BufferCount = 2,
                    ModeDescription = new ModeDescription(width, height, new Rational(60, 1), Format.B8G8R8A8_UNorm),
                    IsWindowed = true,
                    OutputHandle = handle,
                    SampleDescription = new SampleDescription(1, 0),
                    SwapEffect = SwapEffect.FlipDiscard,
                    Usage = Usage.RenderTargetOutput,
                    Flags = SwapChainFlags.AllowModeSwitch
                };

                // Create swap chain
                var swapChain = new SwapChain(factory, Device, desc);
                window.Chain = swapChain;

                // Create render target view
                using (var backBuffer = swapChain.GetBackBuffer<Texture2D>(0)) {
                    var renderView = new RenderTargetView(Device, backBuffer);
                    window.RenderTarget = renderView;
                }

                // Prevent window scaling
                factory.MakeWindowAssociation(handle, WindowAssociationFlags.IgnoreAll);
            }
        }

        public override RenderModelData BuildFromRaw(RawModelData data) {
            SharpDXModelData model = new SharpDXModelData();

            Vertex[] vertices = new Vertex[data.Positions.Length];

            for (int i = 0; i < data.Positions.Length; i++) {
                float3 pos = data.Positions[i];
                float3 normal = data.Normals[i];
                float2 tex = data.TexCoords[i];
                vertices[i] = new Vertex(pos, normal, tex);
            }

            model.VertexBuffer = Buffer.Create(
               Device,
               BindFlags.VertexBuffer,
               vertices);

            if (data.Indices16 == null) {

            } else {
                model.IndexBuffer = Buffer.Create(Device, BindFlags.IndexBuffer, data.Indices16);
            }

            return model;
        }

        public override void Draw() {
            base.Draw();

            for (int i = 0; i < windows.Count; i++) {
                SharpDXWindow window = windows[i];

                Device.ImmediateContext.OutputMerger.SetRenderTargets(window.RenderTarget);
                Device.ImmediateContext.ClearRenderTargetView(window.RenderTarget, new SharpDX.Mathematics.Interop.RawColor4(1f, 0.5f, 0, 1.0f));



                window.Chain.Present(0, PresentFlags.None);
            }
        }
    }
}
