namespace helengine.painting.tests {
    /// <summary>Checks admission of real authored brushes, including untrusted and newer data.</summary>
    public sealed class PaintContractTests {
        /// <summary>Constructs explicit valid authored data; production provides no implicit brush defaults.</summary>
        static PaintBrushDefinition ValidBrush() {
            return new PaintBrushDefinition { Version=1, Id="cozy.round", Diameter=1.5, Red=0.12, Green=0.16, Blue=0.10, Opacity=1 };
        }

        /// <summary>Valid boundary diameters are admitted without mutation.</summary>
        [Theory]
        [InlineData(0.2)]
        [InlineData(1.5)]
        [InlineData(12.0)]
        public void AcceptsSupportedBrush(double diameter) {
            PaintBrushDefinition brush=ValidBrush(); brush.Diameter=diameter;
            Assert.Equal(0,PaintContract.ValidateBrush(brush));
            Assert.Equal(diameter,brush.Diameter);
        }

        /// <summary>Invalid values cannot reach raster allocation or instancing.</summary>
        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(12.1)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void RejectsInvalidDiameter(double value) {
            PaintBrushDefinition brush=ValidBrush(); brush.Diameter=value;
            Assert.Equal(1,PaintContract.ValidateBrush(brush));
        }

        /// <summary>Newer or unknown brushes are rejected rather than silently approximated.</summary>
        [Fact]
        public void RejectsUnsupportedBrushAndVersion() {
            PaintBrushDefinition brush=ValidBrush(); brush.Version=3;
            Assert.Equal(2,PaintContract.ValidateBrush(brush));
            brush.Version=1; brush.Id="unknown";
            Assert.Equal(2,PaintContract.ValidateBrush(brush));
        }

        /// <summary>Missing and nonfinite color data is invalid, including on an eraser.</summary>
        [Fact]
        public void RejectsInvalidColorAndMissingBrush() {
            Assert.Equal(1,PaintContract.ValidateBrush(null));
            PaintBrushDefinition brush=ValidBrush(); brush.Red=double.NaN;
            Assert.Equal(1,PaintContract.ValidateBrush(brush));
            brush.Red=0; brush.Opacity=1.01; brush.Eraser=true;
            Assert.Equal(1,PaintContract.ValidateBrush(brush));
        }
    }
}
