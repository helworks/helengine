namespace helengine.tools.buildwaiter.tests {
    /// <summary>
    /// Verifies invocation-proof and acknowledgment paths use canonical identities beneath the output root.
    /// </summary>
    public sealed class BuildInvocationProofPathsTests {
        /// <summary>
        /// Canonical invocation identity used by path assertions.
        /// </summary>
        const string InvocationId = "b40ab19d-4d81-4db0-a0d4-9b818b49c7c0";

        /// <summary>
        /// Ensures malformed, noncanonical, and non-lowercase identities cannot select proof files.
        /// </summary>
        /// <param name="invocationId">Invocation identity candidate.</param>
        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("B40AB19D-4D81-4DB0-A0D4-9B818B49C7C0")]
        [InlineData(" b40ab19d-4d81-4db0-a0d4-9b818b49c7c0")]
        [InlineData("b40ab19d4d814db0a0d49b818b49c7c0")]
        [InlineData("not-a-guid")]
        public void GetProofPath_WhenInvocationIdIsNotCanonical_Throws(string invocationId) {
            Assert.Throws<ArgumentException>(() =>
                BuildInvocationProofPaths.GetProofPath("output", invocationId));
        }

        /// <summary>
        /// Ensures canonical proof and acknowledgment names are fixed children of the normalized output root.
        /// </summary>
        [Fact]
        public void Paths_WhenInputsAreCanonical_ReturnExpectedOutputChildren() {
            string output = Path.GetFullPath("output");

            Assert.Equal(
                Path.Combine(output, $".helengine-build-state.{InvocationId}.json"),
                BuildInvocationProofPaths.GetProofPath(output, InvocationId));
            Assert.Equal(
                Path.Combine(output, $".helengine-build-state.{InvocationId}.ack"),
                BuildInvocationProofPaths.GetAcknowledgementPath(output, InvocationId));
        }
    }
}
