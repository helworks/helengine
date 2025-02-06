using SharpDX.Direct3D11;
using SharpDX.DXGI;
using D3DDevice = SharpDX.Direct3D11.Device;

namespace helengine.sharpdx {
    public class SharpDXRenderManager : RenderManager {
        List<SharpDXWindow> windows;
        Dictionary<IntPtr, SharpDXWindow> windowsDict;

        public SharpDXRenderManager() {
            windows = new List<SharpDXWindow>();
            windowsDict = new Dictionary<nint, SharpDXWindow>();
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
            SharpDXWindow window = new SharpDXWindow();
            windows.Add(window);
            windowsDict.Add(handle, window);

            SwapChainDescription scd = new SwapChainDescription() {
                BufferCount = 1, //how many buffers are used for writing. it's recommended to have at least 2 buffers but this is an example
                Flags = SwapChainFlags.None,
                IsWindowed = true, //it's windowed
                ModeDescription = new ModeDescription(
                    width, // windows width
                    height, // windows height
                    new Rational(60, 1), // refresh rate
                    Format.R8G8B8A8_UNorm // pixel format, you should resreach this for your specific implementation
                ),
                OutputHandle = handle, //the magic 
                SampleDescription = new SampleDescription(1, 0), //the first number is how many samples to take, anything above one is multisampling.
                SwapEffect = SwapEffect.Discard,
                Usage = Usage.RenderTargetOutput
            };

            D3DDevice.CreateWithSwapChain(
                SharpDX.Direct3D.DriverType.Hardware,//hardware if you have a graphics card otherwise you can use software
                DeviceCreationFlags.Debug,//helps debuging don't use this for release verion
                scd,//the swapchain description made above
                out window.Device, out window.SwapChain//our directx objects
            );

            window.Target = Texture2D.FromSwapChain<Texture2D>(window.SwapChain, 0);
            window.TargetView = new RenderTargetView(window.Device, window.Target);

            window.Device.ImmediateContext.OutputMerger.SetRenderTargets(window.TargetView);
        }

        public override void Draw() {
            base.Draw();

            for (int i = 0; i < windows.Count; i++) {
                SharpDXWindow window = windows[i];

                window.Device.ImmediateContext.ClearRenderTargetView(window.TargetView, new SharpDX.Mathematics.Interop.RawColor4(1f, 0.5f, 0, 1.0f));
                
                
                
                window.SwapChain.Present(0, PresentFlags.None);
            }
        }
    }
}
