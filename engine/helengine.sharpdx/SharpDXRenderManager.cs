using helengine;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Drawing;
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

    public static InputElement[] Elements = [
        new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
        new InputElement("NORMAL", 0, Format.R32G32B32_Float, 12, 0),
        new InputElement("TEXCOORD", 0, Format.R32G32_Float, 24, 0)
    ];
}

namespace helengine.sharpdx {
    public class SharpDXRenderManager : RenderManager {
        List<SharpDXWindow> windows;
        Dictionary<IntPtr, SharpDXWindow> windowsDict;

        public D3DDevice Device { get; private set; }
        public Adapter1 Adapter { get; private set; }

        private InputLayout layout;
        private Buffer constantBuffer;

        private VertexShader vertexShader;
        private PixelShader pixelShader;

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


            var vertexShaderByteCode = ShaderBytecode.CompileFromFile("shaders\\MiniCube.fx", "VS", "vs_4_0");
            vertexShader = new VertexShader(Device, vertexShaderByteCode);

            var pixelShaderByteCode = ShaderBytecode.CompileFromFile("shaders\\MiniCube.fx", "PS", "ps_4_0");
            pixelShader = new PixelShader(Device, pixelShaderByteCode);

            var signature = ShaderSignature.GetInputSignature(vertexShaderByteCode);

            layout = new InputLayout(
                Device,
                signature,
                Vertex.Elements
            );

            constantBuffer = new Buffer(Device, Utilities.SizeOf<float4x4>(), ResourceUsage.Default,
                BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);

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

                Texture2D depthBuffer = new Texture2D(Device,
                new Texture2DDescription() {
                    Format = Format.D32_Float_S8X24_UInt,
                    ArraySize = 1,
                    MipLevels = 1,
                    Width = width,
                    Height = height,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Default,
                    BindFlags = BindFlags.DepthStencil,
                    CpuAccessFlags = CpuAccessFlags.None,
                    OptionFlags = ResourceOptionFlags.None
                });

                window.DepthView = new DepthStencilView(Device, depthBuffer);

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
                model.Indices = (ushort)data.Indices16.Length;
                model.IndexBuffer = Buffer.Create(Device, BindFlags.IndexBuffer, data.Indices16);
            }

            return model;
        }

        private void drawCamera(ICamera camera) {
            var context = Device.ImmediateContext;
            var drawableBuckets = Core.Instance.ObjectManager.Drawables3D;

            float4x4 view;
            float3 pos = new float3(0, 0, -8);
            float3 target = new float3(0, 0, 0);
            float3 up = new float3(0, 1, 0);
            float4x4.CreateLookAt(ref pos, ref target, ref up, out view);

            float4x4 projection;
            float4x4.CreatePerspectiveFieldOfView((float)Math.PI / 4.0f, 1280 / 720.0f, 0.1f, 100f, out projection);

            float4x4 viewProj;
            float4x4.Multiply(ref view, ref projection, out viewProj);

            for (int i = 0; i < windows.Count; i++) {
                SharpDXWindow window = windows[i];
                Device.ImmediateContext.OutputMerger.SetTargets(window.DepthView, window.RenderTarget);
                Device.ImmediateContext.ClearDepthStencilView(window.DepthView, DepthStencilClearFlags.Depth, 1.0f, 0);
                Device.ImmediateContext.ClearRenderTargetView(window.RenderTarget, new SharpDX.Mathematics.Interop.RawColor4(1f, 0.5f, 0, 1.0f));
                Device.ImmediateContext.Rasterizer.SetViewport(0, 0, 1280, 720);

                for (int j = 0; j < drawableBuckets.Length; j++) {
                    var drawableList = drawableBuckets[j];

                    for (int k = 0; k < drawableList.Count; k++) {
                        var drawable = drawableList[k];

                        Entity parent = drawable.Parent;

                        SharpDXModelData data = (SharpDXModelData)drawable.RenderData;

                        // state change
                        context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
                        context.VertexShader.Set(vertexShader);
                        context.PixelShader.Set(pixelShader);
                        context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(data.VertexBuffer, 32, 0));
                        context.InputAssembler.SetIndexBuffer(data.IndexBuffer, Format.R16_UInt, 0);
                        context.VertexShader.SetConstantBuffer(0, constantBuffer);


                        // draw
                        float4 orientation = parent.Orientation;
                        float4x4 rotation;
                        float4x4.CreateFromQuaternion(ref orientation, out rotation);

                        float3 scale = parent.Scale;
                        float4x4 size;
                        float4x4.CreateScale(scale.X, scale.Y, scale.Z, out size);

                        float4x4 world;
                        float4x4.Multiply(ref rotation, ref size, out world);

                        float4x4 worldViewProj;
                        float4x4.Multiply(ref world, ref viewProj, out worldViewProj);

                        float4x4 worldViewProjTransposed;
                        float4x4.Transpose(ref worldViewProj, out worldViewProjTransposed);

                        context.UpdateSubresource(ref worldViewProjTransposed, constantBuffer);

                        context.DrawIndexed(data.Indices, 0, 0);
                        context.Flush();
                    }

                }

                window.Chain.Present(0, PresentFlags.None);
            }
        }

        public override void Draw() {
            base.Draw();

            var context = Device.ImmediateContext;
            context.InputAssembler.InputLayout = layout;

            var cameraBuckets = Core.Instance.ObjectManager.Cameras;

            for (int i = 0; i < cameraBuckets.Length; i++) {
                var cameras = cameraBuckets[i];

                for (int j = 0; j < cameras.Count; j++) {
                    var camera = cameras[j];
                    drawCamera(camera);
                }
            }
        }
    }
}
