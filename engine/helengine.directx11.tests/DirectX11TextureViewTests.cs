using System.Runtime.CompilerServices;
using helengine;
using SharpDX.Direct3D11;
namespace helengine.directx11.tests {
    /// <summary>Checks live texture-view lookup using inert managed wrappers without invoking DirectX.</summary>
    public class DirectX11TextureViewTests {
        /// <summary>Replacing the live view on the same texture identity is visible immediately and disposed textures are rejected.</summary>
        [Fact]
        public void TextureViewIsReadFromLiveOwner() {
            var binder = (DirectX11MaterialBinder)RuntimeHelpers.GetUninitializedObject(typeof(DirectX11MaterialBinder));
            using var first = new ShaderResourceView(IntPtr.Zero);
            using var second = new ShaderResourceView(IntPtr.Zero);
            var texture = new DirectX11TextureResource { Resource = first };
            Assert.Same(first, binder.ResolveTextureResourceView(texture));
            texture.Resource = second;
            Assert.Same(second, binder.ResolveTextureResourceView(texture));
            texture.Dispose();
            Assert.Throws<ObjectDisposedException>(() => binder.ResolveTextureResourceView(texture));
        }
        /// <summary>Render-target view replacement is read from the live target even without a material-property revision.</summary>
        [Fact]
        public void RenderTargetViewIsReadFromLiveOwner() {
            var binder = (DirectX11MaterialBinder)RuntimeHelpers.GetUninitializedObject(typeof(DirectX11MaterialBinder));
            var target = (DirectX11RenderTargetResource)RuntimeHelpers.GetUninitializedObject(typeof(DirectX11RenderTargetResource));
            using var first = new ShaderResourceView(IntPtr.Zero);
            using var second = new ShaderResourceView(IntPtr.Zero);
            var property = typeof(DirectX11RenderTargetResource).GetProperty(nameof(DirectX11RenderTargetResource.ShaderResourceView));
            property.SetValue(target, first); Assert.Same(first, binder.ResolveTextureResourceView(target));
            property.SetValue(target, second); Assert.Same(second, binder.ResolveTextureResourceView(target));
            second.Dispose();
            Assert.Throws<InvalidOperationException>(() => binder.ResolveTextureResourceView(target));
        }
    }
}
