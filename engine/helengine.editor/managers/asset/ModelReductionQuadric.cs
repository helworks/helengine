namespace helengine.editor {
    /// <summary>Accumulates symmetric plane-error quadrics in double precision.</summary>
    sealed class ModelReductionQuadric {
        /// <summary>Full homogeneous matrix; fixed-size storage keeps arithmetic straightforward.</summary>
        readonly double[] Values = new double[16];
        /// <summary>Adds one weighted normalized face plane.</summary>
        public void AddPlane(double x, double y, double z, double w, double weight) {
            double[] plane = { x, y, z, w };
            for (int row = 0; row < 4; row++) {
                for (int column = 0; column < 4; column++) Values[row * 4 + column] += plane[row] * plane[column] * weight;
            }
        }
        /// <summary>Merges the geometric error of a removed position into its surviving endpoint.</summary>
        public void Add(ModelReductionQuadric other) {
            for (int index = 0; index < Values.Length; index++) Values[index] += other.Values[index];
        }
        /// <summary>Evaluates the squared distance error at an existing source vertex position.</summary>
        public double Evaluate(float3 position) {
            double[] point = { position.X, position.Y, position.Z, 1d };
            double error = 0d;
            for (int row = 0; row < 4; row++) {
                for (int column = 0; column < 4; column++) error += point[row] * Values[row * 4 + column] * point[column];
            }
            return Math.Max(0d, error);
        }
    }
}
