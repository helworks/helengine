namespace helengine.directx11 {
    /// <summary>Owns dependency-aware prepared bindings and reference-counted constant resources for one DirectX renderer.</summary>
    sealed class DirectX11MaterialBindingCache<T> : IDisposable where T : class, IDisposable {
        /// <summary>Prepared leaf materials indexed by managed object identity.</summary>
        readonly Dictionary<ShaderRuntimeMaterial, DirectX11PreparedMaterial<T>> Materials = new(ReferenceEqualityComparer.Instance);
        /// <summary>Only live payload versions are retained; equal payloads share resources across materials and slots.</summary>
        readonly Dictionary<byte[], DirectX11ConstantContent<T>> Contents = new(new DirectX11ConstantContentComparer());
        /// <summary>Creates and uploads a resource exactly once for a previously unseen payload.</summary>
        readonly Func<byte[], T> CreateResource;
        /// <summary>Accepts the backend resource factory; tests use disposable CPU resources without a DirectX device.</summary>
        public DirectX11MaterialBindingCache(Func<byte[], T> createResource) { CreateResource = createResource ?? throw new ArgumentNullException(nameof(createResource)); }
        /// <summary>Number of currently retained prepared materials, useful for retention diagnostics.</summary>
        public int MaterialCount => Materials.Count;
        /// <summary>Number of distinct live constant payloads, independent of material count.</summary>
        public int ContentCount => Contents.Count;
        /// <summary>Returns current prepared bindings, transactionally replacing invalid entries and releasing obsolete contents.</summary>
        public DirectX11PreparedMaterial<T> Get(ShaderRuntimeMaterial material) {
            ArgumentNullException.ThrowIfNull(material);
            bool replacing = Materials.TryGetValue(material, out var prepared);
            if (replacing) {
                if (prepared.IsCurrent()) {
                    return prepared;
                }
            }
            prepared = new DirectX11PreparedMaterial<T>();
            try {
                ShaderRuntimeMaterial dependency = material;
                while (dependency != null) {
                    prepared.Dependencies.Add(new DirectX11MaterialDependency(dependency));
                    dependency = dependency.ParentMaterial as ShaderRuntimeMaterial;
                }
                foreach (var binding in material.Layout.ConstantBufferBindings) {
                    if (binding == null || IsEngineManaged(binding.Name)) {
                        continue;
                    }
                    DirectX11ConstantContent<T> content = null;
                    if (material.TryResolveConstantBufferData(binding.Name, out var data)) {
                        content = Acquire(data);
                    }
                    prepared.Constants.Add(new DirectX11PreparedConstant<T>(binding.Slot, content));
                }
                foreach (var binding in material.Layout.TextureBindings) {
                    if (binding == null) {
                        continue;
                    }
                    var texture = ResolveTexture(material, binding.Name);
                    if (texture != null) {
                        prepared.Textures.Add(new DirectX11PreparedTexture(binding, texture));
                    }
                }
                if (replacing) {
                    ReleaseEntry(material);
                }
                Materials.Add(material, prepared);
                return prepared;
            } catch {
                ReleaseContents(prepared);
                if (replacing) {
                    ReleaseEntry(material);
                }
                throw;
            }
        }
        /// <summary>Invalidates a released material and every prepared descendant before runtime disposal destroys ancestry.</summary>
        public void Release(RuntimeMaterial material) {
            var removed = new List<ShaderRuntimeMaterial>();
            foreach (var pair in Materials) {
                if (pair.Value.DependsOn(material)) {
                    removed.Add(pair.Key);
                }
            }
            foreach (var key in removed) {
                ReleaseEntry(key);
            }
        }
        /// <summary>Removes only the requested entry in constant lookup time; descendants validate captured revisions on their next use.</summary>
        void ReleaseEntry(ShaderRuntimeMaterial material) {
            if (Materials.Remove(material, out var prepared)) {
                ReleaseContents(prepared);
            }
        }
        /// <summary>Releases every retained resource once and clears both caches.</summary>
        public void Dispose() {
            foreach (var content in Contents.Values) {
                content.Resource.Dispose();
            }
            Contents.Clear();
            Materials.Clear();
        }
        /// <summary>Leaves transform, forward-light and shadow constants under renderer ownership.</summary>
        internal static bool IsEngineManaged(string name) => name == "TransformBuffer" || name == "ForwardLightBuffer" || name == "ShadowBuffer";
        /// <summary>Resolves local texture overrides with the existing null-means-inherit behavior.</summary>
        static RuntimeTexture ResolveTexture(ShaderRuntimeMaterial material, string name) {
            while (material != null) {
                int index = material.Layout.FindTextureBindingIndex(name);
                if (index >= 0) {
                    var texture = material.Properties.GetTexture(index);
                    if (texture != null) {
                        return texture;
                    }
                }
                material = material.ParentMaterial as ShaderRuntimeMaterial;
            }
            return null;
        }
        /// <summary>Acquires immutable content after exact byte comparison, creating resources only on pool misses.</summary>
        DirectX11ConstantContent<T> Acquire(byte[] data) {
            if (!Contents.TryGetValue(data, out var content)) {
                content = new DirectX11ConstantContent<T>(data, CreateResource(data));
                Contents.Add(data, content);
            }
            content.References++;
            return content;
        }
        /// <summary>Drops an entry's content references and immediately disposes versions with no remaining users.</summary>
        void ReleaseContents(DirectX11PreparedMaterial<T> prepared) {
            foreach (var binding in prepared.Constants) {
                var content = binding.Content;
                if (content == null) {
                    continue;
                }
                if (--content.References == 0) {
                    Contents.Remove(content.Data);
                    content.Resource.Dispose();
                }
            }
        }
    }
}
