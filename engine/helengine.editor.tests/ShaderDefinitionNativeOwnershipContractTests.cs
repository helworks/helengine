namespace helengine.editor.tests {
    /// <summary>
    /// Verifies native ownership contracts for the nested shader-definition object graph.
    /// </summary>
    public class ShaderDefinitionNativeOwnershipContractTests {
        /// <summary>
        /// Ensures a module definition accepts ownership of its program and binary arrays and can release them.
        /// </summary>
        [Fact]
        public void ShaderModuleDefinition_Constructor_TakesOwnershipOfDefinitionArrays() {
            System.Reflection.ConstructorInfo constructor = Assert.Single(typeof(ShaderModuleDefinition).GetConstructors());

            Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(ShaderModuleDefinition)));
            AssertTakesOwnership(constructor.GetParameters()[1]);
            AssertTakesOwnership(constructor.GetParameters()[2]);
        }

        /// <summary>
        /// Ensures a program definition accepts ownership of every metadata array retained for its lifetime.
        /// </summary>
        [Fact]
        public void ShaderProgramDefinition_Constructor_TakesOwnershipOfMetadataArrays() {
            System.Reflection.ConstructorInfo constructor = Assert.Single(typeof(ShaderProgramDefinition).GetConstructors());

            Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(ShaderProgramDefinition)));
            for (int parameterIndex = 3; parameterIndex < constructor.GetParameters().Length; parameterIndex++) {
                AssertTakesOwnership(constructor.GetParameters()[parameterIndex]);
            }
        }

        /// <summary>
        /// Ensures a shader binding accepts ownership of the constant-member array retained by the binding.
        /// </summary>
        [Fact]
        public void ShaderBinding_Constructor_TakesOwnershipOfConstantMembers() {
            System.Reflection.ConstructorInfo constructor = Assert.Single(typeof(ShaderBinding).GetConstructors());

            Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(ShaderBinding)));
            AssertTakesOwnership(constructor.GetParameters()[5]);
        }

        /// <summary>
        /// Ensures a shader variant accepts ownership of its define-array container.
        /// </summary>
        [Fact]
        public void ShaderVariant_Constructor_TakesOwnershipOfDefineArray() {
            System.Reflection.ConstructorInfo constructor = Assert.Single(typeof(ShaderVariant).GetConstructors());

            Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(ShaderVariant)));
            AssertTakesOwnership(constructor.GetParameters()[1]);
        }

        /// <summary>
        /// Ensures embedded bytecode is copied so a binary descriptor never owns storage retained by its source object.
        /// </summary>
        [Fact]
        public void ShaderProgramBinary_BytecodeConstructor_CopiesInputStorage() {
            byte[] source = { 1, 2, 3, 4 };

            ShaderProgramBinary binary = new ShaderProgramBinary("Program", ShaderStage.Vertex, "dx11", "default", source);

            Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(ShaderProgramBinary)));
            Assert.NotSame(source, binary.Bytecode);
            Assert.Equal(source, binary.Bytecode);
        }

        /// <summary>
        /// Ensures a serialized shader asset owns the program and binary arrays assigned while converting a definition.
        /// </summary>
        [Fact]
        public void ShaderAsset_DefinitionArrays_AreOwnedMembers() {
            AssertOwnedMember(typeof(ShaderAsset).GetField(nameof(ShaderAsset.Programs)));
            AssertOwnedMember(typeof(ShaderAsset).GetField(nameof(ShaderAsset.Binaries)));
        }

        /// <summary>
        /// Ensures every nested serialized shader array is retained by exactly one owning asset object.
        /// </summary>
        [Fact]
        public void ShaderAssetGraph_NestedArrays_AreOwnedMembers() {
            AssertOwnedMember(typeof(ShaderProgramAsset).GetField(nameof(ShaderProgramAsset.Bindings)));
            AssertOwnedMember(typeof(ShaderProgramAsset).GetField(nameof(ShaderProgramAsset.Inputs)));
            AssertOwnedMember(typeof(ShaderProgramAsset).GetField(nameof(ShaderProgramAsset.Outputs)));
            AssertOwnedMember(typeof(ShaderProgramAsset).GetField(nameof(ShaderProgramAsset.Variants)));
            AssertOwnedMember(typeof(ShaderBindingAsset).GetField(nameof(ShaderBindingAsset.Members)));
            AssertOwnedMember(typeof(ShaderVariantAsset).GetField(nameof(ShaderVariantAsset.Defines)));
            AssertOwnedMember(typeof(ShaderBinaryAsset).GetField(nameof(ShaderBinaryAsset.Bytecode)));
        }

        /// <summary>
        /// Ensures serialized shader binaries copy embedded bytecode instead of sharing storage owned by a runtime descriptor.
        /// </summary>
        [Fact]
        public void ShaderBinaryAsset_FromBinary_CopiesBytecodeStorage() {
            byte[] source = { 5, 6, 7, 8 };
            ShaderProgramBinary binary = new ShaderProgramBinary("Program", ShaderStage.Vertex, "dx11", "default", source);

            ShaderBinaryAsset asset = ShaderBinaryAsset.FromBinary(binary);

            Assert.NotSame(binary.Bytecode, asset.Bytecode);
            Assert.Equal(binary.Bytecode, asset.Bytecode);
        }

        /// <summary>
        /// Verifies that one reflected parameter carries the native ownership-transfer contract.
        /// </summary>
        /// <param name="parameter">Constructor parameter whose allocation is retained by the destination object.</param>
        static void AssertTakesOwnership(System.Reflection.ParameterInfo parameter) {
            Assert.NotEmpty(parameter.GetCustomAttributes(typeof(NativeTakesOwnershipAttribute), false));
        }

        /// <summary>
        /// Verifies that one reflected field carries the native owned-member contract.
        /// </summary>
        /// <param name="field">Field that retains an allocated native object for the containing object's lifetime.</param>
        static void AssertOwnedMember(System.Reflection.FieldInfo field) {
            Assert.NotNull(field);
            Assert.NotEmpty(field.GetCustomAttributes(typeof(NativeOwnedMemberAttribute), false));
        }
    }
}
