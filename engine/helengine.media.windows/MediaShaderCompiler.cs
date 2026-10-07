using helengine.directx11;
namespace helengine.media.windows;
/// <summary>Uses the engine's shared compiler, include resolver and source hash cache for media passes.</summary>
public sealed class MediaShaderCompiler {
    /// <summary>Shared compiler service retained for a render session.</summary>
    readonly ShaderCompileService Service;
    /// <summary>Configures DirectX 11 through the existing engine backend.</summary>
    public MediaShaderCompiler() {Service=new(new ShaderFilesystemIncludeResolver(AppContext.BaseDirectory),new ShaderMemoryCompileCache(),new ShaderSourceHasher());Service.RegisterBackend(new DirectX11ShaderBackend());}
    /// <summary>Compiles a package-owned shader entry point and preserves its diagnostics on failure.</summary>
    public byte[] Compile(string relativePath,string entry,ShaderStage stage) {
        string path=Path.Combine(AppContext.BaseDirectory,relativePath);
        var result=Service.CompileFromFile(path,relativePath+":"+entry,entry,stage,ShaderCompileTarget.DirectX11,new ShaderModel(5,0),"default",Array.Empty<ShaderDefine>(),new ShaderCompileOptions(ShaderBindingPolicies.Default,false,true,false));
        if(!result.Success) {throw new InvalidOperationException("Media shader compilation failed: "+relativePath+" "+entry);}
        return result.Binary.Bytecode;
    }
}
