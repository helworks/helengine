using System.Reflection;
using helengine;
using Xunit;

namespace helengine.core.tests.scene.runtime {
    /// <summary>
    /// Verifies scene-reference fixups remain a call-scoped borrow throughout runtime scene loading.
    /// </summary>
    public sealed class RuntimeSceneFixupsOwnershipContractTests {
        /// <summary>
        /// Ensures each loader and deserializer fixups parameter is explicitly non-escaping and does not transfer ownership.
        /// </summary>
        [Fact]
        public void FixupsParameters_AcrossRuntimeLoadAndDeserialization_AreNoEscapeBorrows() {
            Type serviceType = typeof(RuntimeSceneLoadService);
            Type fixupsType = typeof(RuntimeSceneReferenceFixups);
            MethodInfo[] methods = [
                serviceType.GetMethod("LoadRootEntity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                serviceType.GetMethod("LoadEntity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                serviceType.GetMethod("LoadComponent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                typeof(IRuntimeComponentDeserializer).GetMethod("Deserialize"),
                typeof(AutomaticScriptComponentRuntimeDeserializer).GetMethod("Deserialize")
            ];

            Assert.Equal(5, methods.Length);
            foreach (MethodInfo method in methods) {
                Assert.NotNull(method);
                ParameterInfo fixupsParameter = Array.Find(method.GetParameters(), parameter => parameter.ParameterType == fixupsType);
                Assert.NotNull(fixupsParameter);
                Assert.Equal("fixups", fixupsParameter.Name);
                Assert.True(fixupsParameter.IsDefined(typeof(NativeNoEscapeAttribute), false));
                Assert.False(fixupsParameter.IsDefined(typeof(NativeTakesOwnershipAttribute), false));
                Assert.False(fixupsParameter.IsDefined(typeof(NativeRetainsBorrowAttribute), false));
            }
        }

        /// <summary>
        /// Ensures decoded scene references return to the reader caller as owned values while fixups retain only a borrow.
        /// </summary>
        [Fact]
        public void SceneReferenceRead_TransfersReturnOwnershipAndFixupsRetainBorrow() {
            MethodInfo readReference = typeof(EngineBinaryReader).GetMethod("ReadSceneEntityReference");
            Assert.NotNull(readReference);
            Assert.True(readReference.IsDefined(typeof(NativeOwnedReturnAttribute), false));

            MethodInfo track = typeof(RuntimeSceneReferenceFixups).GetMethod("Track");
            Assert.NotNull(track);
            ParameterInfo trackReference = Array.Find(
                track.GetParameters(),
                parameter => parameter.ParameterType == typeof(SceneEntityReference));
            Assert.NotNull(trackReference);
            Assert.Equal("reference", trackReference.Name);
            Assert.True(trackReference.IsDefined(typeof(NativeRetainsBorrowAttribute), false));
            Assert.False(trackReference.IsDefined(typeof(NativeTakesOwnershipAttribute), false));
            Assert.False(trackReference.IsDefined(typeof(NativeNoEscapeAttribute), false));

            Type requestType = typeof(RuntimeSceneReferenceFixups).GetNestedType("Request", BindingFlags.NonPublic);
            Assert.NotNull(requestType);
            ConstructorInfo requestConstructor = requestType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                [typeof(SceneEntityReference), typeof(string), typeof(uint)],
                null);
            Assert.NotNull(requestConstructor);
            ParameterInfo requestReference = requestConstructor.GetParameters()[0];
            Assert.Equal("reference", requestReference.Name);
            Assert.Equal(typeof(SceneEntityReference), requestReference.ParameterType);
            Assert.True(requestReference.IsDefined(typeof(NativeRetainsBorrowAttribute), false));
            Assert.False(requestReference.IsDefined(typeof(NativeTakesOwnershipAttribute), false));
            Assert.False(requestReference.IsDefined(typeof(NativeNoEscapeAttribute), false));
        }
    }
}
