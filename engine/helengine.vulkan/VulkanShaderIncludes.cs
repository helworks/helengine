namespace helengine.vulkan {
    /// <summary>Connects shaderc's native include lifetime to the engine resolver, preserving compiler preprocessing and diagnostics.</summary>
    unsafe sealed class VulkanShaderIncludes : IDisposable {
        /// <summary>Resolves includes in the same source namespace chosen by the compile service.</summary>
        readonly IShaderIncludeResolver Includes;
        /// <summary>Roots the managed resolve callback until native compilation has returned.</summary>
        Silk.NET.Shaderc.PfnIncludeResolveFn Resolver;
        /// <summary>Roots the callback that frees each native include result after shaderc consumes it.</summary>
        Silk.NET.Shaderc.PfnIncludeResultReleaseFn Releaser;
        /// <summary>Prevents releasing callback delegates twice.</summary>
        bool Disposed;

        /// <summary>Registers both callbacks for these specific compile options without changing binding or numeric policies.</summary>
        public VulkanShaderIncludes(Silk.NET.Shaderc.Shaderc shaderc, Silk.NET.Shaderc.CompileOptions* options, IShaderIncludeResolver includes) {
            Includes = includes ?? throw new ArgumentNullException(nameof(includes));
            Resolver = new Silk.NET.Shaderc.PfnIncludeResolveFn(Resolve);
            Releaser = new Silk.NET.Shaderc.PfnIncludeResultReleaseFn(Release);
            shaderc.CompileOptionsSetIncludeCallbacks(options, Resolver, Releaser, null);
        }

        /// <summary>Returns an owned UTF-8 result, translating resolver failures to shaderc's explicit empty-source-name error form.</summary>
        Silk.NET.Shaderc.IncludeResult* Resolve(void* userData, byte* requested, int kind, byte* requesting, nuint depth) {
            string name = string.Empty;
            string content;
            try {
                if (depth > 64) { throw new InvalidDataException("Shader include nesting exceeds 64 levels."); }
                ShaderIncludeResult resolved = Includes.Resolve(System.Runtime.InteropServices.Marshal.PtrToStringUTF8((IntPtr)requesting), System.Runtime.InteropServices.Marshal.PtrToStringUTF8((IntPtr)requested));
                if (resolved == null || string.IsNullOrEmpty(resolved.Path) || resolved.Source == null) { throw new InvalidDataException("Shader include resolver returned an invalid result."); }
                name = resolved.Path;
                content = resolved.Source;
            } catch (Exception error) {
                name = string.Empty;
                content = "Shader include resolution failed: " + error.Message;
            }
            Silk.NET.Shaderc.IncludeResult* result = (Silk.NET.Shaderc.IncludeResult*)System.Runtime.InteropServices.NativeMemory.AllocZeroed((nuint)sizeof(Silk.NET.Shaderc.IncludeResult));
            result->SourceName = (byte*)System.Runtime.InteropServices.Marshal.StringToCoTaskMemUTF8(name);
            result->SourceNameLength = (nuint)System.Text.Encoding.UTF8.GetByteCount(name);
            result->Content = (byte*)System.Runtime.InteropServices.Marshal.StringToCoTaskMemUTF8(content);
            result->ContentLength = (nuint)System.Text.Encoding.UTF8.GetByteCount(content);
            return result;
        }

        /// <summary>Frees the exact result and strings owned by one completed include request.</summary>
        static void Release(void* userData, Silk.NET.Shaderc.IncludeResult* result) {
            System.Runtime.InteropServices.Marshal.FreeCoTaskMem((IntPtr)result->SourceName);
            System.Runtime.InteropServices.Marshal.FreeCoTaskMem((IntPtr)result->Content);
            System.Runtime.InteropServices.NativeMemory.Free(result);
        }

        /// <summary>Unroots callbacks only after the synchronous native compiler has released its include results.</summary>
        public void Dispose() {
            if (Disposed) { return; }
            Disposed = true;
            Resolver.Dispose(); Releaser.Dispose();
        }
    }
}
