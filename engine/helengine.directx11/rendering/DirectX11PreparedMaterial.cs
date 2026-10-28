namespace helengine.directx11 {
    /// <summary>Holds one material's resolved bindings and dependency snapshots for reuse across draws.</summary>
    sealed class DirectX11PreparedMaterial<T> where T : class, IDisposable {
        /// <summary>Snapshots of the leaf and every shader-material ancestor.</summary>
        public List<DirectX11MaterialDependency> Dependencies { get; } = new();
        /// <summary>Material-owned constants including explicit missing-value unbinds.</summary>
        public List<DirectX11PreparedConstant<T>> Constants { get; } = new();
        /// <summary>Resolved runtime textures; native views are deliberately not retained.</summary>
        public List<DirectX11PreparedTexture> Textures { get; } = new();
        /// <summary>Validates the complete dependency chain without fresh resolution or payload copying.</summary>
        public bool IsCurrent() {
            foreach (var dependency in Dependencies) {
                if (!dependency.IsCurrent()) {
                    return false;
                }
            }
            return true;
        }
        /// <summary>Finds whether this entry retains the supplied material anywhere in its captured ancestry.</summary>
        public bool DependsOn(RuntimeMaterial material) {
            foreach (var dependency in Dependencies) {
                if (ReferenceEquals(dependency.Material, material)) {
                    return true;
                }
            }
            return false;
        }
    }
}
