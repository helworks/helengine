using helengine;
namespace helengine.core.tests.assets {
    /// <summary>Guards material-scoped sampling inheritance without changing unrelated materials.</summary>
    public sealed class MaterialTextureSamplingTests {
        /// <summary>Instances follow root changes while other material roots retain their defaults.</summary>
        [Fact] public void SamplingIsScopedAndInherited(){
            using var root=new RuntimeMaterial();using var unrelated=new RuntimeMaterial();using var child=root.CreateInstance();
            root.TextureSampling=MaterialTextureSampling.RepeatAnisotropic16;
            Assert.Equal(MaterialTextureSampling.RepeatAnisotropic16,child.TextureSampling);
            Assert.Equal(MaterialTextureSampling.Default,unrelated.TextureSampling);
            child.TextureSampling=MaterialTextureSampling.ClampAnisotropic16;
            Assert.Equal(MaterialTextureSampling.RepeatAnisotropic16,root.TextureSampling);
            root.TextureSampling=MaterialTextureSampling.Default;Assert.Equal(MaterialTextureSampling.ClampAnisotropic16,child.TextureSampling);
            child.TextureSampling=MaterialTextureSampling.Default;Assert.Equal(MaterialTextureSampling.Default,child.TextureSampling);
        }
        /// <summary>Unknown policies fail before a backend can silently substitute another sampler.</summary>
        [Fact] public void UnknownSamplingFails(){using var material=new RuntimeMaterial();Assert.Throws<ArgumentOutOfRangeException>(()=>material.TextureSampling=(MaterialTextureSampling)99);}
    }
}
