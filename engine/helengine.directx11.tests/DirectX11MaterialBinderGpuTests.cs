using helengine;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
namespace helengine.directx11.tests {
    /// <summary>Validates native constant-buffer creation and binding using the Windows WARP device.</summary>
    public class DirectX11MaterialBinderGpuTests {
        /// <summary>Separate materials with equal bytes bind the same immutable native buffer and mutations replace it.</summary>
        [WindowsDirectXFact]
        public void EqualMaterialConstantsShareImmutableGpuBuffer() {
            using var device = new Device(DriverType.Warp, DeviceCreationFlags.None);
            using var states = new DirectX11PipelineStateCache(device);
            using var binder = new DirectX11MaterialBinder(device, states);
            var first = CreateMaterial(); var second = CreateMaterial();
            binder.ApplyMaterialConstantBufferBindings(first);
            using var firstBuffer = device.ImmediateContext.VertexShader.GetConstantBuffers(4, 1)[0];
            Assert.Equal(ResourceUsage.Immutable, firstBuffer.Description.Usage);
            binder.ApplyMaterialConstantBufferBindings(second);
            using var secondBuffer = device.ImmediateContext.VertexShader.GetConstantBuffers(4, 1)[0];
            Assert.Equal(firstBuffer.NativePointer, secondBuffer.NativePointer);
            var changed = new byte[16]; changed[0] = 9;
            second.Properties.SetConstantBufferData(0, changed);
            binder.ApplyMaterialConstantBufferBindings(second);
            using var changedBuffer = device.ImmediateContext.VertexShader.GetConstantBuffers(4, 1)[0];
            Assert.NotEqual(firstBuffer.NativePointer, changedBuffer.NativePointer);
            second.Properties.SetConstantBufferData(0, null);
            binder.ApplyMaterialConstantBufferBindings(second);
            Assert.Null(device.ImmediateContext.VertexShader.GetConstantBuffers(4, 1)[0]);
            binder.ReleaseMaterial(first); binder.ReleaseMaterial(second);
        }
        /// <summary>Replacing the shader resource on the active material binds the new programs without requiring a material identity change.</summary>
        [WindowsDirectXFact]
        public void HotReloadRebindsShadersOnSameMaterial() {
            using var device = new Device(DriverType.Warp, DeviceCreationFlags.None);
            using var states = new DirectX11PipelineStateCache(device);
            using var binder = new DirectX11MaterialBinder(device, states);
            using var vertex = SharpDX.D3DCompiler.ShaderBytecode.Compile("float4 main(float4 position : POSITION) : SV_POSITION { return position; }", "main", "vs_4_0");
            using var pixel = SharpDX.D3DCompiler.ShaderBytecode.Compile("float4 main() : SV_Target { return float4(1, 0, 0, 1); }", "main", "ps_4_0");
            var elements = new[] { new InputElement("POSITION", 0, SharpDX.DXGI.Format.R32G32B32A32_Float, 0) };
            using var first = new DirectX11ShaderResource(device, vertex.Bytecode.Data, pixel.Bytecode.Data, elements, "v", "p", "default");
            using var second = new DirectX11ShaderResource(device, vertex.Bytecode.Data, pixel.Bytecode.Data, elements, "v", "p", "default");
            var material = new DirectX11MaterialResource(first);
            binder.ApplyMaterial(material, material);
            material.UpdateShaderResource(second);
            binder.ApplyMaterial(material, material);
            using var boundVertex = device.ImmediateContext.VertexShader.Get();
            using var boundPixel = device.ImmediateContext.PixelShader.Get();
            Assert.Equal(second.VertexShader.NativePointer, boundVertex.NativePointer);
            Assert.Equal(second.PixelShader.NativePointer, boundPixel.NativePointer);
        }
        /// <summary>Constructs a valid single-buffer shader material independently of shader compilation.</summary>
        static ShaderRuntimeMaterial CreateMaterial() {
            var material = new ShaderRuntimeMaterial();
            material.SetLayout(new MaterialLayout("test", "v", "p", "default", new MaterialRenderState(), Array.Empty<MaterialLayoutBinding>(),
                new[] { new MaterialLayoutBinding("Color", ShaderResourceType.ConstantBuffer, 0, 4, 16) }, Array.Empty<MaterialLayoutBinding>()));
            material.Properties.SetConstantBufferData(0, new byte[16]);
            return material;
        }
    }
}
