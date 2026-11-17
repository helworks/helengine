using helengine;
namespace helengine.files.tests.assets.font {
    /// <summary>Exercises the real component visibility lifecycle against an evicting content-backed font cache.</summary>
    public sealed class PagedTextLifecycleTests {
        /// <summary>Visible runs pin every used page; hiding, text replacement and disposal release pins while hidden geometry remains reusable.</summary>
        [Fact]
        public void VisibilityReplacementAndDisposalReleaseRetainedFontPages() {
            FontPageContentSource source = new FontPageContentSource(); using ContentManager content = new ContentManager(source);
            content.RegisterProcessor<TextureAsset>(RuntimeContentProcessorIds.TextureAsset, new AssetContentProcessor<TextureAsset>());
            using NativeAtlasRenderManager atlasRenderer = new NativeAtlasRenderManager(); using PagedTextTestRenderer renderer = new PagedTextTestRenderer();
            using Core core = new Core(new CoreInitializationOptions { ContentStreamSource = source }); core.Initialize(renderer, null, null, new PlatformInfo("test", "test"));
            using FontAsset font = new FontAsset(new FontInfo("paged", 1, 1), new ManagedRuntimeTexture { Width = 1, Height = 1 },
                new Dictionary<char, FontChar> {
                    ['A'] = new FontChar(new float4(0, 0, 1, 1), 0, 1, 0, 0) { PageIndex = 1 },
                    ['B'] = new FontChar(new float4(0, 0, 1, 1), 0, 1, 0, 0) { PageIndex = 2 }
                }, 1, 1, 1) {
                Pages = new[] { new FontPage("a.hasset", 1, 1), new FontPage("b.hasset", 1, 1), new FontPage("c.hasset", 1, 1) }, PageBudgetBytes = 4
            };
            font.ConfigurePageCache(content, atlasRenderer);
            using RuntimeMaterial template = new RuntimeMaterial(); template.SetPrimaryTexture(font.Texture);
            using Entity entity = new Entity(core); entity.InitComponents();
            TextMeshComponent text = new TextMeshComponent { Font = font, Text = "AB", AtlasMaterial = template }; entity.AddComponent(text);
            RuntimeMaterial[] initial = text.Materials; Assert.Equal(2, initial.Length); RuntimeModel first = text.Model;
            RuntimeTexture firstPage = initial[0].ResolvePrimaryTexture(), secondPage = initial[1].ResolvePrimaryTexture();
            Assert.False(firstPage.IsDisposed); Assert.False(secondPage.IsDisposed);
            font.ResolvePageTexture(3); Assert.False(firstPage.IsDisposed); Assert.False(secondPage.IsDisposed);
            entity.Enabled = false; Assert.Empty(text.Materials); Assert.Null(text.Model);
            font.ResolvePageTexture(3); Assert.True(firstPage.IsDisposed); Assert.True(secondPage.IsDisposed);
            entity.Enabled = true; Assert.Same(first, text.Model); Assert.Equal(1, renderer.BuildCount);
            RuntimeTexture reacquiredFirst = text.Materials[0].ResolvePrimaryTexture(), reacquiredSecond = text.Materials[1].ResolvePrimaryTexture();
            Assert.NotSame(firstPage, reacquiredFirst); Assert.NotSame(secondPage, reacquiredSecond);
            text.Text = "A"; font.ResolvePageTexture(3); Assert.True(reacquiredFirst.IsDisposed); Assert.True(reacquiredSecond.IsDisposed);
            Assert.NotSame(first, text.Model); Assert.Equal(2, renderer.BuildCount);
            RuntimeTexture finalPage = text.Materials[0].ResolvePrimaryTexture(); text.Dispose();
            font.ResolvePageTexture(3); Assert.True(finalPage.IsDisposed); Assert.False(font.IsDisposed);
            Assert.Same(font.Texture, template.ResolvePrimaryTexture());
        }
    }
}
