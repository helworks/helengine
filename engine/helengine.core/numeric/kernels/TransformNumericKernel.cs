namespace helengine {
    /// <summary>Shares stored transform boundaries and expression order between runtime and cook evaluation.</summary>
    public static class TransformNumericKernel<T, TMath> where TMath : struct, INumericOperations<T> {
        /// <summary>Multiplies stored vector components and commits each resulting component once.</summary>
        public static float3 Multiply(float3 left, float3 right, TMath math) {
            return new float3(math.Write(math.Multiply(math.Read(left.X), math.Read(right.X))), math.Write(math.Multiply(math.Read(left.Y), math.Read(right.Y))), math.Write(math.Multiply(math.Read(left.Z), math.Read(right.Z))));
        }
        /// <summary>Adds stored vector components at the selected arithmetic width.</summary>
        public static float3 Add(float3 left, float3 right, TMath math) {
            return new float3(math.Write(math.Add(math.Read(left.X), math.Read(right.X))), math.Write(math.Add(math.Read(left.Y), math.Read(right.Y))), math.Write(math.Add(math.Read(left.Z), math.Read(right.Z))));
        }
        /// <summary>Normalizes a stored quaternion without narrowing its dot product or reciprocal before the result.</summary>
        public static float4 Normalize(float4 value, TMath math) {
            T x = math.Read(value.X); T y = math.Read(value.Y); T z = math.Read(value.Z); T w = math.Read(value.W);
            T squared = math.Add(math.Add(math.Add(math.Multiply(x, x), math.Multiply(y, y)), math.Multiply(z, z)), math.Multiply(w, w));
            T inverse = math.Divide(math.Number(1d), math.Sqrt(squared));
            return new float4(math.Write(math.Multiply(x, inverse)), math.Write(math.Multiply(y, inverse)), math.Write(math.Multiply(z, inverse)), math.Write(math.Multiply(w, inverse)));
        }
        /// <summary>Rotates a stored vector, retaining evaluation-width cross products until the final vector store.</summary>
        public static float3 Rotate(float3 value, float4 rotation, TMath math) {
            T qx = math.Read(rotation.X); T qy = math.Read(rotation.Y); T qz = math.Read(rotation.Z); T qw = math.Read(rotation.W);
            T vx = math.Read(value.X); T vy = math.Read(value.Y); T vz = math.Read(value.Z);
            T two = math.Number(2d);
            T tx = math.Multiply(two, math.Subtract(math.Multiply(qy, vz), math.Multiply(qz, vy)));
            T ty = math.Multiply(two, math.Subtract(math.Multiply(qz, vx), math.Multiply(qx, vz)));
            T tz = math.Multiply(two, math.Subtract(math.Multiply(qx, vy), math.Multiply(qy, vx)));
            T cx = math.Subtract(math.Multiply(qy, tz), math.Multiply(qz, ty));
            T cy = math.Subtract(math.Multiply(qz, tx), math.Multiply(qx, tz));
            T cz = math.Subtract(math.Multiply(qx, ty), math.Multiply(qy, tx));
            return new float3(math.Write(math.Add(math.Add(vx, math.Multiply(tx, qw)), cx)), math.Write(math.Add(math.Add(vy, math.Multiply(ty, qw)), cy)), math.Write(math.Add(math.Add(vz, math.Multiply(tz, qw)), cz)));
        }
        /// <summary>Concatenates two quaternions using the runtime's established multiplication order.</summary>
        public static float4 Concatenate(float4 first, float4 second, TMath math) {
            T x1 = math.Read(first.X); T y1 = math.Read(first.Y); T z1 = math.Read(first.Z); T w1 = math.Read(first.W);
            T x2 = math.Read(second.X); T y2 = math.Read(second.Y); T z2 = math.Read(second.Z); T w2 = math.Read(second.W);
            T x = math.Add(math.Add(math.Multiply(x2, w1), math.Multiply(x1, w2)), math.Subtract(math.Multiply(y2, z1), math.Multiply(z2, y1)));
            T y = math.Add(math.Add(math.Multiply(y2, w1), math.Multiply(y1, w2)), math.Subtract(math.Multiply(z2, x1), math.Multiply(x2, z1)));
            T z = math.Add(math.Add(math.Multiply(z2, w1), math.Multiply(z1, w2)), math.Subtract(math.Multiply(x2, y1), math.Multiply(y2, x1)));
            T w = math.Subtract(math.Multiply(w2, w1), math.Add(math.Add(math.Multiply(x2, x1), math.Multiply(y2, y1)), math.Multiply(z2, z1)));
            return new float4(math.Write(x), math.Write(y), math.Write(z), math.Write(w));
        }
    }
}
