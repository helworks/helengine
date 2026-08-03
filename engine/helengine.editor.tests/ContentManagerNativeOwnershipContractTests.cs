namespace helengine.editor.tests {
    /// <summary>
    /// Verifies that native content processor registration transfers every retained allocation to its long-lived owner.
    /// </summary>
    public class ContentManagerNativeOwnershipContractTests {
        /// <summary>
        /// Ensures a concrete processor registration passed to the manager is retained instead of deleted by the caller.
        /// </summary>
        [Fact]
        public void RegisterProcessor_RegistrationParameter_TakesNativeOwnership() {
            System.Reflection.MethodInfo method = typeof(ContentManager).GetMethod(
                nameof(ContentManager.RegisterProcessor),
                new[] { typeof(ContentProcessorRegistration) });

            Assert.NotNull(method);
            AssertOwnershipTransfer(method.GetParameters()[0]);
        }

        /// <summary>
        /// Ensures the typed registration overload transfers the processor allocation into its registration wrapper.
        /// </summary>
        [Fact]
        public void RegisterProcessor_GenericProcessorParameter_TakesNativeOwnership() {
            System.Reflection.MethodInfo method = typeof(ContentManager)
                .GetMethods()
                .Single(candidate => candidate.Name == nameof(ContentManager.RegisterProcessor)
                    && candidate.IsGenericMethodDefinition);

            AssertOwnershipTransfer(method.GetParameters()[1]);
        }

        /// <summary>
        /// Ensures a registration object owns the processor instance it retains for subsequent content loads.
        /// </summary>
        [Fact]
        public void ContentProcessorRegistration_ProcessorParameter_TakesNativeOwnership() {
            System.Reflection.ConstructorInfo constructor = Assert.Single(typeof(ContentProcessorRegistration).GetConstructors());

            AssertOwnershipTransfer(constructor.GetParameters()[1]);
        }

        /// <summary>
        /// Verifies that one reflected parameter carries the native ownership-transfer contract.
        /// </summary>
        /// <param name="parameter">Parameter whose native lifetime contract must retain the supplied allocation.</param>
        static void AssertOwnershipTransfer(System.Reflection.ParameterInfo parameter) {
            Assert.NotEmpty(parameter.GetCustomAttributes(typeof(NativeTakesOwnershipAttribute), false));
        }
    }
}
